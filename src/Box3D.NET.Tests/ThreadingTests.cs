// SPDX-License-Identifier: MIT

using System;
using System.Collections.Concurrent;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Box3D.Tests;

/// <summary>
/// Checks the threading contract: worlds are independent and may be stepped in
/// parallel, and a single world is not thread-safe.
/// </summary>
/// <remarks>
/// <para>
/// The promise worth testing is the first one. An application that runs a
/// server-side simulation per match, or a client that predicts several futures,
/// depends on worlds not sharing anything. Box3D documents them as completely
/// independent; that claim is worth exercising rather than trusting, because a
/// shared allocator or a global cache would only show up under contention.
/// </para>
/// <para>
/// These tests do not attempt to prove the absence of a race, which no test can
/// do. They apply enough concurrent pressure that a shared mutable structure
/// would very likely corrupt or crash.
/// </para>
/// </remarks>
[Collection(NativeCollection.Name)]
public class ThreadingTests
{
    private const int WorldCount = 8;
    private const int StepCount = 240;

    /// <summary>Builds a small scene whose outcome is easy to check.</summary>
    private static Body[] BuildScene(PhysicsWorld world, int bodyCount, float seed)
    {
        Body ground = world.CreateStaticBody(new Vector3(0.0f, -0.5f, 0.0f));
        ground.AddBox(new Box(new Vector3(30.0f, 0.5f, 30.0f)));

        var bodies = new Body[bodyCount];
        for (int i = 0; i < bodyCount; i++)
        {
            bodies[i] = world.CreateDynamicBody(new Vector3(
                ((i % 6) * 1.1f) - 2.75f + seed,
                1.0f + ((i / 6) * 1.1f),
                ((i % 4) * 1.1f) - 1.65f));

            bodies[i].AddBox(Box.Cube(0.45f));
            bodies[i].UserData = (ulong)i;
        }

        return bodies;
    }

