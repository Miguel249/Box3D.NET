// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using Box3D.Native;
using Xunit;

namespace Box3D.Tests;

/// <summary>
/// Covers the heavy collision geometry: hulls, meshes and height fields.
/// </summary>
/// <remarks>
/// These carry the only non-trivial ownership rules in the library, so the tests
/// check the rules as much as the geometry: that building allocates, that
/// disposing gives it back, and that a mesh outliving its shapes is what the API
/// actually requires.
/// </remarks>
[Collection(NativeCollection.Name)]
public class GeometryTests
{
    // A unit tetrahedron: the smallest thing with a genuine volume.
    private static readonly Vector3[] TetrahedronPoints =
    [
        new Vector3(0.0f, 0.0f, 0.0f),
        new Vector3(1.0f, 0.0f, 0.0f),
        new Vector3(0.0f, 1.0f, 0.0f),
        new Vector3(0.0f, 0.0f, 1.0f),
    ];

    // A two-triangle floor spanning ten metres, wound so it faces up.
    private static readonly Vector3[] FloorVertices =
    [
        new Vector3(-5.0f, 0.0f, -5.0f),
        new Vector3(5.0f, 0.0f, -5.0f),
        new Vector3(5.0f, 0.0f, 5.0f),
        new Vector3(-5.0f, 0.0f, 5.0f),
    ];

    private static readonly int[] FloorIndices = [0, 2, 1, 0, 3, 2];

    // ---------------------------------------------------------------- hulls

    [NativeFact]
    public void A_hull_from_points_has_volume_and_bounds()
    {
        using ConvexHull hull = ConvexHull.FromPoints(TetrahedronPoints);

        Assert.Equal(4, hull.VertexCount);
        Assert.Equal(4, hull.FaceCount);

        // A corner tetrahedron of a unit cube encloses a sixth of it.
        Assert.Equal(1.0f / 6.0f, hull.Volume, 3);

        Assert.Equal(Vector3.Zero, hull.Bounds.Min);
        Assert.Equal(Vector3.One, hull.Bounds.Max);
    }

    [NativeFact]
    public void A_hull_needs_four_points()
    {
        Assert.Throws<ArgumentException>(() => ConvexHull.FromPoints(TetrahedronPoints.AsSpan(0, 3)));
    }

    [NativeFact]
    public void A_degenerate_point_cloud_is_rejected_rather_than_crashing()
    {
        // Four coplanar points enclose nothing, so there is no hull to build.
        Vector3[] coplanar =
        [
            new Vector3(0.0f, 0.0f, 0.0f),
            new Vector3(1.0f, 0.0f, 0.0f),
            new Vector3(1.0f, 0.0f, 1.0f),
            new Vector3(0.0f, 0.0f, 1.0f),
        ];

        Assert.Throws<ArgumentException>(() => ConvexHull.FromPoints(coplanar));
    }

    [NativeFact]
    public void The_generated_hulls_are_solid()
    {
        using ConvexHull cylinder = ConvexHull.Cylinder(2.0f, 1.0f);
        using ConvexHull cone = ConvexHull.Cone(2.0f, 1.0f);
        using ConvexHull rock = ConvexHull.Rock(0.5f);

        Assert.True(cylinder.Volume > 0.0f);
        Assert.True(cone.Volume > 0.0f);
        Assert.True(rock.Volume > 0.0f);

        // A cylinder of radius 1 and height 2 encloses about 2*pi, a little less
        // once tessellated into flat faces.
        Assert.True(cylinder.Volume is > 5.5f and < 6.3f, $"cylinder volume was {cylinder.Volume}");
    }

    [NativeFact]
    public void A_hull_is_copied_into_the_shape_so_it_can_be_disposed()
    {
        using var world = new PhysicsWorld(WorldSettings.Default with
        {
            Gravity = new Vector3(0.0f, -10.0f, 0.0f),
        });

        Body ground = world.CreateStaticBody(new Vector3(0.0f, -0.5f, 0.0f));
        ground.AddBox(new Box(new Vector3(20.0f, 0.5f, 20.0f)));

        Body body = world.CreateDynamicBody(new Vector3(0.0f, 3.0f, 0.0f));

        using (ConvexHull hull = ConvexHull.Cylinder(1.0f, 0.5f))
        {
            body.AddHull(hull);
        }

        // The hull is gone but the shape keeps working, because attaching it
        // interned a copy in the world's hull database.
        for (int i = 0; i < 180; i++)
        {
            world.Step(1.0f / 60.0f);
        }

        // A cylinder stands on the body origin rather than being centred on it,
        // so resting on ground whose surface is at y = 0 leaves the origin there.
        Assert.True(
            MathF.Abs(body.Position.Y) < 0.1f,
            $"expected the cylinder to rest with its base at the origin, Y was {body.Position.Y}");
        Assert.True(body.Mass > 0.0f);
    }

