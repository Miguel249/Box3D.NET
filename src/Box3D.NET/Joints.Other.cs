// SPDX-License-Identifier: MIT

using System.Numerics;
using Box3D.Native;

namespace Box3D;

/// <summary>
/// Holds two bodies rigidly together, with optional softness in either
/// translation or rotation.
/// </summary>
/// <remarks>
/// A stiffness of zero means fully rigid. Long chains of weld joints flex
/// regardless, because the solver is approximate; for a rigid assembly, prefer
/// putting several shapes on one body.
/// </remarks>
/// <example>
/// <code>
/// // A destructible strut: welded rigidly, but reports when it is overloaded.
/// var strut = WeldJointDefinition.Weld(wall, beam, jointPoint) with
/// {
///     Base = WeldJointDefinition.Weld(wall, beam, jointPoint).Base with
///     {
///         ForceThreshold = 5000.0f,
///     },
/// };
/// </code>
/// </example>
public readonly record struct WeldJointDefinition
{
    /// <summary>Gets the settings shared by every joint.</summary>
    public JointDefinition Base { get; init; }

    /// <summary>Gets the translational stiffness in cycles per second. Zero is rigid.</summary>
    public float LinearHertz { get; init; }

    /// <summary>Gets the rotational stiffness in cycles per second. Zero is rigid.</summary>
    public float AngularHertz { get; init; }

    /// <summary>Gets the translational damping ratio, where one is critical damping.</summary>
    public float LinearDampingRatio { get; init; }

    /// <summary>Gets the rotational damping ratio, where one is critical damping.</summary>
    public float AngularDampingRatio { get; init; }

    /// <summary>Gets the engine defaults, with no bodies attached.</summary>
    public static WeldJointDefinition Default => FromNative(NativeDefaults.WeldJoint);

    /// <summary>Welds two bodies together at a shared world-space point.</summary>
    /// <param name="bodyA">The first body.</param>
    /// <param name="bodyB">The second body.</param>
    /// <param name="worldAnchor">The shared point, in world space.</param>
    /// <returns>The definition.</returns>
    public static WeldJointDefinition Weld(Body bodyA, Body bodyB, Vector3 worldAnchor)
    {
        // The weld constrains the full relative pose, so the frames need only
        // agree on the anchor; each keeps its body's current orientation.
        JointFrame frameA = new(bodyA.ToLocalPoint(worldAnchor));
        JointFrame frameB = new(bodyB.ToLocalPoint(worldAnchor));

        return Default with { Base = JointDefinition.Connect(bodyA, bodyB, frameA, frameB) };
    }

    internal b3WeldJointDef ToNative()
    {
        b3WeldJointDef def = NativeDefaults.WeldJoint;

        def.@base = Base.ToNative();
        def.linearHertz = LinearHertz;
        def.angularHertz = AngularHertz;
        def.linearDampingRatio = LinearDampingRatio;
        def.angularDampingRatio = AngularDampingRatio;

        return def;
    }

    internal static WeldJointDefinition FromNative(in b3WeldJointDef def) => new()
    {
        Base = JointDefinition.FromNative(def.@base),
        LinearHertz = def.linearHertz,
        AngularHertz = def.angularHertz,
        LinearDampingRatio = def.linearDampingRatio,
        AngularDampingRatio = def.angularDampingRatio,
    };
}

