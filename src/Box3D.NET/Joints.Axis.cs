// SPDX-License-Identifier: MIT

using System.Numerics;
using Box3D.Native;

namespace Box3D;

/// <summary>
/// Creates a hinge: body B may rotate about the z axis of the joint frame,
/// and nothing else.
/// </summary>
/// <example>
/// <code>
/// var def = RevoluteJointDefinition.Hinge(frame, door, hingePoint, Vector3.UnitY) with
/// {
///     LimitsEnabled = true,
///     LowerAngle = 0.0f,
///     UpperAngle = MathF.PI * 0.5f,   // opens ninety degrees
///     MotorEnabled = true,
///     MotorSpeed = 1.5f,
///     MaxMotorTorque = 20.0f,          // a door closer
/// };
///
/// RevoluteJoint hinge = world.CreateRevoluteJoint(def);
/// </code>
/// </example>
public readonly record struct RevoluteJointDefinition
{
    /// <summary>Gets the settings shared by every joint.</summary>
    public JointDefinition Base { get; init; }

    /// <summary>Gets the angle treated as zero by the limits, in radians.</summary>
    public float TargetAngle { get; init; }

    /// <summary>Gets a value indicating whether the angular spring is enabled.</summary>
    public bool SpringEnabled { get; init; }

    /// <summary>Gets the spring stiffness in cycles per second.</summary>
    /// <remarks>Keep it below a quarter of the simulation rate; at 60 Hz that means 15 Hz or less.</remarks>
    public float Hertz { get; init; }

    /// <summary>Gets the spring damping ratio, where one is critical damping.</summary>
    public float DampingRatio { get; init; }

    /// <summary>Gets a value indicating whether the angle limits are enforced.</summary>
    public bool LimitsEnabled { get; init; }

    /// <summary>Gets the lower angle limit in radians. At least minus 0.99 pi.</summary>
    public float LowerAngle { get; init; }

    /// <summary>Gets the upper angle limit in radians. At most 0.99 pi.</summary>
    public float UpperAngle { get; init; }

    /// <summary>Gets a value indicating whether the motor is enabled.</summary>
    public bool MotorEnabled { get; init; }

    /// <summary>Gets the maximum torque the motor may apply, in newton-metres.</summary>
    public float MaxMotorTorque { get; init; }

    /// <summary>Gets the speed the motor drives towards, in radians per second.</summary>
    public float MotorSpeed { get; init; }

    /// <summary>Gets the engine defaults, with no bodies attached.</summary>
    public static RevoluteJointDefinition Default => FromNative(NativeDefaults.RevoluteJoint);

    /// <summary>
    /// Creates a hinge between two bodies about a world-space axis.
    /// </summary>
    /// <param name="bodyA">The body the hinge is anchored to, such as a door frame.</param>
    /// <param name="bodyB">The body that swings, such as the door.</param>
    /// <param name="worldAnchor">The hinge point, in world space.</param>
    /// <param name="worldAxis">The axis to turn about, in world space.</param>
    /// <returns>The definition.</returns>
    public static RevoluteJointDefinition Hinge(
        Body bodyA,
        Body bodyB,
        Vector3 worldAnchor,
        Vector3 worldAxis)
    {
        (JointFrame frameA, JointFrame frameB) = Joint.FramesFromWorldAnchor(bodyA, bodyB, worldAnchor, worldAxis);

        return Default with { Base = JointDefinition.Connect(bodyA, bodyB, frameA, frameB) };
    }

    internal b3RevoluteJointDef ToNative()
    {
        b3RevoluteJointDef def = NativeDefaults.RevoluteJoint;

        def.@base = Base.ToNative();
        def.targetAngle = TargetAngle;
        def.enableSpring = SpringEnabled;
        def.hertz = Hertz;
        def.dampingRatio = DampingRatio;
        def.enableLimit = LimitsEnabled;
        def.lowerAngle = LowerAngle;
        def.upperAngle = UpperAngle;
        def.enableMotor = MotorEnabled;
        def.maxMotorTorque = MaxMotorTorque;
        def.motorSpeed = MotorSpeed;

        return def;
    }

    internal static RevoluteJointDefinition FromNative(in b3RevoluteJointDef def) => new()
    {
        Base = JointDefinition.FromNative(def.@base),
        TargetAngle = def.targetAngle,
        SpringEnabled = def.enableSpring,
        Hertz = def.hertz,
        DampingRatio = def.dampingRatio,
        LimitsEnabled = def.enableLimit,
        LowerAngle = def.lowerAngle,
        UpperAngle = def.upperAngle,
        MotorEnabled = def.enableMotor,
        MaxMotorTorque = def.maxMotorTorque,
        MotorSpeed = def.motorSpeed,
    };
}

