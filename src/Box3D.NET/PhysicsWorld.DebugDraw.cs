// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Box3D.Native;

namespace Box3D;

public sealed unsafe partial class PhysicsWorld
{
    /*
     * How a managed drawer is reached from a C callback.
     *
     * Box3D takes plain C function pointers, so every callback has to be a
     * static method carrying [UnmanagedCallersOnly]. That attribute forbids
     * generics, which is the whole difficulty: the thunk cannot be
     * SegmentThunk<T> and so cannot name the drawer's type.
     *
     * The indirection below solves it without giving up on either performance
     * or type safety. The thunks are non-generic, as required, and receive a
     * DrawContext through Box3D's own user-context pointer. That context holds
     * a pointer to the caller's drawer plus one *managed* function pointer per
     * callback, each bound to Dispatch<T> for the concrete T. Managed function
     * pointers may target an instantiated generic method, so the type comes
     * back at exactly the point it is needed.
     *
     * What this buys: no delegates, no closures, no boxing, no GCHandle, and
     * nothing allocated per frame or per call. The constraint to struct is what
     * keeps the interface calls devirtualised rather than dispatched through
     * an interface table.
     *
     * The drawer is copied into a local first. Taking a pointer to caller
     * storage would be unsound: a drawer living in a heap object can be moved
     * by a collection that runs while the thread sits in the native call. A
     * local cannot move, the GC still traces any references inside it, and the
     * value is copied back afterwards so the caller sees whatever the drawer
     * accumulated.
     */

    private struct DrawContext
    {
        public void* Drawer;

        public delegate*<void*, Vector3, Vector3, b3HexColor, void> Segment;
        public delegate*<void*, Vector3, float, b3HexColor, void> Point;
        public delegate*<void*, b3Transform, void> Transform;
        public delegate*<void*, Vector3, float, b3HexColor, float, void> Sphere;
        public delegate*<void*, Vector3, Vector3, float, b3HexColor, float, void> Capsule;
        public delegate*<void*, b3AABB, b3HexColor, void> Bounds;
        public delegate*<void*, Vector3, b3Transform, b3HexColor, void> Box;
        public delegate*<void*, Vector3, byte*, b3HexColor, void> String;
    }

    /// <summary>
    /// Emits the world's debug geometry to a drawer.
    /// </summary>
    /// <typeparam name="T">
    /// The drawer. Constrained to a value type so that the calls stay
    /// devirtualised and nothing is boxed; put whatever state the renderer
    /// needs inside it, references included.
    /// </typeparam>
    /// <param name="drawer">
    /// The drawer, passed by reference. Anything it accumulates during the call
    /// is visible on return.
    /// </param>
    /// <param name="options">What to draw. Everything is off by default.</param>
    /// <exception cref="ObjectDisposedException">The world has been disposed.</exception>
    /// <remarks>
    /// <para>
    /// Synchronous: every callback runs before this returns, on the calling
    /// thread. The drawer must not touch this world while it runs, for the same
    /// reason nothing may touch a world mid-step.
    /// </para>
    /// <para>
    /// A frame allocates nothing on the managed heap, provided the drawer does
    /// not allocate itself.
    /// </para>
    /// <para>
    /// <b>Shapes are the one thing this does not draw.</b> Box3D does not emit
    /// shape geometry as primitives: it asks the application to build a drawable
    /// once, through a callback on the world definition, hands back the opaque
    /// handle it returned, and expects the renderer to draw that handle at a
    /// transform. Those callbacks are set when the world is created and are not
    /// yet surfaced by this layer, so
    /// <see cref="DebugDrawOptions.DrawShapes"/> currently produces nothing.
    /// Everything else — joints, contacts, normals, forces, bounding boxes,
    /// centres of mass, islands — is emitted in full.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var drawer = new WireframeDrawer(renderer);
    /// world.Draw(ref drawer, DebugDrawOptions.Default with
    /// {
    ///     DrawJoints = true,
    ///     DrawContacts = true,
    /// });
    /// </code>
    /// </example>
    public void Draw<T>(ref T drawer, in DebugDrawOptions options)
        where T : struct, IDebugDrawer
    {
        ThrowIfDisposed();

        // Copied to the stack: see the note above on why caller storage cannot
        // be pointed at across a native call.
        T local = drawer;

        DrawContext context = new()
        {
            Drawer = Unsafe.AsPointer(ref local),
            Segment = &DispatchSegment<T>,
            Point = &DispatchPoint<T>,
            Transform = &DispatchTransform<T>,
            Sphere = &DispatchSphere<T>,
            Capsule = &DispatchCapsule<T>,
            Bounds = &DispatchBounds<T>,
            Box = &DispatchBox<T>,
            String = &DispatchString<T>,
        };

        b3DebugDraw draw = options.ToNative();

        draw.DrawSegmentFcn = &SegmentThunk;
        draw.DrawPointFcn = &PointThunk;
        draw.DrawTransformFcn = &TransformThunk;
        draw.DrawSphereFcn = &SphereThunk;
        draw.DrawCapsuleFcn = &CapsuleThunk;
        draw.DrawBoundsFcn = &BoundsThunk;
        draw.DrawBoxFcn = &BoxThunk;
        draw.DrawStringFcn = &StringThunk;
        draw.context = &context;

        try
        {
            B3.b3World_Draw(_id, &draw, options.CategoryMask);
        }
        finally
        {
            // Even if a drawer throws, give the caller what was collected
            // before it did.
            drawer = local;
        }
    }