/// <summary>
/// Holds two bodies rigidly together.
/// </summary>
public readonly record struct WeldJoint
{
    internal WeldJoint(b3JointId id) => NativeId = id;

    /// <summary>Gets the native identifier this handle wraps.</summary>
    /// <remarks>
    /// Internal on purpose: exposing it would put a Box3D.Native type in the
    /// public surface and weld the C ABI to this API. Reach it through
    /// <c>Box3D.Interop.NativeInterop.ToNativeId</c> when you genuinely need the
    /// C function this layer does not wrap.
    /// </remarks>
    internal b3JointId NativeId { get; }

    /// <summary>Gets the generic handle, carrying what every joint has in common.</summary>
    public Joint AsJoint => new(NativeId);

    /// <summary>Gets or sets the translational stiffness in cycles per second. Zero is rigid.</summary>
    public float LinearHertz
    {
        get => B3.b3WeldJoint_GetLinearHertz(NativeId);
        set => B3.b3WeldJoint_SetLinearHertz(NativeId, value);
    }

    /// <summary>Gets or sets the rotational stiffness in cycles per second. Zero is rigid.</summary>
    public float AngularHertz
    {
        get => B3.b3WeldJoint_GetAngularHertz(NativeId);
        set => B3.b3WeldJoint_SetAngularHertz(NativeId, value);
    }

    /// <summary>Gets or sets the translational damping ratio.</summary>
    public float LinearDampingRatio
    {
        get => B3.b3WeldJoint_GetLinearDampingRatio(NativeId);
        set => B3.b3WeldJoint_SetLinearDampingRatio(NativeId, value);
    }

    /// <summary>Gets or sets the rotational damping ratio.</summary>
    public float AngularDampingRatio
    {
        get => B3.b3WeldJoint_GetAngularDampingRatio(NativeId);
        set => B3.b3WeldJoint_SetAngularDampingRatio(NativeId, value);
    }

    /// <summary>Destroys this joint.</summary>
    /// <param name="wakeBodies">Whether to wake the attached bodies.</param>
    public void Destroy(bool wakeBodies = true) => B3.b3DestroyJoint(NativeId, wakeBodies);
}

