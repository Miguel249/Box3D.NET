// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Box3D.Native;

namespace Box3D;

/// <summary>
/// A simulation world: the container for bodies, shapes and constraints, and the
/// thing you step forward in time.
/// </summary>
/// <remarks>
/// <para>
/// Worlds are completely independent and may be stepped in parallel on separate
/// threads. Up to <see cref="MaxCount"/> of them may exist at once.
/// </para>
/// <para>
/// This is the only type in the library that owns a native resource, and so the
/// only one that is <see cref="IDisposable"/>. Bodies, shapes and joints are
/// handles into a world and die with it. Dispose the world and everything in it
/// goes with it.
/// </para>
/// <para>
/// A world is not thread-safe. One thread may step it, and no other thread may
/// touch it or anything in it while that step is running.
/// </para>
/// <para>
/// Creating and disposing worlds is the exception: those are serialised
/// process-wide and may be done from any thread. Box3D itself makes no such
/// promise — its table of worlds is global and unguarded — so this class holds
/// the mutex the engine asks callers to hold.
/// </para>
/// </remarks>
/// <example>
/// A ball falling onto the ground:
/// <code>
/// using var world = new PhysicsWorld(WorldSettings.Default with
/// {
///     Gravity = new Vector3(0.0f, -9.81f, 0.0f),
/// });
///
/// Body ground = world.CreateBody(BodyDefinition.Static());
/// ground.AddBox(new Box(new Vector3(50.0f, 0.5f, 50.0f)));
///
/// Body ball = world.CreateBody(BodyDefinition.Dynamic(new Vector3(0.0f, 10.0f, 0.0f)));
/// ball.AddSphere(new Sphere(0.5f));
///
/// for (int frame = 0; frame &lt; 120; frame++)
/// {
///     world.Step(1.0f / 60.0f);
/// }
///
/// Console.WriteLine(ball.Position);
/// </code>
/// </example>
public sealed unsafe partial class PhysicsWorld : IDisposable
{
    /*
     * There is deliberately no finalizer.
     *
     * The usual guidance is that a type owning an unmanaged resource should have
     * one. Here it would be actively dangerous: the finalizer runs on the GC
     * thread at a time of the runtime's choosing, and destroying a world while
     * another thread is inside Step corrupts the simulation rather than merely
     * leaking. A world is also a long-lived object with an obvious owner, not
     * something created and dropped in a loop.
     *
     * The consequence is that forgetting to dispose leaks the world until the
     * process exits. That is a bug the developer can see - b3GetWorldCount keeps
     * climbing, and the world limit is eventually hit - and it is a far better
     * failure than a use-after-free that only shows up under load.
     */

    /*
     * Creating and destroying a world is guarded process-wide.
     *
     * Box3D keeps its worlds in one global table and picks a slot for a new
     * world by scanning it for a free entry, then marking that entry in use
     * some thirty lines later, after memsetting the whole struct. Nothing
     * synchronises the two. Two threads creating a world at the same time can
     * therefore select the same slot, and the second one zeroes the world the
     * first is still filling in; the corrupted world then spins forever inside
     * Step rather than failing outright. The engine's own documentation is
     * explicit about it: "You will get a race condition if you create or
     * destroy Box3D worlds from multiple threads. Use a mutex to guard those
     * operations."
     *
     * The lock covers only the two native calls, so it costs nothing where it
     * would matter: Step, queries and body edits never touch it. Worlds are
     * long-lived, so the lock is uncontended in any realistic use.
     *
     * It cannot guard what it cannot see: code calling b3CreateWorld through
     * Box3D.NET.Native directly bypasses this, and the caller is then on the
     * hook for its own synchronisation.
     */
    private static readonly object LifetimeLock = new object();

    private b3WorldId _id;
    private bool _disposed;

