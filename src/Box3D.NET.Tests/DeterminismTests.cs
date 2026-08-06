// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using Xunit;

namespace Box3D.Tests;

/// <summary>
/// Checks that identical inputs produce identical results, bit for bit.
/// </summary>
/// <remarks>
/// <para>
/// Box3D is built for cross-platform determinism, which is what makes lockstep
/// networking and replay possible. The binding can undermine that in ways the
/// engine cannot see: reordering floating point work, letting a value take a
/// different path in and out, or introducing state that varies between runs.
/// </para>
/// <para>
/// These tests hash the whole world state after a fixed number of steps and
/// compare. A hash rather than a tolerance is deliberate: determinism is an
/// exact property, and "close enough" would pass while a replay diverged.
/// </para>
/// </remarks>
[Collection(NativeCollection.Name)]
public class DeterminismTests
{
    /// <summary>
    /// Builds the same scene every time and returns a hash of the final state.
    /// </summary>
    /// <param name="workerCount">Worker threads for the world.</param>
    /// <param name="steps">How many steps to run.</param>
    /// <returns>A hash over every body's position and velocity.</returns>
    private static ulong SimulateAndHash(int workerCount = 1, int steps = 120)
    {
        using var world = new PhysicsWorld(WorldSettings.Default with
        {
            Gravity = new Vector3(0.0f, -10.0f, 0.0f),
            WorkerCount = workerCount,
        });

        Body ground = world.CreateStaticBody(new Vector3(0.0f, -0.5f, 0.0f));
        ground.AddBox(new Box(new Vector3(50.0f, 0.5f, 50.0f)));

        // A pile that actually interacts: bodies stacked close enough to collide
        // with each other, which is where any non-determinism would show up.
        var bodies = new Body[64];
        for (int i = 0; i < bodies.Length; i++)
        {
            bodies[i] = world.CreateDynamicBody(new Vector3(
                ((i % 8) * 0.9f) - 3.6f,
                1.0f + ((i / 8) * 1.1f),
                ((i % 3) * 0.4f) - 0.4f));

            bodies[i].AddBox(Box.Cube(0.4f));
        }

        for (int step = 0; step < steps; step++)
        {
            world.Step(1.0f / 60.0f);
        }

        return HashState(bodies);
    }

    /// <summary>Hashes the exact bits of every body's position and velocity.</summary>
    private static ulong HashState(ReadOnlySpan<Body> bodies)
    {
        // FNV-1a over the raw bit patterns. Comparing bits rather than values
        // means a difference of one unit in the last place still fails, which is
        // the point.
        ulong hash = 14695981039346656037UL;

        foreach (Body body in bodies)
        {
            MixVector(ref hash, body.Position);
            MixVector(ref hash, body.LinearVelocity);
            MixVector(ref hash, body.AngularVelocity);

            Quaternion rotation = body.Rotation;
            MixFloat(ref hash, rotation.X);
            MixFloat(ref hash, rotation.Y);
            MixFloat(ref hash, rotation.Z);
            MixFloat(ref hash, rotation.W);
        }

        return hash;
    }

    private static void MixVector(ref ulong hash, Vector3 value)
    {
        MixFloat(ref hash, value.X);
        MixFloat(ref hash, value.Y);
        MixFloat(ref hash, value.Z);
    }

    private static void MixFloat(ref ulong hash, float value)
    {
        // The raw bits, so a difference of one unit in the last place still
        // changes the hash. Comparing values with a tolerance would let a
        // genuine divergence pass.
        hash ^= BitConverter.SingleToUInt32Bits(value);
        hash *= 1099511628211UL;
    }

    [NativeFact]
    public void The_same_scene_run_twice_gives_the_same_result()
    {
        ulong first = SimulateAndHash();
        ulong second = SimulateAndHash();

        Assert.Equal(first, second);
    }

