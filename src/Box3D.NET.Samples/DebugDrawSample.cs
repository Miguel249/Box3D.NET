// SPDX-License-Identifier: MIT

using System;
using System.Numerics;

namespace Box3D.Samples;

/// <summary>
/// Feeds a world's debug geometry to a drawer, which is how an application
/// gets Box3D's internal state onto a screen.
/// </summary>
/// <remarks>
/// <para>
/// The drawer here counts primitives instead of rendering them, because the
/// samples run headless. A real one would push vertices into whatever renderer
/// the application already has: Box3D.NET emits points, segments and boxes in
/// world space and has no opinion about how they are drawn.
/// </para>
/// <para>
/// Running this under NativeAOT is the point of having it here. Debug draw
/// reaches managed code through function pointers and
/// <c>[UnmanagedCallersOnly]</c> thunks, which is exactly the kind of interop
/// an AOT compiler can get wrong, so CI publishing and running this proves the
/// path works without a JIT.
/// </para>
/// </remarks>
internal static class DebugDrawSample
{
    /// <summary>A drawer that tallies what the engine emits.</summary>
    /// <remarks>
    /// A struct, and taken by <c>ref</c>, so that the whole frame costs no
    /// allocation: no delegate is created, nothing is boxed, and the interface
    /// calls are devirtualised.
    /// </remarks>
    private struct TallyDrawer : IDebugDrawer
    {
        public int Segments;
        public int Points;
        public int Bounds;
        public int Boxes;
        public int Shapes;
        public int Other;

        public Vector3 LowestPoint;

        public readonly int Total => Segments + Points + Bounds + Boxes + Shapes + Other;

        public void DrawShape(nint handle, Vector3 position, Quaternion rotation, DebugColor color) => Shapes++;

        public void DrawSegment(Vector3 p1, Vector3 p2, DebugColor color) => Segments++;

        public void DrawPoint(Vector3 position, float size, DebugColor color)
        {
            Points++;
            if (position.Y < LowestPoint.Y)
            {
                LowestPoint = position;
            }
        }

        public void DrawBounds(BoundingBox bounds, DebugColor color) => Bounds++;

        public void DrawBox(Vector3 extents, Vector3 position, Quaternion rotation, DebugColor color) => Boxes++;

        public void DrawTransform(Vector3 position, Quaternion rotation) => Other++;

        public void DrawSphere(Vector3 center, float radius, DebugColor color, float alpha) => Other++;

        public void DrawCapsule(Vector3 p1, Vector3 p2, float radius, DebugColor color, float alpha) => Other++;

        public void DrawString(Vector3 position, ReadOnlySpan<byte> utf8Text, DebugColor color) => Other++;
    }

    /// <summary>
    /// Stands in for the part of a renderer that turns a shape into something
    /// drawable, once.
    /// </summary>
    /// <remarks>
    /// A real implementation would build a mesh here and return whatever
    /// identifies it — a buffer id, an index into an array, a handle. Box3D
    /// never looks inside the value; it only hands it back at draw time.
    /// </remarks>
    private sealed class MeshFactory : IDebugShapeFactory
    {
        private nint _next = 1;

        public int Built { get; private set; }

        public int Released { get; private set; }

        public nint CreateShape(in DebugShape shape)
        {
            Built++;

            // A renderer would branch on the type here to pick a tessellation.
            if (shape.TryGetSphere(out Sphere sphere))
            {
                Console.WriteLine($"   built a drawable for a sphere of radius {sphere.Radius:F2}");
            }
            else
            {
                Console.WriteLine($"   built a drawable for a {shape.Type} shape");
            }

            return _next++;
        }

        public void DestroyShape(nint handle) => Released++;
    }

    public static void Run()
    {
        var factory = new MeshFactory();

        // The factory goes in at construction because Box3D takes these
        // callbacks in the world definition, not at draw time.
        using var world = new PhysicsWorld(
            WorldSettings.Default with { Gravity = new Vector3(0.0f, -9.81f, 0.0f) },
            factory);

        Body ground = world.CreateStaticBody(new Vector3(0.0f, -0.5f, 0.0f));
        ground.AddBox(new Box(new Vector3(20.0f, 0.5f, 20.0f)));

        for (int i = 0; i < 6; i++)
        {
            Body box = world.CreateDynamicBody(new Vector3(0.0f, 1.0f + (i * 0.9f), 0.0f));
            box.AddBox(Box.Cube(0.4f));
        }

        Body ball = world.CreateDynamicBody(new Vector3(0.3f, 8.0f, 0.0f));
        ball.AddSphere(new Sphere(0.5f));

        // Let the stack settle so there are real contacts to look at.
        for (int step = 0; step < 90; step++)
        {
            world.Step(1.0f / 60.0f);
        }

        // Shapes first, which is what the factory is for. The drawables are
        // built on this call and reused by every later one.
        var shapes = new TallyDrawer { LowestPoint = new Vector3(0.0f, float.MaxValue, 0.0f) };
        world.Draw(ref shapes, DebugDrawOptions.Default with { DrawShapes = true });

        Console.WriteLine($"shapes drawn: {shapes.Shapes}, drawables built: {factory.Built}");
        SampleRunner.Expect(shapes.Shapes == 8, "expected the ground, six boxes and the ball to be drawn");

        int builtAfterFirstFrame = factory.Built;
        world.Draw(ref shapes, DebugDrawOptions.Default with { DrawShapes = true });

        Console.WriteLine($"drawables built after a second frame: {factory.Built}");
        SampleRunner.Expect(
            factory.Built == builtAfterFirstFrame,
            "drawables must be built once and reused, not rebuilt every frame");

        // Ask for the broad-phase boxes: one per shape, which makes the count
        // something the sample can check rather than merely print.
        var bounds = new TallyDrawer { LowestPoint = new Vector3(0.0f, float.MaxValue, 0.0f) };
        world.Draw(ref bounds, DebugDrawOptions.Default with { DrawBounds = true });

        Console.WriteLine($"broad-phase boxes drawn: {bounds.Bounds}");
        SampleRunner.Expect(bounds.Bounds >= 8, "expected a bounding box for the ground, each of the six boxes and the ball");

        // Contacts and their normals, which is what debug draw is usually for.
        var contacts = new TallyDrawer { LowestPoint = new Vector3(0.0f, float.MaxValue, 0.0f) };
        world.Draw(ref contacts, DebugDrawOptions.Default with
        {
            DrawContacts = true,
            DrawContactNormals = true,
        });

        Console.WriteLine($"contact primitives drawn: {contacts.Total} ({contacts.Points} points, {contacts.Segments} segments)");
        SampleRunner.Expect(contacts.Total > 0, "a settled stack should produce contact geometry");

        // Everything off draws nothing, so a renderer never pays for geometry
        // it did not ask for.
        var silent = new TallyDrawer();
        world.Draw(ref silent, DebugDrawOptions.Default);

        Console.WriteLine($"primitives with every option off: {silent.Total}");
        SampleRunner.Expect(silent.Total == 0, "the default options should draw nothing at all");

        // Drawing is not free of work, but it is free of allocation. This is
        // the property that makes it usable every frame.
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int frame = 0; frame < 10; frame++)
        {
            world.Draw(ref bounds, DebugDrawOptions.Default with { DrawBounds = true });
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Console.WriteLine($"managed bytes allocated over 10 drawn frames: {allocated}");
        SampleRunner.Expect(allocated == 0, "debug draw must not allocate per frame");
    }
}
