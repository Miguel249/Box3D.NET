// SPDX-License-Identifier: MIT

using System.Numerics;
using Box3D.Native;

namespace Box3D;

/// <summary>
/// Holds two points a set distance apart. The basis for ropes and springs.
/// </summary>
/// <remarks>
/// With <see cref="SpringEnabled"/> off the joint is rigid and both the limit
/// and the motor are ignored. With it on, the limit becomes a rope that can go
/// slack between <see cref="MinLength"/> and <see cref="MaxLength"/>.
/// </remarks>
/// <example>
/// <code>
/// // A rope: free to sag, but never longer than three metres.
/// var rope = DistanceJointDefinition.Between(anchor, load, anchorPoint, loadPoint) with
/// {
///     SpringEnabled = true,
///     Hertz = 4.0f,
///     DampingRatio = 0.5f,
///     LimitsEnabled = true,
///     MinLength = 0.1f,
///     MaxLength = 3.0f,
/// };
/// </code>
/// </example>
public readonly record struct DistanceJointDefinition
{
    /// <summary>Gets the settings shared by every joint.</summary>
    public JointDefinition Base { get; init; }

    /// <summary>Gets the rest length, clamped to a stable minimum.</summary>
    public float Length { get; init; }

    /// <summary>Gets a value indicating whether the joint behaves as a spring rather than a rigid link.</summary>
    public bool SpringEnabled { get; init; }

    /// <summary>Gets the spring stiffness in cycles per second.</summary>
    public float Hertz { get; init; }

    /// <summary>Gets the spring damping ratio.</summary>
    public float DampingRatio { get; init; }

    /// <summary>Gets the lower spring force, which bounds how much tension the joint sustains.</summary>
    public float LowerSpringForce { get; init; }

    /// <summary>Gets the upper spring force, which bounds how much compression the joint sustains.</summary>
    public float UpperSpringForce { get; init; }

    /// <summary>Gets a value indicating whether the length limits are enforced.</summary>
    public bool LimitsEnabled { get; init; }

    /// <summary>Gets the minimum length.</summary>
    public float MinLength { get; init; }

    /// <summary>Gets the maximum length. Must be at least <see cref="MinLength"/>.</summary>
    public float MaxLength { get; init; }

    /// <summary>Gets a value indicating whether the motor is enabled.</summary>
    public bool MotorEnabled { get; init; }

    /// <summary>Gets the maximum force the motor may apply, in newtons.</summary>
    public float MaxMotorForce { get; init; }

    /// <summary>Gets the speed the motor drives towards, in metres per second.</summary>
    public float MotorSpeed { get; init; }

    /// <summary>Gets the engine defaults, with no bodies attached.</summary>
    public static DistanceJointDefinition Default => FromNative(B3.b3DefaultDistanceJointDef());

    /// <summary>
    /// Connects a point on one body to a point on another, taking the current
    /// separation as the rest length.
    /// </summary>
    /// <param name="bodyA">The first body.</param>
    /// <param name="bodyB">The second body.</param>
    /// <param name="worldAnchorA">The attachment point on the first body, in world space.</param>
    /// <param name="worldAnchorB">The attachment point on the second body, in world space.</param>
    /// <returns>The definition.</returns>
    public static DistanceJointDefinition Between(
        Body bodyA,
        Body bodyB,
        Vector3 worldAnchorA,
        Vector3 worldAnchorB)
    {
        JointFrame frameA = new(bodyA.ToLocalPoint(worldAnchorA));
        JointFrame frameB = new(bodyB.ToLocalPoint(worldAnchorB));

        return Default with
        {
            Base = JointDefinition.Connect(bodyA, bodyB, frameA, frameB),

            // Taking the current separation means the joint starts satisfied
            // rather than snapping the bodies together on the first step.
            Length = Vector3.Distance(worldAnchorA, worldAnchorB),
        };
    }

    internal b3DistanceJointDef ToNative()
    {
        b3DistanceJointDef def = B3.b3DefaultDistanceJointDef();

        def.@base = Base.ToNative();
        def.length = Length;
        def.enableSpring = SpringEnabled;
        def.hertz = Hertz;
        def.dampingRatio = DampingRatio;
        def.lowerSpringForce = LowerSpringForce;
        def.upperSpringForce = UpperSpringForce;
        def.enableLimit = LimitsEnabled;
        def.minLength = MinLength;
        def.maxLength = MaxLength;
        def.enableMotor = MotorEnabled;
        def.maxMotorForce = MaxMotorForce;
        def.motorSpeed = MotorSpeed;

        return def;
    }

    internal static DistanceJointDefinition FromNative(in b3DistanceJointDef def) => new()
    {
        Base = JointDefinition.FromNative(def.@base),
        Length = def.length,
        SpringEnabled = def.enableSpring,
        Hertz = def.hertz,
        DampingRatio = def.dampingRatio,
        LowerSpringForce = def.lowerSpringForce,
        UpperSpringForce = def.upperSpringForce,
        LimitsEnabled = def.enableLimit,
        MinLength = def.minLength,
        MaxLength = def.maxLength,
        MotorEnabled = def.enableMotor,
        MaxMotorForce = def.maxMotorForce,
        MotorSpeed = def.motorSpeed,
    };
}

