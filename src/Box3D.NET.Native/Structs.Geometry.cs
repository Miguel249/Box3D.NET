// SPDX-License-Identifier: MIT
// Mirror of the geometry types in include/box3d/types.h.

using System.Numerics;
using System.Runtime.InteropServices;

namespace Box3D.Native;

/*
 * Ownership of geometry data
 * --------------------------
 * b3HullData, b3MeshData, b3HeightFieldData and b3CompoundData all have their
 * arrays allocated in one block that extends past the end of the struct, reached
 * through byte offsets. They therefore cannot be copied by value, which is why
 * every function here takes or returns a pointer to them.
 *
 * Who frees what:
 *   b3CreateHull / b3CreateCylinder / b3CreateCone / b3CreateRock /
 *   b3CloneHull / b3CloneAndTransformHull  -> caller frees with b3DestroyHull
 *   b3MakeCubeHull and the other b3Make*Hull functions return a b3BoxHull by
 *   value. It is self-contained and must NOT be passed to b3DestroyHull.
 *   b3CreateMesh / b3Create*Mesh           -> caller frees with b3DestroyMesh
 *   b3CreateHeightField / b3CreateGrid /
 *   b3CreateWave / b3LoadHeightField       -> caller frees with b3DestroyHeightField
 *   b3CreateCompound                       -> caller frees with b3DestroyCompound
 *
 * Shapes created from a mesh or height field hold a reference rather than a
 * copy, so that data must outlive every shape built from it. Hulls and compounds
 * are cloned into the shape and may be destroyed once the shape exists.
 */

/// <summary>A solid sphere. Mirror of <c>b3Sphere</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct b3Sphere
{
    /// <summary>The centre, in shape-local space.</summary>
    public Vector3 center;

    /// <summary>The radius.</summary>
    public float radius;
}

/// <summary>
/// A solid capsule: two hemispheres joined by a cylinder. Mirror of <c>b3Capsule</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct b3Capsule
{
    /// <summary>The centre of the first hemisphere, in shape-local space.</summary>
    public Vector3 center1;

    /// <summary>The centre of the second hemisphere, in shape-local space.</summary>
    public Vector3 center2;

    /// <summary>The radius of the hemispheres.</summary>
    public float radius;
}

/// <summary>A convex hull vertex, identified by an outgoing half-edge. Mirror of <c>b3HullVertex</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct b3HullVertex
{
    /// <summary>The index of a half-edge originating at this vertex.</summary>
    public byte edge;
}

/// <summary>A half-edge in the convex hull data structure. Mirror of <c>b3HullHalfEdge</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct b3HullHalfEdge
{
    /// <summary>The index of the next half-edge, counter-clockwise.</summary>
    public byte next;

    /// <summary>The index of the twin half-edge.</summary>
    public byte twin;

    /// <summary>The index of the origin vertex.</summary>
    public byte origin;

    /// <summary>The index of the face to the left of this half-edge.</summary>
    public byte face;
}

/// <summary>A convex hull face, identified by one of its half-edges. Mirror of <c>b3HullFace</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct b3HullFace
{
    /// <summary>The index of an arbitrary half-edge on this face.</summary>
    public byte edge;
}