    [NativeFact]
    public void Disposing_a_hull_twice_is_harmless()
    {
        ConvexHull hull = ConvexHull.Rock(0.5f);

        hull.Dispose();
        hull.Dispose();

        Assert.True(hull.IsDisposed);
        Assert.Throws<ObjectDisposedException>(() => hull.VertexCount);
    }

    [NativeFact]
    public void A_hull_gives_its_memory_back()
    {
        int before = B3.b3GetByteCount();

        ConvexHull hull = ConvexHull.Cylinder(2.0f, 1.0f, sides: 32);
        Assert.True(B3.b3GetByteCount() > before, "building a hull should allocate");

        hull.Dispose();

        Assert.Equal(before, B3.b3GetByteCount());
    }

    // --------------------------------------------------------------- meshes

    [NativeFact]
    public void A_mesh_reports_what_it_was_built_from()
    {
        using CollisionMesh mesh = CollisionMesh.FromTriangles(FloorVertices, FloorIndices);

        Assert.Equal(2, mesh.TriangleCount);
        Assert.True(mesh.VertexCount >= 3);
        Assert.True(mesh.ByteCount > 0);
        Assert.Equal(0, mesh.DegenerateTriangleCount);

        Assert.Equal(-5.0f, mesh.Bounds.Min.X, 3);
        Assert.Equal(5.0f, mesh.Bounds.Max.Z, 3);
    }

    [NativeFact]
    public void A_mesh_rejects_an_index_count_that_is_not_whole_triangles()
    {
        Assert.Throws<ArgumentException>(() =>
            CollisionMesh.FromTriangles(FloorVertices, [0, 1, 2, 3]));
    }

    [NativeFact]
    public void A_mesh_rejects_an_out_of_range_index()
    {
        // Box3D asserts on this, and asserts are compiled out of a release build,
        // so an index past the end would read arbitrary memory while building.
        Assert.Throws<ArgumentException>(() =>
            CollisionMesh.FromTriangles(FloorVertices, [0, 1, 99]));
    }

    [NativeFact]
    public void A_mesh_rejects_a_material_index_count_that_is_not_per_triangle()
    {
        Assert.Throws<ArgumentException>(() =>
            CollisionMesh.FromTriangles(FloorVertices, FloorIndices, materialIndices: [0, 0, 0]));
    }

    [NativeFact]
    public void A_body_rests_on_a_mesh_floor()
    {
        using CollisionMesh floor = CollisionMesh.FromTriangles(FloorVertices, FloorIndices);

        using (var world = new PhysicsWorld(WorldSettings.Default with
        {
            Gravity = new Vector3(0.0f, -10.0f, 0.0f),
        }))
        {
            Body ground = world.CreateStaticBody();
            ground.AddMesh(floor);

            Body ball = world.CreateDynamicBody(new Vector3(0.0f, 5.0f, 0.0f));
            ball.AddSphere(new Sphere(0.5f));

            for (int i = 0; i < 300; i++)
            {
                world.Step(1.0f / 60.0f);
            }

            // The mesh surface sits at y = 0, so a half-metre ball rests at 0.5.
            Assert.True(ball.Position.Y is > 0.3f and < 0.7f, $"the ball settled at {ball.Position.Y}");
        }

        // World first, then the mesh: the shape held a borrowed pointer to it.
    }

    [NativeFact]
    public void A_mesh_can_be_shared_by_several_shapes_at_different_scales()
    {
        using CollisionMesh floor = CollisionMesh.FromTriangles(FloorVertices, FloorIndices);

        using var world = new PhysicsWorld();

        Body first = world.CreateStaticBody();
        first.AddMesh(floor);

        Body second = world.CreateStaticBody(new Vector3(50.0f, 0.0f, 0.0f));
        second.AddMesh(floor, scale: new Vector3(0.5f, 1.0f, 0.5f));

        world.Step(1.0f / 60.0f);

        // The scaled instance covers half the ground of the original.
        Assert.Equal(10.0f, first.Bounds.Size.X, 1);
        Assert.Equal(5.0f, second.Bounds.Size.X, 1);
    }

