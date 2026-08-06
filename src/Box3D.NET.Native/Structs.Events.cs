// SPDX-License-Identifier: MIT
// Mirror of the event types in include/box3d/types.h.

using System.Numerics;
using System.Runtime.InteropServices;

namespace Box3D.Native;

/*
 * Box3D buffers events during the step and hands back arrays afterwards rather
 * than invoking callbacks, because the simulation is multithreaded and because
 * applications usually want to mutate the world in response to an event, which
 * is unsafe mid-step.
 *
 * Every array below points into world-owned memory that is valid only until the
 * next call to b3World_Step. Do not retain these pointers. Ids carried by an
 * event may also have been destroyed by an earlier iteration of your own event
 * loop, so validate with b3Shape_IsValid or b3Contact_IsValid before use.
 */

/// <summary>Raised when a shape begins overlapping a sensor. Mirror of <c>b3SensorBeginTouchEvent</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct b3SensorBeginTouchEvent
{
    /// <summary>The sensor shape.</summary>
    public b3ShapeId sensorShapeId;

    /// <summary>The shape that began touching the sensor.</summary>
    public b3ShapeId visitorShapeId;
}

/// <summary>
/// Raised when a shape stops overlapping a sensor. Mirror of <c>b3SensorEndTouchEvent</c>.
/// </summary>
/// <remarks>
/// Also raised when either shape is destroyed or its transform or filter changes,
/// so both ids may already be invalid. Check with <c>b3Shape_IsValid</c>.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public struct b3SensorEndTouchEvent
{
    /// <summary>The sensor shape. May have been destroyed.</summary>
    public b3ShapeId sensorShapeId;

    /// <summary>The shape that stopped touching the sensor. May have been destroyed.</summary>
    public b3ShapeId visitorShapeId;
}

/// <summary>
/// The sensor events buffered during the last step. Mirror of <c>b3SensorEvents</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct b3SensorEvents
{
    /// <summary>The begin-touch events. Valid until the next step.</summary>
    public b3SensorBeginTouchEvent* beginEvents;

    /// <summary>The end-touch events. Valid until the next step.</summary>
    public b3SensorEndTouchEvent* endEvents;

    /// <summary>The number of begin-touch events.</summary>
    public int beginCount;

    /// <summary>The number of end-touch events.</summary>
    public int endCount;
}

/// <summary>Raised when two shapes begin touching. Mirror of <c>b3ContactBeginTouchEvent</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct b3ContactBeginTouchEvent
{
    /// <summary>The first shape.</summary>
    public b3ShapeId shapeIdA;

    /// <summary>The second shape.</summary>
    public b3ShapeId shapeIdB;

    /// <summary>
    /// The contact. Transient: it may be destroyed by any world mutation or step.
    /// </summary>
    public b3ContactId contactId;
}

/// <summary>
/// Raised when two shapes stop touching. Mirror of <c>b3ContactEndTouchEvent</c>.
/// </summary>
/// <remarks>
/// Also raised by anything that destroys a contact, such as destroying a body or
/// shape or changing a filter or body type, so the ids may already be invalid.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public struct b3ContactEndTouchEvent
{
    /// <summary>The first shape. May have been destroyed.</summary>
    public b3ShapeId shapeIdA;

    /// <summary>The second shape. May have been destroyed.</summary>
    public b3ShapeId shapeIdB;

    /// <summary>The contact. May have been destroyed.</summary>
    public b3ContactId contactId;
}

/// <summary>
/// Raised when two shapes collide faster than the hit event threshold.
/// Mirror of <c>b3ContactHitEvent</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct b3ContactHitEvent
{
    /// <summary>The first shape.</summary>
    public b3ShapeId shapeIdA;

    /// <summary>The second shape.</summary>
    public b3ShapeId shapeIdB;

    /// <summary>The contact. May have been destroyed.</summary>
    public b3ContactId contactId;

    /// <summary>
    /// The point where the shapes hit, midway between the two surfaces.
    /// </summary>
    /// <remarks>
    /// This may be a speculative point at which the shapes were not yet touching
    /// at the start of the step.
    /// </remarks>
    public Vector3 point;

    /// <summary>The unit normal pointing from shape A to shape B.</summary>
    public Vector3 normal;

    /// <summary>The approach speed. Always positive, usually in metres per second.</summary>
    public float approachSpeed;

    /// <summary>The user material identifier on shape A.</summary>
    public ulong userMaterialIdA;

    /// <summary>The user material identifier on shape B.</summary>
    public ulong userMaterialIdB;
}

