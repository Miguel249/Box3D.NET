// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using Box3D.Native;
using Xunit;

namespace Box3D.Tests;

/// <summary>
/// High volume and long-running scenarios, and the leak checks that go with them.
/// </summary>
/// <remarks>
/// <para>
/// Every leak check reads <c>b3GetByteCount</c>, which is Box3D's own count of
/// live allocations, before and after. It is exact rather than a heuristic: the
/// number must return to where it started, not merely stay near it.
/// </para>
/// <para>
/// That only holds if nothing else is allocating at the same time, which is why
/// everything touching the native library shares a non-parallel collection. See
/// <see cref="NativeCollection"/>.
/// </para>
/// </remarks>
[Collection(NativeCollection.Name)]
public class StressTests
{
    /// <summary>
    /// Runs an operation and asserts that Box3D's live byte count comes back to
    /// where it started.
    /// </summary>
    /// <param name="what">What the operation is, for the failure message.</param>
    /// <param name="operation">The operation, which must release everything it takes.</param>
    private static void AssertNoLeak(string what, Action operation)
    {
        // One warm-up pass first. The first world of a process, and the first
        // use of some internal pools, allocate structures that are then reused,
        // so measuring from cold would report that as a leak.
        operation();

        int before = B3.b3GetByteCount();
        operation();
        int after = B3.b3GetByteCount();

        Assert.True(
            before == after,
            $"{what} leaked {after - before} bytes ({before} before, {after} after)");
    }

    // -------------------------------------------------------------- volume

    [NativeFact]
    public void A_thousand_bodies_simulate_and_settle()
    {
        using var world = new PhysicsWorld(WorldSettings.Default with
        {
            Gravity = new Vector3(0.0f, -10.0f, 0.0f),
        });

        Body ground = world.CreateStaticBody(new Vector3(0.0f, -0.5f, 0.0f));
        ground.AddBox(new Box(new Vector3(100.0f, 0.5f, 100.0f)));

        const int Count = 1000;
        const int Side = 32;

        var bodies = new Body[Count];

        // Spread across the ground rather than stacked into a tower. A
        // forty-level stack is a solver torture test that never fully settles,
        // which measures something different from what this test is for.
        for (int i = 0; i < Count; i++)
        {
            bodies[i] = world.CreateDynamicBody(new Vector3(
                ((i % Side) * 1.5f) - (Side * 0.75f),
                1.0f,
                ((i / Side) * 1.5f) - (Side * 0.75f)));

            bodies[i].AddBox(Box.Cube(0.5f));
        }

        for (int step = 0; step < 400; step++)
        {
            world.Step(1.0f / 60.0f);
        }

        foreach (Body body in bodies)
        {
            Assert.False(float.IsNaN(body.Position.Y));
            Assert.True(body.Position.Y > -2.0f, $"a body escaped downwards to {body.Position.Y}");
        }

        // A settled pile should be mostly or entirely asleep, which is what
        // makes a large scene affordable.
        Assert.True(
            world.AwakeBodyCount < Count / 2,
            $"{world.AwakeBodyCount} of {Count} bodies were still awake after settling");
    }

    [NativeFact]
    public void A_body_can_carry_many_shapes()
    {
        using var world = new PhysicsWorld();

        Body body = world.CreateDynamicBody(new Vector3(0.0f, 10.0f, 0.0f));

        const int ShapeCount = 200;
        var deferred = ShapeDefinition.Default with { UpdateBodyMass = false };

        for (int i = 0; i < ShapeCount; i++)
        {
            body.AddSphere(new Sphere(new Vector3(i * 0.05f, 0.0f, 0.0f), 0.1f), deferred);
        }

        body.RecomputeMass();

        Assert.Equal(ShapeCount, body.ShapeCount);
        Assert.True(body.Mass > 0.0f);

        // Reading them all back into caller-provided space, which is the
        // allocation-free path.
        var shapes = new Shape[ShapeCount];
        Assert.Equal(ShapeCount, body.GetShapes(shapes));

        world.Step(1.0f / 60.0f);
    }

    [NativeFact]
    public void A_long_chain_of_joints_stays_connected()
    {
        using var world = new PhysicsWorld(WorldSettings.Default with
        {
            Gravity = new Vector3(0.0f, -10.0f, 0.0f),
        });

        const int Links = 60;

        Body anchor = world.CreateStaticBody(new Vector3(0.0f, 50.0f, 0.0f));
        anchor.AddBox(Box.Cube(0.2f));

        Body previous = anchor;
        Vector3 previousPoint = new(0.0f, 50.0f, 0.0f);

        var links = new Body[Links];

        for (int i = 0; i < Links; i++)
        {
            Vector3 position = new(0.0f, 50.0f - ((i + 1) * 0.5f), 0.0f);

            links[i] = world.CreateDynamicBody(position);
            links[i].AddSphere(new Sphere(0.1f));

            world.CreateDistanceJoint(
                DistanceJointDefinition.Between(previous, links[i], previousPoint, position));

            previous = links[i];
            previousPoint = position;
        }

        for (int step = 0; step < 300; step++)
        {
            world.Step(1.0f / 60.0f);
        }

        // The chain cannot be longer than the sum of its rest lengths, whatever
        // the solver does under load.
        float span = 50.0f - links[Links - 1].Position.Y;

        Assert.True(span <= (Links * 0.5f) + 2.0f, $"the chain stretched to {span}");
        Assert.False(float.IsNaN(links[Links - 1].Position.Y));
    }

