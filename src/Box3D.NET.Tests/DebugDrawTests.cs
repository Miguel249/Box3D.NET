// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Numerics;
using Xunit;

namespace Box3D.Tests;

/// <summary>
/// Checks that debug draw reaches a managed drawer, carries usable data, and
/// costs nothing per frame.
/// </summary>
/// <remarks>
/// There is no renderer here and no picture to compare against, so these tests
/// assert the two things that can be established without one: that Box3D's
/// primitives arrive with sane values, and that getting them across the native
/// boundary allocates nothing. Whether the result looks right on screen is a
/// question for the samples.
/// </remarks>
[Collection(NativeCollection.Name)]
public class DebugDrawTests
{
    /// <summary>Counts what arrives, and remembers enough to check it.</summary>
    private struct CountingDrawer : IDebugDrawer
    {
        public int Segments;
        public int Points;
        public int Transforms;
        public int Spheres;
        public int Capsules;
        public int Bounds;
        public int Boxes;
        public int Strings;
        public int Shapes;

        public nint LastShapeHandle;

        public BoundingBox LastBounds;
        public Vector3 LastSegmentStart;
        public DebugColor LastColor;
        public bool SawNonFiniteValue;

        public readonly int Total => Segments + Points + Transforms + Spheres + Capsules + Bounds + Boxes + Strings + Shapes;

        public void DrawShape(nint handle, Vector3 position, Quaternion rotation, DebugColor color)
        {
            Shapes++;
            LastShapeHandle = handle;
            LastColor = color;
            Check(position);
        }

        public void DrawSegment(Vector3 p1, Vector3 p2, DebugColor color)
        {
            Segments++;
            LastSegmentStart = p1;
            LastColor = color;
            Check(p1);
            Check(p2);
        }

        public void DrawPoint(Vector3 position, float size, DebugColor color)
        {
            Points++;
            LastColor = color;
            Check(position);
        }

        public void DrawTransform(Vector3 position, Quaternion rotation)
        {
            Transforms++;
            Check(position);
        }

        public void DrawSphere(Vector3 center, float radius, DebugColor color, float alpha)
        {
            Spheres++;
            Check(center);
        }

        public void DrawCapsule(Vector3 p1, Vector3 p2, float radius, DebugColor color, float alpha)
        {
            Capsules++;
            Check(p1);
            Check(p2);
        }

        public void DrawBounds(BoundingBox bounds, DebugColor color)
        {
            Bounds++;
            LastBounds = bounds;
            LastColor = color;
            Check(bounds.Min);
            Check(bounds.Max);
        }

        public void DrawBox(Vector3 extents, Vector3 position, Quaternion rotation, DebugColor color)
        {
            Boxes++;
            Check(position);
        }

        public void DrawString(Vector3 position, ReadOnlySpan<byte> utf8Text, DebugColor color)
        {
            Strings++;
            Check(position);
        }

        private void Check(Vector3 v)
        {
            if (float.IsNaN(v.X) || float.IsInfinity(v.X) ||
                float.IsNaN(v.Y) || float.IsInfinity(v.Y) ||
                float.IsNaN(v.Z) || float.IsInfinity(v.Z))
            {
                SawNonFiniteValue = true;
            }
        }
    }

    private static PhysicsWorld BuildScene()
    {
        var world = new PhysicsWorld(WorldSettings.Default with
        {
            Gravity = new Vector3(0.0f, -10.0f, 0.0f),
        });

        Body ground = world.CreateStaticBody(new Vector3(0.0f, -0.5f, 0.0f));
        ground.AddBox(new Box(new Vector3(20.0f, 0.5f, 20.0f)));

        for (int i = 0; i < 8; i++)
        {
            Body body = world.CreateDynamicBody(new Vector3(i * 0.6f, 1.0f + (i * 0.5f), 0.0f));
            body.AddBox(Box.Cube(0.4f));
        }

        // Settle, so that there are real contacts and islands to draw.
        for (int step = 0; step < 60; step++)
        {
            world.Step(1.0f / 60.0f);
        }

        return world;
    }

    [NativeFact]
    public void Drawing_bounds_reaches_the_drawer()
    {
        using PhysicsWorld world = BuildScene();

        var drawer = new CountingDrawer();
        world.Draw(ref drawer, DebugDrawOptions.Default with { DrawBounds = true });

        // Nine bodies, so at least nine broad-phase boxes.
        Assert.True(drawer.Bounds >= 9, $"expected a bounding box per shape, got {drawer.Bounds}");
        Assert.False(drawer.SawNonFiniteValue, "a primitive arrived with a NaN or infinite coordinate");

        // The bounds must enclose something, not be the default-constructed box.
        Assert.True(drawer.LastBounds.Max.X >= drawer.LastBounds.Min.X);
        Assert.True(drawer.LastBounds.Max.Y >= drawer.LastBounds.Min.Y);
    }

