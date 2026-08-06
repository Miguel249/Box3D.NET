// SPDX-License-Identifier: MIT

using System;
using System.Numerics;

namespace Box3D.Samples;

/// <summary>
/// Terrain from a height field, and what it costs against the equivalent mesh.
/// </summary>
internal static class HeightFieldSample
{
    public static void Run()
    {
        const int Side = 65;
        const float CellSize = 2.0f;

        // A height map. In a real game this comes from a file; the shape of the
        // data is the same either way: one float per grid point, row by row.
        float[] heights = new float[Side * Side];

        for (int z = 0; z < Side; z++)
        {
            for (int x = 0; x < Side; x++)
            {
                float fx = x / (float)(Side - 1);
                float fz = z / (float)(Side - 1);

                // A gentle bowl, so anything dropped on it rolls to the middle.
                float dx = (fx - 0.5f) * 2.0f;
                float dz = (fz - 0.5f) * 2.0f;

                heights[(z * Side) + x] = 8.0f * ((dx * dx) + (dz * dz));
            }
        }

        // The quantization range is given explicitly. Two terrains that must meet
        // along an edge have to agree on it, or their height steps differ and a
        // seam opens up between them.
        using HeightField terrain = HeightField.FromHeights(
            heights,
            columnCount: Side,
            rowCount: Side,
            scale: new Vector3(CellSize, 1.0f, CellSize),
            minimumHeight: 0.0f,
            maximumHeight: 16.0f);

        Console.WriteLine($"   grid          : {terrain.ColumnCount} x {terrain.RowCount}");
        Console.WriteLine($"   extent        : {terrain.Bounds.Size.X:F0} x {terrain.Bounds.Size.Z:F0} m");
        Console.WriteLine($"   origin corner : {terrain.Bounds.Min}");
        Console.WriteLine($"   memory        : {terrain.ByteCount / 1024} KB");

        // The reason to reach for a height field rather than a mesh.
        using CollisionMesh equivalent = CollisionMesh.Grid(Side - 1, Side - 1, CellSize);
        Console.WriteLine($"   same as mesh  : {equivalent.ByteCount / 1024} KB");

        SampleRunner.Expect(
            terrain.ByteCount < equivalent.ByteCount,
            "a height field stores less than the equivalent mesh");

        // The world is scoped inside the terrain's lifetime. This ordering is not
        // stylistic: shapes hold a borrowed pointer into the height field, so it
        // has to outlive them.
        using (var world = new PhysicsWorld(WorldSettings.Default with
        {
            Gravity = new Vector3(0.0f, -10.0f, 0.0f),
        }))
        {
            Body ground = world.CreateStaticBody();
            ground.AddHeightField(terrain, ShapeDefinition.Default with
            {
                Material = PhysicsMaterial.Default with { Friction = 0.4f },
            });

            // A height field grows in positive x and z from its body origin
            // rather than being centred on it, so the middle of the bowl is at
            // the centre of the bounds, not at the world origin. Reading it back
            // rather than assuming is the habit worth copying.
            Vector3 bowlCentre = terrain.Bounds.Center;

            Console.WriteLine($"   bowl centre   : ({bowlCentre.X:F0}, {bowlCentre.Z:F0})");

            const float StartRadius = 40.0f;

            Span<Body> balls = stackalloc Body[8];
            for (int i = 0; i < balls.Length; i++)
            {
                float angle = i * MathF.Tau / balls.Length;

                balls[i] = world.CreateDynamicBody(new Vector3(
                    bowlCentre.X + (MathF.Cos(angle) * StartRadius),
                    30.0f,
                    bowlCentre.Z + (MathF.Sin(angle) * StartRadius)));

                balls[i].AddSphere(new Sphere(1.0f));
            }

            for (int step = 0; step < 600; step++)
            {
                world.Step(1.0f / 60.0f);
            }

            float meanDistance = 0.0f;
            foreach (Body ball in balls)
            {
                Vector3 offset = ball.Position - bowlCentre;
                meanDistance += new Vector3(offset.X, 0.0f, offset.Z).Length();
            }

            meanDistance /= balls.Length;

            Console.WriteLine($"   balls settled : {meanDistance:F1} m from the centre on average");

            // They started 40 m out. A bowl gathers them.
            SampleRunner.Expect(meanDistance < StartRadius, "the bowl gathered the balls inwards");

            foreach (Body ball in balls)
            {
                SampleRunner.Expect(ball.Position.Y > -1.0f, "no ball fell through the terrain");
            }
        }

        // World disposed above, terrain disposed here by the using declaration.
    }
}

/// <summary>
/// A triangle mesh built from raw geometry, with per-triangle materials.
/// </summary>
internal static class MeshSample
{
    public static void Run()
    {
        // A ramp: flat ground, then a slope up. Two quads, four triangles.
        Vector3[] vertices =
        [
            new Vector3(-10.0f, 0.0f, -10.0f),
            new Vector3(0.0f, 0.0f, -10.0f),
            new Vector3(0.0f, 0.0f, 10.0f),
            new Vector3(-10.0f, 0.0f, 10.0f),
            new Vector3(10.0f, 5.0f, -10.0f),
            new Vector3(10.0f, 5.0f, 10.0f),
        ];

        // Counter-clockwise seen from above, which is what makes the surface
        // solid from that side. Reverse it and bodies fall straight through.
        int[] indices =
        [
            0, 2, 1,
            0, 3, 2,
            1, 5, 4,
            1, 2, 5,
        ];

        // One material per triangle: the flat part is grippy, the ramp is ice.
        byte[] materials = [0, 0, 1, 1];

        using CollisionMesh ramp = CollisionMesh.FromTriangles(
            vertices,
            indices,
            MeshOptions.Default,
            materials);

        Console.WriteLine($"   triangles     : {ramp.TriangleCount}");
        Console.WriteLine($"   vertices      : {ramp.VertexCount}");
        Console.WriteLine($"   degenerate    : {ramp.DegenerateTriangleCount}");
        Console.WriteLine($"   bounds        : {ramp.Bounds.Size}");

        SampleRunner.Expect(ramp.TriangleCount == 4, "all four triangles survived the build");
        SampleRunner.Expect(ramp.DegenerateTriangleCount == 0, "none of them were degenerate");

        using (var world = new PhysicsWorld(WorldSettings.Default with
        {
            Gravity = new Vector3(0.0f, -10.0f, 0.0f),
        }))
        {
            Body level = world.CreateStaticBody();
            level.AddMesh(ramp);

            // Dropped on the flat part.
            Body crate = world.CreateDynamicBody(new Vector3(-5.0f, 3.0f, 0.0f));
            crate.AddBox(Box.Cube(0.5f));

            for (int step = 0; step < 300; step++)
            {
                world.Step(1.0f / 60.0f);
            }

            Console.WriteLine($"   crate rests at: y = {crate.Position.Y:F2}");

            SampleRunner.Expect(crate.Position.Y is > 0.3f and < 0.7f, "the crate rests on the mesh surface");
        }
    }
}