    /*
     * Pins the debug shape factory, when there is one.
     *
     * Unlike the drawing callbacks, which live only for the duration of a Draw
     * call, these are stored in the world and invoked whenever a shape is first
     * drawn or destroyed. The factory therefore has to stay reachable and stay
     * put for the world's whole life, which a stack pointer cannot do and the
     * GC will not do unasked.
     *
     * Released in Dispose, and deliberately after b3DestroyWorld: destroying a
     * world calls the destroy callback for every drawable still alive, so
     * freeing the handle first would tear down the factory mid-teardown.
     */
    private GCHandle _shapeFactoryHandle;

    /// <summary>Creates a world with the engine's default settings.</summary>
    public PhysicsWorld()
        : this(WorldSettings.Default)
    {
    }

    /// <summary>Creates a world with the given settings.</summary>
    /// <param name="settings">The tuning to apply.</param>
    /// <exception cref="InvalidOperationException">
    /// The engine refused to create the world, which happens when
    /// <see cref="MaxCount"/> are already live.
    /// </exception>
    /// <remarks>
    /// Safe to call from several threads at once: construction and
    /// <see cref="Dispose"/> are serialised process-wide, because the engine's
    /// table of worlds is global and unsynchronised. Everything else about a
    /// world remains single-threaded; see the type documentation.
    /// </remarks>
    public PhysicsWorld(WorldSettings settings)
        : this(settings, null)
    {
    }

    /// <summary>
    /// Creates a world that can draw its shapes, given something to build the
    /// drawables with.
    /// </summary>
    /// <param name="settings">The tuning to apply.</param>
    /// <param name="shapeFactory">
    /// Builds and releases the drawable for each shape, or <see langword="null"/>
    /// for a world whose shapes are never drawn.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// The engine refused to create the world, which happens when
    /// <see cref="MaxCount"/> are already live.
    /// </exception>
    /// <remarks>
    /// <para>
    /// The factory belongs here and not on <see cref="WorldSettings"/> for two
    /// reasons. Box3D takes these callbacks in the world definition, so they
    /// genuinely cannot be supplied after the world exists. And
    /// <see cref="WorldSettings"/> is a value type compared field by field;
    /// putting a reference in it would mean two settings that describe the same
    /// simulation stopped comparing equal because they named different
    /// factories.
    /// </para>
    /// <para>
    /// The factory is kept alive for as long as the world, and released once
    /// the world has been destroyed.
    /// </para>
    /// </remarks>
    public PhysicsWorld(WorldSettings settings, IDebugShapeFactory? shapeFactory)
    {
        b3WorldDef def = settings.ToNative();

        if (shapeFactory is not null)
        {
            _shapeFactoryHandle = GCHandle.Alloc(shapeFactory);
            def.createDebugShape = &CreateDebugShapeThunk;
            def.destroyDebugShape = &DestroyDebugShapeThunk;
            def.userDebugShapeContext = (void*)GCHandle.ToIntPtr(_shapeFactoryHandle);
        }

        lock (LifetimeLock)
        {
            _id = B3.b3CreateWorld(&def);
        }

        if (_id.IsNull)
        {
            // Nothing owns the handle if the world was never created.
            if (_shapeFactoryHandle.IsAllocated)
            {
                _shapeFactoryHandle.Free();
            }

            throw new InvalidOperationException(
                $"Box3D refused to create a world. {B3.b3GetWorldCount()} of a maximum " +
                $"{MaxCount} are already live; dispose worlds you are finished with.");
        }
    }

    /// <summary>
    /// Gets the maximum number of worlds that may exist at the same time.
    /// </summary>
    /// <remarks>
    /// Creating one beyond this throws. Worlds are cheap to keep and expensive
    /// to leak, so this is normally only reached by forgetting to dispose them.
    /// </remarks>
    public static int MaxCount => Constants.B3_MAX_WORLDS;

    /// <summary>Gets the number of worlds currently alive in this process.</summary>
    /// <remarks>
    /// Useful when hunting a leak: this climbing across frames means worlds are
    /// not being disposed.
    /// </remarks>
    public static int Count => B3.b3GetWorldCount();

