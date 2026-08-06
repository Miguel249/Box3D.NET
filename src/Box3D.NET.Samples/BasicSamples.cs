// SPDX-License-Identifier: MIT

using System;
using System.Numerics;

namespace Box3D.Samples;

/// <summary>
/// The smallest thing that is still a simulation: create a world, step it, and
/// throw it away.
/// </summary>
internal static class BasicWorldSample
{
    public static void Run()
    {
        // The world owns native memory, so it is disposable. Nothing else in the
        // library is: bodies and shapes are handles that die with their world.
        using var world = new PhysicsWorld(WorldSettings.Default with
        {
            Gravity = new Vector3(0.0f, -9.81f, 0.0f),
        });

        // A fixed time step. Feeding a variable one makes the simulation
        // irreproducible and hurts stability, so decouple it from the frame rate.
        const float TimeStep = 1.0f / 60.0f;

        for (int i = 0; i < 60; i++)
        {
            world.Step(TimeStep);
        }

        Console.WriteLine($"   gravity      : {world.Gravity}");
        Console.WriteLine($"   workers      : {world.WorkerCount}");
        Console.WriteLine($"   awake bodies : {world.AwakeBodyCount}");

        SampleRunner.Expect(world.AwakeBodyCount == 0, "an empty world has nothing awake");
    }
}

/// <summary>
/// A body falling under gravity, and the difference between forces and impulses.
/// </summary>
internal static class DynamicBodySample
{
    public static void Run()
    {
        using var world = new PhysicsWorld(WorldSettings.Default with
        {
            Gravity = new Vector3(0.0f, -10.0f, 0.0f),
        });

        // Create bodies where they belong. Creating at the origin and moving
        // afterwards costs close to twice as much.
        Body ball = world.CreateBody(BodyDefinition.Dynamic(new Vector3(0.0f, 50.0f, 0.0f)));

        // A body with no shapes has no mass and no geometry.
        SampleRunner.Expect(ball.Mass == 0.0f, "a body without shapes has no mass");

        ball.AddSphere(new Sphere(0.5f), ShapeDefinition.Default with { Density = 1000.0f });

        SampleRunner.Expect(ball.Mass > 0.0f, "attaching a shape gives the body mass");

        Vector3 start = ball.Position;

        for (int i = 0; i < 60; i++)
        {
            world.Step(1.0f / 60.0f);
        }

        Vector3 afterOneSecond = ball.Position;
        float fallen = start.Y - afterOneSecond.Y;

        Console.WriteLine($"   mass         : {ball.Mass:F1} kg");
        Console.WriteLine($"   fell         : {fallen:F2} m in one second");
        Console.WriteLine($"   velocity     : {ball.LinearVelocity.Y:F2} m/s");

        // Freefall for one second under 10 m/s^2 covers about five metres.
        SampleRunner.Expect(fallen is > 4.0f and < 6.0f, "one second of freefall covers about five metres");

        // An impulse changes velocity immediately; a force accumulates over the
        // step. Use an impulse for a jump, a force for thrust.
        ball.LinearVelocity = Vector3.Zero;
        ball.ApplyImpulseToCenter(new Vector3(0.0f, ball.Mass * 10.0f, 0.0f));

        Console.WriteLine($"   after impulse: {ball.LinearVelocity.Y:F2} m/s");

        SampleRunner.Expect(ball.LinearVelocity.Y > 9.0f, "an impulse of m*v gives a velocity of v");
    }
}

