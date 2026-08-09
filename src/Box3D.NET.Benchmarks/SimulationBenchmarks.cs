// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using BenchmarkDotNet.Attributes;
using Box3D;
using Box3D.Native;

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

        Workload.RequireAwake(_world, BodyCount, $"{BodyCount}-body pile");
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
    /// <summary>How many static spheres the ray travels through.</summary>
    private const int ShapeCount = 200;

    private static readonly Vector3 RayOrigin = new(-1.0f, 0.0f, 0.0f);
    private static readonly Vector3 RayTranslation = new(201.0f, 0.0f, 0.0f);

    private PhysicsWorld _world = null!;
    private b3WorldId _nativeWorld;

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
    public unsafe void Setup()
    {
        _world = new PhysicsWorld();

        // A corridor of static spheres for rays to travel through.
        for (int i = 0; i < ShapeCount; i++)
        {
            Body body = _world.CreateBody(BodyDefinition.Static(new Vector3(i * 1.0f, 0.0f, 0.0f)));
            body.AddSphere(new Sphere(0.4f));
        }

        _world.Step(1.0f / 60.0f);

        // The same corridor through the C API, so that the wrapper's share of a
        // query can be read off rather than guessed at.
        b3WorldDef worldDef = B3.b3DefaultWorldDef();
        _nativeWorld = B3.b3CreateWorld(&worldDef);

        for (int i = 0; i < ShapeCount; i++)
        {
            b3BodyDef def = B3.b3DefaultBodyDef();
            def.type = b3BodyType.b3_staticBody;
            def.position = new Vector3(i * 1.0f, 0.0f, 0.0f);

            b3BodyId body = B3.b3CreateBody(_nativeWorld, &def);
            b3ShapeDef shapeDef = B3.b3DefaultShapeDef();
            b3Sphere sphere = new() { center = Vector3.Zero, radius = 0.4f };
            _ = B3.b3CreateSphereShape(body, &shapeDef, &sphere);
        }

        B3.b3World_Step(_nativeWorld, 1.0f / 60.0f, 4);

        // The ray has to actually travel the corridor, or every figure below is
        // the broad phase rejecting it.
        CountHits probe = default;
        _world.Raycast(RayOrigin, RayTranslation, ref probe);
        Workload.RequireHits(probe.Count, ShapeCount, "corridor raycast");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _world.Dispose();
        B3.b3DestroyWorld(_nativeWorld);
    }

    [Benchmark(Baseline = true, Description = "RaycastClosest, C API")]
    public unsafe b3RayResult RaycastClosestNative() =>
        B3.b3World_CastRayClosest(_nativeWorld, RayOrigin, RayTranslation, B3.b3DefaultQueryFilter());

    [Benchmark(Description = "RaycastClosest, Box3D.NET")]
    public RaycastHit RaycastClosest() => _world.RaycastClosest(RayOrigin, RayTranslation);

    [Benchmark(Description = "Raycast with callback, nearest")]
    public RaycastHit RaycastCallbackNearest()
    {
        NearestHit callback = default;
        _world.Raycast(RayOrigin, RayTranslation, ref callback);

        return callback.Nearest;
    }

    [Benchmark(Description = "Raycast with callback, all hits")]
    public int RaycastCallbackAll()
    {
        CountHits callback = default;
        _world.Raycast(RayOrigin, RayTranslation, ref callback);

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
/// <remarks>
/// <para>
/// Draining is measured on its own as well as together with the step. Together
/// is what a frame costs; on its own is the only way to see what the
/// enumeration itself costs and whether it allocates, because a step's own
/// figure would swamp it.
/// </para>
/// <para>
/// Draining without stepping is legitimate rather than a trick: the event
/// buffers belong to the world and stay valid until the next step, so reading
/// them twice reads the same events. That is the documented lifetime, and this
/// benchmark depends on it.
/// </para>
/// </remarks>
[MemoryDiagnoser]
[HideColumns("Job", "Error", "StdDev")]
public class EventBenchmarks
{
    private const int BodyCount = 200;

    private PhysicsWorld _world = null!;

    [GlobalSetup]
    public void Setup()
    {
        _world = new PhysicsWorld(WorldSettings.Default with
        {
            Gravity = new Vector3(0.0f, -10.0f, 0.0f),

            // Sleep off, for the same reason as everywhere else: a sleeping
            // body raises no move event, so a settled scene would measure an
            // empty enumeration.
            EnableSleep = false,
        });

        Body ground = _world.CreateBody(BodyDefinition.Static(new Vector3(0.0f, -0.5f, 0.0f)));
        ground.AddBox(new Box(new Vector3(100.0f, 0.5f, 100.0f)), ShapeDefinition.Default with
        {
            EnableContactEvents = true,
        });

        for (int i = 0; i < BodyCount; i++)
        {
            Body body = _world.CreateBody(BodyDefinition.Dynamic(
                new Vector3((i % 20) * 1.2f, 1.0f + ((i / 20) * 1.2f), 0.0f)));

            body.AddBox(Box.Cube(0.5f), ShapeDefinition.Default with { EnableContactEvents = true });
        }

        _world.Step(1.0f / 60.0f);

        Workload.RequireAwake(_world, BodyCount, $"{BodyCount}-body event scene");
        Workload.RequireHits(_world.Events.BodyMoves.Count, BodyCount, "body move event");
    }

    [GlobalCleanup]
    public void Cleanup() => _world.Dispose();

    [Benchmark(Baseline = true, Description = "Step then drain every event")]
    public int StepAndDrainEvents()
    {
        _world.Step(1.0f / 60.0f);
        return Drain();
    }

    [Benchmark(Description = "Drain every event, no step")]
    public int DrainEvents() => Drain();

    private int Drain()
    {
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

        foreach (ContactEndEvent end in events.ContactEnds)
        {
            total += end.ShapeA.IsValid ? 1 : 0;
        }

        foreach (ContactHitEvent hit in events.ContactHits)
        {
            total += hit.ApproachSpeed > 0.0f ? 1 : 0;
        }

        foreach (SensorBeginEvent sensor in events.SensorBegins)
        {
            total += sensor.Sensor.IsValid ? 1 : 0;
        }

        foreach (SensorEndEvent sensor in events.SensorEnds)
        {
            total += sensor.Sensor.IsValid ? 1 : 0;
        }

        return total;
    }
}
