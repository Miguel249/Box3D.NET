// SPDX-License-Identifier: MIT
// Mirror of the compound types in include/box3d/types.h.

using System.Numerics;
using System.Runtime.InteropServices;

namespace Box3D.Native;

/*
 * A baked compound is a single broad-phase shape holding potentially thousands
 * of children, and is restricted to static bodies. Run-time compounds, which may
 * be dynamic or kinematic, are built instead by attaching several shapes to one
 * body with the ordinary shape creation functions.
 *
 * Everything in b3CompoundDef is deep-cloned into the baked compound, so the
 * definition and the hulls and meshes it points at may be released afterwards.
 */

/// <summary>A capsule instance in a compound definition. Mirror of <c>b3CompoundCapsuleDef</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct b3CompoundCapsuleDef
{
    /// <summary>The capsule, in compound-local space.</summary>
    public b3Capsule capsule;

    /// <summary>The surface material.</summary>
    public b3SurfaceMaterial material;
}

/// <summary>A convex hull instance in a compound definition. Mirror of <c>b3CompoundHullDef</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct b3CompoundHullDef
{
    /// <summary>The shared hull. Cloned into the compound.</summary>
    public b3HullData* hull;

    /// <summary>The transform placing the hull in compound-local space.</summary>
    public b3Transform transform;

    /// <summary>The surface material.</summary>
    public b3SurfaceMaterial material;
}

/// <summary>A triangle mesh instance in a compound definition. Mirror of <c>b3CompoundMeshDef</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct b3CompoundMeshDef
{
    /// <summary>The shared mesh data.</summary>
    public b3MeshData* meshData;

    /// <summary>The transform placing the mesh in compound-local space.</summary>
    public b3Transform transform;

    /// <summary>The instance scale. May be non-uniform and may have negative components.</summary>
    public Vector3 scale;

    /// <summary>
    /// The surface materials, lined up with the material indices on the triangles.
    /// </summary>
    /// <remarks>
    /// Limited to <see cref="Constants.B3_MAX_COMPOUND_MESH_MATERIALS"/> inside a
    /// compound. A mesh with more materials must be used outside one.
    /// </remarks>
    public b3SurfaceMaterial* materials;

    /// <summary>The number of materials.</summary>
    public int materialCount;
}

/// <summary>A sphere instance in a compound definition. Mirror of <c>b3CompoundSphereDef</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct b3CompoundSphereDef
{
    /// <summary>The sphere, in compound-local space.</summary>
    public b3Sphere sphere;

    /// <summary>The surface material.</summary>
    public b3SurfaceMaterial material;
}

/// <summary>
/// The definition used to bake a compound shape. Mirror of <c>b3CompoundDef</c>.
/// </summary>
/// <remarks>All referenced data is cloned into the resulting compound.</remarks>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct b3CompoundDef
{
    /// <summary>The capsule instances.</summary>
    public b3CompoundCapsuleDef* capsules;

    /// <summary>The number of capsule instances.</summary>
    public int capsuleCount;

    /// <summary>The hull instances.</summary>
    public b3CompoundHullDef* hulls;

    /// <summary>The number of hull instances.</summary>
    public int hullCount;

    /// <summary>The mesh instances.</summary>
    public b3CompoundMeshDef* meshes;

    /// <summary>The number of mesh instances.</summary>
    public int meshCount;

    /// <summary>The sphere instances.</summary>
    public b3CompoundSphereDef* spheres;

    /// <summary>The number of sphere instances.</summary>
    public int sphereCount;
}

/// <summary>
/// A baked compound shape. Mirror of <c>b3CompoundData</c>.
/// </summary>
/// <remarks>
/// A large but highly optimized structure with its arrays living past the end,
/// reached through byte offsets. It appears in the broad phase as a single shape.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public struct b3CompoundData
{
    /// <summary>The format version. Must equal <see cref="Constants.B3_COMPOUND_VERSION"/>.</summary>
    public ulong version;

    /// <summary>The total size of this compound in bytes, including the trailing arrays.</summary>
    public int byteCount;

    /// <summary>The byte offset of the tree node array from the start of this structure.</summary>
    public int nodeOffset;

    /// <summary>
    /// The immutable dynamic tree over the children.
    /// </summary>
    /// <remarks>Its node pointer must be fixed up using <see cref="nodeOffset"/> after deserialization.</remarks>
    public b3DynamicTree tree;

    /// <summary>The byte offset of the material array from the start of this structure.</summary>
    public int materialOffset;

    /// <summary>The number of materials.</summary>
    public int materialCount;

    /// <summary>The byte offset of the capsule array from the start of this structure.</summary>
    public int capsuleOffset;

    /// <summary>The number of capsules.</summary>
    public int capsuleCount;

    /// <summary>The byte offset of the hull instance array from the start of this structure.</summary>
    public int hullOffset;

    /// <summary>The number of hull instances.</summary>
    public int hullCount;

    /// <summary>The number of distinct hulls shared by those instances. Diagnostic.</summary>
    public int sharedHullCount;

    /// <summary>The byte offset of the mesh instance array from the start of this structure.</summary>
    public int meshOffset;

    /// <summary>The number of mesh instances.</summary>
    public int meshCount;

    /// <summary>The number of distinct meshes shared by those instances. Diagnostic.</summary>
    public int sharedMeshCount;

    /// <summary>The byte offset of the sphere array from the start of this structure.</summary>
    public int sphereOffset;

    /// <summary>The number of spheres.</summary>
    public int sphereCount;
}