/// <summary>
/// Holds two points a set distance apart.
/// </summary>
public readonly record struct DistanceJoint
{
    internal DistanceJoint(b3JointId id) => NativeId = id;

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

    /// <summary>Gets the current distance between the two anchors.</summary>
    public float CurrentLength => B3.b3DistanceJoint_GetCurrentLength(NativeId);

    /// <summary>Gets or sets the rest length.</summary>
    public float Length
    {
        get => B3.b3DistanceJoint_GetLength(NativeId);
        set => B3.b3DistanceJoint_SetLength(NativeId, value);
    }

    /// <summary>Gets or sets a value indicating whether the joint behaves as a spring.</summary>
    public bool SpringEnabled
    {
        get => B3.b3DistanceJoint_IsSpringEnabled(NativeId);
        set => B3.b3DistanceJoint_EnableSpring(NativeId, value);
    }

    /// <summary>Gets or sets the spring stiffness in cycles per second.</summary>
    public float Hertz
    {
        get => B3.b3DistanceJoint_GetSpringHertz(NativeId);
        set => B3.b3DistanceJoint_SetSpringHertz(NativeId, value);
    }

    /// <summary>Gets or sets the spring damping ratio.</summary>
    public float DampingRatio
    {
        get => B3.b3DistanceJoint_GetSpringDampingRatio(NativeId);
        set => B3.b3DistanceJoint_SetSpringDampingRatio(NativeId, value);
    }

    /// <summary>Gets or sets a value indicating whether the length limits are enforced.</summary>
    public bool LimitsEnabled
    {
        get => B3.b3DistanceJoint_IsLimitEnabled(NativeId);
        set => B3.b3DistanceJoint_EnableLimit(NativeId, value);
    }

    /// <summary>Gets the minimum length.</summary>
    public float MinLength => B3.b3DistanceJoint_GetMinLength(NativeId);

    /// <summary>Gets the maximum length.</summary>
    public float MaxLength => B3.b3DistanceJoint_GetMaxLength(NativeId);

    /// <summary>Gets or sets a value indicating whether the motor is enabled.</summary>
    public bool MotorEnabled
    {
        get => B3.b3DistanceJoint_IsMotorEnabled(NativeId);
        set => B3.b3DistanceJoint_EnableMotor(NativeId, value);
    }

    /// <summary>Gets or sets the speed the motor drives towards, in metres per second.</summary>
    public float MotorSpeed
    {
        get => B3.b3DistanceJoint_GetMotorSpeed(NativeId);
        set => B3.b3DistanceJoint_SetMotorSpeed(NativeId, value);
    }

    /// <summary>Gets or sets the maximum force the motor may apply, in newtons.</summary>
    public float MaxMotorForce
    {
        get => B3.b3DistanceJoint_GetMaxMotorForce(NativeId);
        set => B3.b3DistanceJoint_SetMaxMotorForce(NativeId, value);
    }

    /// <summary>Gets the force the motor is currently applying, in newtons.</summary>
    public float MotorForce => B3.b3DistanceJoint_GetMotorForce(NativeId);

    /// <summary>Sets both length limits.</summary>
    /// <param name="minLength">The minimum length.</param>
    /// <param name="maxLength">The maximum length.</param>
    public void SetLengthRange(float minLength, float maxLength) =>
        B3.b3DistanceJoint_SetLengthRange(NativeId, minLength, maxLength);

    /// <summary>Sets the force range the spring may apply.</summary>
    /// <param name="lowerForce">The lower bound, which limits tension.</param>
    /// <param name="upperForce">The upper bound, which limits compression.</param>
    public void SetSpringForceRange(float lowerForce, float upperForce) =>
        B3.b3DistanceJoint_SetSpringForceRange(NativeId, lowerForce, upperForce);

    /// <summary>Destroys this joint.</summary>
    /// <param name="wakeBodies">Whether to wake the attached bodies.</param>
    public void Destroy(bool wakeBodies = true) => B3.b3DestroyJoint(NativeId, wakeBodies);
}