    [NativeFact]
    public void A_mesh_may_only_go_on_a_static_body()
    {
        using CollisionMesh floor = CollisionMesh.FromTriangles(FloorVertices, FloorIndices);
        using var world = new PhysicsWorld();

        Body dynamic = world.CreateDynamicBody();

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => dynamic.AddMesh(floor));
        Assert.Contains("static", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [NativeFact]
    public void A_mesh_gives_its_memory_back()
    {
        int before = B3.b3GetByteCount();

        CollisionMesh mesh = CollisionMesh.Grid(16, 16, 1.0f);
        Assert.True(B3.b3GetByteCount() > before, "building a mesh should allocate");

        mesh.Dispose();

        Assert.Equal(before, B3.b3GetByteCount());
    }

    [NativeFact]
    public void The_generated_meshes_build()
    {
        using CollisionMesh grid = CollisionMesh.Grid(8, 8, 1.0f);
        using CollisionMesh wave = CollisionMesh.Wave(8, 8, 1.0f, 2.0f);
        using CollisionMesh hollow = CollisionMesh.HollowBox(Vector3.Zero, new Vector3(5.0f));

        Assert.True(grid.TriangleCount > 0);
        Assert.True(wave.TriangleCount > 0);
        Assert.True(hollow.TriangleCount > 0);

        // A wave surface is not flat, unlike the grid.
        Assert.True(wave.Bounds.Size.Y > 0.1f, "the wave mesh should have relief");
    }

    [NativeFact]
    public void A_wave_frequency_that_aliases_produces_a_flat_surface()
    {
        // The surface is sampled at sin(2*pi * frequency * cellWidth * i), so
        // when frequency * cellWidth is a whole number every sample lands on a
        // zero crossing and the waves vanish. This is easy to hit by accident,
        // and produces a mesh that looks broken rather than flat, so the default
        // frequency is chosen to avoid it and the behaviour is pinned here.
        using CollisionMesh aliased = CollisionMesh.Wave(
            8, 8, cellWidth: 1.0f, amplitude: 5.0f, rowFrequency: 1.0f, columnFrequency: 1.0f);

        using CollisionMesh proper = CollisionMesh.Wave(
            8, 8, cellWidth: 1.0f, amplitude: 5.0f, rowFrequency: 0.25f, columnFrequency: 0.25f);

        Assert.True(aliased.Bounds.Size.Y < 0.01f, "sampling on the zero crossings flattens the wave");
        Assert.True(proper.Bounds.Size.Y > 1.0f, "a frequency clear of the sampling rate gives real relief");
    }

    // --------------------------------------------------------- height fields

    [NativeFact]
    public void A_height_field_reports_its_grid()
    {
        float[] heights = new float[4 * 4];

        using HeightField field = HeightField.FromHeights(heights, 4, 4, Vector3.One);

        Assert.Equal(4, field.ColumnCount);
        Assert.Equal(4, field.RowCount);
        Assert.True(field.ByteCount > 0);
    }

    [NativeFact]
    public void A_height_field_rejects_the_wrong_number_of_heights()
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() =>
            HeightField.FromHeights(new float[10], 4, 4, Vector3.One));