    [NativeFact]
    public void Many_worlds_can_exist_at_once()
    {
        // Up to the documented maximum, minus a margin for whatever else the
        // test run is holding.
        int target = Math.Min(PhysicsWorld.MaxCount - PhysicsWorld.Count - 4, 64);
        var worlds = new PhysicsWorld[target];

        try
        {
            for (int i = 0; i < target; i++)
            {
                worlds[i] = new PhysicsWorld();

                Body body = worlds[i].CreateDynamicBody(new Vector3(0.0f, 5.0f, 0.0f));
                body.AddSphere(new Sphere(0.5f));
            }

            foreach (PhysicsWorld world in worlds)
            {
                world.Step(1.0f / 60.0f);
            }

            Assert.True(PhysicsWorld.Count >= target);
        }
        finally
        {
            foreach (PhysicsWorld world in worlds)
            {
                world?.Dispose();
            }
        }
    }

    [NativeFact]
    public void Exhausting_the_world_limit_throws_rather_than_corrupting()
    {
        var worlds = new System.Collections.Generic.List<PhysicsWorld>();

        try
        {
            // Fill up to the limit.
            while (PhysicsWorld.Count < PhysicsWorld.MaxCount)
            {
                worlds.Add(new PhysicsWorld());
            }

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => new PhysicsWorld());

            // The message has to say what to do about it.
            Assert.Contains("dispose", error.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            foreach (PhysicsWorld world in worlds)
            {
                world.Dispose();
            }
        }
    }

    // ---------------------------------------------------------------- leaks

    [NativeFact]
    public void Creating_and_destroying_worlds_does_not_leak()
    {
        AssertNoLeak("world create and destroy", () =>
        {
            for (int i = 0; i < 8; i++)
            {
                using var world = new PhysicsWorld();

                Body body = world.CreateDynamicBody(new Vector3(0.0f, 5.0f, 0.0f));
                body.AddSphere(new Sphere(0.5f));

                world.Step(1.0f / 60.0f);
            }
        });
    }

    [NativeFact]
    public void Creating_and_destroying_bodies_in_a_live_world_does_not_leak()
    {
        // Churn inside one world, which is what a game spawning and despawning
        // does, and the case a per-world leak would hide in.
        using var world = new PhysicsWorld(WorldSettings.Default with
        {
            Gravity = new Vector3(0.0f, -10.0f, 0.0f),
        });

        Body ground = world.CreateStaticBody(new Vector3(0.0f, -0.5f, 0.0f));
        ground.AddBox(new Box(new Vector3(20.0f, 0.5f, 20.0f)));

        // Settle the world's own growth first.
        for (int warm = 0; warm < 2; warm++)
        {
            Churn(world);
        }

        int before = B3.b3GetByteCount();
        Churn(world);
        int after = B3.b3GetByteCount();

        // Box3D's arrays grow and keep their capacity, so this checks that
        // repeated churn does not grow without bound rather than that it returns
        // to exactly zero.
        Assert.True(
            after <= before,
            $"body churn grew the allocation from {before} to {after}");

        static void Churn(PhysicsWorld world)
        {
            var bodies = new Body[100];

            for (int i = 0; i < bodies.Length; i++)
            {
                bodies[i] = world.CreateDynamicBody(new Vector3(i * 0.3f, 5.0f, 0.0f));
                bodies[i].AddSphere(new Sphere(0.2f));
            }

            for (int step = 0; step < 10; step++)
            {
                world.Step(1.0f / 60.0f);
            }

            foreach (Body body in bodies)
            {
                body.Destroy();
            }

            for (int step = 0; step < 10; step++)
            {
                world.Step(1.0f / 60.0f);
            }
        }
    }

    [NativeFact]
    public void Creating_and_destroying_joints_does_not_leak()
    {
        AssertNoLeak("joint create and destroy", () =>
        {
            using var world = new PhysicsWorld();

            Body a = world.CreateStaticBody(Vector3.Zero);
            a.AddBox(Box.Cube(0.25f));

            Body b = world.CreateDynamicBody(new Vector3(1.0f, 0.0f, 0.0f));
            b.AddBox(Box.Cube(0.25f));

            for (int i = 0; i < 50; i++)
            {
                RevoluteJoint joint = world.CreateRevoluteJoint(
                    RevoluteJointDefinition.Hinge(a, b, Vector3.Zero, Vector3.UnitZ));

                world.Step(1.0f / 60.0f);
                joint.Destroy();
            }
        });
    }