    /// <summary>Gets the native identifier this handle wraps.</summary>
    /// <remarks>
    /// Internal on purpose: exposing it would put a Box3D.Native type in the
    /// public surface and weld the C ABI to this API. Reach it through
    /// <c>Box3D.Interop.NativeInterop.ToNativeId</c> when you genuinely need the
    /// C function this layer does not wrap.
    /// </remarks>
    internal b3WorldId NativeId => _id;

    /// <summary>Gets a value indicating whether this world has been disposed.</summary>
    public bool IsDisposed => _disposed;

    /// <summary>
    /// Gets a reference identifying this world, for comparing against the
    /// <c>World</c> of a body, shape or joint.
    /// </summary>
    public WorldReference Reference => new(_id);

    /// <summary>
    /// Gets or sets the application identifier attached to this world.
    /// </summary>
    /// <remarks>See <see cref="Box3D.UserData"/>.</remarks>
    public ulong UserData
    {
        get
        {
            ThrowIfDisposed();
            return Box3D.UserData.FromPointer(B3.b3World_GetUserData(_id));
        }

        set
        {
            ThrowIfDisposed();
            B3.b3World_SetUserData(_id, Box3D.UserData.ToPointer(value));
        }
    }

    /// <summary>Gets or sets the gravity vector, usually in metres per second squared.</summary>
    public Vector3 Gravity
    {
        get
        {
            ThrowIfDisposed();
            return B3.b3World_GetGravity(_id);
        }

        set
        {
            ThrowIfDisposed();
            Validate.Finite(value);
            B3.b3World_SetGravity(_id, value);
        }
    }

    /// <summary>Gets or sets a value indicating whether bodies may sleep.</summary>
    public bool SleepEnabled
    {
        get
        {
            ThrowIfDisposed();
            return B3.b3World_IsSleepingEnabled(_id);
        }

        set
        {
            ThrowIfDisposed();
            B3.b3World_EnableSleeping(_id, value);
        }
    }

    /// <summary>Gets or sets a value indicating whether continuous collision is enabled.</summary>
    public bool ContinuousEnabled
    {
        get
        {
            ThrowIfDisposed();
            return B3.b3World_IsContinuousEnabled(_id);
        }

        set
        {
            ThrowIfDisposed();
            B3.b3World_EnableContinuous(_id, value);
        }
    }

    /// <summary>Gets the number of bodies currently awake.</summary>
    /// <remarks>A useful signal that a scene has settled: it falls to zero when everything is asleep.</remarks>
    public int AwakeBodyCount
    {
        get
        {
            ThrowIfDisposed();
            return B3.b3World_GetAwakeBodyCount(_id);
        }
    }

    /// <summary>Gets the bounding box covering the whole simulation.</summary>
    public BoundingBox Bounds
    {
        get
        {
            ThrowIfDisposed();
            return BoundingBox.FromNative(B3.b3World_GetBounds(_id));
        }
    }

    /// <summary>Gets the number of worker threads this world is using.</summary>
    public int WorkerCount
    {
        get
        {
            ThrowIfDisposed();
            return B3.b3World_GetWorkerCount(_id);
        }
    }

    /// <summary>
    /// Advances the simulation by one time step.
    /// </summary>
    /// <param name="timeStep">
    /// How far to advance, in seconds. Keep this fixed; a varying step makes the
    /// simulation non-reproducible and hurts stability. One sixtieth is typical.
    /// </param>
    /// <param name="subStepCount">
    /// How many solver sub-steps to take. More is more accurate and more
    /// expensive. Four is the usual choice.
    /// </param>
    /// <exception cref="ObjectDisposedException">The world has been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="timeStep"/> is negative, or <paramref name="subStepCount"/> is not positive.
    /// </exception>
    /// <remarks>
    /// This is where collision detection, integration and constraint solving
    /// happen. Events raised during the step are available afterwards through
    /// <see cref="Events"/> until the next call.
    /// </remarks>
    public void Step(float timeStep, int subStepCount = 4)
    {
        ThrowIfDisposed();

        // Finiteness first: an infinite step is neither negative nor zero, so
        // the guards below would let it through and it would integrate every
        // body to infinity in one call.
        Validate.Finite(timeStep);
        ArgumentOutOfRangeException.ThrowIfNegative(timeStep);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(subStepCount);

        B3.b3World_Step(_id, timeStep, subStepCount);
    }

