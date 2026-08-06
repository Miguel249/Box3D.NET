// SPDX-License-Identifier: MIT
// Mirror of the shape types in include/box3d/types.h.

using System.Numerics;
using System.Runtime.InteropServices;

namespace Box3D.Native;

/// <summary>
/// Collision filtering data for a shape. Mirror of <c>b3Filter</c>.
/// </summary>
/// <remarks>
/// Affects shape-versus-shape collision as well as queries such as ray casts.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public struct b3Filter
{
    /// <summary>
    /// The categories this shape belongs to. Normally a single bit.
    /// </summary>
    public ulong categoryBits;

    /// <summary>
    /// The categories this shape accepts for collision.
    /// </summary>
    public ulong maskBits;

    /// <summary>
    /// A group index. Shapes in the same negative group never collide; shapes in
    /// the same positive group always collide. Zero has no effect.
    /// </summary>
    /// <remarks>
    /// Non-zero group filtering wins over the mask bits. A common use is giving
    /// each ragdoll a unique negative group to disable self-collision.
    /// </remarks>
    public int groupIndex;
}

/// <summary>
/// Surface properties, supported per triangle on meshes and height fields.
/// Mirror of <c>b3SurfaceMaterial</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct b3SurfaceMaterial
{
    /// <summary>The Coulomb friction coefficient, usually in the range zero to one.</summary>
    public float friction;

    /// <summary>The coefficient of restitution, usually in the range zero to one.</summary>
    public float restitution;

    /// <summary>The rolling resistance, usually in the range zero to one. Applies to spheres and capsules only.</summary>
    public float rollingResistance;

    /// <summary>
    /// The tangent velocity for conveyor belts, in shape-local space.
    /// </summary>
    /// <remarks>Projected onto the contact surface at solve time.</remarks>
    public Vector3 tangentVelocity;

    /// <summary>
    /// An application-defined material identifier.
    /// </summary>
    /// <remarks>
    /// Returned with query results and passed to the friction and restitution
    /// mixing callbacks. Box3D never interprets it.
    /// </remarks>
    public ulong userMaterialId;

    /// <summary>
    /// A custom debug draw colour, ignored when zero.
    /// </summary>
    /// <remarks>
    /// The low 24 bits are RGB; the high byte may carry a <see cref="b3DebugMaterial"/>
    /// preset. See <c>B3.b3MakeDebugColor</c>.
    /// </remarks>
    public uint customColor;

    /// <summary>Explicit padding. Must be zero.</summary>
    public uint padding;
}

/// <summary>
/// The definition used to create a shape. Mirror of <c>b3ShapeDef</c>.
/// </summary>
/// <remarks>
/// Always start from <c>B3.b3DefaultShapeDef()</c>. The definition and the
/// geometry are cloned by Box3D, except for meshes and height fields, whose data
/// is referenced and must outlive the shape.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct b3ShapeDef
{
    /// <summary>An optional name for debugging, as a null-terminated UTF-8 string.</summary>
    public byte* name;

    /// <summary>Application data associated with the shape.</summary>
    public void* userData;

    /// <summary>
    /// Per-triangle surface materials for mesh shapes.
    /// </summary>
    /// <remarks>
    /// Indexed by the triangle material indices of the mesh. Ignored for convex
    /// and compound shapes. The array is cloned during shape creation.
    /// </remarks>
    public b3SurfaceMaterial* materials;

    /// <summary>The number of entries in <see cref="materials"/>.</summary>
    public int materialCount;

    /// <summary>The base surface material. Ignored for compound shapes.</summary>
    public b3SurfaceMaterial baseMaterial;

    /// <summary>The density, usually in kilograms per cubic metre.</summary>
    public float density;

    /// <summary>The explosion scale used by <c>b3World_Explode</c>. Non-dimensional.</summary>
    public float explosionScale;

    /// <summary>The collision filtering data.</summary>
    public b3Filter filter;

    /// <summary>
    /// Whether the custom filter callback applies to this shape.
    /// </summary>
    /// <remarks>Only one of the two shapes in a pair needs to enable it.</remarks>
    public NativeBool enableCustomFiltering;

    /// <summary>
    /// Whether this shape is a sensor, generating overlap events but no collision response.
    /// </summary>
    /// <remarks>
    /// Sensors have no continuous collision; use a ray or shape cast for that.
    /// A sensor still contributes to body mass if its density is non-zero.
    /// Sensor events are off by default even for sensors.
    /// </remarks>
    public NativeBool isSensor;

    /// <summary>Whether sensor events are raised for this shape. Off by default.</summary>
    public NativeBool enableSensorEvents;

    /// <summary>Whether contact events are raised for this shape. Off by default.</summary>
    public NativeBool enableContactEvents;

    /// <summary>Whether hit events are raised for this shape. Off by default.</summary>
    public NativeBool enableHitEvents;

    /// <summary>
    /// Whether pre-solve events are raised for this shape. Off by default.
    /// </summary>
    /// <remarks>Expensive, and the callback runs on worker threads. Ignored for sensors.</remarks>
    public NativeBool enablePreSolveEvents;

    /// <summary>
    /// Whether the shape scans for contacts on the next step.
    /// </summary>
    /// <remarks>
    /// Ignored for dynamic and kinematic shapes, which always invoke contact
    /// creation. Disabling it substantially speeds up bulk creation of static shapes.
    /// </remarks>
    public NativeBool invokeContactCreation;

    /// <summary>
    /// Whether the body recomputes its mass when this shape is created. On by default.
    /// </summary>
    /// <remarks>
    /// If this is false you must call <c>b3Body_ApplyMassFromShapes</c> or
    /// <c>b3Body_SetMassData</c> before simulating.
    /// </remarks>
    public NativeBool updateBodyMass;

    /// <summary>
    /// Whether speculative collision is enabled. Leave this on unless ghost
    /// collisions matter more than continuous collision under rotation.
    /// </summary>
    public NativeBool enableSpeculativeContact;

    /// <summary>Used internally to detect a valid definition. Do not set.</summary>
    public int internalValue;
}
