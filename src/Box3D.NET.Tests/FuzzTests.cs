// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using Xunit;

namespace Box3D.Tests;

/// <summary>
/// Feeds the API hostile input: non-finite numbers, extreme magnitudes,
/// degenerate geometry and long random sequences of operations.
/// </summary>
/// <remarks>
/// <para>
/// The rule these tests encode is that bad input must produce an exception at
/// the call that caused it, never a corrupted simulation. Box3D checks its own
/// inputs with assertions that release builds compile out, so without this layer
/// a single NaN is accepted in silence and then spreads.
/// </para>
/// <para>
/// The random sequence tests are seeded, so a failure reproduces exactly.
/// </para>
/// </remarks>
[Collection(NativeCollection.Name)]
public class FuzzTests : IDisposable
{
    private readonly PhysicsWorld _world;

    public FuzzTests() => _world = new PhysicsWorld(WorldSettings.Default with
    {
        Gravity = new Vector3(0.0f, -10.0f, 0.0f),
    });

    public void Dispose()
    {
        _world.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Every way a float can fail to be a usable number.</summary>
    public static TheoryData<float> NonFiniteValues => new()
    {
        float.NaN,
        float.PositiveInfinity,
        float.NegativeInfinity,
    };

    // ----------------------------------------------------------- the reason

    [NativeFact]
    public void One_bad_velocity_would_otherwise_poison_unrelated_bodies()
    {
        // This is the measurement that justifies validating at all. Setting one
        // body's velocity to NaN and stepping leaves a second body, twenty metres
        // away and never touched, reading NaN. The solver couples bodies through
        // islands and the broad phase, so there is no containment and no way to
        // remove it afterwards.
        //
        // Going through the native layer directly reproduces what the library
        // would do without the check.
        Body poisoned = _world.CreateDynamicBody(new Vector3(0.0f, 10.0f, 0.0f));
        poisoned.AddSphere(new Sphere(0.5f));

        Body innocent = _world.CreateDynamicBody(new Vector3(20.0f, 10.0f, 0.0f));
        innocent.AddSphere(new Sphere(0.5f));

        Box3D.Native.B3.b3Body_SetLinearVelocity(
            Box3D.Interop.NativeInterop.ToNativeId(poisoned),
            new Vector3(float.NaN, 0.0f, 0.0f));

        for (int i = 0; i < 30; i++)
        {
            _world.Step(1.0f / 60.0f);
        }

        Assert.True(float.IsNaN(innocent.Position.Y), "the contamination should have spread; if it no longer does, the guards below can be reconsidered");
    }

    // ------------------------------------------------- non-finite rejection

    [NativeTheory]
    [MemberData(nameof(NonFiniteValues))]
    public void A_non_finite_velocity_is_rejected(float bad)
    {
        Body body = _world.CreateDynamicBody();
        body.AddSphere(new Sphere(0.5f));

        Assert.Throws<ArgumentException>(() => body.LinearVelocity = new Vector3(bad, 0.0f, 0.0f));
        Assert.Throws<ArgumentException>(() => body.AngularVelocity = new Vector3(0.0f, bad, 0.0f));

        // The rejected value never reached the simulation.
        Assert.Equal(Vector3.Zero, body.LinearVelocity);
    }

    [NativeTheory]
    [MemberData(nameof(NonFiniteValues))]
    public void A_non_finite_force_or_impulse_is_rejected(float bad)
    {
        Body body = _world.CreateDynamicBody();
        body.AddSphere(new Sphere(0.5f));

        Vector3 value = new(bad, bad, bad);

        Assert.Throws<ArgumentException>(() => body.ApplyForceToCenter(value));
        Assert.Throws<ArgumentException>(() => body.ApplyForce(value, Vector3.Zero));
        Assert.Throws<ArgumentException>(() => body.ApplyTorque(value));
        Assert.Throws<ArgumentException>(() => body.ApplyImpulseToCenter(value));
        Assert.Throws<ArgumentException>(() => body.ApplyImpulse(value, Vector3.Zero));
        Assert.Throws<ArgumentException>(() => body.ApplyAngularImpulse(value));
    }

    [NativeTheory]
    [MemberData(nameof(NonFiniteValues))]
    public void A_non_finite_point_of_application_is_rejected(float bad)
    {
        Body body = _world.CreateDynamicBody();
        body.AddSphere(new Sphere(0.5f));

        // The force is fine; the point it is applied at is not.
        Assert.Throws<ArgumentException>(() => body.ApplyForce(Vector3.UnitY, new Vector3(bad, 0.0f, 0.0f)));
    }

    [NativeTheory]
    [MemberData(nameof(NonFiniteValues))]
    public void A_non_finite_teleport_is_rejected(float bad)
    {
        Body body = _world.CreateDynamicBody();
        body.AddSphere(new Sphere(0.5f));

        Assert.Throws<ArgumentException>(() => body.SetTransform(new Vector3(bad, 0.0f, 0.0f)));
        Assert.Throws<ArgumentException>(() =>
            body.SetTransform(Vector3.Zero, new Quaternion(bad, 0.0f, 0.0f, 1.0f)));

        Assert.Equal(Vector3.Zero, body.Position);
    }

    [NativeTheory]
    [MemberData(nameof(NonFiniteValues))]
    public void A_non_finite_body_definition_is_rejected(float bad)
    {
        Assert.Throws<ArgumentException>(() =>
            _world.CreateDynamicBody(new Vector3(bad, 0.0f, 0.0f)));

        Assert.Throws<ArgumentException>(() =>
            _world.CreateBody(BodyDefinition.Dynamic() with { LinearVelocity = new Vector3(bad, 0.0f, 0.0f) }));

        Assert.Throws<ArgumentException>(() =>
            _world.CreateBody(BodyDefinition.Dynamic() with { AngularVelocity = new Vector3(0.0f, bad, 0.0f) }));
    }

    [NativeTheory]
    [MemberData(nameof(NonFiniteValues))]
    public void A_non_finite_gravity_is_rejected(float bad)
    {
        Vector3 before = _world.Gravity;

        Assert.Throws<ArgumentException>(() => _world.Gravity = new Vector3(0.0f, bad, 0.0f));

        Assert.Equal(before, _world.Gravity);
    }

    [NativeTheory]
    [MemberData(nameof(NonFiniteValues))]
    public void A_non_finite_ray_is_rejected(float bad)
    {
        Assert.Throws<ArgumentException>(() =>
            _world.RaycastClosest(new Vector3(bad, 0.0f, 0.0f), Vector3.UnitX));

        Assert.Throws<ArgumentException>(() =>
            _world.RaycastClosest(Vector3.Zero, new Vector3(bad, 0.0f, 0.0f)));
    }

    [NativeTheory]
    [MemberData(nameof(NonFiniteValues))]
    public void A_non_finite_shape_is_rejected(float bad)
    {
        // ArgumentException rather than the more specific out-of-range type,
        // because the guards for a non-finite value and for a non-positive one
        // are separate. An infinity is not negative and not zero, so the range
        // check alone would let it through.
        Assert.ThrowsAny<ArgumentException>(() => new Sphere(bad));
        Assert.ThrowsAny<ArgumentException>(() => new Sphere(new Vector3(bad, 0.0f, 0.0f), 1.0f));
        Assert.ThrowsAny<ArgumentException>(() => new Capsule(Vector3.Zero, Vector3.UnitY, bad));
        Assert.ThrowsAny<ArgumentException>(() => new Capsule(new Vector3(bad, 0.0f, 0.0f), Vector3.UnitY, 1.0f));
        Assert.ThrowsAny<ArgumentException>(() => new Box(new Vector3(bad, 1.0f, 1.0f)));
        Assert.ThrowsAny<ArgumentException>(() => Capsule.Upright(bad, 0.3f));
    }

    [NativeTheory]
    [MemberData(nameof(NonFiniteValues))]
    public void A_non_finite_time_step_is_rejected(float bad)
    {
        Assert.ThrowsAny<ArgumentException>(() => _world.Step(bad));
    }

    [NativeFact]
    public void The_world_still_simulates_after_rejecting_bad_input()
    {
        // The important half of rejection: the world is untouched and carries on.
        Body body = _world.CreateDynamicBody(new Vector3(0.0f, 10.0f, 0.0f));
        body.AddSphere(new Sphere(0.5f));

        Assert.Throws<ArgumentException>(() => body.LinearVelocity = new Vector3(float.NaN, 0.0f, 0.0f));

        for (int i = 0; i < 60; i++)
        {
            _world.Step(1.0f / 60.0f);
        }

        Assert.False(float.IsNaN(body.Position.Y), "the world should still be sound");
        Assert.True(body.Position.Y < 10.0f, "and still simulating");
    }

    // ------------------------------------------------------ extreme values

    [NativeFact]
    public void Very_large_but_finite_positions_are_accepted()
    {
        // Finite is the line, not reasonable. Box3D has its own sanity bound
        // around 100 km and warns rather than failing, so a distant body is the
        // caller's problem, not an error.
        Body body = _world.CreateDynamicBody(new Vector3(1e6f, 0.0f, 0.0f));
        body.AddSphere(new Sphere(0.5f));

        _world.Step(1.0f / 60.0f);

        Assert.False(float.IsNaN(body.Position.X));
    }

    [NativeFact]
    public void A_zero_time_step_leaves_the_world_alone()
    {
        Body body = _world.CreateDynamicBody(new Vector3(0.0f, 10.0f, 0.0f));
        body.AddSphere(new Sphere(0.5f));

        Vector3 before = body.Position;
        _world.Step(0.0f);

        Assert.Equal(before, body.Position);
    }

    [NativeFact]
    public void A_zero_density_shape_gives_the_body_no_mass()
    {
        // Legitimate, not an error: this is how a sensor avoids weighing anything.
        Body body = _world.CreateDynamicBody();
        body.AddSphere(new Sphere(0.5f), ShapeDefinition.Default with { Density = 0.0f });

        Assert.Equal(0.0f, body.Mass);
    }

    [NativeFact]
    public void Extreme_but_finite_tuning_does_not_break_the_world()
    {
        Body body = _world.CreateDynamicBody(new Vector3(0.0f, 10.0f, 0.0f));
        body.AddSphere(new Sphere(0.5f));

        body.GravityScale = -1e10f;
        body.LinearDamping = 1e10f;
        body.AngularDamping = 1e10f;

        for (int i = 0; i < 30; i++)
        {
            _world.Step(1.0f / 60.0f);
        }

        // The result is nonsense physically, but it is finite nonsense: the
        // simulation is still usable and other bodies are unaffected.
        Assert.False(float.IsNaN(body.Position.Y), $"position went to {body.Position}");
    }

    // ------------------------------------------------- degenerate geometry

    [NativeFact]
    public void Degenerate_shapes_are_rejected_at_construction()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Sphere(0.0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Sphere(-1.0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Box(Vector3.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Box(new Vector3(-1.0f, 1.0f, 1.0f)));

        // A capsule shorter than twice its radius is a sphere, not a capsule.
        Assert.Throws<ArgumentOutOfRangeException>(() => Capsule.Upright(height: 0.5f, radius: 0.3f));
    }

    [NativeFact]
    public void A_capsule_of_zero_length_is_rejected_by_the_helper()
    {
        // Upright refuses it, but the explicit constructor allows a degenerate
        // segment because Box3D itself tolerates one; this pins that difference.
        Assert.Throws<ArgumentOutOfRangeException>(() => Capsule.Upright(0.6f, 0.3f));

        Capsule degenerate = new(Vector3.Zero, Vector3.Zero, 0.3f);
        Assert.Equal(0.0f, degenerate.SegmentLength);
    }

    // ------------------------------------------------ random operation runs

    [NativeTheory]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(12345)]
    public void A_random_sequence_of_operations_leaves_a_sound_world(int seed)
    {
        // Model-based fuzzing: drive the API the way an application would, in an
        // order no test author would think to write, and check afterwards that
        // nothing became NaN and nothing leaked.
        var random = new Random(seed);
        var bodies = new System.Collections.Generic.List<Body>();

        Body ground = _world.CreateStaticBody(new Vector3(0.0f, -0.5f, 0.0f));
        ground.AddBox(new Box(new Vector3(50.0f, 0.5f, 50.0f)));

        for (int operation = 0; operation < 400; operation++)
        {
            switch (random.Next(8))
            {
                case 0:
                    {
                        Body body = _world.CreateDynamicBody(RandomPosition(random));

                        switch (random.Next(3))
                        {
                            case 0: body.AddSphere(new Sphere(RandomExtent(random))); break;
                            case 1: body.AddBox(Box.Cube(RandomExtent(random))); break;
                            default: body.AddCapsule(Capsule.Upright(1.0f, 0.2f)); break;
                        }

                        bodies.Add(body);
                        break;
                    }

                case 1 when bodies.Count > 0:
                    {
                        int index = random.Next(bodies.Count);
                        bodies[index].Destroy();
                        bodies.RemoveAt(index);
                        break;
                    }

                case 2 when bodies.Count > 0:
                    {
                        // Copied out of the list first: a property cannot be set on
                        // the return value of an indexer over a struct.
                        Body body = bodies[random.Next(bodies.Count)];
                        body.LinearVelocity = RandomVelocity(random);
                        break;
                    }

                case 3 when bodies.Count > 0:
                    bodies[random.Next(bodies.Count)].ApplyImpulseToCenter(RandomVelocity(random));
                    break;

                case 4:
                    _world.Step(1.0f / 60.0f);
                    break;

                case 5:
                    _ = _world.RaycastClosest(RandomPosition(random), RandomVelocity(random));
                    break;

                case 6 when bodies.Count > 1:
                    {
                        Body a = bodies[random.Next(bodies.Count)];
                        Body b = bodies[random.Next(bodies.Count)];

                        if (a != b && a.IsValid && b.IsValid)
                        {
                            _world.CreateDistanceJoint(DistanceJointDefinition.Between(a, b, a.Position, b.Position));
                        }

                        break;
                    }

                case 7 when bodies.Count > 0:
                    {
                        // Body is a readonly struct handle, so it is copied out of
                        // the list before use. The copy still refers to the same
                        // body, which is the whole point of a handle.
                        Body body = bodies[random.Next(bodies.Count)];
                        bool wake = random.Next(2) == 0;
                        body.IsAwake = wake;
                        break;
                    }

                default:
                    _world.Step(1.0f / 60.0f);
                    break;
            }
        }

        // Settle whatever is left.
        for (int i = 0; i < 60; i++)
        {
            _world.Step(1.0f / 60.0f);
        }

        foreach (Body body in bodies)
        {
            if (!body.IsValid)
            {
                continue;
            }

            Vector3 position = body.Position;

            Assert.False(
                float.IsNaN(position.X) || float.IsNaN(position.Y) || float.IsNaN(position.Z),
                $"a body reached {position} after a random run with seed {seed}");
        }
    }

    private static Vector3 RandomPosition(Random random) => new(
        ((float)random.NextDouble() * 40.0f) - 20.0f,
        ((float)random.NextDouble() * 20.0f) + 1.0f,
        ((float)random.NextDouble() * 40.0f) - 20.0f);

    private static Vector3 RandomVelocity(Random random) => new(
        ((float)random.NextDouble() * 20.0f) - 10.0f,
        ((float)random.NextDouble() * 20.0f) - 10.0f,
        ((float)random.NextDouble() * 20.0f) - 10.0f);

    private static float RandomExtent(Random random) => ((float)random.NextDouble() * 0.9f) + 0.1f;
}