    /// <summary>Creates a body in this world.</summary>
    /// <param name="definition">The body's initial state.</param>
    /// <returns>The new body, with no shapes attached yet.</returns>
    /// <exception cref="ObjectDisposedException">The world has been disposed.</exception>
    /// <example>
    /// <code>
    /// Body crate = world.CreateBody(BodyDefinition.Dynamic(new Vector3(0, 5, 0)));
    /// crate.AddBox(Box.Cube(0.5f));
    /// </code>
    /// </example>
    public Body CreateBody(BodyDefinition definition)
    {
        ThrowIfDisposed();

        Validate.Finite(definition.Position);
        Validate.Finite(definition.Rotation);
        Validate.Finite(definition.LinearVelocity);
        Validate.Finite(definition.AngularVelocity);

        // Naming a body is a debugging aid that almost nothing uses, and stack
        // space is zeroed on entry, so reserving the name buffer unconditionally
        // charged every body creation for a 128-byte memset it did not need.
        if (definition.Name is null)
        {
            b3BodyDef def = definition.ToNative(null);
            return new Body(B3.b3CreateBody(_id, &def));
        }

        return CreateNamedBody(definition);
    }

    // Kept out of the common path so its stack frame is not paid for by callers
    // who never name a body.
    private Body CreateNamedBody(BodyDefinition definition)
    {
        Span<byte> scratch = stackalloc byte[128];
        Utf8Buffer name = new(definition.Name, scratch);
        try
        {
            fixed (byte* namePtr = name.Span)
            {
                b3BodyDef def = definition.ToNative(namePtr);
                return new Body(B3.b3CreateBody(_id, &def));
            }
        }
        finally
        {
            name.Dispose();
        }
    }

    /// <summary>Creates a body with the default settings for its type.</summary>
    /// <param name="type">How the body participates in the simulation.</param>
    /// <param name="position">The initial world position.</param>
    /// <returns>The new body.</returns>
    public Body CreateBody(BodyType type, Vector3 position = default) =>
        CreateBody(BodyDefinition.Default with { Type = type, Position = position });

    /*
     * The three shorthands below exist because they are what the first five
     * minutes with this library look like. Reaching CreateBody through a
     * BodyDefinition is the right shape once a body needs damping or motion
     * locks, and it is needless ceremony when it does not.
     */

    /// <summary>Creates a fully simulated body that responds to forces and collisions.</summary>
    /// <param name="position">The initial world position of the body origin.</param>
    /// <param name="rotation">The initial world rotation, or null for none.</param>
    /// <returns>The new body, with no shapes attached yet.</returns>
    /// <remarks>
    /// Shorthand for <c>CreateBody(BodyDefinition.Dynamic(position, rotation))</c>.
    /// Use the definition overload when you need anything beyond a pose.
    /// </remarks>
    /// <example>
    /// <code>
    /// Body crate = world.CreateDynamicBody(new Vector3(0.0f, 10.0f, 0.0f));
    /// crate.AddBox(Box.Cube(0.5f));
    /// </code>
    /// </example>
    public Body CreateDynamicBody(Vector3 position = default, Quaternion? rotation = null) =>
        CreateBody(BodyDefinition.Dynamic(position, rotation));