    [NativeFact]
    public void Independent_worlds_step_correctly_in_parallel()
    {
        var worlds = new PhysicsWorld[WorldCount];
        var scenes = new Body[WorldCount][];

        try
        {
            for (int i = 0; i < WorldCount; i++)
            {
                worlds[i] = new PhysicsWorld(WorldSettings.Default with
                {
                    Gravity = new Vector3(0.0f, -10.0f, 0.0f),
                });

                scenes[i] = BuildScene(worlds[i], 40, i * 0.01f);
            }

            // One thread per world, all stepping at once. If anything were
            // shared, this is where it would tear.
            Parallel.For(0, WorldCount, index =>
            {
                for (int step = 0; step < StepCount; step++)
                {
                    worlds[index].Step(1.0f / 60.0f);
                }
            });

            for (int i = 0; i < WorldCount; i++)
            {
                foreach (Body body in scenes[i])
                {
                    Vector3 position = body.Position;

                    Assert.False(float.IsNaN(position.Y), $"world {i} produced NaN under parallel stepping");
                    Assert.True(position.Y > -1.0f, $"world {i} lost a body through the floor");
                }
            }
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
    public void Parallel_stepping_gives_the_same_answer_as_serial_stepping()
    {
        // The stronger claim: not merely that nothing crashed, but that a world
        // stepped alongside seven others reaches exactly the state it would have
        // reached on its own.
        Vector3[] serial;

        using (var world = new PhysicsWorld(WorldSettings.Default with
        {
            Gravity = new Vector3(0.0f, -10.0f, 0.0f),
        }))
        {
            Body[] bodies = BuildScene(world, 40, 0.0f);

            for (int step = 0; step < StepCount; step++)
            {
                world.Step(1.0f / 60.0f);
            }

            serial = Array.ConvertAll(bodies, b => b.Position);
        }

        var worlds = new PhysicsWorld[WorldCount];
        var scenes = new Body[WorldCount][];

        try
        {
            for (int i = 0; i < WorldCount; i++)
            {
                worlds[i] = new PhysicsWorld(WorldSettings.Default with
                {
                    Gravity = new Vector3(0.0f, -10.0f, 0.0f),
                });

                // Every world gets the identical scene this time.
                scenes[i] = BuildScene(worlds[i], 40, 0.0f);
            }

            Parallel.For(0, WorldCount, index =>
            {
                for (int step = 0; step < StepCount; step++)
                {
                    worlds[index].Step(1.0f / 60.0f);
                }
            });

            for (int i = 0; i < WorldCount; i++)
            {
                for (int b = 0; b < serial.Length; b++)
                {
                    Assert.Equal(serial[b], scenes[i][b].Position);
                }
            }
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
    public void Creating_and_destroying_worlds_concurrently_is_safe()
    {
        // World creation touches the global world table, which is the one piece
        // of state every world genuinely shares. Box3D leaves that table
        // unsynchronised and tells the caller to hold a mutex; PhysicsWorld is
        // that caller, and serialises creation and disposal internally. This
        // test is what holds it to that. Without the lock it does not fail, it
        // hangs: two threads land on the same slot and the resulting world
        // spins inside Step forever.
        var failures = new ConcurrentBag<Exception>();

        Parallel.For(0, 32, _ =>
        {
            try
            {
                using var world = new PhysicsWorld();

                Body body = world.CreateDynamicBody(new Vector3(0.0f, 5.0f, 0.0f));
                body.AddSphere(new Sphere(0.5f));

                for (int step = 0; step < 30; step++)
                {
                    world.Step(1.0f / 60.0f);
                }
            }
            catch (Exception ex)
            {
                failures.Add(ex);
            }
        });

        Assert.Empty(failures);
    }

    [NativeFact]
    public void Queries_run_in_parallel_across_separate_worlds()
    {
        var worlds = new PhysicsWorld[4];

        try
        {
            for (int i = 0; i < worlds.Length; i++)
            {
                worlds[i] = new PhysicsWorld();

                for (int b = 0; b < 50; b++)
                {
                    Body body = worlds[i].CreateStaticBody(new Vector3(b * 1.0f, 0.0f, 0.0f));
                    body.AddSphere(new Sphere(0.4f));
                }

                worlds[i].Step(1.0f / 60.0f);
            }

            var hits = new int[worlds.Length];

            Parallel.For(0, worlds.Length, index =>
            {
                int found = 0;

                for (int i = 0; i < 500; i++)
                {
                    RaycastHit hit = worlds[index].RaycastClosest(
                        new Vector3(-5.0f, 0.0f, 0.0f),
                        new Vector3(60.0f, 0.0f, 0.0f));

                    if (hit.Hit)
                    {
                        found++;
                    }
                }

                hits[index] = found;
            });

            // Every world holds the same scene, so every world must report the
            // same number of hits.
            foreach (int found in hits)
            {
                Assert.Equal(500, found);
            }
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
    public void A_world_with_internal_workers_steps_correctly_under_load()
    {
        // Box3D starts and owns the threads when the worker count is above one.
        // This exercises that path against a pile large enough to be split
        // across them.
        using var world = new PhysicsWorld(WorldSettings.Default with
        {
            Gravity = new Vector3(0.0f, -10.0f, 0.0f),
            WorkerCount = Math.Min(8, Environment.ProcessorCount),
        });

        Assert.True(world.WorkerCount >= 1);

        Body[] bodies = BuildScene(world, 400, 0.0f);

        for (int step = 0; step < 240; step++)
        {
            world.Step(1.0f / 60.0f);
        }

        foreach (Body body in bodies)
        {
            Assert.False(float.IsNaN(body.Position.Y));
            Assert.True(body.Position.Y > -1.0f);
        }
    }

    [NativeFact]
    public void Several_multithreaded_worlds_can_run_at_once()
    {
        // The hardest case: worlds that each start their own worker threads,
        // stepping simultaneously. Oversubscribes the machine on purpose.
        var worlds = new PhysicsWorld[4];

        try
        {
            for (int i = 0; i < worlds.Length; i++)
            {
                worlds[i] = new PhysicsWorld(WorldSettings.Default with
                {
                    Gravity = new Vector3(0.0f, -10.0f, 0.0f),
                    WorkerCount = 4,
                });

                BuildScene(worlds[i], 100, 0.0f);
            }

            Parallel.For(0, worlds.Length, index =>
            {
                for (int step = 0; step < 120; step++)
                {
                    worlds[index].Step(1.0f / 60.0f);
                }
            });

            foreach (PhysicsWorld world in worlds)
            {
                Assert.False(float.IsNaN(world.Bounds.Min.Y), "a world produced a NaN bound");
            }
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
    public void Handles_are_safe_to_pass_between_threads()
    {
        // Body is a value type holding an index and a generation, with no
        // affinity to the thread that made it. Reading through one from another
        // thread, while nothing is stepping, has to work.
        using var world = new PhysicsWorld();

        Body body = world.CreateDynamicBody(new Vector3(1.0f, 2.0f, 3.0f));
        body.AddSphere(new Sphere(0.5f));
        body.UserData = 4242;

        Vector3 position = default;
        ulong userData = 0;

        var thread = new Thread(() =>
        {
            position = body.Position;
            userData = body.UserData;
        });

        thread.Start();
        thread.Join();

        Assert.Equal(new Vector3(1.0f, 2.0f, 3.0f), position);
        Assert.Equal(4242UL, userData);
    }
}