/// <summary>
/// A convex hull. Mirror of <c>b3HullData</c>.
/// </summary>
/// <remarks>
/// The vertex, point, edge, plane and face arrays live past the end of this
/// structure and are reached through the offset fields, so a hull cannot be
/// copied by value. Use the accessors on <c>Box3D</c> to read them.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public struct b3HullData
{
    /// <summary>The format version. Must equal <see cref="Constants.B3_HULL_VERSION"/>.</summary>
    public ulong version;

    /// <summary>The total size of this hull in bytes, including the trailing arrays.</summary>
    public int byteCount;

    /// <summary>A content hash of this hull, computed with this field set to zero.</summary>
    public uint hash;

    /// <summary>The local-space bounding box.</summary>
    public b3AABB aabb;

    /// <summary>The surface area, usually in square metres.</summary>
    public float surfaceArea;

    /// <summary>The volume, usually in cubic metres.</summary>
    public float volume;

    /// <summary>The radius of the largest sphere centred at the centroid that fits inside the hull.</summary>
    public float innerRadius;

    /// <summary>The local centroid.</summary>
    public Vector3 center;

    /// <summary>The inertia tensor about the centroid.</summary>
    public b3Matrix3 centralInertia;

    /// <summary>The number of vertices.</summary>
    public int vertexCount;

    /// <summary>The byte offset of the vertex array from the start of this structure.</summary>
    public int vertexOffset;

    /// <summary>The byte offset of the point array from the start of this structure.</summary>
    public int pointOffset;

    /// <summary>The number of half-edges, which is twice the edge count.</summary>
    public int edgeCount;

    /// <summary>The byte offset of the half-edge array from the start of this structure.</summary>
    public int edgeOffset;

    /// <summary>The number of faces. Hull faces are convex polygons.</summary>
    public int faceCount;

    /// <summary>The byte offset of the face plane array from the start of this structure.</summary>
    public int planeOffset;

    /// <summary>The byte offset of the face array from the start of this structure.</summary>
    public int faceOffset;

    /// <summary>The byte offset of the structure-of-arrays vertex data.</summary>
    public int soaVertexOffset;

    /// <summary>The byte offset of the structure-of-arrays normal data.</summary>
    public int soaNormalOffset;

    /// <summary>Explicit padding. Hull identity is a hash over raw bytes, so no implicit padding is allowed.</summary>
    public int padding;
}

/// <summary>
/// A self-contained box hull. Mirror of <c>b3BoxHull</c>.
/// </summary>
/// <remarks>
/// Returned by value from the <c>b3Make*Hull</c> functions. Because the arrays
/// are embedded rather than heap allocated, this must never be passed to
/// <c>b3DestroyHull</c>.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct b3BoxHull
{
    /// <summary>The embedded hull header. Its offsets index into the arrays that follow.</summary>
    public b3HullData @base;

    /// <summary>The eight box vertices.</summary>
    public fixed byte boxVertices[8 * 1];

    /// <summary>The eight box points, as three floats each.</summary>
    public fixed float boxPoints[8 * 3];

    /// <summary>The twenty-four box half-edges, as four bytes each.</summary>
    public fixed byte boxEdges[24 * 4];

    /// <summary>The six box face planes, as four floats each.</summary>
    public fixed float boxPlanes[6 * 4];

    /// <summary>The six box faces.</summary>
    public fixed byte boxFaces[6 * 1];

    /// <summary>Explicit padding.</summary>
    public fixed byte padding[10];

    /// <summary>Vertex x coordinates, structure-of-arrays form.</summary>
    public fixed float vx[8];

    /// <summary>Vertex y coordinates, structure-of-arrays form.</summary>
    public fixed float vy[8];

    /// <summary>Vertex z coordinates, structure-of-arrays form.</summary>
    public fixed float vz[8];

    /// <summary>Normal x components, structure-of-arrays form.</summary>
    public fixed float nx[8];

    /// <summary>Normal y components, structure-of-arrays form.</summary>
    public fixed float ny[8];

    /// <summary>Normal z components, structure-of-arrays form.</summary>
    public fixed float nz[8];
}

/// <summary>
/// The definition used to build a reusable collision mesh. Mirror of <c>b3MeshDef</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct b3MeshDef
{
    /// <summary>The vertex positions.</summary>
    public Vector3* vertices;

    /// <summary>Three vertex indices per triangle, wound counter-clockwise.</summary>
    public int* indices;

    /// <summary>
    /// One material index per triangle, indexing into <c>b3ShapeDef.materials</c>.
    /// </summary>
    /// <remarks>
    /// This indirection lets one baked mesh be reused by shapes carrying different
    /// run-time materials.
    /// </remarks>
    public byte* materialIndices;

    /// <summary>The vertex welding tolerance, in length units.</summary>
    public float weldTolerance;

    /// <summary>The number of vertices. Must be at least three.</summary>
    public int vertexCount;

    /// <summary>The number of triangles. Must be at least one.</summary>
    public int triangleCount;

    /// <summary>Whether to weld nearby vertices.</summary>
    public NativeBool weldVertices;

    /// <summary>
    /// Whether to use a median split instead of a surface area heuristic.
    /// </summary>
    /// <remarks>Faster to build and a good trade for grid-structured meshes.</remarks>
    public NativeBool useMedianSplit;

    /// <summary>Whether to compute triangle adjacency from shared edges.</summary>
    /// <remarks>Required for the ghost-collision suppression driven by <see cref="b3MeshEdgeFlags"/>.</remarks>
    public NativeBool identifyEdges;
}

