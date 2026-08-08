// SPDX-License-Identifier: MIT

using System;
using System.Numerics;

namespace Box3D.Samples;

/// <summary>
/// Reading collision events after a step, rather than being called back during one.
/// </summary>
internal static class ContactEventsSample
{
    public static void Run()
    {
        using var world = new PhysicsWorld(WorldSettings.Default with
        {
            Gravity = new Vector3(0.0f, -10.0f, 0.0f),

            // Only collisions approaching faster than this raise a hit event.
            HitEventThreshold = 1.0f,
        });

        Body ground = world.CreateBody(BodyDefinition.Static(new Vector3(0.0f, -0.5f, 0.0f)));

        // Events are opt-in per shape, because collecting them is not free.
        var reporting = ShapeDefinition.Default with
        {
            EnableContactEvents = true,
            EnableHitEvents = true,
        };

        ground.AddBox(new Box(new Vector3(50.0f, 0.5f, 50.0f)), reporting);

        Body ball = world.CreateBody(BodyDefinition.Dynamic(new Vector3(0.0f, 5.0f, 0.0f)));
        ball.AddSphere(new Sphere(0.5f), reporting with
        {
            Material = PhysicsMaterial.Default with { Restitution = 0.6f },
        });

        int begins = 0;
        int ends = 0;
        float hardestHit = 0.0f;

        for (int i = 0; i < 240; i++)
        {
            world.Step(1.0f / 60.0f);

            // Box3D buffers events during the step and hands them back
            // afterwards, because the solver is multithreaded and because
            // applications usually want to change the world in response.
            WorldEvents events = world.Events;

            begins += events.ContactBegins.Count;
            ends += events.ContactEnds.Count;

            foreach (ContactHitEvent hit in events.ContactHits)
            {
                hardestHit = MathF.Max(hardestHit, hit.ApproachSpeed);
            }
        }

        Console.WriteLine($"   begin touch  : {begins}");
        Console.WriteLine($"   end touch    : {ends}");
        Console.WriteLine($"   hardest hit  : {hardestHit:F2} m/s");

        SampleRunner.Expect(begins > 0, "a falling ball touches the ground");
        SampleRunner.Expect(ends > 0, "a bouncing ball leaves it again");
        SampleRunner.Expect(hardestHit > 1.0f, "the first impact is above the hit threshold");
    }
}

/// <summary>
/// A sensor: geometry that reports overlaps without pushing anything.
/// </summary>
internal static class SensorSample
{
    public static void Run()
    {
        using var world = new PhysicsWorld(WorldSettings.Default with
        {
            Gravity = new Vector3(0.0f, -10.0f, 0.0f),
        });

        // A trigger volume. Density is zero because a sensor still contributes
        // mass to its body if it has any, which is rarely what a trigger wants.
        Body trigger = world.CreateBody(BodyDefinition.Static(new Vector3(0.0f, 5.0f, 0.0f)));
        trigger.AddBox(new Box(new Vector3(2.0f, 0.5f, 2.0f)), ShapeDefinition.Default with
        {
            IsSensor = true,
            EnableSensorEvents = true,
            Density = 0.0f,
        });

        // The visitor needs sensor events enabled too.
        Body ball = world.CreateBody(BodyDefinition.Dynamic(new Vector3(0.0f, 15.0f, 0.0f)));
        ball.AddSphere(new Sphere(0.5f), ShapeDefinition.Default with
        {
            EnableSensorEvents = true,
        });

        int entered = 0;
        int left = 0;

        for (int i = 0; i < 180; i++)
        {
            world.Step(1.0f / 60.0f);

            WorldEvents events = world.Events;
            entered += events.SensorBegins.Count;
            left += events.SensorEnds.Count;
        }

        Console.WriteLine($"   entered      : {entered}");
        Console.WriteLine($"   left         : {left}");
        Console.WriteLine($"   ball ended at: y = {ball.Position.Y:F2}");

        SampleRunner.Expect(entered == 1, "the ball passes into the trigger once");
        SampleRunner.Expect(left == 1, "and out the other side");

        // The decisive part: a sensor never pushes back, so the ball keeps falling.
        SampleRunner.Expect(ball.Position.Y < 0.0f, "a sensor does not stop anything");
    }
}

