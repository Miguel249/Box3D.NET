// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Numerics;

namespace Box3D.Visualizer.Rendering;

/// <summary>
/// An indexed triangle mesh in shape-local space.
/// </summary>
/// <remarks>
/// This is what a drawable is, on this renderer. Box3D asks the application to
/// build one per shape and then hands the handle back every frame with a
/// transform, so the tessellation below happens once no matter how long the
/// simulation runs.
/// </remarks>
internal sealed class Mesh
{
    /// <summary>Creates a mesh from its arrays, which it takes ownership of.</summary>
    /// <param name="positions">The vertex positions.</param>
    /// <param name="normals">The vertex normals, one per position.</param>
    /// <param name="indices">Three indices per triangle.</param>
    public Mesh(Vector3[] positions, Vector3[] normals, int[] indices)
    {
        ArgumentNullException.ThrowIfNull(positions);
        ArgumentNullException.ThrowIfNull(normals);
        ArgumentNullException.ThrowIfNull(indices);

        Positions = positions;
        Normals = normals;
        Indices = indices;
    }

    /// <summary>Gets the vertex positions.</summary>
    public Vector3[] Positions { get; }

    /// <summary>Gets the vertex normals.</summary>
    public Vector3[] Normals { get; }

    /// <summary>Gets three indices per triangle.</summary>
    public int[] Indices { get; }

    /// <summary>Gets the number of triangles.</summary>
    public int TriangleCount => Indices.Length / 3;
}

/// <summary>
/// Accumulates triangles into a <see cref="Mesh"/>.
/// </summary>
internal sealed class MeshBuilder
{
    private readonly List<Vector3> _positions = new();
    private readonly List<Vector3> _normals = new();
    private readonly List<int> _indices = new();

    /// <summary>Gets the number of vertices added so far.</summary>
    public int VertexCount => _positions.Count;

    /// <summary>Adds a vertex with its own normal, for smooth surfaces.</summary>
    /// <param name="position">The position.</param>
    /// <param name="normal">The normal.</param>
    /// <returns>The index of the new vertex.</returns>
    public int AddVertex(Vector3 position, Vector3 normal)
    {
        _positions.Add(position);
        _normals.Add(normal);
        return _positions.Count - 1;
    }

    /// <summary>Adds a triangle over vertices already added.</summary>
    /// <param name="a">The first vertex index.</param>
    /// <param name="b">The second vertex index.</param>
    /// <param name="c">The third vertex index.</param>
    public void AddTriangle(int a, int b, int c)
    {
        _indices.Add(a);
        _indices.Add(b);
        _indices.Add(c);
    }

    /// <summary>Adds a quad over vertices already added, as two triangles.</summary>
    /// <param name="a">The first vertex index.</param>
    /// <param name="b">The second vertex index.</param>
    /// <param name="c">The third vertex index.</param>
    /// <param name="d">The fourth vertex index.</param>
    public void AddQuad(int a, int b, int c, int d)
    {
        AddTriangle(a, b, c);
        AddTriangle(a, c, d);
    }

    /// <summary>
    /// Adds a triangle with its own vertices and a single face normal, for
    /// surfaces that should read as faceted rather than smooth.
    /// </summary>
    /// <param name="a">The first corner.</param>
    /// <param name="b">The second corner.</param>
    /// <param name="c">The third corner.</param>
    public void AddFlatTriangle(Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 normal = Vector3.Cross(b - a, c - a);

        // A degenerate triangle has no normal to speak of. Keeping it with a
        // zero normal would shade as a black sliver, so it is dropped.
        float length = normal.Length();
        if (length <= float.Epsilon)
        {
            return;
        }

        normal /= length;

        int i0 = AddVertex(a, normal);
        int i1 = AddVertex(b, normal);
        int i2 = AddVertex(c, normal);

        AddTriangle(i0, i1, i2);
    }

    /// <summary>Adds a convex polygon as a fan of flat triangles.</summary>
    /// <param name="loop">The corners, in order around the polygon.</param>
    /// <param name="normal">The face normal.</param>
    public void AddConvexPolygon(ReadOnlySpan<Vector3> loop, Vector3 normal)
    {
        if (loop.Length < 3)
        {
            return;
        }

        int first = AddVertex(loop[0], normal);
        int previous = AddVertex(loop[1], normal);

        for (int i = 2; i < loop.Length; i++)
        {
            int current = AddVertex(loop[i], normal);
            AddTriangle(first, previous, current);
            previous = current;
        }
    }

    /// <summary>Produces the finished mesh.</summary>
    /// <returns>The mesh, or <see langword="null"/> when nothing was added.</returns>
    public Mesh? Build() =>
        _indices.Count == 0
            ? null
            : new Mesh(_positions.ToArray(), _normals.ToArray(), _indices.ToArray());
}
