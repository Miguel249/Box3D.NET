// SPDX-License-Identifier: MIT
// Mirror of the joint definition types in include/box3d/types.h.

using System.Numerics;
using System.Runtime.InteropServices;

namespace Box3D.Native;

/*
 * Every joint definition embeds b3JointDef as its first member, mirroring the C
 * layout exactly. This is composition rather than inheritance because these are
 * value types describing a C ABI: a base struct at offset zero is how C models
 * the shared prefix, and any managed inheritance would destroy blittability.
 *
 * Local frames are measured from the body origin rather than the centre of mass,
 * because the application may not know where the centre of mass will end up, and
 * because adding or removing a shape moves it and would otherwise break the joint.
 */

/// <summary>
/// The fields shared by every joint definition. Mirror of <c>b3JointDef</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct b3JointDef
{
    /// <summary>Application data associated with the joint.</summary>
    public void* userData;

    /// <summary>The first attached body.</summary>
    public b3BodyId bodyIdA;

    /// <summary>The second attached body.</summary>
    public b3BodyId bodyIdB;

    /// <summary>The joint frame on body A, relative to body A's origin.</summary>
    public b3Transform localFrameA;

    /// <summary>The joint frame on body B, relative to body B's origin.</summary>
    public b3Transform localFrameB;

    /// <summary>The force above which a joint event is raised, in newtons.</summary>
    public float forceThreshold;

    /// <summary>The torque above which a joint event is raised, in newton-metres.</summary>
    public float torqueThreshold;

    /// <summary>The constraint stiffness in cycles per second. Advanced.</summary>
    public float constraintHertz;

    /// <summary>The constraint damping ratio. Advanced.</summary>
    public float constraintDampingRatio;

    /// <summary>The scale used when drawing this joint.</summary>
    public float drawScale;

    /// <summary>Whether the two attached bodies may collide with each other.</summary>
    public NativeBool collideConnected;

    /// <summary>Used internally to detect a valid definition. Do not set.</summary>
    public int internalValue;
}

/// <summary>
/// Connects a point on one body to a point on another by a segment.
/// Mirror of <c>b3DistanceJointDef</c>.
/// </summary>
/// <remarks>Useful for ropes and springs. Start from <c>B3.b3DefaultDistanceJointDef()</c>.</remarks>
[StructLayout(LayoutKind.Sequential)]
public struct b3DistanceJointDef
{
    /// <summary>The shared joint fields.</summary>
    public b3JointDef @base;

    /// <summary>The rest length. Clamped to a stable minimum.</summary>
    public float length;

    /// <summary>
    /// Whether the joint behaves like a spring. When false the joint is rigid and
    /// both the limit and the motor are overridden.
    /// </summary>
    public NativeBool enableSpring;

    /// <summary>The lower spring force, controlling how much tension the joint sustains.</summary>
    public float lowerSpringForce;

    /// <summary>The upper spring force, controlling how much compression the joint sustains.</summary>
    public float upperSpringForce;

    /// <summary>The spring stiffness in cycles per second.</summary>
    public float hertz;

    /// <summary>The spring damping ratio. Non-dimensional.</summary>
    public float dampingRatio;

    /// <summary>Whether the length limit is enabled.</summary>
    public NativeBool enableLimit;

    /// <summary>The minimum length. Clamped to a stable minimum.</summary>
    public float minLength;

    /// <summary>The maximum length. Must be at least the minimum length.</summary>
    public float maxLength;

    /// <summary>Whether the motor is enabled.</summary>
    public NativeBool enableMotor;

    /// <summary>The maximum motor force, usually in newtons.</summary>
    public float maxMotorForce;

    /// <summary>The desired motor speed, usually in metres per second.</summary>
    public float motorSpeed;
}

/// <summary>
/// Controls the relative position and velocity of two bodies while staying
/// responsive to collisions. Mirror of <c>b3MotorJointDef</c>.
/// </summary>
/// <remarks>
/// A spring controls position and rotation while a velocity motor controls
/// velocity; both may be combined, and each has its own force and torque limit.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public struct b3MotorJointDef
{
    /// <summary>The shared joint fields.</summary>
    public b3JointDef @base;

    /// <summary>The desired relative linear velocity.</summary>
    public Vector3 linearVelocity;

    /// <summary>The maximum force the velocity motor may apply, in newtons.</summary>
    public float maxVelocityForce;

    /// <summary>The desired relative angular velocity.</summary>
    public Vector3 angularVelocity;

    /// <summary>The maximum torque the velocity motor may apply, in newton-metres.</summary>
    public float maxVelocityTorque;

    /// <summary>The linear spring stiffness in cycles per second.</summary>
    public float linearHertz;

    /// <summary>The linear spring damping ratio.</summary>
    public float linearDampingRatio;

    /// <summary>The maximum linear spring force, in newtons.</summary>
    public float maxSpringForce;

    /// <summary>The angular spring stiffness in cycles per second.</summary>
    public float angularHertz;

    /// <summary>The angular spring damping ratio.</summary>
    public float angularDampingRatio;

    /// <summary>The maximum angular spring torque, in newton-metres.</summary>
    public float maxSpringTorque;
}

