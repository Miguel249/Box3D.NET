// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using BenchmarkDotNet.Attributes;
using Box3D;
using Box3D.Native;

namespace Box3D.Benchmarks;

/// <summary>
/// Steps identical worlds through the wrapper and through the C API, at three
/// scales, to show what the wrapper costs once there is real work to do.
/// </summary>
/// <remarks>
/// <para>
/// Both worlds are built the same way and settled the same number of frames, so
/// the only difference is which <c>Step</c> is called. The ratio column is the
/// number worth reading: it is the wrapper's overhead as a fraction of a real
/// frame, rather than as a fraction of an empty call.
/// </para>
/// <para>
/// The scene is a settled grid rather than a collapsing pile. A pile spends the
/// benchmark resolving whatever configuration it happens to be in when the
/// measurement starts, which makes the result depend on the warm-up rather than
/// on the engine.
/// </para>
/// </remarks>
[MemoryDiagnoser]
[HideColumns("Job", "RatioSD")]
public class StepScaleBenchmarks
{
    private PhysicsWorld _world = null!;
    private b3WorldId _nativeWorld;

    /// <summary>Gets or sets the number of dynamic bodies in the scene.</summary>
    [Params(100, 1_000, 10_000)]
    public int BodyCount { get; set; }

    [GlobalSetup]
    public unsafe void Setup()
    {
        _world = new PhysicsWorld(WorldSettings.Default with
        {
            Gravity = new Vector3(0.0f, -10.0f, 0.0f),

            // Sleep off, deliberately.
            //
            // Box3D puts a body that has stopped moving to sleep and then skips
            // it entirely. A settled scene therefore costs almost nothing to
            // step, and the figure stops responding to body count: 100 bodies
            // and 10,000 bodies both measured about 210 ns, which is the sleep
            // check and nothing else. Keeping every body awake is what a frame
            // costs while the simulation is actually simulating, and that is
            // the number worth publishing.
            EnableSleep = false,
        });

        Body ground = _world.CreateBody(BodyDefinition.Static(new Vector3(0.0f, -0.5f, 0.0f)));
        ground.AddBox(new Box(new Vector3(500.0f, 0.5f, 500.0f)));

        Populate(_world, BodyCount);

        b3WorldDef worldDef = B3.b3DefaultWorldDef();
        worldDef.gravity = new Vector3(0.0f, -10.0f, 0.0f);
        // Matching the managed world exactly, or the two are not comparable.
        worldDef.enableSleep = false;
        _nativeWorld = B3.b3CreateWorld(&worldDef);

        b3BodyDef groundDef = B3.b3DefaultBodyDef();
        groundDef.type = b3BodyType.b3_staticBody;
        groundDef.position = new Vector3(0.0f, -0.5f, 0.0f);
        b3BodyId nativeGround = B3.b3CreateBody(_nativeWorld, &groundDef);

        b3ShapeDef groundShape = B3.b3DefaultShapeDef();
        b3BoxHull groundHull = B3.b3MakeBoxHull(500.0f, 0.5f, 500.0f);
        _ = B3.b3CreateHullShape(nativeGround, &groundShape, &groundHull.@base);

        PopulateNative(_nativeWorld, BodyCount);

        // Settle both, so the measurement is of a steady state.
        for (int i = 0; i < 60; i++)
        {
            _world.Step(1.0f / 60.0f);
            B3.b3World_Step(_nativeWorld, 1.0f / 60.0f, 4);
        }

        // And confirm the steady state is still a busy one. Without this the
        // benchmark reports a number whether or not there is anything to solve.
        Workload.RequireAwake(_world, BodyCount, $"{BodyCount}-body grid, wrapper");
        Workload.RequireAwake(_nativeWorld, BodyCount, $"{BodyCount}-body grid, C API");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _world.Dispose();
        B3.b3DestroyWorld(_nativeWorld);
    }

    [Benchmark(Baseline = true, Description = "Step, C API")]
    public void StepNative() => B3.b3World_Step(_nativeWorld, 1.0f / 60.0f, 4);

    [Benchmark(Description = "Step, Box3D.NET")]
    public void StepWrapper() => _world.Step(1.0f / 60.0f);

    private static void Populate(PhysicsWorld world, int count)
    {
        int perSide = (int)MathF.Ceiling(MathF.Sqrt(count));
        int created = 0;

        for (int x = 0; x < perSide && created < count; x++)
        {
            for (int z = 0; z < perSide && created < count; z++, created++)
            {
                Body body = world.CreateBody(BodyDefinition.Dynamic(
                    new Vector3((x * 1.5f) - (perSide * 0.75f), 2.0f, (z * 1.5f) - (perSide * 0.75f))));

                body.AddBox(Box.Cube(0.5f));
            }
        }
    }

    private static unsafe void PopulateNative(b3WorldId world, int count)
    {
        int perSide = (int)MathF.Ceiling(MathF.Sqrt(count));
        int created = 0;

        b3BoxHull hull = B3.b3MakeBoxHull(0.5f, 0.5f, 0.5f);

        for (int x = 0; x < perSide && created < count; x++)
        {
            for (int z = 0; z < perSide && created < count; z++, created++)
            {
                b3BodyDef def = B3.b3DefaultBodyDef();
                def.type = b3BodyType.b3_dynamicBody;
                def.position = new Vector3((x * 1.5f) - (perSide * 0.75f), 2.0f, (z * 1.5f) - (perSide * 0.75f));

                b3BodyId body = B3.b3CreateBody(world, &def);
                b3ShapeDef shapeDef = B3.b3DefaultShapeDef();
                _ = B3.b3CreateHullShape(body, &shapeDef, &hull.@base);
            }
        }
    }
}