/// <summary>A triangle in a collision mesh. Mirror of <c>b3MeshTriangle</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct b3MeshTriangle
{
    /// <summary>The index of the first vertex.</summary>
    public int index1;

    /// <summary>The index of the second vertex.</summary>
    public int index2;

    /// <summary>The index of the third vertex.</summary>
    public int index3;
}

/// <summary>
/// A node in the mesh bounding volume hierarchy. Mirror of <c>b3MeshNode</c>.
/// </summary>
/// <remarks>
/// The middle word is a union of an internal-node and a leaf-node layout, both
/// packed as bit fields. Read it through <see cref="Axis"/>, <see cref="ChildOffset"/>,
/// <see cref="IsLeaf"/> and <see cref="TriangleCount"/> rather than directly.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public struct b3MeshNode
{
    /// <summary>The lower bound of the node bounding box.</summary>
    public Vector3 lowerBound;

    /// <summary>
    /// The packed union of the internal-node and leaf-node bit fields.
    /// </summary>
    /// <remarks>The low two bits are the split axis, or three when the node is a leaf.</remarks>
    public uint data;

    /// <summary>The upper bound of the node bounding box.</summary>
    public Vector3 upperBound;

    /// <summary>The index of the first triangle for a leaf node.</summary>
    public uint triangleOffset;

    /// <summary>Gets the split axis of an internal node: zero, one or two.</summary>
    public readonly uint Axis => data & 0x3u;

    /// <summary>Gets a value indicating whether this node is a leaf.</summary>
    public readonly bool IsLeaf => (data & 0x3u) == 3u;

    /// <summary>Gets the offset of the second child, valid when <see cref="IsLeaf"/> is false.</summary>
    public readonly uint ChildOffset => data >> 2;

    /// <summary>Gets the number of triangles, valid when <see cref="IsLeaf"/> is true.</summary>
    public readonly uint TriangleCount => data >> 2;
}

/// <summary>
/// A baked triangle mesh with its bounding volume hierarchy. Mirror of <c>b3MeshData</c>.
/// </summary>
/// <remarks>
/// The node, vertex, triangle, material and flag arrays live past the end of
/// this structure, so it cannot be copied by value.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public struct b3MeshData
{
    /// <summary>The format version. Must equal <see cref="Constants.B3_MESH_VERSION"/>.</summary>
    public ulong version;

    /// <summary>The total size of this mesh in bytes, including the trailing arrays.</summary>
    public int byteCount;

    /// <summary>A content hash of this mesh, computed with this field set to zero.</summary>
    public uint hash;

    /// <summary>The local-space bounding box.</summary>
    public b3AABB bounds;

    /// <summary>The combined single-sided surface area of all triangles.</summary>
    public float surfaceArea;

    /// <summary>The height of the bounding volume hierarchy.</summary>
    public int treeHeight;

    /// <summary>The number of degenerate triangles found while building. Diagnostic.</summary>
    public int degenerateCount;

    /// <summary>The byte offset of the node array from the start of this structure.</summary>
    public int nodeOffset;

    /// <summary>The number of hierarchy nodes.</summary>
    public int nodeCount;

    /// <summary>The byte offset of the vertex array from the start of this structure.</summary>
    public int vertexOffset;

    /// <summary>The number of vertices.</summary>
    public int vertexCount;

    /// <summary>The byte offset of the triangle array from the start of this structure.</summary>
    public int triangleOffset;

    /// <summary>The number of triangles.</summary>
    public int triangleCount;

    /// <summary>The byte offset of the material index array from the start of this structure.</summary>
    public int materialOffset;

    /// <summary>The number of materials.</summary>
    public int materialCount;

    /// <summary>The byte offset of the triangle flag array from the start of this structure.</summary>
    public int flagsOffset;
}

