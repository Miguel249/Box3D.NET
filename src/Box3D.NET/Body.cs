// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using Box3D.Native;

namespace Box3D;

/// <summary>
/// A rigid body: a position, an orientation and a velocity, with shapes attached
/// to give it collision geometry.
/// </summary>
/// <remarks>
/// <para>
/// Like <see cref="Shape"/>, a body is a handle rather than an object. It is a
/// small value the world resolves, so copying is free and copies refer to the
/// same body. It owns nothing, so it is not <see cref="IDisposable"/>; use
/// <see cref="Destroy"/> to remove it, or destroy the world to remove
/// everything.
/// </para>
/// <para>
/// A body with no shapes has no collision geometry and no mass. Create the body,
/// then attach shapes to it.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// Body crate = world.CreateBody(BodyDefinition.Dynamic(new Vector3(0, 10, 0)));
/// crate.AddBox(Box.Cube(0.5f));
///
/// crate.LinearVelocity = new Vector3(5.0f, 0.0f, 0.0f);
/// crate.ApplyForceToCenter(new Vector3(0.0f, 100.0f, 0.0f));
///
/// world.Step(1.0f / 60.0f);
/// Console.WriteLine(crate.Position);
/// </code>
/// </example>
public readonly record struct Body
{
    internal Body(b3BodyId id) => Id = id;

    /// <summary>Gets the underlying native identifier.</summary>
    /// <remarks>
    /// Exposed so that code can drop down to <see cref="Box3D.Native"/> for
    /// anything this layer does not yet cover.
    /// </remarks>
    public b3BodyId Id { get; }

    /// <summary>Gets a value indicating whether this handle refers to a live body.</summary>
    public bool IsValid => !Id.IsNull && B3.b3Body_IsValid(Id);

    /// <summary>Gets or sets how the body participates in the simulation.</summary>
    /// <remarks>
    /// Changing this is expensive and always recomputes the mass properties.
    /// </remarks>
    public BodyType Type
    {
        get => (BodyType)B3.b3Body_GetType(Id);
        set => B3.b3Body_SetType(Id, (b3BodyType)value);
    }

    /// <summary>Gets the world position of the body origin.</summary>
    /// <remarks>Use <see cref="SetTransform"/> to move a body; it is a teleport and is not cheap.</remarks>
    public Vector3 Position => B3.b3Body_GetPosition(Id);

    /// <summary>Gets the world rotation.</summary>
    public Quaternion Rotation => B3.b3Body_GetRotation(Id);

    /// <summary>Gets the world position of the centre of mass.</summary>
    /// <remarks>
    /// Not the same as <see cref="Position"/> unless the shapes happen to be
    /// balanced around the body origin. Velocities are measured here.
    /// </remarks>
    public Vector3 CenterOfMass => B3.b3Body_GetWorldCenter(Id);

    /// <summary>Gets the centre of mass in body-local space.</summary>
    public Vector3 LocalCenterOfMass => B3.b3Body_GetLocalCenter(Id);

    /// <summary>Gets or sets the linear velocity of the centre of mass, usually in metres per second.</summary>
    public Vector3 LinearVelocity
    {
        get => B3.b3Body_GetLinearVelocity(Id);
        set => B3.b3Body_SetLinearVelocity(Id, value);
    }

    /// <summary>Gets or sets the angular velocity, in radians per second.</summary>
    public Vector3 AngularVelocity
    {
        get => B3.b3Body_GetAngularVelocity(Id);
        set => B3.b3Body_SetAngularVelocity(Id, value);
    }

    /// <summary>Gets the mass, usually in kilograms.</summary>
    /// <remarks>Zero for static and kinematic bodies, and for a dynamic body with no shapes.</remarks>
    public float Mass => B3.b3Body_GetMass(Id);

    /// <summary>Gets or sets the multiplier applied to gravity for this body.</summary>
    public float GravityScale
    {
        get => B3.b3Body_GetGravityScale(Id);
        set => B3.b3Body_SetGravityScale(Id, value);
    }

    /// <summary>Gets or sets the linear damping.</summary>
    public float LinearDamping
    {
        get => B3.b3Body_GetLinearDamping(Id);
        set => B3.b3Body_SetLinearDamping(Id, value);
    }

    /// <summary>Gets or sets the angular damping.</summary>
    public float AngularDamping
    {
        get => B3.b3Body_GetAngularDamping(Id);
        set => B3.b3Body_SetAngularDamping(Id, value);
    }

    /// <summary>Gets or sets a value indicating whether the body is awake.</summary>
    /// <remarks>
    /// Waking or sleeping a body affects every body in its island, which can be
    /// surprising and is not cheap.
    /// </remarks>
    public bool IsAwake
    {
        get => B3.b3Body_IsAwake(Id);
        set => B3.b3Body_SetAwake(Id, value);
    }

    /// <summary>Gets or sets a value indicating whether the body is allowed to sleep.</summary>
    /// <remarks>Setting this to false wakes the body.</remarks>
    public bool CanSleep
    {
        get => B3.b3Body_IsSleepEnabled(Id);
        set => B3.b3Body_EnableSleep(Id, value);
    }

    /// <summary>Gets or sets a value indicating whether the body uses continuous collision as a bullet.</summary>
    public bool IsBullet
    {
        get => B3.b3Body_IsBullet(Id);
        set => B3.b3Body_SetBullet(Id, value);
    }

    /// <summary>Gets a value indicating whether the body takes part in the simulation.</summary>
    /// <remarks>Use <see cref="Enable"/> and <see cref="Disable"/> to change it.</remarks>
    public bool IsEnabled => B3.b3Body_IsEnabled(Id);

    /// <summary>Gets or sets the axes the body may not move along or rotate about.</summary>
    public MotionLocks MotionLocks
    {
        get => Box3D.MotionLocks.FromNative(B3.b3Body_GetMotionLocks(Id));
        set => B3.b3Body_SetMotionLocks(Id, value.ToNative());
    }

    /// <summary>Gets the number of shapes attached to this body.</summary>
    public int ShapeCount => B3.b3Body_GetShapeCount(Id);

    /// <summary>Gets the world-space bounding box covering every attached shape.</summary>
    /// <remarks>
    /// May not contain the body origin. Empty and centred on the origin when the
    /// body has no shapes.
    /// </remarks>
    public BoundingBox Bounds => BoundingBox.FromNative(B3.b3Body_ComputeAABB(Id));

    // ------------------------------------------------------------- geometry

    /// <summary>Attaches a sphere to this body.</summary>
    /// <param name="sphere">The sphere, in body-local space.</param>
    /// <param name="definition">The shape settings, or null for the defaults.</param>
    /// <returns>The new shape.</returns>
    public unsafe Shape AddSphere(Sphere sphere, ShapeDefinition? definition = null)
    {
        ShapeDefinition settings = definition ?? ShapeDefinition.Default;

        Span<byte> scratch = stackalloc byte[128];
        Utf8Buffer name = new(settings.Name, scratch);
        try
        {
            fixed (byte* namePtr = name.Span)
            {
                b3ShapeDef def = settings.ToNative(name.IsNull ? null : namePtr);
                b3Sphere native = sphere.ToNative();

                return new Shape(B3.b3CreateSphereShape(Id, &def, &native));
            }
        }
        finally
        {
            name.Dispose();
        }
    }

    /// <summary>Attaches a capsule to this body.</summary>
    /// <param name="capsule">The capsule, in body-local space.</param>
    /// <param name="definition">The shape settings, or null for the defaults.</param>
    /// <returns>The new shape.</returns>
    public unsafe Shape AddCapsule(Capsule capsule, ShapeDefinition? definition = null)
    {
        ShapeDefinition settings = definition ?? ShapeDefinition.Default;

        Span<byte> scratch = stackalloc byte[128];
        Utf8Buffer name = new(settings.Name, scratch);
        try
        {
            fixed (byte* namePtr = name.Span)
            {
                b3ShapeDef def = settings.ToNative(name.IsNull ? null : namePtr);
                b3Capsule native = capsule.ToNative();

                return new Shape(B3.b3CreateCapsuleShape(Id, &def, &native));
            }
        }
        finally
        {
            name.Dispose();
        }
    }

    /// <summary>Attaches a box to this body.</summary>
    /// <param name="box">The box, in body-local space.</param>
    /// <param name="definition">The shape settings, or null for the defaults.</param>
    /// <returns>The new shape.</returns>
    /// <remarks>
    /// The box is a convex hull underneath, but it is self-contained, so nothing
    /// needs to be released afterwards.
    /// </remarks>
    public unsafe Shape AddBox(Box box, ShapeDefinition? definition = null)
    {
        ShapeDefinition settings = definition ?? ShapeDefinition.Default;

        Span<byte> scratch = stackalloc byte[128];
        Utf8Buffer name = new(settings.Name, scratch);
        try
        {
            fixed (byte* namePtr = name.Span)
            {
                b3ShapeDef def = settings.ToNative(name.IsNull ? null : namePtr);
                b3BoxHull hull = box.ToNativeHull();

                return new Shape(B3.b3CreateHullShape(Id, &def, &hull.@base));
            }
        }
        finally
        {
            name.Dispose();
        }
    }

    /// <summary>Copies the handles of every shape on this body into the supplied span.</summary>
    /// <param name="destination">
    /// Receives the shapes. Size it with <see cref="ShapeCount"/>; a shorter span
    /// is filled as far as it goes.
    /// </param>
    /// <returns>The number of shapes written.</returns>
    /// <remarks>
    /// Takes a caller-provided span rather than returning an array, so that a
    /// per-frame walk over shapes allocates nothing.
    /// </remarks>
    /// <example>
    /// <code>
    /// Span&lt;Shape&gt; shapes = stackalloc Shape[body.ShapeCount];
    /// int count = body.GetShapes(shapes);
    /// foreach (Shape shape in shapes[..count])
    /// {
    ///     shape.Friction = 0.1f;
    /// }
    /// </code>
    /// </example>
    public unsafe int GetShapes(Span<Shape> destination)
    {
        if (destination.IsEmpty)
        {
            return 0;
        }

        // Shape is a single b3ShapeId field, so the spans have the same layout
        // and the ids can be written straight into the caller's buffer.
        fixed (Shape* target = destination)
        {
            return B3.b3Body_GetShapes(Id, (b3ShapeId*)target, destination.Length);
        }
    }

    // --------------------------------------------------------------- forces

    /// <summary>Applies a force at the centre of mass, producing no rotation.</summary>
    /// <param name="force">The force, in newtons.</param>
    /// <param name="wake">Whether to wake the body first. A sleeping body ignores the force.</param>
    /// <remarks>
    /// Forces accumulate over a step and are the right tool for sustained
    /// effects such as thrust. For an instantaneous change use
    /// <see cref="ApplyImpulseToCenter"/>.
    /// </remarks>
    public void ApplyForceToCenter(Vector3 force, bool wake = true) =>
        B3.b3Body_ApplyForceToCenter(Id, force, wake);

    /// <summary>Applies a force at a world-space point, producing torque if it is off-centre.</summary>
    /// <param name="force">The force, in newtons.</param>
    /// <param name="point">The point of application, in world space.</param>
    /// <param name="wake">Whether to wake the body first.</param>
    public void ApplyForce(Vector3 force, Vector3 point, bool wake = true) =>
        B3.b3Body_ApplyForce(Id, force, point, wake);

    /// <summary>Applies a torque, changing angular velocity without moving the body.</summary>
    /// <param name="torque">The torque, in newton-metres.</param>
    /// <param name="wake">Whether to wake the body first.</param>
    public void ApplyTorque(Vector3 torque, bool wake = true) =>
        B3.b3Body_ApplyTorque(Id, torque, wake);

    /// <summary>Changes the velocity immediately, with no rotation.</summary>
    /// <param name="impulse">The impulse, in newton-seconds.</param>
    /// <param name="wake">Whether to wake the body first.</param>
    /// <remarks>
    /// For one-shot events such as an explosion or a jump. A sustained effect
    /// works better as a force, which the sub-stepping solver handles more
    /// smoothly.
    /// </remarks>
    public void ApplyImpulseToCenter(Vector3 impulse, bool wake = true) =>
        B3.b3Body_ApplyLinearImpulseToCenter(Id, impulse, wake);

    /// <summary>Changes the velocity immediately at a world-space point, adding spin if it is off-centre.</summary>
    /// <param name="impulse">The impulse, in newton-seconds.</param>
    /// <param name="point">The point of application, in world space.</param>
    /// <param name="wake">Whether to wake the body first.</param>
    public void ApplyImpulse(Vector3 impulse, Vector3 point, bool wake = true) =>
        B3.b3Body_ApplyLinearImpulse(Id, impulse, point, wake);

    /// <summary>Changes the angular velocity immediately.</summary>
    /// <param name="impulse">The angular impulse, in kilogram metres squared per second.</param>
    /// <param name="wake">Whether to wake the body first.</param>
    public void ApplyAngularImpulse(Vector3 impulse, bool wake = true) =>
        B3.b3Body_ApplyAngularImpulse(Id, impulse, wake);

    // ------------------------------------------------------------- movement

    /// <summary>Teleports the body.</summary>
    /// <param name="position">The new world position of the body origin.</param>
    /// <param name="rotation">The new world rotation, or null to keep the current one.</param>
    /// <remarks>
    /// This is a teleport, not a move: it does not sweep, so the body can pass
    /// through geometry, and it is expensive. Prefer creating the body where it
    /// belongs, or driving a kinematic body with
    /// <see cref="MoveTowards"/> or a velocity.
    /// </remarks>
    public void SetTransform(Vector3 position, Quaternion? rotation = null) =>
        B3.b3Body_SetTransform(Id, position, rotation ?? Rotation);

    /// <summary>
    /// Sets the velocity needed to arrive at a target pose after one time step.
    /// </summary>
    /// <param name="position">The target world position.</param>
    /// <param name="rotation">The target world rotation.</param>
    /// <param name="timeStep">The time step the velocity is computed for.</param>
    /// <param name="wake">Whether to wake the body if the movement is significant.</param>
    /// <remarks>
    /// This is how to drive a kinematic body such as a moving platform: it keeps
    /// a real velocity, so contacts push other bodies correctly, which a
    /// teleport does not.
    /// </remarks>
    public void MoveTowards(Vector3 position, Quaternion rotation, float timeStep, bool wake = true) =>
        B3.b3Body_SetTargetTransform(Id, new b3Transform { p = position, q = rotation }, timeStep, wake);

    /// <summary>Converts a point from world space into body-local space.</summary>
    /// <param name="worldPoint">The point, in world space.</param>
    /// <returns>The point, in body-local space.</returns>
    public Vector3 ToLocalPoint(Vector3 worldPoint) => B3.b3Body_GetLocalPoint(Id, worldPoint);

    /// <summary>Converts a point from body-local space into world space.</summary>
    /// <param name="localPoint">The point, in body-local space.</param>
    /// <returns>The point, in world space.</returns>
    public Vector3 ToWorldPoint(Vector3 localPoint) => B3.b3Body_GetWorldPoint(Id, localPoint);

    /// <summary>Converts a direction from world space into body-local space.</summary>
    /// <param name="worldVector">The direction, in world space.</param>
    /// <returns>The direction, in body-local space.</returns>
    public Vector3 ToLocalVector(Vector3 worldVector) => B3.b3Body_GetLocalVector(Id, worldVector);

    /// <summary>Converts a direction from body-local space into world space.</summary>
    /// <param name="localVector">The direction, in body-local space.</param>
    /// <returns>The direction, in world space.</returns>
    public Vector3 ToWorldVector(Vector3 localVector) => B3.b3Body_GetWorldVector(Id, localVector);

    /// <summary>Gets the velocity of a point attached to this body.</summary>
    /// <param name="worldPoint">The point, in world space.</param>
    /// <returns>The velocity of that point, usually in metres per second.</returns>
    /// <remarks>
    /// Accounts for rotation, so a point on the rim of a spinning wheel moves
    /// even when the wheel's centre does not.
    /// </remarks>
    public Vector3 GetVelocityAt(Vector3 worldPoint) => B3.b3Body_GetWorldPointVelocity(Id, worldPoint);

    // --------------------------------------------------------------- state

    /// <summary>
    /// Recomputes the mass properties from the attached shapes and their densities.
    /// </summary>
    /// <remarks>
    /// Only needed after attaching or removing shapes with the mass update
    /// deferred, or to undo an explicit mass override.
    /// </remarks>
    public void RecomputeMass() => B3.b3Body_ApplyMassFromShapes(Id);

    /// <summary>Removes the body from the simulation without destroying it.</summary>
    /// <remarks>A disabled body neither moves nor collides. Expensive; do not do it every frame.</remarks>
    public void Disable() => B3.b3Body_Disable(Id);

    /// <summary>Returns a disabled body to the simulation.</summary>
    /// <remarks>Expensive.</remarks>
    public void Enable() => B3.b3Body_Enable(Id);

    /// <summary>Destroys this body along with every shape and joint attached to it.</summary>
    /// <remarks>
    /// Every handle to the body, its shapes and its joints becomes invalid.
    /// Destroying the world destroys its bodies, so this is only needed to
    /// remove one while keeping the rest.
    /// </remarks>
    public void Destroy() => B3.b3DestroyBody(Id);
}