/// <summary>
/// A hinge. Body B rotates about one axis relative to body A.
/// </summary>
public readonly record struct RevoluteJoint
{
    internal RevoluteJoint(b3JointId id) => NativeId = id;

    /// <summary>Gets the native identifier this handle wraps.</summary>
    /// <remarks>
    /// Internal on purpose: exposing it would put a Box3D.Native type in the
    /// public surface and weld the C ABI to this API. Reach it through
    /// <c>Box3D.Interop.NativeInterop.ToNativeId</c> when you genuinely need the
    /// C function this layer does not wrap.
    /// </remarks>
    internal b3JointId NativeId { get; }

    // Reached through here rather than through NativeId so that a stale handle
    // is rejected instead of being indexed into a freed slot. See Validate.
    private b3JointId Id => Validate.Handle(NativeId);

    /// <summary>Gets the generic handle, carrying what every joint has in common.</summary>
    public Joint AsJoint => new(NativeId);

    /// <summary>Gets the current angle relative to the target angle, in radians.</summary>
    public float Angle => B3.b3RevoluteJoint_GetAngle(Id);

    /// <summary>Gets or sets the angle treated as zero by the limits, in radians.</summary>
    public float TargetAngle
    {
        get => B3.b3RevoluteJoint_GetTargetAngle(Id);
        set => B3.b3RevoluteJoint_SetTargetAngle(Id, value);
    }

    /// <summary>Gets or sets a value indicating whether the angular spring is enabled.</summary>
    public bool SpringEnabled
    {
        get => B3.b3RevoluteJoint_IsSpringEnabled(Id);
        set => B3.b3RevoluteJoint_EnableSpring(Id, value);
    }

    /// <summary>Gets or sets the spring stiffness in cycles per second.</summary>
    public float Hertz
    {
        get => B3.b3RevoluteJoint_GetSpringHertz(Id);
        set => B3.b3RevoluteJoint_SetSpringHertz(Id, value);
    }

    /// <summary>Gets or sets the spring damping ratio.</summary>
    public float DampingRatio
    {
        get => B3.b3RevoluteJoint_GetSpringDampingRatio(Id);
        set => B3.b3RevoluteJoint_SetSpringDampingRatio(Id, value);
    }

    /// <summary>Gets or sets a value indicating whether the angle limits are enforced.</summary>
    public bool LimitsEnabled
    {
        get => B3.b3RevoluteJoint_IsLimitEnabled(Id);
        set => B3.b3RevoluteJoint_EnableLimit(Id, value);
    }

    /// <summary>Gets the lower angle limit in radians.</summary>
    public float LowerAngle => B3.b3RevoluteJoint_GetLowerLimit(Id);

    /// <summary>Gets the upper angle limit in radians.</summary>
    public float UpperAngle => B3.b3RevoluteJoint_GetUpperLimit(Id);

    /// <summary>Gets or sets a value indicating whether the motor is enabled.</summary>
    public bool MotorEnabled
    {
        get => B3.b3RevoluteJoint_IsMotorEnabled(Id);
        set => B3.b3RevoluteJoint_EnableMotor(Id, value);
    }

    /// <summary>Gets or sets the speed the motor drives towards, in radians per second.</summary>
    public float MotorSpeed
    {
        get => B3.b3RevoluteJoint_GetMotorSpeed(Id);
        set => B3.b3RevoluteJoint_SetMotorSpeed(Id, value);
    }

    /// <summary>Gets or sets the maximum torque the motor may apply, in newton-metres.</summary>
    public float MaxMotorTorque
    {
        get => B3.b3RevoluteJoint_GetMaxMotorTorque(Id);
        set => B3.b3RevoluteJoint_SetMaxMotorTorque(Id, value);
    }

    /// <summary>Gets the torque the motor is currently applying, in newton-metres.</summary>
    public float MotorTorque => B3.b3RevoluteJoint_GetMotorTorque(Id);

    /// <summary>Sets both angle limits.</summary>
    /// <param name="lower">The lower limit in radians. At least minus 0.99 pi.</param>
    /// <param name="upper">The upper limit in radians. At most 0.99 pi.</param>
    public void SetLimits(float lower, float upper) => B3.b3RevoluteJoint_SetLimits(Id, lower, upper);

    /// <summary>Destroys this joint.</summary>
    /// <param name="wakeBodies">Whether to wake the attached bodies.</param>
    public void Destroy(bool wakeBodies = true) => B3.b3DestroyJoint(Id, wakeBodies);
}