/// <summary>
/// A reference to baked mesh data together with an instance scale.
/// Mirror of <c>b3Mesh</c>.
/// </summary>
/// <remarks>
/// This is how one baked mesh is reused at several scales. The scale may be
/// non-uniform and may have negative components, but no component may be very
/// small in magnitude.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct b3Mesh
{
    /// <summary>The shared, immutable mesh data. Not owned by this structure.</summary>
    public b3MeshData* data;

    /// <summary>The instance scale.</summary>
    public Vector3 scale;
}

/// <summary>
/// The definition used to build a height field. Mirror of <c>b3HeightFieldDef</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct b3HeightFieldDef
{
    /// <summary>
    /// The grid point heights, with <c>countX * countZ</c> entries.
    /// </summary>
    public float* heights;

    /// <summary>
    /// One material index per cell, with <c>(countX - 1) * (countZ - 1)</c> entries.
    /// </summary>
    /// <remarks>The value <see cref="Constants.B3_HEIGHT_FIELD_HOLE"/> marks a hole.</remarks>
    public byte* materialIndices;

    /// <summary>The scale of the height field. All components must be positive.</summary>
    public Vector3 scale;

    /// <summary>The number of grid lines along the x axis.</summary>
    public int countX;

    /// <summary>The number of grid lines along the z axis.</summary>
    public int countZ;

    /// <summary>
    /// The minimum height used for quantization, in unscaled space.
    /// </summary>
    /// <remarks>
    /// Adjacent height fields must share the same minimum and maximum if their
    /// edges are to line up exactly. Heights are clamped to this range.
    /// </remarks>
    public float globalMinimumHeight;

    /// <summary>The maximum height used for quantization, in unscaled space.</summary>
    public float globalMaximumHeight;

    /// <summary>Whether to use clockwise winding, which flips the field along the y axis.</summary>
    public NativeBool clockwiseWinding;
}

/// <summary>
/// A baked height field with compressed storage. Mirror of <c>b3HeightFieldData</c>.
/// </summary>
/// <remarks>The height, material and flag arrays live past the end of this structure.</remarks>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct b3HeightFieldData
{
    /// <summary>The format version. Must equal <see cref="Constants.B3_HEIGHT_FIELD_VERSION"/>.</summary>
    public ulong version;

    /// <summary>The total size of this height field in bytes, including the trailing arrays.</summary>
    public int byteCount;

    /// <summary>A content hash, computed with this field set to zero.</summary>
    public uint hash;

    /// <summary>The local-space bounding box.</summary>
    public b3AABB aabb;

    /// <summary>The minimum height.</summary>
    public float minHeight;

    /// <summary>The maximum height.</summary>
    public float maxHeight;

    /// <summary>The quantization scale applied to the compressed heights.</summary>
    public float heightScale;

    /// <summary>The overall scale.</summary>
    public Vector3 scale;

    /// <summary>The number of grid columns along the local x axis.</summary>
    public int columnCount;

    /// <summary>The number of grid rows along the local z axis.</summary>
    public int rowCount;

    /// <summary>The byte offset of the compressed height array, one <see cref="ushort"/> per grid point.</summary>
    public int heightsOffset;

    /// <summary>The byte offset of the material index array, one <see cref="byte"/> per cell.</summary>
    public int materialOffset;

    /// <summary>The byte offset of the flag array, one <see cref="byte"/> per triangle.</summary>
    public int flagsOffset;

    /// <summary>Whether the triangles use clockwise winding.</summary>
    public NativeBool clockwise;

    /// <summary>Explicit padding. Identity is a hash over raw bytes, so no implicit padding is allowed.</summary>
    public fixed byte padding[3];
}
