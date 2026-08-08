// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Numerics;
using Box3D.Interop;
using Box3D.Native;
using Box3D.Visualizer.Rendering;

namespace Box3D.Visualizer;

/// <summary>
/// How a shape should look, when the scene has an opinion about it.
/// </summary>
/// <param name="Albedo">The linear surface colour.</param>
/// <param name="CastsShadow">Whether it drops a shadow on the shadow plane.</param>
internal readonly record struct Appearance(Vector3 Albedo, bool CastsShadow = true);

/// <summary>
/// Turns every shape in a world into a mesh, once.
/// </summary>
/// <remarks>
/// <para>
/// This is the half of debug draw that is easy to miss. Box3D does not emit
/// shape geometry as primitives every frame: the first time a shape needs
/// drawing it asks the application to build a drawable, and from then on it
/// hands that opaque handle back with a transform. For a real renderer that is
/// an upload once and a draw call per frame; here it is a tessellation once and
/// a transform per frame, which is the difference between rendering a
/// twenty-second animation in seconds and in minutes.
/// </para>
/// <para>
/// Spheres and capsules arrive whole, as a few floats, so this picks their
/// resolution. Hulls, meshes and height fields point at engine-owned data whose
/// layout belongs to the C API, so those are read through
/// <see cref="Box3D.Interop"/> - the deliberate door down a level. Reading
/// geometry is the one thing a shape factory legitimately does with the world
/// it is called from: it is a read of immutable data, on the calling thread,
/// inside a call the engine is driving.
/// </para>
/// </remarks>
internal sealed class ShapeMeshFactory : IDebugShapeFactory
{
    private readonly Dictionary<nint, Drawable> _drawables = new();
    private readonly Dictionary<Shape, Appearance> _appearance = new();

    private nint _next = 1;

    /// <summary>Gets how many drawables have been built.</summary>
    public int Built { get; private set; }

    /// <summary>Gets how many shapes this could not build a mesh for.</summary>
    public int Skipped { get; private set; }

    /// <summary>Says how a shape should look, before anything is drawn.</summary>
    /// <param name="shape">The shape.</param>
    /// <param name="appearance">Its colour and whether it casts a shadow.</param>
    /// <remarks>
    /// Recorded up front by the scene that built the shape, rather than looked
    /// up when the drawable is created, so that <see cref="CreateShape"/> stays
    /// a dictionary read and never asks the world a question mid-draw.
    /// </remarks>
    public void Paint(Shape shape, Appearance appearance) => _appearance[shape] = appearance;

    /// <inheritdoc/>
    public nint CreateShape(in DebugShape shape)
    {
        Mesh? mesh = Build(in shape);

        if (mesh is null)
        {
            // Returning zero tells Box3D there is nothing to draw for this
            // shape, and it stops asking.
            Skipped++;
            return nint.Zero;
        }

        nint handle = _next++;
        Built++;

        _drawables[handle] = _appearance.TryGetValue(shape.Shape, out Appearance appearance)
            ? new Drawable(mesh, appearance)
            : new Drawable(mesh, null);

        return handle;
    }

    /// <inheritdoc/>
    public void DestroyShape(nint handle) => _drawables.Remove(handle);

    /// <summary>Looks up a drawable built earlier.</summary>
    /// <param name="handle">The handle handed back by the engine.</param>
    /// <param name="drawable">The drawable.</param>
    /// <returns><see langword="true"/> when the handle is one of ours.</returns>
    public bool TryGet(nint handle, out Drawable drawable) => _drawables.TryGetValue(handle, out drawable);

    private static Mesh? Build(in DebugShape shape)
    {
        if (shape.TryGetSphere(out Sphere sphere))
        {
            return Tessellate.Sphere(sphere.Center, sphere.Radius);
        }

        if (shape.TryGetCapsule(out Capsule capsule))
        {
            return Tessellate.Capsule(capsule.Start, capsule.End, capsule.Radius);
        }

        b3ShapeId id = shape.Shape.ToNativeId();

        return shape.Type switch
        {
            ShapeType.Hull => FromHull(id),
            ShapeType.Mesh => FromMesh(id),
            ShapeType.HeightField => FromHeightField(id),
            _ => null,
        };
    }

