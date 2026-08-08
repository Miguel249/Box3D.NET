// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using BenchmarkDotNet.Attributes;
using Box3D;

namespace Box3D.Benchmarks;

/// <summary>
/// Measures the step itself, which is dominated by Box3D rather than by the
/// wrapper, and is here to show what the wrapper is a rounding error against.
/// </summary>
[MemoryDiagnoser]
[HideColumns("Job", "Error", "StdDev")]
public class StepBenchmarks
{
    private PhysicsWorld _world = null!;

    /// <summary>Gets or sets the number of dynamic bodies in the pile.</summary>
    [Params(100, 1000)]
    public int BodyCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _world = new PhysicsWorld(WorldSettings.Default with
        {
            Gravity = new Vector3(0.0f, -10.0f, 0.0f),

            // Sleep off, or this measures nothing. Box3D skips a sleeping body
            // entirely, so a settled pile steps in roughly the same time
            // whether it holds 100 bodies or 10,000 — the figure tracks the
            // sleep check rather than the simulation. See ScaleBenchmarks.
            EnableSleep = false,
        });

        Body ground = _world.CreateBody(BodyDefinition.Static(new Vector3(0.0f, -0.5f, 0.0f)));
        ground.AddBox(new Box(new Vector3(200.0f, 0.5f, 200.0f)));

        // A grid of boxes, spread out enough that they settle rather than
        // spending the whole benchmark resolving a single enormous pile.
        int perSide = (int)MathF.Ceiling(MathF.Sqrt(BodyCount));
        int created = 0;

        for (int x = 0; x < perSide && created < BodyCount; x++)
        {
            for (int z = 0; z < perSide && created < BodyCount; z++, created++)
            {
                Body body = _world.CreateBody(BodyDefinition.Dynamic(
                    new Vector3((x * 1.5f) - (perSide * 0.75f), 2.0f, (z * 1.5f) - (perSide * 0.75f))));

                body.AddBox(Box.Cube(0.5f));
            }
        }

        // Let the pile settle so the measurement reflects a steady state rather
        // than the first, unrepresentative, contact-heavy frames.
        for (int i = 0; i < 60; i++)
        {
            _world.Step(1.0f / 60.0f);
        }
    }

    [GlobalCleanup]
    public void Cleanup() => _world.Dispose();

    [Benchmark(Description = "World.Step")]
    public void Step() => _world.Step(1.0f / 60.0f);
}

/// <summary>
/// Measures spatial queries, and in particular whether the struct-callback
/// design delivers the allocation-free promise it was chosen for.
/// </summary>
[MemoryDiagnoser]
[HideColumns("Job", "Error", "StdDev", "RatioSD")]
public class QueryBenchmarks
{
    private PhysicsWorld _world = null!;

    private struct CountHits : IRaycastCallback
    {
        public int Count;

        public RaycastAction OnHit(in RaycastHit hit)
        {
            Count++;
            return RaycastAction.Continue;
        }
    }

    private struct NearestHit : IRaycastCallback
    {
        public RaycastHit Nearest;

        public RaycastAction OnHit(in RaycastHit hit)
        {
            Nearest = hit;
            return RaycastAction.ClipTo(hit.Fraction);
        }
    }

    private struct CountOverlaps : IOverlapCallback
    {
        public int Count;

        public bool OnOverlap(Shape shape)
        {
            Count++;
            return true;
        }
    }

    [GlobalSetup]
    public void Setup()
    {
        _world = new PhysicsWorld();

        // A corridor of static spheres for rays to travel through.
        for (int i = 0; i < 200; i++)
        {
            Body body = _world.CreateBody(BodyDefinition.Static(new Vector3(i * 1.0f, 0.0f, 0.0f)));
            body.AddSphere(new Sphere(0.4f));
        }

        _world.Step(1.0f / 60.0f);
    }

    [GlobalCleanup]
    public void Cleanup() => _world.Dispose();

    [Benchmark(Baseline = true, Description = "RaycastClosest over 200 shapes")]
    public RaycastHit RaycastClosest() =>
        _world.RaycastClosest(new Vector3(-1.0f, 0.0f, 0.0f), new Vector3(201.0f, 0.0f, 0.0f));

    [Benchmark(Description = "Raycast with callback, nearest")]
    public RaycastHit RaycastCallbackNearest()
    {
        NearestHit callback = default;
        _world.Raycast(new Vector3(-1.0f, 0.0f, 0.0f), new Vector3(201.0f, 0.0f, 0.0f), ref callback);

        return callback.Nearest;
    }

    [Benchmark(Description = "Raycast with callback, all hits")]
    public int RaycastCallbackAll()
    {
        CountHits callback = default;
        _world.Raycast(new Vector3(-1.0f, 0.0f, 0.0f), new Vector3(201.0f, 0.0f, 0.0f), ref callback);

        return callback.Count;
    }

    [Benchmark(Description = "OverlapBox over the whole corridor")]
    public int OverlapBox()
    {
        CountOverlaps callback = default;
        _world.OverlapBox(new Vector3(100.0f, 0.0f, 0.0f), new Vector3(101.0f, 1.0f, 1.0f), ref callback);

        return callback.Count;
    }
}

/// <summary>
/// Measures reading events after a step, which every frame of a real game does.
/// </summary>
[MemoryDiagnoser]
[HideColumns("Job", "Error", "StdDev")]
public class EventBenchmarks
{
    private PhysicsWorld _world = null!;

    [GlobalSetup]
    public void Setup()
    {
        _world = new PhysicsWorld(WorldSettings.Default with
        {
            Gravity = new Vector3(0.0f, -10.0f, 0.0f),
        });

        Body ground = _world.CreateBody(BodyDefinition.Static(new Vector3(0.0f, -0.5f, 0.0f)));
        ground.AddBox(new Box(new Vector3(100.0f, 0.5f, 100.0f)), ShapeDefinition.Default with
        {
            EnableContactEvents = true,
        });

        for (int i = 0; i < 200; i++)
        {
            Body body = _world.CreateBody(BodyDefinition.Dynamic(
                new Vector3((i % 20) * 1.2f, 1.0f + ((i / 20) * 1.2f), 0.0f)));

            body.AddBox(Box.Cube(0.5f), ShapeDefinition.Default with { EnableContactEvents = true });
        }

        _world.Step(1.0f / 60.0f);
    }

    [GlobalCleanup]
    public void Cleanup() => _world.Dispose();

    [Benchmark(Description = "Step then drain every event")]
    public int StepAndDrainEvents()
    {
        _world.Step(1.0f / 60.0f);

        int total = 0;
        WorldEvents events = _world.Events;

        foreach (BodyMoveEvent moved in events.BodyMoves)
        {
            total += moved.FellAsleep ? 0 : 1;
        }

        foreach (ContactBeginEvent begin in events.ContactBegins)
        {
            total += begin.ShapeA.IsValid ? 1 : 0;
        }

        return total;
    }
}
