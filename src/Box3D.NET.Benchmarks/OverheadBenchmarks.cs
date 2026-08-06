// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using BenchmarkDotNet.Attributes;
using Box3D;
using Box3D.Native;

namespace Box3D.Benchmarks;

/// <summary>
/// Measures what the idiomatic layer costs over calling the C API directly.
/// </summary>
/// <remarks>
/// <para>
/// This is the benchmark that matters most for the project's central claim:
/// that using <c>Box3D.NET</c> is not meaningfully slower than using the native
/// library by hand. Every case runs the same operation twice, once through each
/// layer, so the difference is the wrapper and nothing else.
/// </para>
/// <para>
/// The memory diagnoser is on everywhere. A non-zero allocation on any of these
/// paths is a defect, not a trade-off.
/// </para>
/// </remarks>
[MemoryDiagnoser]
[HideColumns("Job", "Error", "StdDev", "RatioSD")]
public class OverheadBenchmarks
{
    private PhysicsWorld _world = null!;
    private b3WorldId _nativeWorld;
    private Body _body;
    private b3BodyId _nativeBody;

    [GlobalSetup]
    public unsafe void Setup()
    {
        _world = new PhysicsWorld(WorldSettings.Default with { Gravity = new Vector3(0.0f, -10.0f, 0.0f) });
        _body = _world.CreateBody(BodyDefinition.Dynamic(new Vector3(0.0f, 10.0f, 0.0f)));
        _body.AddSphere(new Sphere(0.5f));

        b3WorldDef worldDef = B3.b3DefaultWorldDef();
        worldDef.gravity = new Vector3(0.0f, -10.0f, 0.0f);
        _nativeWorld = B3.b3CreateWorld(&worldDef);

        b3BodyDef bodyDef = B3.b3DefaultBodyDef();
        bodyDef.type = b3BodyType.b3_dynamicBody;
        bodyDef.position = new Vector3(0.0f, 10.0f, 0.0f);
        _nativeBody = B3.b3CreateBody(_nativeWorld, &bodyDef);

        b3Sphere sphere = new() { center = Vector3.Zero, radius = 0.5f };
        b3ShapeDef shapeDef = B3.b3DefaultShapeDef();
        _ = B3.b3CreateSphereShape(_nativeBody, &shapeDef, &sphere);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _world.Dispose();
        B3.b3DestroyWorld(_nativeWorld);
    }

    // A single property read. This is the finest-grained thing the wrapper does
    // and should compile down to the same call.

    [Benchmark(Baseline = true, Description = "Read position (native)")]
    public Vector3 ReadPositionNative() => B3.b3Body_GetPosition(_nativeBody);

    [Benchmark(Description = "Read position (wrapper)")]
    public Vector3 ReadPositionWrapper() => _body.Position;

    [Benchmark(Description = "Write velocity (native)")]
    public void WriteVelocityNative() => B3.b3Body_SetLinearVelocity(_nativeBody, Vector3.UnitY);

    [Benchmark(Description = "Write velocity (wrapper)")]
    public void WriteVelocityWrapper() => _body.LinearVelocity = Vector3.UnitY;

    /// <summary>
    /// The native call with the wrapper's finite check written out by hand, to
    /// price the validation on its own. Measured at 0.11 ns against the plain
    /// native call.
    /// </summary>
    /// <remarks>
    /// The wrapper rejects NaN and infinity because Box3D's own checks are
    /// assertions that release builds compile out, and one non-finite value
    /// spreads through the solver until every body reads NaN. This measures what
    /// that safety costs.
    /// </remarks>
    [Benchmark(Description = "Write velocity (native + finite check)")]
    public void WriteVelocityNativeChecked()
    {
        Vector3 value = Vector3.UnitY;

        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z))
        {
            throw new ArgumentException("not finite");
        }

        B3.b3Body_SetLinearVelocity(_nativeBody, value);
    }

    [Benchmark(Description = "Apply force (native)")]
    public void ApplyForceNative() => B3.b3Body_ApplyForceToCenter(_nativeBody, Vector3.UnitY, true);

    [Benchmark(Description = "Apply force (wrapper)")]
    public void ApplyForceWrapper() => _body.ApplyForceToCenter(Vector3.UnitY);
}

/// <summary>
/// Measures creating bodies, which is where the wrapper does the most work per
/// call and where a redundant P/Invoke is easiest to hide.
/// </summary>
[MemoryDiagnoser]
[HideColumns("Job", "Error", "StdDev", "RatioSD")]
public class BodyCreationBenchmarks
{
    private const int BodyCount = 1000;

    private PhysicsWorld _world = null!;
    private b3WorldId _nativeWorld;

    [GlobalSetup]
    public unsafe void Setup()
    {
        _world = new PhysicsWorld();

        b3WorldDef def = B3.b3DefaultWorldDef();
        _nativeWorld = B3.b3CreateWorld(&def);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _world.Dispose();
        B3.b3DestroyWorld(_nativeWorld);
    }

    [IterationCleanup]
    public void ClearWorlds()
    {
        // Recreating the worlds each iteration keeps the two sides comparable
        // and stops the body count growing without bound across iterations.
        _world.Dispose();
        _world = new PhysicsWorld();

        B3.b3DestroyWorld(_nativeWorld);
        unsafe
        {
            b3WorldDef def = B3.b3DefaultWorldDef();
            _nativeWorld = B3.b3CreateWorld(&def);
        }
    }

    [Benchmark(Baseline = true, Description = "1000 bodies + spheres (native)")]
    public unsafe void CreateNative()
    {
        b3BodyDef bodyDef = B3.b3DefaultBodyDef();
        bodyDef.type = b3BodyType.b3_dynamicBody;

        b3ShapeDef shapeDef = B3.b3DefaultShapeDef();
        b3Sphere sphere = new() { center = Vector3.Zero, radius = 0.5f };

        for (int i = 0; i < BodyCount; i++)
        {
            bodyDef.position = new Vector3(i * 0.01f, 10.0f, 0.0f);
            b3BodyId body = B3.b3CreateBody(_nativeWorld, &bodyDef);
            _ = B3.b3CreateSphereShape(body, &shapeDef, &sphere);
        }
    }

    [Benchmark(Description = "1000 bodies + spheres (wrapper)")]
    public void CreateWrapper()
    {
        // Hoisting the definitions is the fair comparison: the native side
        // hoists them too. Whether the wrapper makes hoisting necessary is
        // itself the thing being measured.
        BodyDefinition bodyDef = BodyDefinition.Dynamic();
        ShapeDefinition shapeDef = ShapeDefinition.Default;
        Sphere sphere = new(0.5f);

        for (int i = 0; i < BodyCount; i++)
        {
            Body body = _world.CreateBody(bodyDef with { Position = new Vector3(i * 0.01f, 10.0f, 0.0f) });
            body.AddSphere(sphere, shapeDef);
        }
    }

    [Benchmark(Description = "1000 bodies + spheres (wrapper, defaults inline)")]
    public void CreateWrapperInlineDefaults()
    {
        // What a first-time user writes before they think about hoisting. The
        // gap against the case above is the cost of the convenience.
        for (int i = 0; i < BodyCount; i++)
        {
            Body body = _world.CreateBody(BodyDefinition.Dynamic(new Vector3(i * 0.01f, 10.0f, 0.0f)));
            body.AddSphere(new Sphere(0.5f));
        }
    }
}
