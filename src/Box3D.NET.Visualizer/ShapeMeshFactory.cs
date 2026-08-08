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
/// <para>
/// Baked compounds are the exception, and the reason <see cref="Supply"/>
/// exists: there is no <c>b3Shape_GetCompound</c>, so the children cannot be
/// reached from the shape at all and the scene that baked them has to hand the
/// compound over.
/// </para>
/// </remarks>
internal sealed class ShapeMeshFactory : IDebugShapeFactory
{
    private readonly Dictionary<nint, Drawable> _drawables = new();
    private readonly Dictionary<Shape, Appearance> _appearance = new();
    private readonly Dictionary<Shape, CompoundGeometry> _compounds = new();

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

    /// <summary>Hands over the compound behind a shape, so that it can be drawn.</summary>
    /// <param name="shape">The shape the compound was attached to.</param>
    /// <param name="compound">The compound it was baked from.</param>
    /// <remarks>
    /// <para>
    /// The one piece of geometry a renderer cannot fetch for itself. Every other
    /// kind of shape can be read back from its handle - <c>b3Shape_GetHull</c>,
    /// <c>b3Shape_GetMesh</c>, <c>b3Shape_GetHeightField</c> - but the C API has
    /// no <c>b3Shape_GetCompound</c>, so the only route to the children is the
    /// pointer held by whoever baked them.
    /// </para>
    /// <para>
    /// A compound that is not supplied is counted in <see cref="Skipped"/> and
    /// drawn as nothing, which is the honest outcome: the alternative is a hole
    /// in the picture with no explanation.
    /// </para>
    /// </remarks>
    public void Supply(Shape shape, CompoundGeometry compound)
    {
        ArgumentNullException.ThrowIfNull(compound);

        _compounds[shape] = compound;
    }

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

    private Mesh? Build(in DebugShape shape)
    {
        if (shape.TryGetSphere(out Sphere sphere))
        {
            return Tessellate.Sphere(sphere.Center, sphere.Radius);
        }

        if (shape.TryGetCapsule(out Capsule capsule))
        {
            return Tessellate.Capsule(capsule.Start, capsule.End, capsule.Radius);
        }

        if (shape.Type == ShapeType.Compound)
        {
            return _compounds.TryGetValue(shape.Shape, out CompoundGeometry? compound)
                ? FromCompound(compound)
                : null;
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
        var builder = new MeshBuilder();
        AddHull(builder, B3.b3Shape_GetHull(id), Vector3.Zero, Quaternion.Identity);

        return builder.Build();
    }

    private static unsafe Mesh? FromMesh(b3ShapeId id)
    {
        b3Mesh mesh = B3.b3Shape_GetMesh(id);

        var builder = new MeshBuilder();
        AddMesh(builder, mesh.data, mesh.scale, Vector3.Zero, Quaternion.Identity);

        return builder.Build();
    }

    /// <summary>Tessellates every child of a baked compound into one mesh.</summary>
    /// <param name="compound">The compound, as handed over by <see cref="Supply"/>.</param>
    /// <returns>The mesh, in compound-local space.</returns>
    /// <remarks>
    /// One drawable for the whole compound rather than one per child, because
    /// that is what the engine asks for: a compound is a single shape in the
    /// broad phase and arrives at the drawer as a single handle and a single
    /// transform. Folding the children in here is the tessellation-once bargain
    /// that the rest of this class is built on.
    /// </remarks>
    private static unsafe Mesh? FromCompound(CompoundGeometry compound)
    {
        b3CompoundData* data = compound.ToNativePointer();
        var builder = new MeshBuilder();

        // Walked through b3GetCompoundChild rather than the four typed
        // accessors, because that is the function that knows how the four
        // arrays are laid out end to end and which of them carries a transform.
        int children = data->capsuleCount + data->hullCount + data->meshCount + data->sphereCount;

        for (int i = 0; i < children; i++)
        {
            b3ChildShape child = B3.b3GetCompoundChild(data, i);
            Vector3 position = child.transform.p;
            Quaternion rotation = child.transform.q;

            switch (child.type)
            {
                case b3ShapeType.b3_sphereShape:
                    builder.Add(
                        Tessellate.Sphere(child.sphere.center, child.sphere.radius),
                        position,
                        rotation);
                    break;

                case b3ShapeType.b3_capsuleShape:
                    builder.Add(
                        Tessellate.Capsule(child.capsule.center1, child.capsule.center2, child.capsule.radius),
                        position,
                        rotation);
                    break;

                case b3ShapeType.b3_hullShape:
                    AddHull(builder, child.hull, position, rotation);
                    break;

                case b3ShapeType.b3_meshShape:
                    AddMesh(builder, child.mesh.data, child.mesh.scale, position, rotation);
                    break;

                default:
                    // A compound cannot hold a height field or another compound,
                    // so there is no fourth case to write.
                    break;
            }
        }

        return builder.Build();
    }

    private static unsafe void AddHull(MeshBuilder builder, b3HullData* hull, Vector3 position, Quaternion rotation)
    {
        if (hull is null)
        {
            return;
        }

        Vector3* points = B3.b3GetHullPoints(hull);
        b3HullHalfEdge* edges = B3.b3GetHullEdges(hull);
        b3HullFace* faces = B3.b3GetHullFaces(hull);
        b3Plane* planes = B3.b3GetHullPlanes(hull);

        if (points is null || edges is null || faces is null || planes is null)
        {
            return;
        }

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
                loop[count++] = position + Vector3.Transform(points[edges[edge].origin], rotation);
                edge = edges[edge].next;
            }
            while (edge != start && count < loop.Length);

            builder.AddConvexPolygon(loop[..count], Vector3.Transform(planes[face].normal, rotation));
        }
    }

    private static unsafe void AddMesh(
        MeshBuilder builder,
        b3MeshData* data,
        Vector3 scale,
        Vector3 position,
        Quaternion rotation)
    {
        if (data is null)
        {
            return;
        }

        ReadOnlySpan<Vector3> vertices = B3.GetMeshVertexSpan(data);
        ReadOnlySpan<b3MeshTriangle> triangles = B3.GetMeshTriangleSpan(data);

        foreach (b3MeshTriangle triangle in triangles)
        {
            builder.AddFlatTriangle(
                position + Vector3.Transform(vertices[triangle.index1] * scale, rotation),
                position + Vector3.Transform(vertices[triangle.index2] * scale, rotation),
                position + Vector3.Transform(vertices[triangle.index3] * scale, rotation));
        }
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
