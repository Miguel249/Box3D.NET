// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using Box3D.Native;

namespace Box3D;

/// <summary>
/// The kind of constraint a joint applies.
/// </summary>
public enum JointType
{
    /// <summary>Keeps the z axes of the two joint frames aligned with a spring.</summary>
    Parallel = 0,

    /// <summary>Holds two points a fixed distance apart, optionally as a spring.</summary>
    Distance = 1,

    /// <summary>Applies no constraint; it only stops two bodies colliding.</summary>
    Filter = 2,

    /// <summary>Drives the relative position and velocity of two bodies.</summary>
    Motor = 3,

    /// <summary>Allows sliding along one axis with no relative rotation. A slider.</summary>
    Prismatic = 4,

    /// <summary>Allows rotation about one axis. A hinge.</summary>
    Revolute = 5,

    /// <summary>Allows rotation about a shared point. A ball-in-socket.</summary>
    Spherical = 6,

    /// <summary>Holds two bodies rigidly together, optionally with springs.</summary>
    Weld = 7,

    /// <summary>Models a wheel with suspension, spin and optional steering.</summary>
    Wheel = 8,
}

/// <summary>
/// A joint frame: where a joint attaches to a body, and how it is oriented there.
/// </summary>
/// <remarks>
/// <para>
/// Frames are measured from the body origin rather than from the centre of mass,
/// for two reasons: you rarely know where the centre of mass will end up, and
/// attaching or removing a shape moves it, which would silently move every joint
/// on the body.
/// </para>
/// <para>
/// Which axis matters depends on the joint. A revolute joint turns about the
/// frame's z axis; a prismatic joint slides along its x axis. Use
/// <see cref="Joint.FramesFromWorldAnchor(Body, Body, Vector3, Vector3)"/> to
/// build a matched pair from a world-space anchor and axis instead of working
/// them out by hand.
/// </para>
/// </remarks>
public readonly record struct JointFrame
{
    /// <summary>Initializes a new instance of the <see cref="JointFrame"/> struct.</summary>
    /// <param name="position">The attachment point, relative to the body origin.</param>
    /// <param name="rotation">The frame orientation, relative to the body.</param>
    public JointFrame(Vector3 position, Quaternion rotation)
    {
        Position = position;
        Rotation = rotation;
    }

    /// <summary>Initializes a new instance with no rotation.</summary>
    /// <param name="position">The attachment point, relative to the body origin.</param>
    public JointFrame(Vector3 position)
        : this(position, Quaternion.Identity)
    {
    }

    /// <summary>Gets the attachment point, relative to the body origin.</summary>
    public Vector3 Position { get; init; }

    /// <summary>Gets the frame orientation, relative to the body.</summary>
    public Quaternion Rotation { get; init; }

    /// <summary>Gets the frame at the body origin with no rotation.</summary>
    public static JointFrame Identity => new(Vector3.Zero, Quaternion.Identity);

    internal b3Transform ToNative() => new() { p = Position, q = Rotation };

    internal static JointFrame FromNative(in b3Transform transform) => new(transform.p, transform.q);
}