    [NativeFact]
    public void The_same_scene_is_reproducible_across_many_runs()
    {
        // Repeated because a race would not necessarily show on the second run.
        ulong reference = SimulateAndHash();

        for (int run = 0; run < 5; run++)
        {
            Assert.Equal(reference, SimulateAndHash());
        }
    }

    [NativeFact]
    public void Other_worlds_existing_alongside_do_not_change_the_result()
    {
        // Worlds are documented as completely independent. If one leaked state
        // into another - a shared allocator arena, a global cache - this is what
        // would catch it.
        ulong alone = SimulateAndHash();

        using var noise1 = new PhysicsWorld(WorldSettings.Default with { Gravity = new Vector3(1.0f, -3.0f, 2.0f) });
        using var noise2 = new PhysicsWorld();

        Body a = noise1.CreateDynamicBody(new Vector3(5.0f, 5.0f, 5.0f));
        a.AddSphere(new Sphere(0.7f));

        Body b = noise2.CreateDynamicBody(new Vector3(-5.0f, 3.0f, 0.0f));
        b.AddBox(Box.Cube(0.6f));

        for (int i = 0; i < 30; i++)
        {
            noise1.Step(1.0f / 60.0f);
            noise2.Step(1.0f / 120.0f);
        }

        Assert.Equal(alone, SimulateAndHash());
    }

    [NativeFact]
    public void Interleaving_two_worlds_step_by_step_does_not_change_either()
    {
        // Stepping worlds in an interleaved order is what an application running
        // several simulations does. Neither may notice the other.
        ulong reference = SimulateAndHash(steps: 60);

        using var other = new PhysicsWorld(WorldSettings.Default with
        {
            Gravity = new Vector3(0.0f, -10.0f, 0.0f),
        });

        Body ground = other.CreateStaticBody(new Vector3(0.0f, -0.5f, 0.0f));
        ground.AddBox(new Box(new Vector3(20.0f, 0.5f, 20.0f)));

        Body ball = other.CreateDynamicBody(new Vector3(0.0f, 5.0f, 0.0f));
        ball.AddSphere(new Sphere(0.5f));

        // Now run the reference scene again, stepping the other world between
        // each of its steps.
        using var world = new PhysicsWorld(WorldSettings.Default with
        {
            Gravity = new Vector3(0.0f, -10.0f, 0.0f),
            WorkerCount = 1,
        });

        Body floor = world.CreateStaticBody(new Vector3(0.0f, -0.5f, 0.0f));
        floor.AddBox(new Box(new Vector3(50.0f, 0.5f, 50.0f)));

        var bodies = new Body[64];
        for (int i = 0; i < bodies.Length; i++)
        {
            bodies[i] = world.CreateDynamicBody(new Vector3(
                ((i % 8) * 0.9f) - 3.6f,
                1.0f + ((i / 8) * 1.1f),
                ((i % 3) * 0.4f) - 0.4f));

            bodies[i].AddBox(Box.Cube(0.4f));
        }

        for (int step = 0; step < 60; step++)
        {
            world.Step(1.0f / 60.0f);
            other.Step(1.0f / 60.0f);
        }

        Assert.Equal(reference, HashState(bodies));
    }

    [NativeFact]
    public void A_query_between_steps_does_not_change_the_result()
    {
        // Queries are documented as read-only. If one perturbed the broad phase
        // or left cached state behind, the outcome would drift.
        ulong reference = SimulateAndHash(steps: 60);

        using var world = new PhysicsWorld(WorldSettings.Default with
        {
            Gravity = new Vector3(0.0f, -10.0f, 0.0f),
            WorkerCount = 1,
        });

        Body ground = world.CreateStaticBody(new Vector3(0.0f, -0.5f, 0.0f));
        ground.AddBox(new Box(new Vector3(50.0f, 0.5f, 50.0f)));

        var bodies = new Body[64];
        for (int i = 0; i < bodies.Length; i++)
        {
            bodies[i] = world.CreateDynamicBody(new Vector3(
                ((i % 8) * 0.9f) - 3.6f,
                1.0f + ((i / 8) * 1.1f),
                ((i % 3) * 0.4f) - 0.4f));

            bodies[i].AddBox(Box.Cube(0.4f));
        }

        for (int step = 0; step < 60; step++)
        {
            world.Step(1.0f / 60.0f);

            _ = world.RaycastClosest(new Vector3(-20.0f, 2.0f, 0.0f), new Vector3(40.0f, 0.0f, 0.0f));
            _ = world.Bounds;
            _ = world.AwakeBodyCount;
        }

        Assert.Equal(reference, HashState(bodies));
    }