/// <summary>
/// Steps towers of boxes, where nearly every body is in persistent contact.
/// </summary>
/// <remarks>
/// The grid above spreads bodies out so that the broad phase does most of the
/// work. Stacking is the opposite: a small number of bodies generating a large
/// number of contacts that have to be solved together, which is where a solver
/// actually spends its time and where frame cost stops scaling with body count.
/// </remarks>
[MemoryDiagnoser]
[HideColumns("Job", "RatioSD")]
public class StackingBenchmarks
{
    private PhysicsWorld _world = null!;

    /// <summary>Gets or sets how many boxes tall each tower is.</summary>
    [Params(10, 30)]
    public int TowerHeight { get; set; }

    /// <summary>Gets or sets how many towers there are.</summary>
    [Params(4, 16)]
    public int TowerCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _world = new PhysicsWorld(WorldSettings.Default with
        {
            Gravity = new Vector3(0.0f, -10.0f, 0.0f),

            // Sleep off, deliberately.
            //
            // Box3D puts a body that has stopped moving to sleep and then skips
            // it entirely. A settled scene therefore costs almost nothing to
            // step, and the figure stops responding to body count: 100 bodies
            // and 10,000 bodies both measured about 210 ns, which is the sleep
            // check and nothing else. Keeping every body awake is what a frame
            // costs while the simulation is actually simulating, and that is
            // the number worth publishing.
            EnableSleep = false,
        });

        Body ground = _world.CreateBody(BodyDefinition.Static(new Vector3(0.0f, -0.5f, 0.0f)));
        ground.AddBox(new Box(new Vector3(100.0f, 0.5f, 100.0f)));

        int perSide = (int)MathF.Ceiling(MathF.Sqrt(TowerCount));

        for (int tower = 0; tower < TowerCount; tower++)
        {
            float x = ((tower % perSide) * 4.0f) - (perSide * 2.0f);
            float z = ((tower / perSide) * 4.0f) - (perSide * 2.0f);

            for (int level = 0; level < TowerHeight; level++)
            {
                Body body = _world.CreateBody(BodyDefinition.Dynamic(
                    new Vector3(x, 0.5f + (level * 1.02f), z)));

                body.AddBox(Box.Cube(0.5f));
            }
        }

        // Long settle: a tower that is still shedding energy measures the
        // collapse rather than the steady state.
        for (int i = 0; i < 180; i++)
        {
            _world.Step(1.0f / 60.0f);
        }

        Workload.RequireAwake(_world, TowerCount * TowerHeight, $"{TowerCount}x{TowerHeight} towers");
    }

    [GlobalCleanup]
    public void Cleanup() => _world.Dispose();

    [Benchmark(Description = "Step, stacked")]
    public void Step() => _world.Step(1.0f / 60.0f);
}

/// <summary>
/// Steps chains of revolute joints, which the solver handles separately from
/// contacts.
/// </summary>
[MemoryDiagnoser]
[HideColumns("Job", "RatioSD")]
public class JointBenchmarks
{
    private PhysicsWorld _world = null!;

    /// <summary>Gets or sets the number of links in each chain.</summary>
    [Params(10, 50)]
    public int ChainLength { get; set; }

    /// <summary>Gets or sets how many chains hang in the world.</summary>
    [Params(1, 20)]
    public int ChainCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _world = new PhysicsWorld(WorldSettings.Default with
        {
            Gravity = new Vector3(0.0f, -10.0f, 0.0f),

            // Sleep off, deliberately.
            //
            // Box3D puts a body that has stopped moving to sleep and then skips
            // it entirely. A settled scene therefore costs almost nothing to
            // step, and the figure stops responding to body count: 100 bodies
            // and 10,000 bodies both measured about 210 ns, which is the sleep
            // check and nothing else. Keeping every body awake is what a frame
            // costs while the simulation is actually simulating, and that is
            // the number worth publishing.
            EnableSleep = false,
        });

        for (int chain = 0; chain < ChainCount; chain++)
        {
            float z = chain * 2.0f;

            Body anchor = _world.CreateBody(BodyDefinition.Static(new Vector3(0.0f, 20.0f, z)));
            anchor.AddBox(Box.Cube(0.25f));

            Body previous = anchor;

            for (int link = 0; link < ChainLength; link++)
            {
                Body body = _world.CreateBody(BodyDefinition.Dynamic(
                    new Vector3((link + 1) * 0.6f, 20.0f, z)));

                body.AddBox(new Box(new Vector3(0.25f, 0.1f, 0.1f)));

                Vector3 anchorPoint = new(link * 0.6f, 20.0f, z);

                _ = _world.CreateRevoluteJoint(
                    RevoluteJointDefinition.Hinge(previous, body, anchorPoint, Vector3.UnitZ));

                previous = body;
            }
        }

        for (int i = 0; i < 60; i++)
        {
            _world.Step(1.0f / 60.0f);
        }

        Workload.RequireAwake(_world, ChainCount * ChainLength, $"{ChainCount}x{ChainLength} joint chains");
    }

    [GlobalCleanup]
    public void Cleanup() => _world.Dispose();

    [Benchmark(Description = "Step, jointed")]
    public void Step() => _world.Step(1.0f / 60.0f);
}