/// <summary>
/// A wheel with suspension, a spin motor and optional steering.
/// </summary>
/// <remarks>
/// Body A is the chassis and body B is the wheel. The suspension travels along
/// the frame A x axis; the wheel spins about the frame B z axis.
/// </remarks>
/// <example>
/// <code>
/// var wheel = WheelJointDefinition.Suspension(chassis, tyre, hubPoint, Vector3.UnitY) with
/// {
///     SuspensionEnabled = true,
///     SuspensionHertz = 5.0f,
///     SuspensionDampingRatio = 0.7f,
///     SuspensionLimitEnabled = true,
///     LowerSuspensionLimit = -0.25f,
///     UpperSuspensionLimit = 0.1f,
///     SpinMotorEnabled = true,
///     MaxSpinTorque = 400.0f,
/// };
/// </code>
/// </example>
public readonly record struct WheelJointDefinition
{
    /// <summary>Gets the settings shared by every joint.</summary>
    public JointDefinition Base { get; init; }

    /// <summary>Gets a value indicating whether the suspension spring is enabled.</summary>
    public bool SuspensionEnabled { get; init; }

    /// <summary>Gets the suspension stiffness in cycles per second.</summary>
    public float SuspensionHertz { get; init; }

    /// <summary>Gets the suspension damping ratio.</summary>
    public float SuspensionDampingRatio { get; init; }

    /// <summary>Gets a value indicating whether the suspension travel is limited.</summary>
    public bool SuspensionLimitEnabled { get; init; }

    /// <summary>Gets the lower suspension travel limit.</summary>
    public float LowerSuspensionLimit { get; init; }

    /// <summary>Gets the upper suspension travel limit.</summary>
    public float UpperSuspensionLimit { get; init; }

    /// <summary>Gets a value indicating whether the spin motor is enabled.</summary>
    public bool SpinMotorEnabled { get; init; }

    /// <summary>Gets the maximum torque the spin motor may apply, in newton-metres.</summary>
    public float MaxSpinTorque { get; init; }

    /// <summary>Gets the spin speed the motor drives towards, in radians per second.</summary>
    public float SpinSpeed { get; init; }

    /// <summary>Gets a value indicating whether the wheel may steer.</summary>
    public bool SteeringEnabled { get; init; }

    /// <summary>Gets the steering stiffness in cycles per second.</summary>
    public float SteeringHertz { get; init; }

    /// <summary>Gets the steering damping ratio.</summary>
    public float SteeringDampingRatio { get; init; }

    /// <summary>Gets the steering angle driven towards, in radians.</summary>
    public float TargetSteeringAngle { get; init; }

    /// <summary>Gets the maximum steering torque, in newton-metres.</summary>
    public float MaxSteeringTorque { get; init; }

    /// <summary>Gets a value indicating whether the steering angle is limited.</summary>
    public bool SteeringLimitEnabled { get; init; }

    /// <summary>Gets the lower steering limit in radians.</summary>
    public float LowerSteeringLimit { get; init; }

    /// <summary>Gets the upper steering limit in radians.</summary>
    public float UpperSteeringLimit { get; init; }

    /// <summary>Gets the engine defaults, with no bodies attached.</summary>
    public static WheelJointDefinition Default => FromNative(NativeDefaults.WheelJoint);

    /// <summary>Mounts a wheel on a chassis.</summary>
    /// <param name="chassis">The chassis body.</param>
    /// <param name="wheel">The wheel body.</param>
    /// <param name="worldAnchor">The hub position, in world space.</param>
    /// <param name="worldSuspensionAxis">
    /// The direction the suspension travels along, in world space. Usually the
    /// world up axis.
    /// </param>
    /// <returns>The definition.</returns>
    public static WheelJointDefinition Suspension(
        Body chassis,
        Body wheel,
        Vector3 worldAnchor,
        Vector3 worldSuspensionAxis)
    {
        // The suspension travels along the frame x axis, as with a prismatic joint.
        (JointFrame frameA, JointFrame frameB) =
            PrismaticJointDefinition.FramesForSlideAxis(chassis, wheel, worldAnchor, worldSuspensionAxis);

        return Default with { Base = JointDefinition.Connect(chassis, wheel, frameA, frameB) };
    }

    internal b3WheelJointDef ToNative()
    {
        b3WheelJointDef def = NativeDefaults.WheelJoint;

        def.@base = Base.ToNative();
        def.enableSuspensionSpring = SuspensionEnabled;
        def.suspensionHertz = SuspensionHertz;
        def.suspensionDampingRatio = SuspensionDampingRatio;
        def.enableSuspensionLimit = SuspensionLimitEnabled;
        def.lowerSuspensionLimit = LowerSuspensionLimit;
        def.upperSuspensionLimit = UpperSuspensionLimit;
        def.enableSpinMotor = SpinMotorEnabled;
        def.maxSpinTorque = MaxSpinTorque;
        def.spinSpeed = SpinSpeed;
        def.enableSteering = SteeringEnabled;
        def.steeringHertz = SteeringHertz;
        def.steeringDampingRatio = SteeringDampingRatio;
        def.targetSteeringAngle = TargetSteeringAngle;
        def.maxSteeringTorque = MaxSteeringTorque;
        def.enableSteeringLimit = SteeringLimitEnabled;
        def.lowerSteeringLimit = LowerSteeringLimit;
        def.upperSteeringLimit = UpperSteeringLimit;

        return def;
    }

    internal static WheelJointDefinition FromNative(in b3WheelJointDef def) => new()
    {
        Base = JointDefinition.FromNative(def.@base),
        SuspensionEnabled = def.enableSuspensionSpring,
        SuspensionHertz = def.suspensionHertz,
        SuspensionDampingRatio = def.suspensionDampingRatio,
        SuspensionLimitEnabled = def.enableSuspensionLimit,
        LowerSuspensionLimit = def.lowerSuspensionLimit,
        UpperSuspensionLimit = def.upperSuspensionLimit,
        SpinMotorEnabled = def.enableSpinMotor,
        MaxSpinTorque = def.maxSpinTorque,
        SpinSpeed = def.spinSpeed,
        SteeringEnabled = def.enableSteering,
        SteeringHertz = def.steeringHertz,
        SteeringDampingRatio = def.steeringDampingRatio,
        TargetSteeringAngle = def.targetSteeringAngle,
        MaxSteeringTorque = def.maxSteeringTorque,
        SteeringLimitEnabled = def.enableSteeringLimit,
        LowerSteeringLimit = def.lowerSteeringLimit,
        UpperSteeringLimit = def.upperSteeringLimit,
    };
}