/// <summary>
/// A ball-in-socket: the bodies share a point and may rotate freely about it,
/// within optional cone and twist limits.
/// </summary>
/// <remarks>
/// The cone limits how far the frame B z axis may swing away from the frame A z
/// axis; the twist limits rotation about that axis. Together they are what makes
/// a believable shoulder or hip in a ragdoll.
/// </remarks>
/// <example>
/// <code>
/// // A shoulder: swings within a 60 degree cone, twists a quarter turn either way.
/// var shoulder = SphericalJointDefinition.BallAndSocket(torso, arm, shoulderPoint) with
/// {
///     ConeLimitEnabled = true,
///     ConeAngle = MathF.PI / 3.0f,
///     TwistLimitEnabled = true,
///     LowerTwistAngle = -MathF.PI / 4.0f,
///     UpperTwistAngle = MathF.PI / 4.0f,
/// };
/// </code>
/// </example>
public readonly record struct SphericalJointDefinition
{
    /// <summary>Gets the settings shared by every joint.</summary>
    public JointDefinition Base { get; init; }

    /// <summary>Gets a value indicating whether a spring pulls the two frames into alignment.</summary>
    public bool SpringEnabled { get; init; }

    /// <summary>Gets the spring stiffness in cycles per second. Non-negative.</summary>
    public float Hertz { get; init; }

    /// <summary>Gets the spring damping ratio. Non-negative.</summary>
    public float DampingRatio { get; init; }

    /// <summary>Gets the rotation of frame B relative to frame A that the spring drives towards.</summary>
    public Quaternion TargetRotation { get; init; }

    /// <summary>Gets a value indicating whether the cone limit is enforced.</summary>
    public bool ConeLimitEnabled { get; init; }

    /// <summary>Gets the cone half angle in radians, from zero to pi.</summary>
    public float ConeAngle { get; init; }

    /// <summary>Gets a value indicating whether the twist limit is enforced.</summary>
    public bool TwistLimitEnabled { get; init; }

    /// <summary>Gets the lower twist limit in radians. At least minus 0.99 pi.</summary>
    public float LowerTwistAngle { get; init; }

    /// <summary>Gets the upper twist limit in radians. At most 0.99 pi.</summary>
    public float UpperTwistAngle { get; init; }

    /// <summary>Gets a value indicating whether the motor is enabled.</summary>
    public bool MotorEnabled { get; init; }

    /// <summary>Gets the maximum torque the motor may apply, in newton-metres.</summary>
    public float MaxMotorTorque { get; init; }

    /// <summary>Gets the angular velocity the motor drives towards, in radians per second.</summary>
    public Vector3 MotorVelocity { get; init; }

    /// <summary>Gets the engine defaults, with no bodies attached.</summary>
    public static SphericalJointDefinition Default => FromNative(B3.b3DefaultSphericalJointDef());

    /// <summary>Pins two bodies together at a shared world-space point.</summary>
    /// <param name="bodyA">The first body.</param>
    /// <param name="bodyB">The second body.</param>
    /// <param name="worldAnchor">The shared point, in world space.</param>
    /// <param name="worldAxis">
    /// The axis the cone is centred on and the twist measured about, in world
    /// space. Defaults to the world up axis when omitted.
    /// </param>
    /// <returns>The definition.</returns>
    public static SphericalJointDefinition BallAndSocket(
        Body bodyA,
        Body bodyB,
        Vector3 worldAnchor,
        Vector3? worldAxis = null)
    {
        (JointFrame frameA, JointFrame frameB) =
            Joint.FramesFromWorldAnchor(bodyA, bodyB, worldAnchor, worldAxis ?? Vector3.UnitY);

        return Default with { Base = JointDefinition.Connect(bodyA, bodyB, frameA, frameB) };
    }

    internal b3SphericalJointDef ToNative()
    {
        b3SphericalJointDef def = B3.b3DefaultSphericalJointDef();

        def.@base = Base.ToNative();
        def.enableSpring = SpringEnabled;
        def.hertz = Hertz;
        def.dampingRatio = DampingRatio;
        def.targetRotation = TargetRotation;
        def.enableConeLimit = ConeLimitEnabled;
        def.coneAngle = ConeAngle;
        def.enableTwistLimit = TwistLimitEnabled;
        def.lowerTwistAngle = LowerTwistAngle;
        def.upperTwistAngle = UpperTwistAngle;
        def.enableMotor = MotorEnabled;
        def.maxMotorTorque = MaxMotorTorque;
        def.motorVelocity = MotorVelocity;

        return def;
    }

    internal static SphericalJointDefinition FromNative(in b3SphericalJointDef def) => new()
    {
        Base = JointDefinition.FromNative(def.@base),
        SpringEnabled = def.enableSpring,
        Hertz = def.hertz,
        DampingRatio = def.dampingRatio,
        TargetRotation = def.targetRotation,
        ConeLimitEnabled = def.enableConeLimit,
        ConeAngle = def.coneAngle,
        TwistLimitEnabled = def.enableTwistLimit,
        LowerTwistAngle = def.lowerTwistAngle,
        UpperTwistAngle = def.upperTwistAngle,
        MotorEnabled = def.enableMotor,
        MaxMotorTorque = def.maxMotorTorque,
        MotorVelocity = def.motorVelocity,
    };
}