    [NativeFact]
    public void Building_and_releasing_geometry_does_not_leak()
    {
        AssertNoLeak("hull, mesh and height field lifecycle", () =>
        {
            for (int i = 0; i < 8; i++)
            {
                using ConvexHull hull = ConvexHull.Cylinder(1.0f, 0.5f);
                using CollisionMesh mesh = CollisionMesh.Grid(8, 8, 1.0f);
                using HeightField field = HeightField.Grid(8, 8, Vector3.One);

                using var world = new PhysicsWorld();

                Body dynamic = world.CreateDynamicBody(new Vector3(0.0f, 10.0f, 0.0f));
                dynamic.AddHull(hull);

                Body meshBody = world.CreateStaticBody();
                meshBody.AddMesh(mesh);

                Body terrainBody = world.CreateStaticBody(new Vector3(50.0f, 0.0f, 0.0f));
                terrainBody.AddHeightField(field);

                world.Step(1.0f / 60.0f);
            }
        });
    }

    [NativeFact]
    public void Attaching_and_removing_shapes_does_not_leak()
    {
        AssertNoLeak("shape attach and detach", () =>
        {
            using var world = new PhysicsWorld();

            Body body = world.CreateDynamicBody(new Vector3(0.0f, 5.0f, 0.0f));

            for (int i = 0; i < 100; i++)
            {
                Shape shape = body.AddSphere(new Sphere(0.3f));
                world.Step(1.0f / 60.0f);
                shape.Destroy();
            }
        });
    }

    [NativeFact]
    public void Running_queries_does_not_leak()
    {
        AssertNoLeak("queries", () =>
        {
            using var world = new PhysicsWorld();

            for (int i = 0; i < 20; i++)
            {
                Body body = world.CreateStaticBody(new Vector3(i * 2.0f, 0.0f, 0.0f));
                body.AddSphere(new Sphere(0.5f));
            }

            world.Step(1.0f / 60.0f);

            var counter = new CountHits();

            for (int i = 0; i < 200; i++)
            {
                _ = world.RaycastClosest(new Vector3(-5.0f, 0.0f, 0.0f), new Vector3(60.0f, 0.0f, 0.0f));
                world.Raycast(new Vector3(-5.0f, 0.0f, 0.0f), new Vector3(60.0f, 0.0f, 0.0f), ref counter);
                world.OverlapBox(Vector3.Zero, new Vector3(50.0f), ref counter);
            }
        });
    }

    private struct CountHits : IRaycastCallback, IOverlapCallback
    {
        public int Count;

        public RaycastAction OnHit(in RaycastHit hit)
        {
            Count++;
            return RaycastAction.Continue;
        }

        public bool OnOverlap(Shape shape)
        {
            Count++;
            return true;
        }
    }

    // --------------------------------------------------------- long running

    [NativeFact]
    public void A_long_simulation_stays_stable()
    {
        // Ten simulated seconds of continuous contact. Instability tends to
        // appear over time rather than immediately.
        using var world = new PhysicsWorld(WorldSettings.Default with
        {
            Gravity = new Vector3(0.0f, -10.0f, 0.0f),
        });

        Body ground = world.CreateStaticBody(new Vector3(0.0f, -0.5f, 0.0f));
        ground.AddBox(new Box(new Vector3(30.0f, 0.5f, 30.0f)));

        var bodies = new Body[100];
        for (int i = 0; i < bodies.Length; i++)
        {
            bodies[i] = world.CreateDynamicBody(new Vector3(
                ((i % 10) * 0.8f) - 4.0f,
                1.0f + ((i / 10) * 0.8f),
                0.0f));

            bodies[i].AddBox(Box.Cube(0.35f), ShapeDefinition.Default with
            {
                Material = PhysicsMaterial.Default with { Restitution = 0.3f },
            });
        }

        for (int step = 0; step < 600; step++)
        {
            world.Step(1.0f / 60.0f);

            // Keep stirring so nothing gets the chance to fall asleep and stop
            // exercising the solver.
            if (step % 120 == 0)
            {
                foreach (Body body in bodies)
                {
                    body.ApplyImpulseToCenter(new Vector3(0.0f, 2.0f, 0.0f));
                }
            }
        }

        foreach (Body body in bodies)
        {
            Vector3 position = body.Position;

            Assert.False(float.IsNaN(position.Y), "a long run produced NaN");
            Assert.True(position.Y is > -2.0f and < 100.0f, $"a body drifted to {position.Y}");
        }
    }
}