/// <summary>
/// A wheel with suspension, spin and optional steering.
/// </summary>
public readonly record struct WheelJoint
{
    internal WheelJoint(b3JointId id) => NativeId = id;

    /// <summary>Gets the native identifier this handle wraps.</summary>
    /// <remarks>
    /// Internal on purpose: exposing it would put a Box3D.Native type in the
    /// public surface and weld the C ABI to this API. Reach it through
    /// <c>Box3D.Interop.NativeInterop.ToNativeId</c> when you genuinely need the
    /// C function this layer does not wrap.
    /// </remarks>
    internal b3JointId NativeId { get; }

    /// <summary>Gets the generic handle, carrying what every joint has in common.</summary>
    public Joint AsJoint => new(NativeId);

    /// <summary>Gets the current spin speed, in radians per second.</summary>
    public float SpinSpeed => B3.b3WheelJoint_GetSpinSpeed(NativeId);

    /// <summary>Gets the torque the spin motor is currently applying, in newton-metres.</summary>
    public float SpinTorque => B3.b3WheelJoint_GetSpinTorque(NativeId);

    /// <summary>Gets the current steering angle, in radians.</summary>
    public float SteeringAngle => B3.b3WheelJoint_GetSteeringAngle(NativeId);

    /// <summary>Gets the torque steering is currently applying, in newton-metres.</summary>
    public float SteeringTorque => B3.b3WheelJoint_GetSteeringTorque(NativeId);

    /// <summary>Gets or sets a value indicating whether the suspension spring is enabled.</summary>
    public bool SuspensionEnabled
    {
        get => B3.b3WheelJoint_IsSuspensionEnabled(NativeId);
        set => B3.b3WheelJoint_EnableSuspension(NativeId, value);
    }

    /// <summary>Gets or sets the suspension stiffness in cycles per second.</summary>
    public float SuspensionHertz
    {
        get => B3.b3WheelJoint_GetSuspensionHertz(NativeId);
        set => B3.b3WheelJoint_SetSuspensionHertz(NativeId, value);
    }

    /// <summary>Gets or sets the suspension damping ratio.</summary>
    public float SuspensionDampingRatio
    {
        get => B3.b3WheelJoint_GetSuspensionDampingRatio(NativeId);
        set => B3.b3WheelJoint_SetSuspensionDampingRatio(NativeId, value);
    }

    /// <summary>Gets or sets a value indicating whether the suspension travel is limited.</summary>
    public bool SuspensionLimitEnabled
    {
        get => B3.b3WheelJoint_IsSuspensionLimitEnabled(NativeId);
        set => B3.b3WheelJoint_EnableSuspensionLimit(NativeId, value);
    }

    /// <summary>Gets or sets a value indicating whether the spin motor is enabled.</summary>
    public bool SpinMotorEnabled
    {
        get => B3.b3WheelJoint_IsSpinMotorEnabled(NativeId);
        set => B3.b3WheelJoint_EnableSpinMotor(NativeId, value);
    }

    /// <summary>Gets or sets the spin speed the motor drives towards, in radians per second.</summary>
    public float SpinMotorSpeed
    {
        get => B3.b3WheelJoint_GetSpinMotorSpeed(NativeId);
        set => B3.b3WheelJoint_SetSpinMotorSpeed(NativeId, value);
    }

    /// <summary>Gets or sets the maximum spin torque, in newton-metres.</summary>
    public float MaxSpinTorque
    {
        get => B3.b3WheelJoint_GetMaxSpinTorque(NativeId);
        set => B3.b3WheelJoint_SetMaxSpinTorque(NativeId, value);
    }

    /// <summary>Gets or sets a value indicating whether the wheel may steer.</summary>
    public bool SteeringEnabled
    {
        get => B3.b3WheelJoint_IsSteeringEnabled(NativeId);
        set => B3.b3WheelJoint_EnableSteering(NativeId, value);
    }

    /// <summary>Gets or sets the steering angle driven towards, in radians.</summary>
    public float TargetSteeringAngle
    {
        get => B3.b3WheelJoint_GetTargetSteeringAngle(NativeId);
        set => B3.b3WheelJoint_SetTargetSteeringAngle(NativeId, value);
    }

    /// <summary>Gets or sets the maximum steering torque, in newton-metres.</summary>
    public float MaxSteeringTorque
    {
        get => B3.b3WheelJoint_GetMaxSteeringTorque(NativeId);
        set => B3.b3WheelJoint_SetMaxSteeringTorque(NativeId, value);
    }

    /// <summary>Sets both suspension travel limits.</summary>
    /// <param name="lower">The lower limit.</param>
    /// <param name="upper">The upper limit.</param>
    public void SetSuspensionLimits(float lower, float upper) =>
        B3.b3WheelJoint_SetSuspensionLimits(NativeId, lower, upper);

    /// <summary>Sets both steering angle limits.</summary>
    /// <param name="lower">The lower limit in radians.</param>
    /// <param name="upper">The upper limit in radians.</param>
    public void SetSteeringLimits(float lower, float upper) =>
        B3.b3WheelJoint_SetSteeringLimits(NativeId, lower, upper);

    /// <summary>Destroys this joint.</summary>
    /// <param name="wakeBodies">Whether to wake the attached bodies.</param>
    public void Destroy(bool wakeBodies = true) => B3.b3DestroyJoint(NativeId, wakeBodies);
}

