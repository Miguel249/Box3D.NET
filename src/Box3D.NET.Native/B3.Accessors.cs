// SPDX-License-Identifier: MIT
// Port of the B3_INLINE accessors in include/box3d/collision.h.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Box3D.Native;

/*
 * Hulls, meshes, height fields and compounds keep their arrays in the same
 * allocation as the header, past the end of the struct, and reach them through
 * byte offsets. The C header exposes that with B3_INLINE accessors, which have
 * internal linkage and so are not in the export table. They are reimplemented
 * here.
 *
 * Each accessor returns null when the corresponding offset is zero, exactly as
 * the C version does. The Span overloads pair each pointer with the count field
 * that belongs to it, which is the part that is easy to get wrong by hand.
 *
 * The returned memory is owned by the hull, mesh or height field and is only
 * valid while it is alive. Nothing here copies.
 */

public static unsafe partial class B3
{
    // ------------------------------------------------------------ dynamic tree

    /// <summary>Gets the user data of a tree proxy.</summary>
    /// <param name="tree">The tree.</param>
    /// <param name="proxyId">The proxy index.</param>
    /// <returns>The user data.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong b3DynamicTree_GetUserData(b3DynamicTree* tree, int proxyId) =>
        tree->nodes[proxyId].userData;

    /// <summary>Gets the bounding box of a tree proxy.</summary>
    /// <param name="tree">The tree.</param>
    /// <param name="proxyId">The proxy index.</param>
    /// <returns>The bounding box.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static b3AABB b3DynamicTree_GetAABB(b3DynamicTree* tree, int proxyId) =>
        tree->nodes[proxyId].aabb;

    // -------------------------------------------------------------- hull data

    /// <summary>Gets the vertices of a convex hull.</summary>
    /// <param name="hull">The hull.</param>
    /// <returns>A pointer to the vertex array, or null when the hull has none.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static b3HullVertex* b3GetHullVertices(b3HullData* hull) =>
        hull->vertexOffset == 0 ? null : (b3HullVertex*)((byte*)hull + hull->vertexOffset);

    /// <summary>Gets the points of a convex hull.</summary>
    /// <param name="hull">The hull.</param>
    /// <returns>A pointer to the point array, or null when the hull has none.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3* b3GetHullPoints(b3HullData* hull) =>
        hull->pointOffset == 0 ? null : (Vector3*)((byte*)hull + hull->pointOffset);

    /// <summary>Gets the half-edges of a convex hull.</summary>
    /// <param name="hull">The hull.</param>
    /// <returns>A pointer to the half-edge array, or null when the hull has none.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static b3HullHalfEdge* b3GetHullEdges(b3HullData* hull) =>
        hull->edgeOffset == 0 ? null : (b3HullHalfEdge*)((byte*)hull + hull->edgeOffset);

    /// <summary>Gets the face planes of a convex hull.</summary>
    /// <param name="hull">The hull.</param>
    /// <returns>A pointer to the plane array, or null when the hull has none.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static b3Plane* b3GetHullPlanes(b3HullData* hull) =>
        hull->planeOffset == 0 ? null : (b3Plane*)((byte*)hull + hull->planeOffset);

    /// <summary>Gets the faces of a convex hull.</summary>
    /// <param name="hull">The hull.</param>
    /// <returns>A pointer to the face array, or null when the hull has none.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static b3HullFace* b3GetHullFaces(b3HullData* hull) =>
        hull->faceOffset == 0 ? null : (b3HullFace*)((byte*)hull + hull->faceOffset);

    /// <summary>
    /// Gets the structure-of-arrays vertex data of a convex hull: every x, then
    /// every y, then every z.
    /// </summary>
    /// <param name="hull">The hull.</param>
    /// <returns>A pointer to the data, or null when the hull has none.</returns>
    /// <remarks>
    /// Each of the three runs is padded to a multiple of four, and the padding
    /// repeats the first value.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float* b3GetHullSoaVertices(b3HullData* hull) =>
        hull->soaVertexOffset == 0 ? null : (float*)((byte*)hull + hull->soaVertexOffset);

    /// <summary>Gets the structure-of-arrays face normals of a convex hull.</summary>
    /// <param name="hull">The hull.</param>
    /// <returns>A pointer to the data, or null when the hull has none.</returns>
    /// <remarks>Padded the same way as <see cref="b3GetHullSoaVertices"/>.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float* b3GetHullSoaNormals(b3HullData* hull) =>
        hull->soaNormalOffset == 0 ? null : (float*)((byte*)hull + hull->soaNormalOffset);

    /// <summary>Gets the points of a convex hull as a span.</summary>
    /// <param name="hull">The hull.</param>
    /// <returns>The points, borrowed from the hull.</returns>
    public static ReadOnlySpan<Vector3> GetHullPointSpan(b3HullData* hull)
    {
        Vector3* points = b3GetHullPoints(hull);
        return points == null ? default : new ReadOnlySpan<Vector3>(points, hull->vertexCount);
    }

    /// <summary>Gets the face planes of a convex hull as a span.</summary>
    /// <param name="hull">The hull.</param>
    /// <returns>The planes, borrowed from the hull.</returns>
    public static ReadOnlySpan<b3Plane> GetHullPlaneSpan(b3HullData* hull)
    {
        b3Plane* planes = b3GetHullPlanes(hull);
        return planes == null ? default : new ReadOnlySpan<b3Plane>(planes, hull->faceCount);
    }