    [NativeFact]
    public void Drawing_contacts_reaches_the_drawer()
    {
        using PhysicsWorld world = BuildScene();

        var drawer = new CountingDrawer();
        world.Draw(ref drawer, DebugDrawOptions.Default with
        {
            DrawContacts = true,
            DrawContactNormals = true,
        });

        // The stack has settled onto the ground, so contacts exist.
        Assert.True(drawer.Total > 0, "a settled stack drew no contact geometry at all");
        Assert.False(drawer.SawNonFiniteValue);
    }

    [NativeFact]
    public void Drawing_nothing_calls_nothing()
    {
        // Every option off must be genuinely silent, so that a drawer is never
        // paying for geometry it did not ask for.
        using PhysicsWorld world = BuildScene();

        var drawer = new CountingDrawer();
        world.Draw(ref drawer, DebugDrawOptions.Default);

        Assert.Equal(0, drawer.Total);
    }

    [NativeFact]
    public void The_drawer_sees_its_own_mutations()
    {
        // The drawer is copied to the stack across the native call, so the
        // copy-back has to work or every accumulating drawer silently loses
        // everything it collected.
        using PhysicsWorld world = BuildScene();

        var drawer = new CountingDrawer();
        world.Draw(ref drawer, DebugDrawOptions.Default with { DrawBounds = true });

        Assert.True(drawer.Bounds > 0, "the drawer's mutations did not survive the call");
    }

    [NativeFact]
    public void The_bounds_option_culls()
    {
        using PhysicsWorld world = BuildScene();

        var everything = new CountingDrawer();
        world.Draw(ref everything, DebugDrawOptions.Default with { DrawBounds = true });

        var nowhere = new CountingDrawer();
        world.Draw(ref nowhere, DebugDrawOptions.Default with
        {
            DrawBounds = true,
            // A region the scene is nowhere near.
            Bounds = new BoundingBox(new Vector3(1000.0f), new Vector3(1001.0f)),
        });

        Assert.True(everything.Bounds > 0);
        Assert.True(
            nowhere.Bounds < everything.Bounds,
            $"culling to a distant region drew as much as drawing everything ({nowhere.Bounds} vs {everything.Bounds})");
    }

    [NativeFact]
    public void Colours_carry_channels_and_material()
    {
        using PhysicsWorld world = BuildScene();

        var drawer = new CountingDrawer();
        world.Draw(ref drawer, DebugDrawOptions.Default with { DrawBounds = true });

        // Box3D draws bounding boxes in gold, so the colour must be a real one
        // rather than a zeroed struct.
        Assert.True(drawer.LastColor.Packed != 0, "every primitive arrived with colour 0");

        (float r, float g, float b) = drawer.LastColor.ToUnitRgb();
        Assert.InRange(r, 0.0f, 1.0f);
        Assert.InRange(g, 0.0f, 1.0f);
        Assert.InRange(b, 0.0f, 1.0f);
    }