/// <summary>
/// The contact events buffered during the last step. Mirror of <c>b3ContactEvents</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct b3ContactEvents
{
    /// <summary>The begin-touch events. Valid until the next step.</summary>
    public b3ContactBeginTouchEvent* beginEvents;

    /// <summary>The end-touch events. Valid until the next step.</summary>
    public b3ContactEndTouchEvent* endEvents;

    /// <summary>The hit events. Valid until the next step.</summary>
    public b3ContactHitEvent* hitEvents;

    /// <summary>The number of begin-touch events.</summary>
    public int beginCount;

    /// <summary>The number of end-touch events.</summary>
    public int endCount;

    /// <summary>The number of hit events.</summary>
    public int hitCount;
}

/// <summary>
/// Reports a body moved by the simulation. Mirror of <c>b3BodyMoveEvent</c>.
/// </summary>
/// <remarks>
/// <para>
/// Not reported for bodies the application moved itself. This is the efficient
/// way to push transforms into game objects: the data arrives as one contiguous
/// array containing only the bodies that actually moved, rather than requiring a
/// call to <c>b3Body_GetTransform</c> per body.
/// </para>
/// <para>
/// If sleeping is disabled every dynamic and kinematic body raises this event.
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct b3BodyMoveEvent
{
    /// <summary>The body's user data.</summary>
    public void* userData;

    /// <summary>The new body transform.</summary>
    public b3Transform transform;

    /// <summary>The body.</summary>
    public b3BodyId bodyId;

    /// <summary>
    /// Whether the body fell asleep this step.
    /// </summary>
    /// <remarks>
    /// When false the application may treat the associated game object as awake.
    /// </remarks>
    public NativeBool fellAsleep;
}

/// <summary>
/// The body events buffered during the last step. Mirror of <c>b3BodyEvents</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct b3BodyEvents
{
    /// <summary>The move events. Valid until the next step.</summary>
    public b3BodyMoveEvent* moveEvents;

    /// <summary>The number of move events.</summary>
    public int moveCount;
}

/// <summary>
/// Reports an awake joint whose force or torque exceeded its threshold.
/// Mirror of <c>b3JointEvent</c>.
/// </summary>
/// <remarks>The observed force and torque are not included, for efficiency.</remarks>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct b3JointEvent
{
    /// <summary>The joint.</summary>
    public b3JointId jointId;

    /// <summary>The joint's user data, included for convenience.</summary>
    public void* userData;
}

/// <summary>
/// The joint events buffered during the last step. Mirror of <c>b3JointEvents</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct b3JointEvents
{
    /// <summary>The joint events. Valid until the next step.</summary>
    public b3JointEvent* jointEvents;

    /// <summary>The number of events.</summary>
    public int count;
}

/// <summary>
/// The contact data between two shapes. Mirror of <c>b3ContactData</c>.
/// </summary>
/// <remarks>
/// By convention the manifold normal points from shape A to shape B.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct b3ContactData
{
    /// <summary>
    /// The contact. May be retained across steps, but validate it with
    /// <c>b3Contact_IsValid</c> before each use.
    /// </summary>
    public b3ContactId contactId;

    /// <summary>The first shape.</summary>
    public b3ShapeId shapeIdA;

    /// <summary>The second shape.</summary>
    public b3ShapeId shapeIdB;

    /// <summary>
    /// The contact manifolds. Points into internal memory; do not retain.
    /// </summary>
    public b3Manifold* manifolds;

    /// <summary>
    /// The number of manifolds. Mesh and height field collision can produce several.
    /// </summary>
    public int manifoldCount;
}