        // The message has to say what was expected, because this is the mistake
        // everyone makes first.
        Assert.Contains("16", error.Message, StringComparison.Ordinal);
    }

    [NativeFact]
    public void A_height_field_rejects_the_wrong_number_of_materials()
    {
        // A 4 by 4 grid of points has 3 by 3 cells, so nine materials, not sixteen.
        ArgumentException error = Assert.Throws<ArgumentException>(() =>
            HeightField.FromHeights(new float[16], 4, 4, Vector3.One, materialIndices: new byte[16]));

        Assert.Contains("9", error.Message, StringComparison.Ordinal);
    }

    [NativeFact]
    public void A_height_field_rejects_a_non_positive_scale()
    {
        Assert.Throws<ArgumentException>(() =>
            HeightField.FromHeights(new float[16], 4, 4, new Vector3(1.0f, 0.0f, 1.0f)));
    }

    [NativeFact]
    public void A_body_rests_on_terrain_at_the_right_height()
    {
        // A flat plateau two units up, over a 9 by 9 grid of one-metre cells.
        const int Side = 9;
        float[] heights = new float[Side * Side];
        Array.Fill(heights, 2.0f);

        using HeightField terrain = HeightField.FromHeights(
            heights,
            Side,
            Side,
            new Vector3(1.0f, 1.0f, 1.0f),
            minimumHeight: 0.0f,
            maximumHeight: 4.0f);

        using (var world = new PhysicsWorld(WorldSettings.Default with
        {
            Gravity = new Vector3(0.0f, -10.0f, 0.0f),
        }))
        {
            Body ground = world.CreateStaticBody();
            ground.AddHeightField(terrain);

            // Dropped over the middle of the grid.
            Body ball = world.CreateDynamicBody(new Vector3(4.0f, 8.0f, 4.0f));
            ball.AddSphere(new Sphere(0.5f));

            for (int i = 0; i < 300; i++)
            {
                world.Step(1.0f / 60.0f);
            }

            // The surface is at 2, so a half-metre ball comes to rest at 2.5.
            Assert.True(ball.Position.Y is > 2.2f and < 2.8f, $"the ball settled at {ball.Position.Y}");
        }
    }

    [NativeFact]
    public void A_height_field_may_only_go_on_a_static_body()
    {
        using HeightField field = HeightField.Grid(4, 4, Vector3.One);
        using var world = new PhysicsWorld();

        Body kinematic = world.CreateKinematicBody();

        Assert.Throws<InvalidOperationException>(() => kinematic.AddHeightField(field));
    }

    [NativeFact]
    public void A_height_field_gives_its_memory_back()
    {
        int before = B3.b3GetByteCount();

        HeightField field = HeightField.Grid(32, 32, Vector3.One);
        Assert.True(B3.b3GetByteCount() > before, "building a height field should allocate");

        field.Dispose();

        Assert.Equal(before, B3.b3GetByteCount());
    }

    [NativeFact]
    public void A_height_field_is_smaller_than_the_equivalent_mesh()
    {
        // The reason to reach for terrain rather than a mesh: one compressed
        // height per grid point instead of triangles and a hierarchy over them.
        const int Side = 64;

        using HeightField field = HeightField.Grid(Side, Side, Vector3.One);
        using CollisionMesh mesh = CollisionMesh.Grid(Side - 1, Side - 1, 1.0f);

        Assert.True(
            field.ByteCount < mesh.ByteCount,
            $"height field {field.ByteCount} bytes against mesh {mesh.ByteCount} bytes");
    }

    [NativeFact]
    public void Disposed_geometry_cannot_be_attached()
    {
        using var world = new PhysicsWorld();
        Body ground = world.CreateStaticBody();

        CollisionMesh mesh = CollisionMesh.Grid(4, 4, 1.0f);
        mesh.Dispose();

        Assert.Throws<ObjectDisposedException>(() => ground.AddMesh(mesh));
    }

    // ------------------------------------------------------- baked compounds

    [NativeFact]
    public void A_compound_counts_the_children_it_was_built_from()
    {
        using ConvexHull tetrahedron = ConvexHull.FromPoints(TetrahedronPoints);
        using CollisionMesh floor = CollisionMesh.FromTriangles(FloorVertices, FloorIndices);

        using CompoundGeometry compound = new CompoundBuilder()
            .AddSphere(new Sphere(new Vector3(0.0f, 1.0f, 0.0f), 0.5f))
            .AddSphere(new Sphere(new Vector3(2.0f, 1.0f, 0.0f), 0.5f))
            .AddCapsule(Capsule.Upright(1.0f, 0.25f))
            .AddHull(tetrahedron, new Vector3(4.0f, 0.0f, 0.0f))
            .AddMesh(floor, Vector3.Zero)
            .Build();

        Assert.Equal(2, compound.SphereCount);
        Assert.Equal(1, compound.CapsuleCount);
        Assert.Equal(1, compound.HullCount);
        Assert.Equal(1, compound.MeshCount);
        Assert.Equal(5, compound.ChildCount);
        Assert.True(compound.ByteCount > 0, $"a baked compound occupies {compound.ByteCount} bytes");
    }

    [NativeFact]
    public void A_compound_encloses_every_child()
    {
        using CompoundGeometry compound = new CompoundBuilder()
            .AddSphere(new Sphere(new Vector3(-3.0f, 0.0f, 0.0f), 1.0f))
            .AddSphere(new Sphere(new Vector3(3.0f, 0.0f, 0.0f), 1.0f))
            .Build();

        BoundingBox bounds = compound.Bounds;

        Assert.True(bounds.Min.X <= -4.0f, $"the box starts at {bounds.Min.X}");
        Assert.True(bounds.Max.X >= 4.0f, $"the box ends at {bounds.Max.X}");
    }

    [NativeFact]
    public void A_compound_keeps_its_sources_alive_no_longer_than_the_bake()
    {
        // Everything in the definition is cloned, so the hull may go as soon as
        // the compound exists. This is the difference between a compound and a
        // mesh, and it is easy to get backwards.
        ConvexHull tetrahedron = ConvexHull.FromPoints(TetrahedronPoints);

        using CompoundGeometry compound = new CompoundBuilder()
            .AddHull(tetrahedron, Vector3.Zero)
            .Build();

        tetrahedron.Dispose();

        using var world = new PhysicsWorld();
        Body scenery = world.CreateStaticBody();
        scenery.AddCompound(compound);

        for (int i = 0; i < 10; i++)
        {
            world.Step(1.0f / 60.0f);
        }

        Assert.Equal(1, compound.HullCount);
    }

    [NativeFact]
    public void A_compound_arrives_as_one_shape()
    {
        using ConvexHull tetrahedron = ConvexHull.FromPoints(TetrahedronPoints);

        using CompoundGeometry compound = new CompoundBuilder()
            .AddHull(tetrahedron, Vector3.Zero)
            .AddHull(tetrahedron, new Vector3(2.0f, 0.0f, 0.0f))
            .AddHull(tetrahedron, new Vector3(4.0f, 0.0f, 0.0f))
            .Build();

        using var world = new PhysicsWorld();
        Body scenery = world.CreateStaticBody();

        Shape shape = scenery.AddCompound(compound);

        // Three children, one broad-phase shape. That is the whole point of
        // baking one.
        Assert.Equal(1, scenery.ShapeCount);
        Assert.Equal(ShapeType.Compound, shape.Type);
    }

    [NativeFact]
    public void A_body_rests_on_a_compound()
    {
        using CompoundGeometry floor = new CompoundBuilder()
            .AddSphere(new Sphere(new Vector3(0.0f, -0.5f, 0.0f), 1.0f))
            .Build();

        using (var world = new PhysicsWorld(WorldSettings.Default with
        {
            Gravity = new Vector3(0.0f, -10.0f, 0.0f),
        }))
        {
            Body scenery = world.CreateStaticBody();
            scenery.AddCompound(floor);

            Body ball = world.CreateDynamicBody(new Vector3(0.0f, 4.0f, 0.0f));
            ball.AddSphere(new Sphere(0.25f));

            for (int i = 0; i < 300; i++)
            {
                world.Step(1.0f / 60.0f);
            }

            // The child sphere reaches 0.5, so a quarter-metre ball settles at
            // 0.75. If the compound were not colliding, the ball would be gone.
            Assert.True(ball.Position.Y is > 0.5f and < 1.0f, $"the ball settled at {ball.Position.Y}");
        }
    }

    [NativeFact]
    public void A_compound_may_only_go_on_a_static_body()
    {
        using CompoundGeometry compound = new CompoundBuilder()
            .AddSphere(new Sphere(0.5f))
            .Build();

        using var world = new PhysicsWorld();
        Body dynamic = world.CreateDynamicBody();

        Assert.Throws<InvalidOperationException>(() => dynamic.AddCompound(compound));
    }

    [NativeFact]
    public void An_empty_compound_is_refused()
    {
        var builder = new CompoundBuilder();

        Assert.Equal(0, builder.ChildCount);
        Assert.Throws<InvalidOperationException>(builder.Build);
    }

    [NativeFact]
    public void A_compound_built_from_disposed_geometry_is_refused()
    {
        ConvexHull tetrahedron = ConvexHull.FromPoints(TetrahedronPoints);

        var builder = new CompoundBuilder().AddHull(tetrahedron, Vector3.Zero);
        tetrahedron.Dispose();

        Assert.Throws<ObjectDisposedException>(builder.Build);
    }

    [NativeFact]
    public void A_compound_gives_its_memory_back()
    {
        using ConvexHull tetrahedron = ConvexHull.FromPoints(TetrahedronPoints);

        int before = B3.b3GetByteCount();

        CompoundGeometry compound = new CompoundBuilder()
            .AddHull(tetrahedron, Vector3.Zero)
            .AddSphere(new Sphere(1.0f))
            .Build();

        Assert.True(B3.b3GetByteCount() > before, "baking a compound should allocate");

        compound.Dispose();

        Assert.Equal(before, B3.b3GetByteCount());
    }

    [NativeFact]
    public void A_disposed_compound_answers_nothing()
    {
        CompoundGeometry compound = new CompoundBuilder()
            .AddSphere(new Sphere(1.0f))
            .Build();

        compound.Dispose();

        Assert.True(compound.IsDisposed);
        Assert.Throws<ObjectDisposedException>(() => compound.ChildCount);

        // Disposing twice is not an error, and must not free the same block
        // again.
        compound.Dispose();
    }
}