    /// <summary>Creates an immovable body, for level geometry.</summary>
    /// <param name="position">The initial world position of the body origin.</param>
    /// <param name="rotation">The initial world rotation, or null for none.</param>
    /// <returns>The new body, with no shapes attached yet.</returns>
    /// <example>
    /// <code>
    /// Body ground = world.CreateStaticBody(new Vector3(0.0f, -0.5f, 0.0f));
    /// ground.AddBox(new Box(new Vector3(50.0f, 0.5f, 50.0f)));
    /// </code>
    /// </example>
    public Body CreateStaticBody(Vector3 position = default, Quaternion? rotation = null) =>
        CreateBody(BodyDefinition.Static(position, rotation));

    /// <summary>
    /// Creates a body the application moves itself, which pushes dynamic bodies
    /// without being pushed back.
    /// </summary>
    /// <param name="position">The initial world position of the body origin.</param>
    /// <param name="rotation">The initial world rotation, or null for none.</param>
    /// <returns>The new body, with no shapes attached yet.</returns>
    /// <remarks>
    /// Drive it with <see cref="Body.LinearVelocity"/> or
    /// <see cref="Body.MoveTowards"/> rather than
    /// <see cref="Body.SetTransform"/>, so that contacts push other bodies
    /// correctly.
    /// </remarks>
    public Body CreateKinematicBody(Vector3 position = default, Quaternion? rotation = null) =>
        CreateBody(BodyDefinition.Kinematic(position, rotation));

    // ------------------------------------------------------------- queries

    /*
     * Query callbacks without allocating
     * ----------------------------------
     * The obvious design takes a delegate. That allocates a closure per call,
     * needs a GC handle to survive the transition, and does not survive
     * NativeAOT trimming cleanly.
     *
     * Instead the query is generic over a struct implementing the callback
     * interface. The JIT specializes the method for that struct and devirtualizes
     * the interface call, so the user's code is inlined into the dispatcher with
     * no delegate and no boxing.
     *
     * Getting from the native callback back to that generic code takes one
     * indirection, because [UnmanagedCallersOnly] cannot be applied to a generic
     * method. The context struct therefore carries:
     *
     *   - a pointer to the caller's callback instance, living on their stack, and
     *   - a *managed* function pointer to a generic helper, which may be generic
     *     precisely because it is not the UnmanagedCallersOnly one.
     *
     * The non-generic native thunk reads both out of the context and calls
     * through. That costs one indirect call per hit, against an allocation and a
     * GC handle per query for the delegate design.
     */

    private struct RaycastContext
    {
        public void* Callback;
        public delegate*<void*, in RaycastHit, float> Invoke;
    }

    private struct OverlapContext
    {
        public void* Callback;
        public delegate*<void*, Shape, bool> Invoke;
    }

    private static float InvokeRaycast<TCallback>(void* callback, in RaycastHit hit)
        where TCallback : struct, IRaycastCallback =>
        Unsafe.AsRef<TCallback>(callback).OnHit(in hit).Value;