/// <summary>
/// Creates a slider: body B translates along the x axis of the joint frame and
/// cannot rotate relative to body A.
/// </summary>
/// <example>
/// <code>
/// // A lift that travels four metres straight up, driven by a motor.
/// var def = PrismaticJointDefinition.Slider(shaft, platform, basePoint, Vector3.UnitY) with
/// {
///     LimitsEnabled = true,
///     LowerTranslation = 0.0f,
///     UpperTranslation = 4.0f,
///     MotorEnabled = true,
///     MotorSpeed = 1.0f,
///     MaxMotorForce = 5000.0f,
/// };
/// </code>
/// </example>
public readonly record struct PrismaticJointDefinition
{
    /// <summary>Gets the settings shared by every joint.</summary>
    public JointDefinition Base { get; init; }

    /// <summary>Gets a value indicating whether the spring along the slide axis is enabled.</summary>
    public bool SpringEnabled { get; init; }

    /// <summary>Gets the spring stiffness in cycles per second.</summary>
    public float Hertz { get; init; }

    /// <summary>Gets the spring damping ratio.</summary>
    public float DampingRatio { get; init; }

    /// <summary>Gets the translation the spring drives towards, in metres.</summary>
    public float TargetTranslation { get; init; }

    /// <summary>Gets a value indicating whether the translation limits are enforced.</summary>
    public bool LimitsEnabled { get; init; }

    /// <summary>Gets the lower translation limit.</summary>
    public float LowerTranslation { get; init; }

    /// <summary>Gets the upper translation limit.</summary>
    public float UpperTranslation { get; init; }

    /// <summary>Gets a value indicating whether the motor is enabled.</summary>
    public bool MotorEnabled { get; init; }

    /// <summary>Gets the maximum force the motor may apply, in newtons.</summary>
    public float MaxMotorForce { get; init; }

    /// <summary>Gets the speed the motor drives towards, in metres per second.</summary>
    public float MotorSpeed { get; init; }

    /// <summary>Gets the engine defaults, with no bodies attached.</summary>
    public static PrismaticJointDefinition Default => FromNative(NativeDefaults.PrismaticJoint);

    /// <summary>
    /// Creates a slider between two bodies along a world-space direction.
    /// </summary>
    /// <param name="bodyA">The body the slider is anchored to.</param>
    /// <param name="bodyB">The body that slides.</param>
    /// <param name="worldAnchor">The point where the translation reads zero, in world space.</param>
    /// <param name="worldAxis">The direction of travel, in world space.</param>
    /// <returns>The definition.</returns>
    /// <remarks>
    /// A prismatic joint slides along its frame x axis, so the axis given here is
    /// placed there rather than on z.
    /// </remarks>
    public static PrismaticJointDefinition Slider(
        Body bodyA,
        Body bodyB,
        Vector3 worldAnchor,
        Vector3 worldAxis)
    {
        (JointFrame frameA, JointFrame frameB) = FramesForSlideAxis(bodyA, bodyB, worldAnchor, worldAxis);

        return Default with { Base = JointDefinition.Connect(bodyA, bodyB, frameA, frameB) };
    }

    /// <summary>
    /// Builds frames whose x axis lies along the requested world direction, which
    /// is what the prismatic and wheel joints slide along.
    /// </summary>
    internal static (JointFrame FrameA, JointFrame FrameB) FramesForSlideAxis(
        Body bodyA,
        Body bodyB,
        Vector3 worldAnchor,
        Vector3 worldAxis)
    {
        Vector3 axis = B3Math.b3Normalize(worldAxis);
        if (axis == Vector3.Zero)
        {
            throw new System.ArgumentException("The slide axis must not be a zero vector.", nameof(worldAxis));
        }

        Quaternion worldRotation = B3.b3ComputeQuatBetweenUnitVectors(Vector3.UnitX, axis);

        JointFrame frameA = new(
            bodyA.ToLocalPoint(worldAnchor),
            B3Math.b3InvMulQuat(bodyA.Rotation, worldRotation));

        JointFrame frameB = new(
            bodyB.ToLocalPoint(worldAnchor),
            B3Math.b3InvMulQuat(bodyB.Rotation, worldRotation));

        return (frameA, frameB);
    }

    internal b3PrismaticJointDef ToNative()
    {
        b3PrismaticJointDef def = NativeDefaults.PrismaticJoint;

        def.@base = Base.ToNative();
        def.enableSpring = SpringEnabled;
        def.hertz = Hertz;
        def.dampingRatio = DampingRatio;
        def.targetTranslation = TargetTranslation;
        def.enableLimit = LimitsEnabled;
        def.lowerTranslation = LowerTranslation;
        def.upperTranslation = UpperTranslation;
        def.enableMotor = MotorEnabled;
        def.maxMotorForce = MaxMotorForce;
        def.motorSpeed = MotorSpeed;

        return def;
    }

    internal static PrismaticJointDefinition FromNative(in b3PrismaticJointDef def) => new()
    {
        Base = JointDefinition.FromNative(def.@base),
        SpringEnabled = def.enableSpring,
        Hertz = def.hertz,
        DampingRatio = def.dampingRatio,
        TargetTranslation = def.targetTranslation,
        LimitsEnabled = def.enableLimit,
        LowerTranslation = def.lowerTranslation,
        UpperTranslation = def.upperTranslation,
        MotorEnabled = def.enableMotor,
        MaxMotorForce = def.maxMotorForce,
        MotorSpeed = def.motorSpeed,
    };
}