/// <summary>A capsule stored inside a baked compound. Mirror of <c>b3CompoundCapsule</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct b3CompoundCapsule
{
    /// <summary>The capsule, in compound-local space.</summary>
    public b3Capsule capsule;

    /// <summary>The index into the compound's shared material array.</summary>
    public int materialIndex;
}

/// <summary>A convex hull stored inside a baked compound. Mirror of <c>b3CompoundHull</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct b3CompoundHull
{
    /// <summary>The shared hull owned by the compound.</summary>
    public b3HullData* hull;

    /// <summary>The transform of this instance.</summary>
    public b3Transform transform;

    /// <summary>The index into the compound's shared material array.</summary>
    public int materialIndex;
}

/// <summary>A scaled triangle mesh stored inside a baked compound. Mirror of <c>b3CompoundMesh</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct b3CompoundMesh
{
    /// <summary>The shared mesh data owned by the compound.</summary>
    public b3MeshData* meshData;

    /// <summary>The transform of this instance.</summary>
    public b3Transform transform;

    /// <summary>The non-uniform scale of this instance.</summary>
    public Vector3 scale;

    /// <summary>
    /// Indirection from a triangle's material index to the compound's shared material array.
    /// </summary>
    /// <remarks>
    /// The triangle material index is clamped to
    /// <see cref="Constants.B3_MAX_COMPOUND_MESH_MATERIALS"/> before indexing here.
    /// </remarks>
    public fixed int materialIndices[Constants.B3_MAX_COMPOUND_MESH_MATERIALS];
}

/// <summary>A sphere stored inside a baked compound. Mirror of <c>b3CompoundSphere</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct b3CompoundSphere
{
    /// <summary>The sphere, in compound-local space.</summary>
    public b3Sphere sphere;

    /// <summary>The index into the compound's shared material array.</summary>
    public int materialIndex;
}

/// <summary>
/// A child shape of a compound, as a tagged union. Mirror of <c>b3ChildShape</c>.
/// </summary>
/// <remarks>
/// <see cref="type"/> selects which of <see cref="capsule"/>, <see cref="hull"/>,
/// <see cref="mesh"/> or <see cref="sphere"/> is valid. Reading the wrong member
/// yields garbage rather than an error.
/// </remarks>
[StructLayout(LayoutKind.Explicit)]
public unsafe struct b3ChildShape
{
    // The union occupies the first 28 bytes: b3Capsule is the largest member at
    // two Vector3 plus a float. The fields after it are placed explicitly.

    /// <summary>The capsule, valid when <see cref="type"/> is <see cref="b3ShapeType.b3_capsuleShape"/>.</summary>
    [FieldOffset(0)]
    public b3Capsule capsule;

    /// <summary>The hull, valid when <see cref="type"/> is <see cref="b3ShapeType.b3_hullShape"/>.</summary>
    [FieldOffset(0)]
    public b3HullData* hull;

    /// <summary>The mesh, valid when <see cref="type"/> is <see cref="b3ShapeType.b3_meshShape"/>.</summary>
    [FieldOffset(0)]
    public b3Mesh mesh;

    /// <summary>The sphere, valid when <see cref="type"/> is <see cref="b3ShapeType.b3_sphereShape"/>.</summary>
    [FieldOffset(0)]
    public b3Sphere sphere;

    /// <summary>The transform placing this child in compound-local space.</summary>
    [FieldOffset(32)]
    public b3Transform transform;

    /// <summary>The material indices. Index zero is used for convex shapes.</summary>
    [FieldOffset(60)]
    public fixed int materialIndices[Constants.B3_MAX_COMPOUND_MESH_MATERIALS];

    /// <summary>The union tag.</summary>
    [FieldOffset(76)]
    public b3ShapeType type;
}