    private static bool InvokeOverlap<TCallback>(void* callback, Shape shape)
        where TCallback : struct, IOverlapCallback =>
        Unsafe.AsRef<TCallback>(callback).OnOverlap(shape);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static float RaycastThunk(
        b3ShapeId shapeId,
        Vector3 point,
        Vector3 normal,
        float fraction,
        ulong userMaterialId,
        int triangleIndex,
        int childIndex,
        void* context)
    {
        ref RaycastContext ctx = ref Unsafe.AsRef<RaycastContext>(context);

        RaycastHit hit = new()
        {
            Hit = true,
            Shape = new Shape(shapeId),
            Point = point,
            Normal = normal,
            Fraction = fraction,
            UserMaterialId = userMaterialId,
            TriangleIndex = triangleIndex,
            ChildIndex = childIndex,
        };

        return ctx.Invoke(ctx.Callback, in hit);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static NativeBool OverlapThunk(b3ShapeId shapeId, void* context)
    {
        ref OverlapContext ctx = ref Unsafe.AsRef<OverlapContext>(context);
        return ctx.Invoke(ctx.Callback, new Shape(shapeId));
    }

    /// <summary>
    /// Casts a ray through the world, reporting every shape it reaches to a
    /// callback.
    /// </summary>
    /// <typeparam name="TCallback">
    /// The callback type. A struct, so the call is resolved statically and
    /// nothing is allocated.
    /// </typeparam>
    /// <param name="origin">The start of the ray, in world space.</param>
    /// <param name="translation">
    /// The ray vector. Its length is the range, and hit fractions are relative to it.
    /// </param>
    /// <param name="callback">
    /// The callback, passed by reference so that anything it records is visible
    /// afterwards.
    /// </param>
    /// <param name="filter">Which shapes the ray may hit, or null for everything.</param>
    /// <exception cref="ObjectDisposedException">The world has been disposed.</exception>
    /// <remarks>
    /// Shapes arrive in no particular order. For the nearest hit either return
    /// <see cref="RaycastAction.ClipTo"/> from the callback or use
    /// <see cref="RaycastClosest"/>.
    /// </remarks>
    /// <example>
    /// <code>
    /// struct CountHits : IRaycastCallback
    /// {
    ///     public int Count;
    ///
    ///     public RaycastAction OnHit(in RaycastHit hit)
    ///     {
    ///         Count++;
    ///         return RaycastAction.Continue;
    ///     }
    /// }
    ///
    /// var callback = new CountHits();
    /// world.Raycast(Vector3.Zero, new Vector3(100.0f, 0.0f, 0.0f), ref callback);
    /// </code>
    /// </example>
    public void Raycast<TCallback>(
        Vector3 origin,
        Vector3 translation,
        ref TCallback callback,
        QueryFilter? filter = null)
        where TCallback : struct, IRaycastCallback
    {
        ThrowIfDisposed();
        Validate.Finite(origin);
        Validate.Finite(translation);

        RaycastContext context = new()
        {
            Callback = Unsafe.AsPointer(ref callback),
            Invoke = &InvokeRaycast<TCallback>,
        };

        _ = B3.b3World_CastRay(
            _id,
            origin,
            translation,
            (filter ?? QueryFilter.Default).ToNative(),
            &RaycastThunk,
            &context);
    }

    /// <summary>Casts a ray and returns the nearest shape it hits.</summary>
    /// <param name="origin">The start of the ray, in world space.</param>
    /// <param name="translation">The ray vector. Its length is the range.</param>
    /// <param name="filter">Which shapes the ray may hit, or null for everything.</param>
    /// <returns>
    /// The nearest hit. Check <see cref="RaycastHit.Hit"/> before reading anything else.
    /// </returns>
    /// <exception cref="ObjectDisposedException">The world has been disposed.</exception>
    /// <remarks>
    /// Ignores shapes the ray starts inside. Allocates nothing and needs no
    /// callback, so this is the one to reach for unless you need custom filtering.
    /// </remarks>
    /// <example>
    /// <code>
    /// var hit = world.RaycastClosest(muzzle, aim * 100.0f);
    /// if (hit.Hit)
    /// {
    ///     Spawn(Impact, hit.Point, hit.Normal);
    /// }
    /// </code>
    /// </example>
    public RaycastHit RaycastClosest(Vector3 origin, Vector3 translation, QueryFilter? filter = null)
    {
        ThrowIfDisposed();
        Validate.Finite(origin);
        Validate.Finite(translation);

        b3RayResult result = B3.b3World_CastRayClosest(
            _id,
            origin,
            translation,
            (filter ?? QueryFilter.Default).ToNative());

        return RaycastHit.FromNative(result);
    }

    /// <summary>
    /// Finds every shape whose bounding box overlaps the given box.
    /// </summary>
    /// <typeparam name="TCallback">
    /// The callback type. A struct, so nothing is allocated.
    /// </typeparam>
    /// <param name="bounds">The world-space box to test against.</param>
    /// <param name="callback">The callback, passed by reference.</param>
    /// <param name="filter">Which shapes may be reported, or null for everything.</param>
    /// <exception cref="ObjectDisposedException">The world has been disposed.</exception>
    /// <remarks>
    /// This is a broad-phase query, so it tests bounding boxes rather than
    /// geometry and can report shapes that do not really overlap. Narrow it
    /// yourself if you need exactness.
    /// </remarks>
    public void OverlapBounds<TCallback>(
        BoundingBox bounds,
        ref TCallback callback,
        QueryFilter? filter = null)
        where TCallback : struct, IOverlapCallback
    {
        ThrowIfDisposed();

        OverlapContext context = new()
        {
            Callback = Unsafe.AsPointer(ref callback),
            Invoke = &InvokeOverlap<TCallback>,
        };

        _ = B3.b3World_OverlapAABB(
            _id,
            bounds.ToNative(),
            (filter ?? QueryFilter.Default).ToNative(),
            &OverlapThunk,
            &context);
    }

    /// <summary>
    /// Finds every shape whose bounding box overlaps a box centred on a point.
    /// </summary>
    /// <typeparam name="TCallback">The callback type.</typeparam>
    /// <param name="center">The centre of the box, in world space.</param>
    /// <param name="halfExtents">The half-widths of the box.</param>
    /// <param name="callback">The callback, passed by reference.</param>
    /// <param name="filter">Which shapes may be reported, or null for everything.</param>
    public void OverlapBox<TCallback>(
        Vector3 center,
        Vector3 halfExtents,
        ref TCallback callback,
        QueryFilter? filter = null)
        where TCallback : struct, IOverlapCallback =>
        OverlapBounds(BoundingBox.FromCenter(center, halfExtents), ref callback, filter);

    // -------------------------------------------------------------- events

    /// <summary>
    /// Gets the events raised by the most recent <see cref="Step"/>.
    /// </summary>
    /// <remarks>
    /// The returned spans point at engine memory and are only valid until the
    /// next step. Read what you need out of them before stepping again.
    /// </remarks>
    public WorldEvents Events
    {
        get
        {
            ThrowIfDisposed();
            return new WorldEvents(_id);
        }
    }

    /// <summary>
    /// Applies a radial impulse to every shape within reach, scaled by how much
    /// of each shape faces the blast.
    /// </summary>
    /// <param name="center">The centre of the explosion, in world space.</param>
    /// <param name="radius">The radius within which the full impulse applies.</param>
    /// <param name="impulsePerArea">
    /// The impulse per unit of facing area. Negative values implode.
    /// </param>
    /// <param name="falloff">The distance beyond the radius over which the impulse fades to nothing.</param>
    /// <param name="filter">Which shape categories are affected. Defaults to all of them.</param>
    /// <exception cref="ObjectDisposedException">The world has been disposed.</exception>
    /// <remarks>Only spheres, capsules and hulls respond.</remarks>
    public void Explode(
        Vector3 center,
        float radius,
        float impulsePerArea,
        float falloff = 0.0f,
        ulong filter = ulong.MaxValue)
    {
        ThrowIfDisposed();

        b3ExplosionDef def = NativeDefaults.Explosion;
        def.position = center;
        def.radius = radius;
        def.impulsePerArea = impulsePerArea;
        def.falloff = falloff;
        def.maskBits = filter;

        B3.b3World_Explode(_id, &def);
    }

    /// <summary>Destroys the world and everything in it.</summary>
    /// <remarks>
    /// Every body, shape and joint handle from this world becomes invalid.
    /// Calling this twice is harmless, and disposing two different worlds from
    /// two threads at once is safe. Disposing a world while another thread is
    /// inside <see cref="Step(float, int)"/> on that same world is not, and no
    /// lock here can make it so.
    /// </remarks>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (!_id.IsNull)
        {
            lock (LifetimeLock)
            {
                B3.b3DestroyWorld(_id);
            }

            _id = default;
        }

        // After the world is gone, never before: destroying it calls the
        // factory back for every drawable still alive.
        if (_shapeFactoryHandle.IsAllocated)
        {
            _shapeFactoryHandle.Free();
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