/// <summary>
/// A ball-in-socket joint.
/// </summary>
public readonly record struct SphericalJoint
{
    internal SphericalJoint(b3JointId id) => NativeId = id;

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

    /// <summary>Gets the current swing away from the cone axis, in radians.</summary>
    public float ConeAngle => B3.b3SphericalJoint_GetConeAngle(NativeId);

    /// <summary>Gets the current twist about the cone axis, in radians.</summary>
    public float TwistAngle => B3.b3SphericalJoint_GetTwistAngle(NativeId);

    /// <summary>Gets or sets a value indicating whether the cone limit is enforced.</summary>
    public bool ConeLimitEnabled
    {
        get => B3.b3SphericalJoint_IsConeLimitEnabled(NativeId);
        set => B3.b3SphericalJoint_EnableConeLimit(NativeId, value);
    }

    /// <summary>Gets or sets the cone half angle in radians.</summary>
    public float ConeLimit
    {
        get => B3.b3SphericalJoint_GetConeLimit(NativeId);
        set => B3.b3SphericalJoint_SetConeLimit(NativeId, value);
    }

    /// <summary>Gets or sets a value indicating whether the twist limit is enforced.</summary>
    public bool TwistLimitEnabled
    {
        get => B3.b3SphericalJoint_IsTwistLimitEnabled(NativeId);
        set => B3.b3SphericalJoint_EnableTwistLimit(NativeId, value);
    }

    /// <summary>Gets the lower twist limit in radians.</summary>
    public float LowerTwistLimit => B3.b3SphericalJoint_GetLowerTwistLimit(NativeId);

    /// <summary>Gets the upper twist limit in radians.</summary>
    public float UpperTwistLimit => B3.b3SphericalJoint_GetUpperTwistLimit(NativeId);

    /// <summary>Gets or sets a value indicating whether the alignment spring is enabled.</summary>
    public bool SpringEnabled
    {
        get => B3.b3SphericalJoint_IsSpringEnabled(NativeId);
        set => B3.b3SphericalJoint_EnableSpring(NativeId, value);
    }

    /// <summary>Gets or sets the spring stiffness in cycles per second.</summary>
    public float Hertz
    {
        get => B3.b3SphericalJoint_GetSpringHertz(NativeId);
        set => B3.b3SphericalJoint_SetSpringHertz(NativeId, value);
    }

    /// <summary>Gets or sets the spring damping ratio.</summary>
    public float DampingRatio
    {
        get => B3.b3SphericalJoint_GetSpringDampingRatio(NativeId);
        set => B3.b3SphericalJoint_SetSpringDampingRatio(NativeId, value);
    }

    /// <summary>Gets or sets the rotation the spring drives towards.</summary>
    public Quaternion TargetRotation
    {
        get => B3.b3SphericalJoint_GetTargetRotation(NativeId);
        set => B3.b3SphericalJoint_SetTargetRotation(NativeId, value);
    }

    /// <summary>Gets or sets a value indicating whether the motor is enabled.</summary>
    public bool MotorEnabled
    {
        get => B3.b3SphericalJoint_IsMotorEnabled(NativeId);
        set => B3.b3SphericalJoint_EnableMotor(NativeId, value);
    }

    /// <summary>Gets or sets the angular velocity the motor drives towards, in radians per second.</summary>
    public Vector3 MotorVelocity
    {
        get => B3.b3SphericalJoint_GetMotorVelocity(NativeId);
        set => B3.b3SphericalJoint_SetMotorVelocity(NativeId, value);
    }

    /// <summary>Gets or sets the maximum torque the motor may apply, in newton-metres.</summary>
    public float MaxMotorTorque
    {
        get => B3.b3SphericalJoint_GetMaxMotorTorque(NativeId);
        set => B3.b3SphericalJoint_SetMaxMotorTorque(NativeId, value);
    }

    /// <summary>Gets the torque the motor is currently applying, in newton-metres.</summary>
    public Vector3 MotorTorque => B3.b3SphericalJoint_GetMotorTorque(NativeId);

    /// <summary>Sets both twist limits.</summary>
    /// <param name="lower">The lower limit in radians.</param>
    /// <param name="upper">The upper limit in radians.</param>
    public void SetTwistLimits(float lower, float upper) =>
        B3.b3SphericalJoint_SetTwistLimits(NativeId, lower, upper);

    /// <summary>Destroys this joint.</summary>
    /// <param name="wakeBodies">Whether to wake the attached bodies.</param>
    public void Destroy(bool wakeBodies = true) => B3.b3DestroyJoint(NativeId, wakeBodies);
}