    // ------------------------------------------------------------ the thunks
    //
    // Non-generic by necessity. Each one reads the context and forwards to the
    // managed function pointer that knows the drawer's type.

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void SegmentThunk(Vector3 p1, Vector3 p2, b3HexColor color, void* context)
    {
        ref DrawContext ctx = ref Unsafe.AsRef<DrawContext>(context);
        ctx.Segment(ctx.Drawer, p1, p2, color);
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void PointThunk(Vector3 p, float size, b3HexColor color, void* context)
    {
        ref DrawContext ctx = ref Unsafe.AsRef<DrawContext>(context);
        ctx.Point(ctx.Drawer, p, size, color);
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void TransformThunk(b3Transform transform, void* context)
    {
        ref DrawContext ctx = ref Unsafe.AsRef<DrawContext>(context);
        ctx.Transform(ctx.Drawer, transform);
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void SphereThunk(Vector3 p, float radius, b3HexColor color, float alpha, void* context)
    {
        ref DrawContext ctx = ref Unsafe.AsRef<DrawContext>(context);
        ctx.Sphere(ctx.Drawer, p, radius, color, alpha);
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void CapsuleThunk(Vector3 p1, Vector3 p2, float radius, b3HexColor color, float alpha, void* context)
    {
        ref DrawContext ctx = ref Unsafe.AsRef<DrawContext>(context);
        ctx.Capsule(ctx.Drawer, p1, p2, radius, color, alpha);
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void BoundsThunk(b3AABB aabb, b3HexColor color, void* context)
    {
        ref DrawContext ctx = ref Unsafe.AsRef<DrawContext>(context);
        ctx.Bounds(ctx.Drawer, aabb, color);
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void BoxThunk(Vector3 extents, b3Transform transform, b3HexColor color, void* context)
    {
        ref DrawContext ctx = ref Unsafe.AsRef<DrawContext>(context);
        ctx.Box(ctx.Drawer, extents, transform, color);
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void StringThunk(Vector3 p, byte* utf8Text, b3HexColor color, void* context)
    {
        ref DrawContext ctx = ref Unsafe.AsRef<DrawContext>(context);
        ctx.String(ctx.Drawer, p, utf8Text, color);
    }

    // --------------------------------------------------------- the dispatchers
    //
    // Generic, and reached through a managed function pointer bound in Draw<T>.
    // This is where the drawer's concrete type comes back.

    private static void DispatchSegment<T>(void* drawer, Vector3 p1, Vector3 p2, b3HexColor color)
        where T : struct, IDebugDrawer =>
        Unsafe.AsRef<T>(drawer).DrawSegment(p1, p2, new DebugColor((uint)color));

    private static void DispatchPoint<T>(void* drawer, Vector3 p, float size, b3HexColor color)
        where T : struct, IDebugDrawer =>
        Unsafe.AsRef<T>(drawer).DrawPoint(p, size, new DebugColor((uint)color));

    private static void DispatchTransform<T>(void* drawer, b3Transform transform)
        where T : struct, IDebugDrawer =>
        Unsafe.AsRef<T>(drawer).DrawTransform(transform.p, transform.q);

    private static void DispatchSphere<T>(void* drawer, Vector3 center, float radius, b3HexColor color, float alpha)
        where T : struct, IDebugDrawer =>
        Unsafe.AsRef<T>(drawer).DrawSphere(center, radius, new DebugColor((uint)color), alpha);

    private static void DispatchCapsule<T>(void* drawer, Vector3 p1, Vector3 p2, float radius, b3HexColor color, float alpha)
        where T : struct, IDebugDrawer =>
        Unsafe.AsRef<T>(drawer).DrawCapsule(p1, p2, radius, new DebugColor((uint)color), alpha);

    private static void DispatchBounds<T>(void* drawer, b3AABB aabb, b3HexColor color)
        where T : struct, IDebugDrawer =>
        Unsafe.AsRef<T>(drawer).DrawBounds(new BoundingBox(aabb.lowerBound, aabb.upperBound), new DebugColor((uint)color));

    private static void DispatchBox<T>(void* drawer, Vector3 extents, b3Transform transform, b3HexColor color)
        where T : struct, IDebugDrawer =>
        Unsafe.AsRef<T>(drawer).DrawBox(extents, transform.p, transform.q, new DebugColor((uint)color));

    private static void DispatchString<T>(void* drawer, Vector3 p, byte* utf8Text, b3HexColor color)
        where T : struct, IDebugDrawer
    {
        // Box3D passes a NUL-terminated C string. Handing over the bytes rather
        // than a string keeps a label from allocating on every frame.
        ReadOnlySpan<byte> text = utf8Text is null
            ? default
            : MemoryMarshal.CreateReadOnlySpanFromNullTerminated(utf8Text);

        Unsafe.AsRef<T>(drawer).DrawString(p, text, new DebugColor((uint)color));
    }
}