    private static unsafe Mesh? FromHull(b3ShapeId id)
    {
        b3HullData* hull = B3.b3Shape_GetHull(id);
        if (hull is null)
        {
            return null;
        }

        Vector3* points = B3.b3GetHullPoints(hull);
        b3HullHalfEdge* edges = B3.b3GetHullEdges(hull);
        b3HullFace* faces = B3.b3GetHullFaces(hull);
        b3Plane* planes = B3.b3GetHullPlanes(hull);

        if (points is null || edges is null || faces is null || planes is null)
        {
            return null;
        }

        var builder = new MeshBuilder();

        // A hull face is a convex polygon reached by walking its half-edge ring.
        // Box3D stores at most 255 half-edges, so the loop below cannot run
        // longer than that even if the ring were somehow malformed.
        Span<Vector3> loop = stackalloc Vector3[64];

        for (int face = 0; face < hull->faceCount; face++)
        {
            int start = faces[face].edge;
            int edge = start;
            int count = 0;

            do
            {
                loop[count++] = points[edges[edge].origin];
                edge = edges[edge].next;
            }
            while (edge != start && count < loop.Length);

            builder.AddConvexPolygon(loop[..count], planes[face].normal);
        }

        return builder.Build();
    }

    private static unsafe Mesh? FromMesh(b3ShapeId id)
    {
        b3Mesh mesh = B3.b3Shape_GetMesh(id);
        if (mesh.data is null)
        {
            return null;
        }

        ReadOnlySpan<Vector3> vertices = B3.GetMeshVertexSpan(mesh.data);
        ReadOnlySpan<b3MeshTriangle> triangles = B3.GetMeshTriangleSpan(mesh.data);

        var builder = new MeshBuilder();

        foreach (b3MeshTriangle triangle in triangles)
        {
            builder.AddFlatTriangle(
                vertices[triangle.index1] * mesh.scale,
                vertices[triangle.index2] * mesh.scale,
                vertices[triangle.index3] * mesh.scale);
        }

        return builder.Build();
    }

    private static unsafe Mesh? FromHeightField(b3ShapeId id)
    {
        b3HeightFieldData* field = B3.b3Shape_GetHeightField(id);
        if (field is null)
        {
            return null;
        }

        ReadOnlySpan<ushort> heights = B3.GetHeightFieldHeightSpan(field);
        byte* materials = B3.b3GetHeightFieldMaterialIndices(field);

        if (heights.IsEmpty)
        {
            return null;
        }

        int columns = field->columnCount;
        int rows = field->rowCount;
        float minimum = field->minHeight;
        float step = field->heightScale;
        Vector3 scale = field->scale;

        var builder = new MeshBuilder();

        for (int row = 0; row < rows - 1; row++)
        {
            for (int column = 0; column < columns - 1; column++)
            {
                // A cell can be marked as a hole, and the engine collides with
                // it as if it were not there. Drawing it would be a lie.
                if (materials is not null && materials[(row * (columns - 1)) + column] == Constants.B3_HEIGHT_FIELD_HOLE)
                {
                    continue;
                }

                int index = (row * columns) + column;

                Vector3 corner00 = scale * new Vector3(column, minimum + (step * heights[index]), row);
                Vector3 corner10 = scale * new Vector3(column + 1, minimum + (step * heights[index + 1]), row);
                Vector3 corner01 = scale * new Vector3(column, minimum + (step * heights[index + columns]), row + 1);
                Vector3 corner11 = scale * new Vector3(column + 1, minimum + (step * heights[index + columns + 1]), row + 1);

                builder.AddFlatTriangle(corner00, corner01, corner10);
                builder.AddFlatTriangle(corner10, corner01, corner11);
            }
        }

        return builder.Build();
    }

    /// <summary>What the factory built for one shape.</summary>
    /// <param name="Mesh">The tessellated geometry, in shape-local space.</param>
    /// <param name="Appearance">
    /// What the scene asked for, or <see langword="null"/> to fall back to the
    /// colour the engine suggests at draw time.
    /// </param>
    internal readonly record struct Drawable(Mesh Mesh, Appearance? Appearance);
}