/// <summary>
/// The settings every joint shares.
/// </summary>
/// <remarks>
/// The specific joint definitions embed this, mirroring how the C API composes
/// them. Build one with <see cref="Joint.FramesFromWorldAnchor(Body, Body, Vector3, Vector3)"/>
/// unless you already know the local frames.
/// </remarks>
public readonly record struct JointDefinition
{
    /// <summary>Gets the first attached body.</summary>
    public Body BodyA { get; init; }

    /// <summary>Gets the second attached body.</summary>
    public Body BodyB { get; init; }

    /// <summary>Gets the joint frame on <see cref="BodyA"/>.</summary>
    public JointFrame FrameA { get; init; }

    /// <summary>Gets the joint frame on <see cref="BodyB"/>.</summary>
    public JointFrame FrameB { get; init; }

    /// <summary>Gets a value indicating whether the two attached bodies may collide.</summary>
    /// <remarks>False by default, which is what you want for most assemblies.</remarks>
    public bool CollideConnected { get; init; }

    /// <summary>
    /// Gets the force above which the joint raises an event, in newtons.
    /// </summary>
    /// <remarks>Use it to detect a joint under enough strain to break.</remarks>
    public float ForceThreshold { get; init; }

    /// <summary>Gets the torque above which the joint raises an event, in newton-metres.</summary>
    public float TorqueThreshold { get; init; }

    /// <summary>Gets the constraint stiffness in cycles per second. Advanced.</summary>
    public float ConstraintHertz { get; init; }

    /// <summary>Gets the constraint damping ratio. Advanced.</summary>
    public float ConstraintDampingRatio { get; init; }

    /// <summary>Gets the scale used when drawing this joint.</summary>
    public float DrawScale { get; init; }

    /// <summary>Creates a definition connecting two bodies with the given frames.</summary>
    /// <param name="bodyA">The first body.</param>
    /// <param name="bodyB">The second body.</param>
    /// <param name="frameA">The frame on the first body.</param>
    /// <param name="frameB">The frame on the second body.</param>
    /// <returns>The definition, with the remaining values taken from the engine defaults.</returns>
    public static JointDefinition Connect(Body bodyA, Body bodyB, JointFrame frameA, JointFrame frameB)
    {
        JointDefinition definition = Default;

        return definition with
        {
            BodyA = bodyA,
            BodyB = bodyB,
            FrameA = frameA,
            FrameB = frameB,
        };
    }

    /// <summary>Gets the engine's default joint settings, with no bodies attached.</summary>
    public static JointDefinition Default
    {
        get
        {
            // Every b3Default*JointDef fills in the same base, so any of them
            // serves as the source of the shared defaults.
            b3RevoluteJointDef def = NativeDefaults.RevoluteJoint;
            return FromNative(def.@base);
        }
    }

    internal unsafe b3JointDef ToNative()
    {
        b3RevoluteJointDef template = NativeDefaults.RevoluteJoint;
        b3JointDef def = template.@base;

        def.bodyIdA = BodyA.NativeId;
        def.bodyIdB = BodyB.NativeId;
        def.localFrameA = FrameA.ToNative();
        def.localFrameB = FrameB.ToNative();
        def.collideConnected = CollideConnected;
        def.forceThreshold = ForceThreshold;
        def.torqueThreshold = TorqueThreshold;
        def.constraintHertz = ConstraintHertz;
        def.constraintDampingRatio = ConstraintDampingRatio;
        def.drawScale = DrawScale;

        return def;
    }

    internal static JointDefinition FromNative(in b3JointDef def) => new()
    {
        BodyA = new Body(def.bodyIdA),
        BodyB = new Body(def.bodyIdB),
        FrameA = JointFrame.FromNative(def.localFrameA),
        FrameB = JointFrame.FromNative(def.localFrameB),
        CollideConnected = def.collideConnected,
        ForceThreshold = def.forceThreshold,
        TorqueThreshold = def.torqueThreshold,
        ConstraintHertz = def.constraintHertz,
        ConstraintDampingRatio = def.constraintDampingRatio,
        DrawScale = def.drawScale,
    };
}