/// <summary>
/// A slider. Body B translates along one axis and cannot rotate relative to body A.
/// </summary>
public readonly record struct PrismaticJoint
{
    internal PrismaticJoint(b3JointId id) => NativeId = id;

    /// <summary>Gets the native identifier this handle wraps.</summary>
    /// <remarks>
    /// Internal on purpose: exposing it would put a Box3D.Native type in the
    /// public surface and weld the C ABI to this API. Reach it through
    /// <c>Box3D.Interop.NativeInterop.ToNativeId</c> when you genuinely need the
    /// C function this layer does not wrap.
    /// </remarks>
    internal b3JointId NativeId { get; }

    // Reached through here rather than through NativeId so that a stale handle
    // is rejected instead of being indexed into a freed slot. See Validate.
    private b3JointId Id => Validate.Handle(NativeId);

    /// <summary>Gets the generic handle, carrying what every joint has in common.</summary>
    public Joint AsJoint => new(NativeId);

    /// <summary>Gets the current translation along the slide axis, usually in metres.</summary>
    public float Translation => B3.b3PrismaticJoint_GetTranslation(Id);

    /// <summary>Gets the current speed along the slide axis, usually in metres per second.</summary>
    public float Speed => B3.b3PrismaticJoint_GetSpeed(Id);

    /// <summary>Gets or sets a value indicating whether the spring is enabled.</summary>
    public bool SpringEnabled
    {
        get => B3.b3PrismaticJoint_IsSpringEnabled(Id);
        set => B3.b3PrismaticJoint_EnableSpring(Id, value);
    }

    /// <summary>Gets or sets the spring stiffness in cycles per second.</summary>
    public float Hertz
    {
        get => B3.b3PrismaticJoint_GetSpringHertz(Id);
        set => B3.b3PrismaticJoint_SetSpringHertz(Id, value);
    }

    /// <summary>Gets or sets the spring damping ratio.</summary>
    public float DampingRatio
    {
        get => B3.b3PrismaticJoint_GetSpringDampingRatio(Id);
        set => B3.b3PrismaticJoint_SetSpringDampingRatio(Id, value);
    }

    /// <summary>Gets or sets the translation the spring drives towards.</summary>
    public float TargetTranslation
    {
        get => B3.b3PrismaticJoint_GetTargetTranslation(Id);
        set => B3.b3PrismaticJoint_SetTargetTranslation(Id, value);
    }

    /// <summary>Gets or sets a value indicating whether the translation limits are enforced.</summary>
    public bool LimitsEnabled
    {
        get => B3.b3PrismaticJoint_IsLimitEnabled(Id);
        set => B3.b3PrismaticJoint_EnableLimit(Id, value);
    }

    /// <summary>Gets the lower translation limit.</summary>
    public float LowerTranslation => B3.b3PrismaticJoint_GetLowerLimit(Id);

    /// <summary>Gets the upper translation limit.</summary>
    public float UpperTranslation => B3.b3PrismaticJoint_GetUpperLimit(Id);

    /// <summary>Gets or sets a value indicating whether the motor is enabled.</summary>
    public bool MotorEnabled
    {
        get => B3.b3PrismaticJoint_IsMotorEnabled(Id);
        set => B3.b3PrismaticJoint_EnableMotor(Id, value);
    }

    /// <summary>Gets or sets the speed the motor drives towards, in metres per second.</summary>
    public float MotorSpeed
    {
        get => B3.b3PrismaticJoint_GetMotorSpeed(Id);
        set => B3.b3PrismaticJoint_SetMotorSpeed(Id, value);
    }

    /// <summary>Gets or sets the maximum force the motor may apply, in newtons.</summary>
    public float MaxMotorForce
    {
        get => B3.b3PrismaticJoint_GetMaxMotorForce(Id);
        set => B3.b3PrismaticJoint_SetMaxMotorForce(Id, value);
    }

    /// <summary>Gets the force the motor is currently applying, in newtons.</summary>
    public float MotorForce => B3.b3PrismaticJoint_GetMotorForce(Id);

    /// <summary>Sets both translation limits.</summary>
    /// <param name="lower">The lower limit.</param>
    /// <param name="upper">The upper limit.</param>
    public void SetLimits(float lower, float upper) => B3.b3PrismaticJoint_SetLimits(Id, lower, upper);

    /// <summary>Destroys this joint.</summary>
    /// <param name="wakeBodies">Whether to wake the attached bodies.</param>
    public void Destroy(bool wakeBodies = true) => B3.b3DestroyJoint(Id, wakeBodies);
}