    [NativeFact]
    public void Drawing_a_frame_allocates_nothing()
    {
        using PhysicsWorld world = BuildScene();

        var options = DebugDrawOptions.Default with
        {
            DrawBounds = true,
            DrawContacts = true,
            DrawContactNormals = true,
            DrawJoints = true,
            DrawMass = true,
        };

        var drawer = new CountingDrawer();

        // Warm up, so that JIT and any first-call setup are not measured.
        for (int i = 0; i < 3; i++)
        {
            world.Draw(ref drawer, options);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();

        for (int frame = 0; frame < 20; frame++)
        {
            world.Draw(ref drawer, options);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(
            allocated == 0,
            $"drawing 20 frames allocated {allocated} bytes; debug draw is supposed to cost nothing per frame");
    }

    [Fact]
    public void Drawing_a_disposed_world_throws()
    {
        var world = new PhysicsWorld();
        world.Dispose();

        var drawer = new CountingDrawer();

        // The lambda captures by reference, which a local function makes legal.
        Assert.Throws<ObjectDisposedException>(Draw);

        void Draw() => world.Draw(ref drawer, DebugDrawOptions.Default);
    }

    /// <summary>Hands out numbered handles and records what it was asked to build.</summary>
    private sealed class RecordingFactory : IDebugShapeFactory
    {
        private nint _next = 1;

        public int Created { get; private set; }

        public int Destroyed { get; private set; }

        public List<ShapeType> Types { get; } = new();

        public List<float> SphereRadii { get; } = new();

        public nint CreateShape(in DebugShape shape)
        {
            Created++;
            Types.Add(shape.Type);

            if (shape.TryGetSphere(out Sphere sphere))
            {
                SphereRadii.Add(sphere.Radius);
            }

            return _next++;
        }

        public void DestroyShape(nint handle) => Destroyed++;
    }

    [NativeFact]
    public void A_shape_factory_is_asked_to_build_a_drawable_per_shape()
    {
        var factory = new RecordingFactory();

        using (var world = new PhysicsWorld(WorldSettings.Default, factory))
        {
            Body ground = world.CreateStaticBody(new Vector3(0.0f, -0.5f, 0.0f));
            ground.AddBox(new Box(new Vector3(10.0f, 0.5f, 10.0f)));

            Body ball = world.CreateDynamicBody(new Vector3(0.0f, 3.0f, 0.0f));
            ball.AddSphere(new Sphere(0.75f));

            world.Step(1.0f / 60.0f);

            // Nothing is built until something actually draws.
            Assert.Equal(0, factory.Created);

            var drawer = new CountingDrawer();
            world.Draw(ref drawer, DebugDrawOptions.Default with { DrawShapes = true });

            Assert.Equal(2, factory.Created);
            Assert.Equal(2, drawer.Shapes);
            Assert.NotEqual(0, drawer.LastShapeHandle);

            // Built once, not once per frame: that is the entire reason Box3D
            // works this way rather than emitting geometry.
            world.Draw(ref drawer, DebugDrawOptions.Default with { DrawShapes = true });
            Assert.Equal(2, factory.Created);
            Assert.Equal(4, drawer.Shapes);
        }

        // Disposing the world must release every drawable it asked for.
        Assert.Equal(factory.Created, factory.Destroyed);
    }

    [NativeFact]
    public void A_shape_factory_sees_the_geometry_it_is_building_for()
    {
        var factory = new RecordingFactory();

        using var world = new PhysicsWorld(WorldSettings.Default, factory);

        Body ball = world.CreateDynamicBody(new Vector3(0.0f, 3.0f, 0.0f));
        ball.AddSphere(new Sphere(0.75f));

        var drawer = new CountingDrawer();
        world.Draw(ref drawer, DebugDrawOptions.Default with { DrawShapes = true });

        Assert.Contains(ShapeType.Sphere, factory.Types);
        Assert.Contains(0.75f, factory.SphereRadii);
    }

    [NativeFact]
    public void Drawing_shapes_without_a_factory_draws_nothing()
    {
        // The flag alone cannot work: with no factory there is no drawable to
        // hand back. This must be silent rather than a crash.
        using PhysicsWorld world = BuildScene();

        var drawer = new CountingDrawer();
        world.Draw(ref drawer, DebugDrawOptions.Default with { DrawShapes = true });

        Assert.Equal(0, drawer.Shapes);
    }

    [NativeFact]
    public void A_destroyed_shape_releases_its_drawable()
    {
        var factory = new RecordingFactory();

        using var world = new PhysicsWorld(WorldSettings.Default, factory);

        Body body = world.CreateDynamicBody(new Vector3(0.0f, 3.0f, 0.0f));
        Shape shape = body.AddSphere(new Sphere(0.5f));

        var drawer = new CountingDrawer();
        world.Draw(ref drawer, DebugDrawOptions.Default with { DrawShapes = true });
        Assert.Equal(1, factory.Created);

        shape.Destroy();

        Assert.Equal(1, factory.Destroyed);
    }

    [Fact]
    public void A_colour_round_trips_through_its_channels()
    {
        var color = new DebugColor(0x12, 0x34, 0x56);

        Assert.Equal(0x12, color.R);
        Assert.Equal(0x34, color.G);
        Assert.Equal(0x56, color.B);
        Assert.Equal(DebugMaterial.Default, color.Material);
        Assert.Equal("#123456", color.ToString());
    }

    [Fact]
    public void A_colour_exposes_the_material_in_its_high_byte()
    {
        var color = new DebugColor(((uint)DebugMaterial.Metallic << 24) | 0xFF0000);

        Assert.Equal(DebugMaterial.Metallic, color.Material);
        Assert.Equal(0xFF, color.R);
        Assert.Equal(0x00, color.G);
    }
}