/// <summary>
/// A constraint between two bodies.
/// </summary>
/// <remarks>
/// <para>
/// Like <see cref="Body"/> and <see cref="Shape"/>, this is a handle rather than
/// an object. It carries what every joint has in common; the specific types such
/// as <see cref="RevoluteJoint"/> add limits, motors and springs.
/// </para>
/// <para>
/// A joint dies with either of the bodies it connects, and with its world.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var hinge = world.CreateRevoluteJoint(
///     RevoluteJointDefinition.Hinge(door, frame, worldHingePoint, Vector3.UnitY) with
///     {
///         LimitsEnabled = true,
///         LowerAngle = 0.0f,
///         UpperAngle = MathF.PI * 0.5f,
///     });
///
/// // The generic handle is always available.
/// Console.WriteLine(hinge.AsJoint.Type); // Revolute
/// </code>
/// </example>
public readonly record struct Joint
{
    internal Joint(b3JointId id) => NativeId = id;

    /// <summary>Gets the native identifier this handle wraps.</summary>
    /// <remarks>
    /// Internal on purpose: exposing it would put a Box3D.Native type in the
    /// public surface and weld the C ABI to this API. Reach it through
    /// <c>Box3D.Interop.NativeInterop.ToNativeId</c> when you genuinely need the
    /// C function this layer does not wrap.
    /// </remarks>
    internal b3JointId NativeId { get; }

    /// <summary>Gets a value indicating whether this handle refers to a live joint.</summary>
    public bool IsValid => !NativeId.IsNull && B3.b3Joint_IsValid(NativeId);

    /// <summary>Gets the kind of constraint this joint applies.</summary>
    public JointType Type => (JointType)B3.b3Joint_GetType(NativeId);

    /// <summary>Gets the first attached body.</summary>
    public Body BodyA => new(B3.b3Joint_GetBodyA(NativeId));

    /// <summary>Gets the second attached body.</summary>
    public Body BodyB => new(B3.b3Joint_GetBodyB(NativeId));

    /// <summary>Gets the world this joint belongs to.</summary>
    public WorldReference World => new(B3.b3Joint_GetWorld(NativeId));

    /// <summary>
    /// Gets or sets the application identifier attached to this joint.
    /// </summary>
    /// <remarks>
    /// Joint events report this alongside the joint, so it is the way to find
    /// out which door or which rope exceeded its threshold without a lookup.
    /// See <see cref="Box3D.UserData"/>.
    /// </remarks>
    public unsafe ulong UserData
    {
        get => Box3D.UserData.FromPointer(B3.b3Joint_GetUserData(NativeId));
        set => B3.b3Joint_SetUserData(NativeId, Box3D.UserData.ToPointer(value));
    }

    /// <summary>Gets or sets a value indicating whether the two attached bodies may collide.</summary>
    public bool CollideConnected
    {
        get => B3.b3Joint_GetCollideConnected(NativeId);
        set => B3.b3Joint_SetCollideConnected(NativeId, value);
    }

    /// <summary>Gets or sets the joint frame on <see cref="BodyA"/>.</summary>
    public JointFrame FrameA
    {
        get => JointFrame.FromNative(B3.b3Joint_GetLocalFrameA(NativeId));
        set => B3.b3Joint_SetLocalFrameA(NativeId, value.ToNative());
    }

    /// <summary>Gets or sets the joint frame on <see cref="BodyB"/>.</summary>
    public JointFrame FrameB
    {
        get => JointFrame.FromNative(B3.b3Joint_GetLocalFrameB(NativeId));
        set => B3.b3Joint_SetLocalFrameB(NativeId, value.ToNative());
    }

    /// <summary>Gets the force the constraint is currently applying, in newtons.</summary>
    /// <remarks>
    /// Compare its length against a threshold to model a joint that breaks under
    /// load.
    /// </remarks>
    public Vector3 ConstraintForce => B3.b3Joint_GetConstraintForce(NativeId);

    /// <summary>Gets the torque the constraint is currently applying, in newton-metres.</summary>
    public Vector3 ConstraintTorque => B3.b3Joint_GetConstraintTorque(NativeId);

    /// <summary>Gets how far the joint is from satisfying its positional constraint, usually in metres.</summary>
    public float LinearSeparation => B3.b3Joint_GetLinearSeparation(NativeId);

    /// <summary>Gets how far the joint is from satisfying its angular constraint, usually in radians.</summary>
    public float AngularSeparation => B3.b3Joint_GetAngularSeparation(NativeId);

    /// <summary>Gets or sets the force above which this joint raises an event, in newtons.</summary>
    public float ForceThreshold
    {
        get => B3.b3Joint_GetForceThreshold(NativeId);
        set => B3.b3Joint_SetForceThreshold(NativeId, value);
    }

    /// <summary>Gets or sets the torque above which this joint raises an event, in newton-metres.</summary>
    public float TorqueThreshold
    {
        get => B3.b3Joint_GetTorqueThreshold(NativeId);
        set => B3.b3Joint_SetTorqueThreshold(NativeId, value);
    }

    /// <summary>Wakes both attached bodies.</summary>
    public void WakeBodies() => B3.b3Joint_WakeBodies(NativeId);

    /// <summary>Sets the constraint softness. Advanced.</summary>
    /// <param name="hertz">The stiffness in cycles per second.</param>
    /// <param name="dampingRatio">The damping ratio, where one is critical damping.</param>
    public void SetConstraintTuning(float hertz, float dampingRatio) =>
        B3.b3Joint_SetConstraintTuning(NativeId, hertz, dampingRatio);

    /// <summary>Destroys this joint.</summary>
    /// <param name="wakeBodies">Whether to wake the attached bodies.</param>
    /// <remarks>Destroying either attached body destroys the joint too.</remarks>
    public void Destroy(bool wakeBodies = true) => B3.b3DestroyJoint(NativeId, wakeBodies);

    /// <summary>
    /// Builds a matched pair of joint frames from a world-space anchor and axis.
    /// </summary>
    /// <param name="bodyA">The first body.</param>
    /// <param name="bodyB">The second body.</param>
    /// <param name="worldAnchor">The point both bodies are pinned to, in world space.</param>
    /// <param name="worldAxis">
    /// The axis the joint acts about, in world space. It is placed on the frames'
    /// z axis, which is what revolute, spherical and parallel joints turn about.
    /// Prismatic and wheel joints use the frames' x axis, so pass the sliding
    /// direction as the axis and the joint will slide along it.
    /// </param>
    /// <returns>The frames, expressed in each body's local space.</returns>
    /// <remarks>
    /// This is the calculation people otherwise get wrong: the two frames must
    /// describe the same world pose from each body's point of view, or the joint
    /// starts out already violated and snaps on the first step.
    /// </remarks>
    /// <example>
    /// <code>
    /// // A door hinged on its left edge, turning about the world up axis.
    /// var (frameA, frameB) = Joint.FramesFromWorldAnchor(
    ///     frame, door, hingePoint, Vector3.UnitY);
    ///
    /// world.CreateRevoluteJoint(new RevoluteJointDefinition
    /// {
    ///     Base = JointDefinition.Connect(frame, door, frameA, frameB),
    /// });
    /// </code>
    /// </example>
    public static (JointFrame FrameA, JointFrame FrameB) FramesFromWorldAnchor(
        Body bodyA,
        Body bodyB,
        Vector3 worldAnchor,
        Vector3 worldAxis)
    {
        Vector3 axis = B3Math.b3Normalize(worldAxis);
        if (axis == Vector3.Zero)
        {
            throw new ArgumentException("The joint axis must not be a zero vector.", nameof(worldAxis));
        }

        // Both frames must describe the same world orientation, so the rotation
        // taking the frame z axis onto the requested axis is computed once and
        // then expressed relative to each body.
        Quaternion worldRotation = B3.b3ComputeQuatBetweenUnitVectors(Vector3.UnitZ, axis);

        JointFrame frameA = new(
            bodyA.ToLocalPoint(worldAnchor),
            B3Math.b3InvMulQuat(bodyA.Rotation, worldRotation));

        JointFrame frameB = new(
            bodyB.ToLocalPoint(worldAnchor),
            B3Math.b3InvMulQuat(bodyB.Rotation, worldRotation));

        return (frameA, frameB);
    }
}