/// <summary>
/// The two kinds of compound: several shapes on one body, and many children
/// baked into one shape.
/// </summary>
/// <remarks>
/// They are not alternatives. A run-time compound works on any body type and can
/// change while the world runs; a baked one is static, fixed, and a single
/// broad-phase proxy no matter how many pieces it holds.
/// </remarks>
internal static class CompoundShapeSample
{
    public static void Run()
    {
        using var world = new PhysicsWorld(WorldSettings.Default with
        {
            Gravity = new Vector3(0.0f, -10.0f, 0.0f),
        });

        Body ground = world.CreateBody(BodyDefinition.Static(new Vector3(0.0f, -0.5f, 0.0f)));
        ground.AddBox(new Box(new Vector3(50.0f, 0.5f, 50.0f)));

        // A dumbbell: a bar with a weight at each end. Attaching several shapes
        // to one body is all a run-time compound is; the baked compound type is
        // for large static geometry.
        Body dumbbell = world.CreateBody(BodyDefinition.Dynamic(new Vector3(0.0f, 5.0f, 0.0f)));

        // Each attachment moves the centre of mass, so the mass update is
        // deferred and done once at the end.
        var deferred = ShapeDefinition.Default with { UpdateBodyMass = false };

        dumbbell.AddBox(new Box(new Vector3(1.0f, 0.1f, 0.1f)), deferred);
        dumbbell.AddSphere(new Sphere(new Vector3(-1.0f, 0.0f, 0.0f), 0.4f), deferred);
        dumbbell.AddSphere(new Sphere(new Vector3(1.0f, 0.0f, 0.0f), 0.4f), deferred);

        dumbbell.RecomputeMass();

        Console.WriteLine($"   shapes       : {dumbbell.ShapeCount}");
        Console.WriteLine($"   mass         : {dumbbell.Mass:F1} kg");
        Console.WriteLine($"   local centre : {dumbbell.LocalCenterOfMass}");

        SampleRunner.Expect(dumbbell.ShapeCount == 3, "all three shapes are attached");
        SampleRunner.Expect(dumbbell.Mass > 0.0f, "the deferred mass update ran");

        // The weights are symmetric, so the centre of mass stays at the origin.
        SampleRunner.Expect(
            MathF.Abs(dumbbell.LocalCenterOfMass.X) < 0.01f,
            "a symmetric compound has a centred mass");

        // Walking the shapes without allocating.
        Span<Shape> shapes = stackalloc Shape[dumbbell.ShapeCount];
        int count = dumbbell.GetShapes(shapes);

        foreach (Shape shape in shapes[..count])
        {
            shape.Friction = 0.8f;
        }

        SampleRunner.Expect(count == 3, "the shape handles come back");

        for (int i = 0; i < 180; i++)
        {
            world.Step(1.0f / 60.0f);
        }

        Console.WriteLine($"   resting at   : y = {dumbbell.Position.Y:F3}");

        SampleRunner.Expect(dumbbell.Position.Y > 0.0f, "the dumbbell rests on the ground");

        BakedCompound(world);
    }