/// <summary>
/// Drives the relative position and velocity of two bodies while leaving them
/// responsive to collisions.
/// </summary>
/// <remarks>
/// A spring controls the pose and a velocity motor controls the velocity. Both
/// can be used at once, each with its own force and torque limit, which is how
/// you get a body that follows a target but still gets pushed around.
/// </remarks>
public readonly record struct MotorJointDefinition
{
    /// <summary>Gets the settings shared by every joint.</summary>
    public JointDefinition Base { get; init; }

    /// <summary>Gets the relative linear velocity driven towards, in metres per second.</summary>
    public Vector3 LinearVelocity { get; init; }

    /// <summary>Gets the maximum force the velocity motor may apply, in newtons.</summary>
    public float MaxVelocityForce { get; init; }

    /// <summary>Gets the relative angular velocity driven towards, in radians per second.</summary>
    public Vector3 AngularVelocity { get; init; }

    /// <summary>Gets the maximum torque the velocity motor may apply, in newton-metres.</summary>
    public float MaxVelocityTorque { get; init; }

    /// <summary>Gets the positional spring stiffness in cycles per second.</summary>
    public float LinearHertz { get; init; }

    /// <summary>Gets the positional spring damping ratio.</summary>
    public float LinearDampingRatio { get; init; }

    /// <summary>Gets the maximum positional spring force, in newtons.</summary>
    public float MaxSpringForce { get; init; }

    /// <summary>Gets the rotational spring stiffness in cycles per second.</summary>
    public float AngularHertz { get; init; }

    /// <summary>Gets the rotational spring damping ratio.</summary>
    public float AngularDampingRatio { get; init; }

    /// <summary>Gets the maximum rotational spring torque, in newton-metres.</summary>
    public float MaxSpringTorque { get; init; }

    /// <summary>Gets the engine defaults, with no bodies attached.</summary>
    public static MotorJointDefinition Default => FromNative(NativeDefaults.MotorJoint);

    internal b3MotorJointDef ToNative()
    {
        b3MotorJointDef def = NativeDefaults.MotorJoint;

        def.@base = Base.ToNative();
        def.linearVelocity = LinearVelocity;
        def.maxVelocityForce = MaxVelocityForce;
        def.angularVelocity = AngularVelocity;
        def.maxVelocityTorque = MaxVelocityTorque;
        def.linearHertz = LinearHertz;
        def.linearDampingRatio = LinearDampingRatio;
        def.maxSpringForce = MaxSpringForce;
        def.angularHertz = AngularHertz;
        def.angularDampingRatio = AngularDampingRatio;
        def.maxSpringTorque = MaxSpringTorque;

        return def;
    }

    internal static MotorJointDefinition FromNative(in b3MotorJointDef def) => new()
    {
        Base = JointDefinition.FromNative(def.@base),
        LinearVelocity = def.linearVelocity,
        MaxVelocityForce = def.maxVelocityForce,
        AngularVelocity = def.angularVelocity,
        MaxVelocityTorque = def.maxVelocityTorque,
        LinearHertz = def.linearHertz,
        LinearDampingRatio = def.linearDampingRatio,
        MaxSpringForce = def.maxSpringForce,
        AngularHertz = def.angularHertz,
        AngularDampingRatio = def.angularDampingRatio,
        MaxSpringTorque = def.maxSpringTorque,
    };
}

