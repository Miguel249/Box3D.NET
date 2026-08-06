// SPDX-License-Identifier: MIT
// Mirror of the body types in include/box3d/types.h.

using System.Numerics;
using System.Runtime.InteropServices;

namespace Box3D.Native;

/// <summary>
/// Per-axis locks restricting body movement. Mirror of <c>b3MotionLocks</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct b3MotionLocks
{
    /// <summary>Prevents translation along the x axis.</summary>
    public NativeBool linearX;

    /// <summary>Prevents translation along the y axis.</summary>
    public NativeBool linearY;

    /// <summary>Prevents translation along the z axis.</summary>
    public NativeBool linearZ;

    /// <summary>Prevents rotation about the x axis.</summary>
    public NativeBool angularX;

    /// <summary>Prevents rotation about the y axis.</summary>
    public NativeBool angularY;

    /// <summary>Prevents rotation about the z axis.</summary>
    public NativeBool angularZ;
}

/// <summary>
/// The definition used to create a rigid body. Mirror of <c>b3BodyDef</c>.
/// </summary>
/// <remarks>
/// Always start from <c>B3.b3DefaultBodyDef()</c>; Box3D rejects definitions
/// whose <see cref="internalValue"/> was never initialized. Definitions are
/// temporary parameter bundles and can be reused freely. Shapes are attached
/// after the body is created.
/// </remarks>
/// <example>
/// <code>
/// b3BodyDef def = B3.b3DefaultBodyDef();
/// def.type = b3BodyType.b3_dynamicBody;
/// def.position = new Vector3(0.0f, 10.0f, 0.0f);
/// b3BodyId body = B3.b3CreateBody(world, in def);
/// </code>
/// </example>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct b3BodyDef
{
    /// <summary>The body type: static, kinematic or dynamic.</summary>
    public b3BodyType type;

    /// <summary>
    /// The initial world position of the body origin.
    /// </summary>
    /// <remarks>
    /// Create bodies where they belong. Creating at the origin and then moving
    /// nearly doubles the cost of body creation, especially after shapes are added.
    /// </remarks>
    public Vector3 position;

    /// <summary>The initial world rotation.</summary>
    public Quaternion rotation;

    /// <summary>The initial linear velocity of the body origin, usually in metres per second.</summary>
    public Vector3 linearVelocity;

    /// <summary>The initial angular velocity, in radians per second.</summary>
    public Vector3 angularVelocity;

    /// <summary>
    /// Linear damping, used to reduce linear velocity.
    /// </summary>
    /// <remarks>
    /// Values above one are allowed but become sensitive to the time step.
    /// Linear damping generally makes objects look like they are floating.
    /// </remarks>
    public float linearDamping;

    /// <summary>Angular damping, used to slow down rotating bodies.</summary>
    public float angularDamping;

    /// <summary>Scales the gravity applied to this body. Non-dimensional.</summary>
    public float gravityScale;

    /// <summary>The sleep speed threshold, usually in metres per second.</summary>
    public float sleepThreshold;

    /// <summary>
    /// An optional name for debugging, as a null-terminated UTF-8 string.
    /// </summary>
    /// <remarks>Box3D copies the string, so the pointer need only be valid during the call.</remarks>
    public byte* name;

    /// <summary>Application data associated with the body.</summary>
    public void* userData;

    /// <summary>Locks restricting linear and angular movement.</summary>
    public b3MotionLocks motionLocks;

    /// <summary>Whether this body may fall asleep.</summary>
    public NativeBool enableSleep;

    /// <summary>Whether the body starts awake.</summary>
    public NativeBool isAwake;

    /// <summary>
    /// Treats the body as a high-speed object that performs continuous collision
    /// against dynamic and kinematic bodies, but not against other bullets.
    /// </summary>
    /// <remarks>
    /// Use sparingly. Bullets do not guarantee accurate collision when both bodies
    /// move fast, because the bullet is swept after all non-bullet bodies have moved.
    /// For projectiles that need precise timing, prefer a ray or shape cast.
    /// </remarks>
    public NativeBool isBullet;

    /// <summary>Whether the body participates in the simulation at all.</summary>
    public NativeBool isEnabled;

    /// <summary>
    /// Allows the body to bypass rotational speed limits.
    /// </summary>
    /// <remarks>Only appropriate for bodies that are symmetric about the spin axis, such as wheels.</remarks>
    public NativeBool allowFastRotation;

    /// <summary>
    /// Whether contact recycling is enabled. On by default.
    /// </summary>
    /// <remarks>
    /// Recycling improves performance but can produce ghost collisions, which are
    /// best avoided on characters.
    /// </remarks>
    public NativeBool enableContactRecycling;

    /// <summary>Used internally to detect a valid definition. Do not set.</summary>
    public int internalValue;
}

/// <summary>
/// The mass properties of a shape or body. Mirror of <c>b3MassData</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct b3MassData
{
    /// <summary>The mass, usually in kilograms.</summary>
    public float mass;

    /// <summary>The local centre of mass.</summary>
    public Vector3 center;

    /// <summary>The inertia tensor about the centre of mass.</summary>
    public b3Matrix3 inertia;
}