/// <summary>
/// A dynamic body coming to rest on static geometry, and going to sleep.
/// </summary>
internal static class CollisionSample
{
    public static void Run()
    {
        using var world = new PhysicsWorld(WorldSettings.Default with
        {
            Gravity = new Vector3(0.0f, -10.0f, 0.0f),
        });

        // Static geometry: infinite mass, never moves, and does not collide with
        // other static bodies. The slab is centred half a metre below the origin
        // so its top face lies exactly at y = 0.
        Body ground = world.CreateBody(BodyDefinition.Static(new Vector3(0.0f, -0.5f, 0.0f)));
        ground.AddBox(new Box(new Vector3(50.0f, 0.5f, 50.0f)));

        Body crate = world.CreateBody(BodyDefinition.Dynamic(new Vector3(0.0f, 5.0f, 0.0f)));
        crate.AddBox(Box.Cube(0.5f), ShapeDefinition.Default with
        {
            Material = PhysicsMaterial.Default with { Friction = 0.6f },
        });

        int stepsUntilAsleep = -1;

        for (int i = 0; i < 300; i++)
        {
            world.Step(1.0f / 60.0f);

            if (stepsUntilAsleep < 0 && !crate.IsAwake)
            {
                stepsUntilAsleep = i;
            }
        }

        Console.WriteLine($"   resting at   : y = {crate.Position.Y:F3}");
        Console.WriteLine($"   asleep after : {stepsUntilAsleep} steps");

        // A half-metre cube resting on the ground has its centre half a metre up.
        SampleRunner.Expect(crate.Position.Y is > 0.4f and < 0.6f, "the crate rests on the ground");

        // Sleeping is what makes a settled scene nearly free to simulate.
        SampleRunner.Expect(stepsUntilAsleep > 0, "a settled body goes to sleep");
        SampleRunner.Expect(world.AwakeBodyCount == 0, "nothing is left awake");
    }
}

/// <summary>
/// Ray casts: the closest hit, and a custom callback that ignores a body.
/// </summary>
internal static class RaycastSample
{
    // A callback is a struct, so the query is specialized for it and inlined:
    // no delegate is allocated and nothing is boxed.
    private struct CountEverything : IRaycastCallback
    {
        public int Count;

        public RaycastAction OnHit(in RaycastHit hit)
        {
            Count++;

            // Carry on past this shape so that every shape along the ray is seen.
            return RaycastAction.Continue;
        }
    }

    private struct NearestExcluding : IRaycastCallback
    {
        public Body Excluded;
        public RaycastHit Nearest;

        public RaycastAction OnHit(in RaycastHit hit)
        {
            if (hit.Shape.Body == Excluded)
            {
                // Behave as though the shape were not there at all.
                return RaycastAction.Ignore;
            }

            Nearest = hit;

            // Shorten the ray so only closer shapes are reported from here on,
            // which turns this into a nearest-hit search.
            return RaycastAction.ClipTo(hit.Fraction);
        }
    }

    public static void Run()
    {
        using var world = new PhysicsWorld();

        // Three spheres in a row along x, at 5, 10 and 15 metres.
        Body near = CreateSphere(world, 5.0f);
        _ = CreateSphere(world, 10.0f);
        _ = CreateSphere(world, 15.0f);

        // Shapes enter the broad phase on the next step.
        world.Step(1.0f / 60.0f);

        Vector3 origin = Vector3.Zero;
        Vector3 ray = new(20.0f, 0.0f, 0.0f);

        RaycastHit closest = world.RaycastClosest(origin, ray);

        Console.WriteLine($"   closest hit  : x = {closest.Point.X:F2}, fraction {closest.Fraction:F3}");
        Console.WriteLine($"   normal       : {closest.Normal}");

        SampleRunner.Expect(closest.Hit, "the ray hits the first sphere");
        SampleRunner.Expect(MathF.Abs(closest.Point.X - 4.0f) < 0.1f, "the near surface of the first sphere is at x = 4");

        var all = new CountEverything();
        world.Raycast(origin, ray, ref all);

        Console.WriteLine($"   shapes along : {all.Count}");

        SampleRunner.Expect(all.Count == 3, "continuing past each hit finds all three spheres");

        var excluding = new NearestExcluding { Excluded = near };
        world.Raycast(origin, ray, ref excluding);

        Console.WriteLine($"   ignoring the first: x = {excluding.Nearest.Point.X:F2}");

        SampleRunner.Expect(
            MathF.Abs(excluding.Nearest.Point.X - 9.0f) < 0.1f,
            "skipping the first sphere leaves the second");

        // A ray that misses everything.
        RaycastHit miss = world.RaycastClosest(new Vector3(0.0f, 100.0f, 0.0f), ray);
        SampleRunner.Expect(!miss.Hit, "a ray above the scene hits nothing");
    }

    private static Body CreateSphere(PhysicsWorld world, float x)
    {
        Body body = world.CreateBody(BodyDefinition.Static(new Vector3(x, 0.0f, 0.0f)));
        body.AddSphere(new Sphere(1.0f));
        return body;
    }
}