/// <summary>
/// Disables collision between two specific bodies. Mirror of <c>b3FilterJointDef</c>.
/// </summary>
/// <remarks>
/// As a side effect of being a joint, it also keeps the two bodies in the same
/// simulation island.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public struct b3FilterJointDef
{
    /// <summary>The shared joint fields.</summary>
    public b3JointDef @base;
}

/// <summary>
/// Constrains the angle between the z axis of frame A and the z axis of frame B
/// with a spring. Mirror of <c>b3ParallelJointDef</c>.
/// </summary>
/// <remarks>Useful for keeping a body upright.</remarks>
[StructLayout(LayoutKind.Sequential)]
public struct b3ParallelJointDef
{
    /// <summary>The shared joint fields.</summary>
    public b3JointDef @base;

    /// <summary>The spring stiffness in cycles per second.</summary>
    public float hertz;

    /// <summary>The spring damping ratio. Non-dimensional.</summary>
    public float dampingRatio;

    /// <summary>The maximum spring torque, usually in newton-metres.</summary>
    public float maxTorque;
}

/// <summary>
/// Allows body B to slide along the x axis of frame A without relative rotation.
/// Mirror of <c>b3PrismaticJointDef</c>.
/// </summary>
/// <remarks>
/// Also called a slider joint. The translation is zero when the two local frame
/// origins coincide in world space.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public struct b3PrismaticJointDef
{
    /// <summary>The shared joint fields.</summary>
    public b3JointDef @base;

    /// <summary>Whether the linear spring along the joint axis is enabled.</summary>
    public NativeBool enableSpring;

    /// <summary>The spring stiffness in cycles per second.</summary>
    public float hertz;

    /// <summary>The spring damping ratio. Non-dimensional.</summary>
    public float dampingRatio;

    /// <summary>The translation the spring drives towards, in metres.</summary>
    public float targetTranslation;

    /// <summary>Whether the translation limit is enabled.</summary>
    public NativeBool enableLimit;

    /// <summary>The lower translation limit.</summary>
    public float lowerTranslation;

    /// <summary>The upper translation limit.</summary>
    public float upperTranslation;

    /// <summary>Whether the motor is enabled.</summary>
    public NativeBool enableMotor;

    /// <summary>The maximum motor force, usually in newtons.</summary>
    public float maxMotorForce;

    /// <summary>The desired motor speed, usually in metres per second.</summary>
    public float motorSpeed;
}

/// <summary>
/// Pins a point on body B to a point on body A, allowing relative rotation about
/// one axis. Mirror of <c>b3RevoluteJointDef</c>.
/// </summary>
/// <remarks>Also called a hinge or pin joint.</remarks>
[StructLayout(LayoutKind.Sequential)]
public struct b3RevoluteJointDef
{
    /// <summary>The shared joint fields.</summary>
    public b3JointDef @base;

    /// <summary>
    /// The angle of body B minus the angle of body A in the reference state, in radians.
    /// This defines the zero angle for the limit.
    /// </summary>
    public float targetAngle;

    /// <summary>Whether the rotational spring on the hinge axis is enabled.</summary>
    public NativeBool enableSpring;

    /// <summary>The spring stiffness in cycles per second.</summary>
    public float hertz;

    /// <summary>The spring damping ratio. Non-dimensional.</summary>
    public float dampingRatio;

    /// <summary>Whether the angle limit is enabled.</summary>
    public NativeBool enableLimit;

    /// <summary>The lower angle limit in radians. At least minus 0.99 pi.</summary>
    public float lowerAngle;

    /// <summary>The upper angle limit in radians. At most 0.99 pi.</summary>
    public float upperAngle;

    /// <summary>Whether the motor is enabled.</summary>
    public NativeBool enableMotor;

    /// <summary>The maximum motor torque, usually in newton-metres.</summary>
    public float maxMotorTorque;

    /// <summary>The desired motor speed in radians per second.</summary>
    public float motorSpeed;
}

/// <summary>
/// Pins a point on body B to a point on body A, allowing rotation about that
/// shared point. Mirror of <c>b3SphericalJointDef</c>.
/// </summary>
/// <remarks>Also called a ball-in-socket or point-to-point joint.</remarks>
[StructLayout(LayoutKind.Sequential)]
public struct b3SphericalJointDef
{
    /// <summary>The shared joint fields.</summary>
    public b3JointDef @base;