/// <summary>
/// Drives the relative position and velocity of two bodies.
/// </summary>
public readonly record struct MotorJoint
{
    internal MotorJoint(b3JointId id) => NativeId = id;

    /// <summary>Gets the native identifier this handle wraps.</summary>
    /// <remarks>
    /// Internal on purpose: exposing it would put a Box3D.Native type in the
    /// public surface and weld the C ABI to this API. Reach it through
    /// <c>Box3D.Interop.NativeInterop.ToNativeId</c> when you genuinely need the
    /// C function this layer does not wrap.
    /// </remarks>
    internal b3JointId NativeId { get; }

    /// <summary>Gets the generic handle, carrying what every joint has in common.</summary>
    public Joint AsJoint => new(NativeId);

    /// <summary>Gets or sets the relative linear velocity driven towards.</summary>
    public Vector3 LinearVelocity
    {
        get => B3.b3MotorJoint_GetLinearVelocity(NativeId);
        set => B3.b3MotorJoint_SetLinearVelocity(NativeId, value);
    }

    /// <summary>Gets or sets the relative angular velocity driven towards.</summary>
    public Vector3 AngularVelocity
    {
        get => B3.b3MotorJoint_GetAngularVelocity(NativeId);
        set => B3.b3MotorJoint_SetAngularVelocity(NativeId, value);
    }

    /// <summary>Gets or sets the maximum force the velocity motor may apply, in newtons.</summary>
    public float MaxVelocityForce
    {
        get => B3.b3MotorJoint_GetMaxVelocityForce(NativeId);
        set => B3.b3MotorJoint_SetMaxVelocityForce(NativeId, value);
    }

    /// <summary>Gets or sets the maximum torque the velocity motor may apply, in newton-metres.</summary>
    public float MaxVelocityTorque
    {
        get => B3.b3MotorJoint_GetMaxVelocityTorque(NativeId);
        set => B3.b3MotorJoint_SetMaxVelocityTorque(NativeId, value);
    }

    /// <summary>Gets or sets the positional spring stiffness in cycles per second.</summary>
    public float LinearHertz
    {
        get => B3.b3MotorJoint_GetLinearHertz(NativeId);
        set => B3.b3MotorJoint_SetLinearHertz(NativeId, value);
    }

    /// <summary>Gets or sets the positional spring damping ratio.</summary>
    public float LinearDampingRatio
    {
        get => B3.b3MotorJoint_GetLinearDampingRatio(NativeId);
        set => B3.b3MotorJoint_SetLinearDampingRatio(NativeId, value);
    }

    /// <summary>Gets or sets the rotational spring stiffness in cycles per second.</summary>
    public float AngularHertz
    {
        get => B3.b3MotorJoint_GetAngularHertz(NativeId);
        set => B3.b3MotorJoint_SetAngularHertz(NativeId, value);
    }

    /// <summary>Gets or sets the rotational spring damping ratio.</summary>
    public float AngularDampingRatio
    {
        get => B3.b3MotorJoint_GetAngularDampingRatio(NativeId);
        set => B3.b3MotorJoint_SetAngularDampingRatio(NativeId, value);
    }

    /// <summary>Gets or sets the maximum positional spring force, in newtons.</summary>
    public float MaxSpringForce
    {
        get => B3.b3MotorJoint_GetMaxSpringForce(NativeId);
        set => B3.b3MotorJoint_SetMaxSpringForce(NativeId, value);
    }

    /// <summary>Gets or sets the maximum rotational spring torque, in newton-metres.</summary>
    public float MaxSpringTorque
    {
        get => B3.b3MotorJoint_GetMaxSpringTorque(NativeId);
        set => B3.b3MotorJoint_SetMaxSpringTorque(NativeId, value);
    }

    /// <summary>Destroys this joint.</summary>
    /// <param name="wakeBodies">Whether to wake the attached bodies.</param>
    public void Destroy(bool wakeBodies = true) => B3.b3DestroyJoint(NativeId, wakeBodies);
}

/// <summary>
/// Keeps the z axes of the two joint frames aligned, using a spring.
/// </summary>
/// <remarks>Useful for keeping a body upright without locking its rotation outright.</remarks>
public readonly record struct ParallelJointDefinition
{
    /// <summary>Gets the settings shared by every joint.</summary>
    public JointDefinition Base { get; init; }

    /// <summary>Gets the spring stiffness in cycles per second.</summary>
    public float Hertz { get; init; }

    /// <summary>Gets the spring damping ratio.</summary>
    public float DampingRatio { get; init; }

    /// <summary>Gets the maximum torque the spring may apply, in newton-metres.</summary>
    public float MaxTorque { get; init; }

    /// <summary>Gets the engine defaults, with no bodies attached.</summary>
    public static ParallelJointDefinition Default => FromNative(NativeDefaults.ParallelJoint);

    internal b3ParallelJointDef ToNative()
    {
        b3ParallelJointDef def = NativeDefaults.ParallelJoint;

        def.@base = Base.ToNative();
        def.hertz = Hertz;
        def.dampingRatio = DampingRatio;
        def.maxTorque = MaxTorque;

        return def;
    }

    internal static ParallelJointDefinition FromNative(in b3ParallelJointDef def) => new()
    {
        Base = JointDefinition.FromNative(def.@base),
        Hertz = def.hertz,
        DampingRatio = def.dampingRatio,
        MaxTorque = def.maxTorque,
    };
}