    /// <summary>Gets the half-edges of a convex hull as a span.</summary>
    /// <param name="hull">The hull.</param>
    /// <returns>The half-edges, borrowed from the hull.</returns>
    public static ReadOnlySpan<b3HullHalfEdge> GetHullEdgeSpan(b3HullData* hull)
    {
        b3HullHalfEdge* edges = b3GetHullEdges(hull);
        return edges == null ? default : new ReadOnlySpan<b3HullHalfEdge>(edges, hull->edgeCount);
    }

    // -------------------------------------------------------------- mesh data

    /// <summary>Gets the bounding volume hierarchy nodes of a mesh.</summary>
    /// <param name="mesh">The mesh.</param>
    /// <returns>A pointer to the node array, or null when the mesh has none.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static b3MeshNode* b3GetMeshNodes(b3MeshData* mesh) =>
        mesh->nodeOffset == 0 ? null : (b3MeshNode*)((byte*)mesh + mesh->nodeOffset);

    /// <summary>Gets the vertices of a mesh.</summary>
    /// <param name="mesh">The mesh.</param>
    /// <returns>A pointer to the vertex array, or null when the mesh has none.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3* b3GetMeshVertices(b3MeshData* mesh) =>
        mesh->vertexOffset == 0 ? null : (Vector3*)((byte*)mesh + mesh->vertexOffset);

    /// <summary>Gets the triangles of a mesh.</summary>
    /// <param name="mesh">The mesh.</param>
    /// <returns>A pointer to the triangle array, or null when the mesh has none.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static b3MeshTriangle* b3GetMeshTriangles(b3MeshData* mesh) =>
        mesh->triangleOffset == 0 ? null : (b3MeshTriangle*)((byte*)mesh + mesh->triangleOffset);

    /// <summary>Gets the per-triangle material indices of a mesh.</summary>
    /// <param name="mesh">The mesh.</param>
    /// <returns>A pointer to the array, or null when the mesh has none.</returns>
    /// <remarks>The entry count equals the triangle count.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte* b3GetMeshMaterialIndices(b3MeshData* mesh) =>
        mesh->materialOffset == 0 ? null : (byte*)mesh + mesh->materialOffset;

    /// <summary>Gets the per-triangle adjacency flags of a mesh.</summary>
    /// <param name="mesh">The mesh.</param>
    /// <returns>A pointer to the array, or null when the mesh has none.</returns>
    /// <remarks>
    /// The entry count equals the triangle count and each value is a
    /// <see cref="b3MeshEdgeFlags"/>.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte* b3GetMeshFlags(b3MeshData* mesh) =>
        mesh->flagsOffset == 0 ? null : (byte*)mesh + mesh->flagsOffset;

    /// <summary>Gets the vertices of a mesh as a span.</summary>
    /// <param name="mesh">The mesh.</param>
    /// <returns>The vertices, borrowed from the mesh.</returns>
    public static ReadOnlySpan<Vector3> GetMeshVertexSpan(b3MeshData* mesh)
    {
        Vector3* vertices = b3GetMeshVertices(mesh);
        return vertices == null ? default : new ReadOnlySpan<Vector3>(vertices, mesh->vertexCount);
    }

    /// <summary>Gets the triangles of a mesh as a span.</summary>
    /// <param name="mesh">The mesh.</param>
    /// <returns>The triangles, borrowed from the mesh.</returns>
    public static ReadOnlySpan<b3MeshTriangle> GetMeshTriangleSpan(b3MeshData* mesh)
    {
        b3MeshTriangle* triangles = b3GetMeshTriangles(mesh);
        return triangles == null ? default : new ReadOnlySpan<b3MeshTriangle>(triangles, mesh->triangleCount);
    }

    // ------------------------------------------------------ height field data

    /// <summary>Gets the compressed heights of a height field.</summary>
    /// <param name="hf">The height field.</param>
    /// <returns>A pointer to the array, or null when the height field has none.</returns>
    /// <remarks>One entry per grid point.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort* b3GetHeightFieldCompressedHeights(b3HeightFieldData* hf) =>
        hf->heightsOffset == 0 ? null : (ushort*)((byte*)hf + hf->heightsOffset);

    /// <summary>Gets the per-cell material indices of a height field.</summary>
    /// <param name="hf">The height field.</param>
    /// <returns>A pointer to the array, or null when the height field has none.</returns>
    /// <remarks>
    /// One entry per cell. The value <see cref="Constants.B3_HEIGHT_FIELD_HOLE"/>
    /// marks a hole.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte* b3GetHeightFieldMaterialIndices(b3HeightFieldData* hf) =>
        hf->materialOffset == 0 ? null : (byte*)hf + hf->materialOffset;

    /// <summary>Gets the per-triangle flags of a height field.</summary>
    /// <param name="hf">The height field.</param>
    /// <returns>A pointer to the array, or null when the height field has none.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte* b3GetHeightFieldFlags(b3HeightFieldData* hf) =>
        hf->flagsOffset == 0 ? null : (byte*)hf + hf->flagsOffset;

    /// <summary>Gets the compressed heights of a height field as a span.</summary>
    /// <param name="hf">The height field.</param>
    /// <returns>The heights, borrowed from the height field.</returns>
    public static ReadOnlySpan<ushort> GetHeightFieldHeightSpan(b3HeightFieldData* hf)
    {
        ushort* heights = b3GetHeightFieldCompressedHeights(hf);
        return heights == null ? default : new ReadOnlySpan<ushort>(heights, hf->columnCount * hf->rowCount);
    }
}