    [NativeFact]
    public void Reading_events_does_not_change_the_result()
    {
        ulong reference = SimulateAndHash(steps: 60);

        using var world = new PhysicsWorld(WorldSettings.Default with
        {
            Gravity = new Vector3(0.0f, -10.0f, 0.0f),
            WorkerCount = 1,
        });

        Body ground = world.CreateStaticBody(new Vector3(0.0f, -0.5f, 0.0f));
        ground.AddBox(new Box(new Vector3(50.0f, 0.5f, 50.0f)));

        var bodies = new Body[64];
        for (int i = 0; i < bodies.Length; i++)
        {
            bodies[i] = world.CreateDynamicBody(new Vector3(
                ((i % 8) * 0.9f) - 3.6f,
                1.0f + ((i / 8) * 1.1f),
                ((i % 3) * 0.4f) - 0.4f));

            bodies[i].AddBox(Box.Cube(0.4f));
        }

        int seen = 0;
        for (int step = 0; step < 60; step++)
        {
            world.Step(1.0f / 60.0f);

            foreach (BodyMoveEvent moved in world.Events.BodyMoves)
            {
                seen += moved.FellAsleep ? 0 : 1;
            }

            foreach (ContactBeginEvent begin in world.Events.ContactBegins)
            {
                seen += begin.ShapeA.IsValid ? 1 : 0;
            }
        }

        Assert.True(seen > 0, "the scene should have produced events at all");
        Assert.Equal(reference, HashState(bodies));
    }

    /*
     * Worker count is deliberately not asserted to preserve the hash.
     *
     * Multithreading re-partitions the constraint graph, and Box3D's own replay
     * validation treats a differing worker count as a cross-thread determinism
     * *test* rather than a guarantee. Claiming bit-identical results across
     * worker counts would be asserting something upstream does not promise. What
     * is checked instead is that a multithreaded world is self-consistent and
     * produces a physically sound result.
     */

    [NativeFact]
    public void A_multithreaded_world_is_reproducible_with_itself()
    {
        ulong first = SimulateAndHash(workerCount: 4);
        ulong second = SimulateAndHash(workerCount: 4);

        Assert.Equal(first, second);
    }

    [NativeFact]
    public void A_multithreaded_world_produces_a_sound_result()
    {
        using var world = new PhysicsWorld(WorldSettings.Default with
        {
            Gravity = new Vector3(0.0f, -10.0f, 0.0f),
            WorkerCount = 4,
        });

        Body ground = world.CreateStaticBody(new Vector3(0.0f, -0.5f, 0.0f));
        ground.AddBox(new Box(new Vector3(50.0f, 0.5f, 50.0f)));

        var bodies = new Body[200];
        for (int i = 0; i < bodies.Length; i++)
        {
            bodies[i] = world.CreateDynamicBody(new Vector3(
                ((i % 10) * 1.2f) - 6.0f,
                1.0f + ((i / 10) * 1.2f),
                ((i % 5) * 1.2f) - 2.4f));

            bodies[i].AddBox(Box.Cube(0.5f));
        }

        for (int step = 0; step < 240; step++)
        {
            world.Step(1.0f / 60.0f);
        }

        foreach (Body body in bodies)
        {
            Vector3 position = body.Position;

            Assert.False(float.IsNaN(position.Y), "a multithreaded step produced NaN");
            Assert.True(position.Y > -1.0f, $"a body fell through the floor to {position.Y}");
        }
    }
}