    /// <summary>Whether a rotational spring aligns the two joint frames.</summary>
    public NativeBool enableSpring;

    /// <summary>
    /// The spring stiffness in cycles per second. Non-negative.
    /// </summary>
    /// <remarks>May be clamped internally according to the time step for stability.</remarks>
    public float hertz;

    /// <summary>The spring damping ratio. Non-negative and non-dimensional.</summary>
    public float dampingRatio;

    /// <summary>The target rotation of frame B relative to frame A.</summary>
    public Quaternion targetRotation;

    /// <summary>Whether the cone limit is enabled. The cone is centred on the z axis of frame A.</summary>
    public NativeBool enableConeLimit;

    /// <summary>The cone half angle in radians, in the range zero to pi.</summary>
    public float coneAngle;

    /// <summary>Whether the twist limit is enabled. The twist is about the z axis of frame B.</summary>
    public NativeBool enableTwistLimit;

    /// <summary>The lower twist limit in radians. At least minus 0.99 pi.</summary>
    public float lowerTwistAngle;

    /// <summary>The upper twist limit in radians. At most 0.99 pi.</summary>
    public float upperTwistAngle;

    /// <summary>Whether the motor is enabled.</summary>
    public NativeBool enableMotor;

    /// <summary>The maximum motor torque, usually in newton-metres. Non-negative.</summary>
    public float maxMotorTorque;

    /// <summary>The desired motor angular velocity in radians per second.</summary>
    public Vector3 motorVelocity;
}

/// <summary>
/// Rigidly connects two bodies, with optional springs to mimic soft-body
/// behaviour. Mirror of <c>b3WeldJointDef</c>.
/// </summary>
/// <remarks>
/// The approximate solver cannot hold many bodies together rigidly, so long
/// chains of weld joints will flex.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public struct b3WeldJointDef
{
    /// <summary>The shared joint fields.</summary>
    public b3JointDef @base;

    /// <summary>The linear stiffness in cycles per second. Zero means maximum stiffness.</summary>
    public float linearHertz;

    /// <summary>The angular stiffness in cycles per second. Zero means maximum stiffness.</summary>
    public float angularHertz;

    /// <summary>The linear damping ratio. One is critical damping.</summary>
    public float linearDampingRatio;

    /// <summary>The angular damping ratio. One is critical damping.</summary>
    public float angularDampingRatio;
}

/// <summary>
/// Models a wheel with suspension, spin and optional steering.
/// Mirror of <c>b3WheelJointDef</c>.
/// </summary>
/// <remarks>
/// Body A is the chassis and body B is the wheel. The wheel spins about the
/// local z axis of frame B and translates along the local x axis of frame A.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public struct b3WheelJointDef
{
    /// <summary>The shared joint fields.</summary>
    public b3JointDef @base;

    /// <summary>Whether the suspension spring is enabled.</summary>
    public NativeBool enableSuspensionSpring;

    /// <summary>The suspension stiffness in cycles per second.</summary>
    public float suspensionHertz;

    /// <summary>The suspension damping ratio. Non-dimensional.</summary>
    public float suspensionDampingRatio;

    /// <summary>Whether the suspension travel limit is enabled.</summary>
    public NativeBool enableSuspensionLimit;

    /// <summary>The lower suspension travel limit.</summary>
    public float lowerSuspensionLimit;

    /// <summary>The upper suspension travel limit.</summary>
    public float upperSuspensionLimit;

    /// <summary>Whether the spin motor is enabled.</summary>
    public NativeBool enableSpinMotor;

    /// <summary>The maximum spin torque, usually in newton-metres.</summary>
    public float maxSpinTorque;

    /// <summary>The desired spin speed in radians per second.</summary>
    public float spinSpeed;

    /// <summary>Whether steering is enabled. When false the steering is fixed forward.</summary>
    public NativeBool enableSteering;

    /// <summary>The steering stiffness in cycles per second.</summary>
    public float steeringHertz;

    /// <summary>The steering damping ratio. Non-dimensional.</summary>
    public float steeringDampingRatio;

    /// <summary>The target steering angle in radians.</summary>
    public float targetSteeringAngle;

    /// <summary>The maximum steering torque, in newton-metres.</summary>
    public float maxSteeringTorque;

    /// <summary>Whether the steering angle limit is enabled.</summary>
    public NativeBool enableSteeringLimit;

    /// <summary>The lower steering angle limit in radians.</summary>
    public float lowerSteeringLimit;

    /// <summary>The upper steering angle limit in radians.</summary>
    public float upperSteeringLimit;
}