/// <summary>
/// Keeps two frames' z axes aligned with a spring.
/// </summary>
public readonly record struct ParallelJoint
{
    internal ParallelJoint(b3JointId id) => NativeId = id;

    /// <summary>Gets the native identifier this handle wraps.</summary>
    /// <remarks>
    /// Internal on purpose: exposing it would put a Box3D.Native type in the
    /// public surface and weld the C ABI to this API. Reach it through
    /// <c>Box3D.Interop.NativeInterop.ToNativeId</c> when you genuinely need the
    /// C function this layer does not wrap.
    /// </remarks>
    internal b3JointId NativeId { get; }

    /// <summary>Gets the generic handle, carrying what every joint has in common.</summary>
    public Joint AsJoint => new(NativeId);

    /// <summary>Gets or sets the spring stiffness in cycles per second.</summary>
    public float Hertz
    {
        get => B3.b3ParallelJoint_GetSpringHertz(NativeId);
        set => B3.b3ParallelJoint_SetSpringHertz(NativeId, value);
    }

    /// <summary>Gets or sets the spring damping ratio.</summary>
    public float DampingRatio
    {
        get => B3.b3ParallelJoint_GetSpringDampingRatio(NativeId);
        set => B3.b3ParallelJoint_SetSpringDampingRatio(NativeId, value);
    }

    /// <summary>Gets or sets the maximum torque the spring may apply, in newton-metres.</summary>
    public float MaxTorque
    {
        get => B3.b3ParallelJoint_GetMaxTorque(NativeId);
        set => B3.b3ParallelJoint_SetMaxTorque(NativeId, value);
    }

    /// <summary>Destroys this joint.</summary>
    /// <param name="wakeBodies">Whether to wake the attached bodies.</param>
    public void Destroy(bool wakeBodies = true) => B3.b3DestroyJoint(NativeId, wakeBodies);
}

/// <summary>
/// Applies no constraint. It exists solely to stop two bodies colliding.
/// </summary>
/// <remarks>
/// Cheaper and more direct than spending a collision category on a single pair.
/// As a side effect the two bodies stay in the same simulation island, so they
/// sleep and wake together.
/// </remarks>
/// <example>
/// <code>
/// // Let a projectile pass through the turret that fired it, and nothing else.
/// world.CreateFilterJoint(new FilterJointDefinition
/// {
///     Base = JointDefinition.Connect(turret, shell, JointFrame.Identity, JointFrame.Identity),
/// });
/// </code>
/// </example>
public readonly record struct FilterJointDefinition
{
    /// <summary>Gets the settings shared by every joint.</summary>
    public JointDefinition Base { get; init; }

    /// <summary>Gets the engine defaults, with no bodies attached.</summary>
    public static FilterJointDefinition Default => new()
    {
        Base = JointDefinition.FromNative(NativeDefaults.FilterJoint.@base),
    };

    /// <summary>Stops two bodies from colliding with each other.</summary>
    /// <param name="bodyA">The first body.</param>
    /// <param name="bodyB">The second body.</param>
    /// <returns>The definition.</returns>
    public static FilterJointDefinition Between(Body bodyA, Body bodyB) => new()
    {
        Base = JointDefinition.Connect(bodyA, bodyB, JointFrame.Identity, JointFrame.Identity),
    };

    internal b3FilterJointDef ToNative()
    {
        b3FilterJointDef def = NativeDefaults.FilterJoint;
        def.@base = Base.ToNative();

        return def;
    }
}

/// <summary>
/// Stops two specific bodies from colliding.
/// </summary>
public readonly record struct FilterJoint
{
    internal FilterJoint(b3JointId id) => NativeId = id;

    /// <summary>Gets the native identifier this handle wraps.</summary>
    /// <remarks>
    /// Internal on purpose: exposing it would put a Box3D.Native type in the
    /// public surface and weld the C ABI to this API. Reach it through
    /// <c>Box3D.Interop.NativeInterop.ToNativeId</c> when you genuinely need the
    /// C function this layer does not wrap.
    /// </remarks>
    internal b3JointId NativeId { get; }

    /// <summary>Gets the generic handle, carrying what every joint has in common.</summary>
    public Joint AsJoint => new(NativeId);

    /// <summary>Destroys this joint, allowing the two bodies to collide again.</summary>
    /// <param name="wakeBodies">Whether to wake the attached bodies.</param>
    public void Destroy(bool wakeBodies = true) => B3.b3DestroyJoint(NativeId, wakeBodies);
}