    /// <summary>The other kind: many children, one shape, static only.</summary>
    /// <param name="world">The world to add the scenery to.</param>
    private static void BakedCompound(PhysicsWorld world)
    {
        // A row of pillars. Built once and shared, so Box3D keeps one copy of
        // the hull and points every instance at it.
        using var pillar = ConvexHull.Cylinder(height: 2.0f, radius: 0.3f);

        var builder = new CompoundBuilder();
        for (int i = 0; i < 8; i++)
        {
            builder.AddHull(pillar, new Vector3((i * 1.5f) - 5.25f, 0.0f, 6.0f));
        }

        using CompoundGeometry colonnade = builder.Build();

        // Everything was cloned into the compound, so the hull could be released
        // right here. The compound itself is borrowed by its shape and must
        // outlive it, which is why it is not disposed until the world is gone.
        Body scenery = world.CreateBody(BodyDefinition.Static());
        Shape shape = scenery.AddCompound(colonnade);

        Console.WriteLine($"   children     : {colonnade.ChildCount} in {colonnade.ByteCount} bytes");
        Console.WriteLine($"   shapes       : {scenery.ShapeCount}");

        SampleRunner.Expect(colonnade.ChildCount == 8, "all eight pillars are in the compound");
        SampleRunner.Expect(scenery.ShapeCount == 1, "eight children arrive as one shape");
        SampleRunner.Expect(shape.Type == ShapeType.Compound, "and it is a compound shape");

        // Dropped onto a pillar rather than beside it, to show the children are
        // what the broad-phase box resolves to.
        Body crate = world.CreateBody(BodyDefinition.Dynamic(new Vector3(-5.25f, 6.0f, 6.0f)));
        crate.AddBox(Box.Cube(0.25f));

        for (int i = 0; i < 240; i++)
        {
            world.Step(1.0f / 60.0f);
        }

        Console.WriteLine($"   crate at     : y = {crate.Position.Y:F3}");

        SampleRunner.Expect(crate.Position.Y > 1.5f, "the crate landed on top of a pillar");
    }
}

/// <summary>
/// Continuous collision: what stops a fast body from passing through a thin wall.
/// </summary>
internal static class ContinuousCollisionSample
{
    public static void Run()
    {
        // A thin wall and a very fast body. At 400 m/s the projectile covers more
        // than six metres per step, so a solver that only tested the shapes where
        // they end up would find them on opposite sides of a wall a tenth of a
        // metre thick and report no collision at all.
        const float Speed = 400.0f;

        Console.WriteLine($"   speed        : {Speed} m/s, wall 0.1 m thick");
        Console.WriteLine($"   travel/step  : {Speed / 60.0f:F2} m");

        float swept = RunTrial(continuousCollision: true);
        float tunnelled = RunTrial(continuousCollision: false);

        Console.WriteLine($"   continuous on : stopped at x = {swept:F2}");
        Console.WriteLine($"   continuous off: ended at   x = {tunnelled:F2}");

        SampleRunner.Expect(swept < 5.0f, "continuous collision stops the projectile at the wall");
        SampleRunner.Expect(tunnelled > 5.0f, "without it the projectile passes straight through");

        // Worth being precise about what the IsBullet flag does, because the
        // name suggests it is what stopped the projectile above, and it is not.
        //
        // A fast dynamic body is always swept against *static* geometry, which
        // is the case here. IsBullet additionally sweeps it against dynamic and
        // kinematic bodies, and is the expensive option: bullets are swept after
        // everything else has moved, and are not swept against each other. Use
        // it for pinball tables and dynamic containers, not for gunfire - for a
        // projectile that must not miss, cast a ray along its path instead.
        Console.WriteLine("   note          : static geometry is swept regardless; IsBullet adds dynamic targets");
    }

    private static float RunTrial(bool continuousCollision)
    {
        using var world = new PhysicsWorld(WorldSettings.Default with
        {
            Gravity = Vector3.Zero,
            EnableContinuous = continuousCollision,
        });

        Body wall = world.CreateBody(BodyDefinition.Static(new Vector3(5.0f, 0.0f, 0.0f)));
        wall.AddBox(new Box(new Vector3(0.05f, 10.0f, 10.0f)));

        Body projectile = world.CreateBody(BodyDefinition.Dynamic(Vector3.Zero) with
        {
            LinearVelocity = new Vector3(400.0f, 0.0f, 0.0f),
        });
        projectile.AddSphere(new Sphere(0.1f));

        for (int i = 0; i < 30; i++)
        {
            world.Step(1.0f / 60.0f);
        }

        return projectile.Position.X;
    }
}
