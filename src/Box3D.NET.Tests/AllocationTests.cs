// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using Xunit;

namespace Box3D.Tests;

/// <summary>
/// Holds the hot paths to allocating nothing.
/// </summary>
/// <remarks>
/// <para>
/// The README says the simulation hot path makes no managed allocations, and
/// the design pays a real price for it: query callbacks are structs rather than
/// delegates, events are enumerated in place rather than copied into arrays,
/// and body names are kept off the common path so that no stack buffer is
/// zeroed. None of that is worth anything if it quietly regresses.
/// </para>
/// <para>
/// The benchmarks measure the same thing, but a benchmark reports a number and
/// a test fails a build. A single boxed enumerator or captured closure
/// reintroduced by a well-meaning refactor shows up here, on the pull request,
/// rather than in a game's frame-time graph.
/// </para>
/// <para>
/// <c>GC.GetAllocatedBytesForCurrentThread</c> is exact, not sampled, and is
/// per-thread, so it is not disturbed by other tests. What does disturb it is
/// the JIT: the first calls through a path allocate while tiered compilation
/// promotes it. Every measurement below therefore runs the same code many times
/// first and only then starts counting.
/// </para>
/// </remarks>
[Collection(NativeCollection.Name)]
public class AllocationTests
{
    /// <summary>How many times a path runs before anything is measured.</summary>
    /// <remarks>
    /// Enough for tiered compilation to have settled. The default promotion
    /// threshold is 30 calls, and a loop containing one is promoted sooner via
    /// on-stack replacement; 200 is comfortably past both.
    /// </remarks>
    private const int WarmUp = 200;

    /// <summary>How many times a path runs while being measured.</summary>
    /// <remarks>
    /// Several iterations rather than one, so that an allocation of a few bytes
    /// every so often is caught as readily as one on every call.
    /// </remarks>
    private const int Measured = 100;

    /// <summary>
    /// Runs an operation and asserts that the managed heap did not grow.
    /// </summary>
    /// <param name="what">What the operation is, for the failure message.</param>
    /// <param name="operation">
    /// The operation. Called by reference to a struct rather than as a delegate,
    /// so that invoking it is not itself an allocation.
    /// </param>
    private static void AssertNoAllocation<TOperation>(string what, ref TOperation operation)
        where TOperation : struct, IOperation
    {
        for (int i = 0; i < WarmUp; i++)
        {
            operation.Run();
        }

        // A collection first, so that the measurement does not begin partway
        // through an allocation context that is about to be reset.
        GC.Collect();
        GC.WaitForPendingFinalizers();

        long before = GC.GetAllocatedBytesForCurrentThread();

        for (int i = 0; i < Measured; i++)
        {
            operation.Run();
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(
            allocated == 0,
            $"{what} allocated {allocated} bytes over {Measured} iterations. The simulation hot path is " +
            "documented as making no managed allocations; a boxed enumerator, a captured closure or a " +
            "delegate is the usual cause.");
    }

    /// <summary>An operation measured for allocations.</summary>
    private interface IOperation
    {
        /// <summary>Runs the operation once.</summary>
        void Run();
    }

    // ----------------------------------------------------------------- step

    private struct StepOperation : IOperation
    {
        public PhysicsWorld World;

        public readonly void Run() => World.Step(1.0f / 60.0f);
    }

    [NativeFact]
    public void SteppingAllocatesNothing()
    {
        using var world = BuildScene();
        var operation = new StepOperation { World = world };

        AssertNoAllocation("World.Step", ref operation);
    }

    // --------------------------------------------------------- body access

    private struct BodyReadOperation : IOperation
    {
        public Body Body;
        public Vector3 Sink;

        public void Run()
        {
            Sink += Body.Position;
            Sink += Body.LinearVelocity;
            Sink += Body.AngularVelocity;
            Sink += Body.CenterOfMass;
            Sink += new Vector3(Body.Mass, Body.GravityScale, Body.LinearDamping);
            Sink += Body.IsAwake ? Vector3.One : Vector3.Zero;
            Sink += new Vector3((float)Body.Type, Body.ShapeCount, Body.UserData);
        }
    }

    [NativeFact]
    public void ReadingBodyStateAllocatesNothing()
    {
        using var world = BuildScene();
        Body body = world.CreateDynamicBody(new Vector3(0.0f, 20.0f, 0.0f));
        body.AddSphere(new Sphere(0.5f));

        var operation = new BodyReadOperation { Body = body };

        AssertNoAllocation("Body state reads", ref operation);
    }

    private struct BodyWriteOperation : IOperation
    {
        public Body Body;

        public readonly void Run()
        {
            Body.LinearVelocity = new Vector3(1.0f, 0.0f, 0.0f);
            Body.AngularVelocity = Vector3.Zero;
            Body.GravityScale = 1.0f;
            Body.UserData = 42;
            Body.ApplyForceToCenter(new Vector3(0.0f, 1.0f, 0.0f));
            Body.ApplyImpulseToCenter(Vector3.Zero);
            Body.ApplyTorque(Vector3.Zero);
        }
    }

    [NativeFact]
    public void WritingBodyStateAllocatesNothing()
    {
        using var world = BuildScene();
        Body body = world.CreateDynamicBody(new Vector3(0.0f, 20.0f, 0.0f));
        body.AddSphere(new Sphere(0.5f));

        var operation = new BodyWriteOperation { Body = body };

        AssertNoAllocation("Body state writes", ref operation);
    }

    private struct BodyLifecycleOperation : IOperation
    {
        public PhysicsWorld World;

        public readonly void Run()
        {
            Body body = World.CreateDynamicBody(new Vector3(0.0f, 30.0f, 0.0f));
            body.AddSphere(new Sphere(0.25f));
            body.Destroy();
        }
    }

    /// <summary>
    /// Creating and destroying a body is on the hot path for anything that
    /// spawns projectiles or debris.
    /// </summary>
    [NativeFact]
    public void CreatingAndDestroyingBodiesAllocatesNothing()
    {
        using var world = BuildScene();
        var operation = new BodyLifecycleOperation { World = world };

        AssertNoAllocation("body create and destroy", ref operation);
    }

    // -------------------------------------------------------------- shapes

    private struct ShapeAccessOperation : IOperation
    {
        public Shape Shape;
        public float Sink;

        public void Run()
        {
            Sink += Shape.Friction;
            Sink += Shape.Restitution;
            Sink += Shape.Density;
            Sink += Shape.IsSensor ? 1.0f : 0.0f;
            Sink += Shape.Bounds.Min.X;
            Sink += Shape.Body.Position.Y;
            Sink += Shape.UserData;
            Shape.Friction = 0.6f;
        }
    }

    [NativeFact]
    public void ShapeAccessAllocatesNothing()
    {
        using var world = BuildScene();
        Body body = world.CreateDynamicBody(new Vector3(0.0f, 20.0f, 0.0f));
        Shape shape = body.AddSphere(new Sphere(0.5f));

        var operation = new ShapeAccessOperation { Shape = shape };

        AssertNoAllocation("Shape access", ref operation);
    }

    // ------------------------------------------------------------- queries

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

    private struct RaycastClosestOperation : IOperation
    {
        public PhysicsWorld World;
        public int Hits;

        public void Run()
        {
            RaycastHit hit = World.RaycastClosest(new Vector3(-25.0f, 0.5f, 0.0f), new Vector3(60.0f, 0.0f, 0.0f));
            Hits += hit.Hit ? 1 : 0;
        }
    }

    [NativeFact]
    public void RaycastClosestAllocatesNothing()
    {
        using var world = BuildScene();
        var operation = new RaycastClosestOperation { World = world };

        AssertNoAllocation("RaycastClosest", ref operation);

        Assert.True(operation.Hits > 0, "the ray must actually hit something, or nothing is being measured");
    }

    private struct RaycastCallbackOperation : IOperation
    {
        public PhysicsWorld World;
        public int Hits;

        public void Run()
        {
            CountHits all = default;
            World.Raycast(new Vector3(-25.0f, 0.5f, 0.0f), new Vector3(60.0f, 0.0f, 0.0f), ref all);

            NearestHit nearest = default;
            World.Raycast(new Vector3(-25.0f, 0.5f, 0.0f), new Vector3(60.0f, 0.0f, 0.0f), ref nearest);

            Hits += all.Count;
        }
    }

    /// <summary>
    /// The reason query callbacks are structs rather than delegates. A delegate
    /// here would allocate a closure and a GC handle on every call.
    /// </summary>
    [NativeFact]
    public void RaycastWithACallbackAllocatesNothing()
    {
        using var world = BuildScene();
        var operation = new RaycastCallbackOperation { World = world };

        AssertNoAllocation("Raycast with a struct callback", ref operation);

        Assert.True(operation.Hits > 0, "the ray must actually hit something, or nothing is being measured");
    }

    private struct OverlapOperation : IOperation
    {
        public PhysicsWorld World;
        public int Found;

        public void Run()
        {
            CountOverlaps callback = default;
            World.OverlapBox(Vector3.Zero, new Vector3(30.0f, 30.0f, 30.0f), ref callback);
            Found += callback.Count;
        }
    }

    [NativeFact]
    public void OverlapAllocatesNothing()
    {
        using var world = BuildScene();
        var operation = new OverlapOperation { World = world };

        AssertNoAllocation("OverlapBox with a struct callback", ref operation);

        Assert.True(operation.Found > 0, "the overlap must actually find shapes, or nothing is being measured");
    }

    // -------------------------------------------------------------- events

    private struct DrainEventsOperation : IOperation
    {
        public PhysicsWorld World;
        public int Seen;

        public void Run()
        {
            WorldEvents events = World.Events;

            foreach (BodyMoveEvent moved in events.BodyMoves)
            {
                Seen += moved.FellAsleep ? 0 : 1;
            }

            foreach (ContactBeginEvent begin in events.ContactBegins)
            {
                Seen += begin.ShapeA.IsValid ? 1 : 0;
            }

            foreach (ContactEndEvent end in events.ContactEnds)
            {
                Seen += end.ShapeA.IsValid ? 1 : 0;
            }

            foreach (ContactHitEvent hit in events.ContactHits)
            {
                Seen += hit.ApproachSpeed > 0.0f ? 1 : 0;
            }

            foreach (SensorBeginEvent sensor in events.SensorBegins)
            {
                Seen += sensor.Sensor.IsValid ? 1 : 0;
            }

            foreach (SensorEndEvent sensor in events.SensorEnds)
            {
                Seen += sensor.Sensor.IsValid ? 1 : 0;
            }
        }
    }

    /// <summary>
    /// Walking every event list, which is what a frame does to push transforms
    /// into game objects.
    /// </summary>
    /// <remarks>
    /// The enumerators are <c>ref struct</c>s over engine memory precisely so
    /// that this costs nothing. A <c>foreach</c> over an interface, or an
    /// enumerator that boxed, would show up here immediately.
    /// </remarks>
    [NativeFact]
    public void DrainingEventsAllocatesNothing()
    {
        using var world = BuildScene();
        world.Step(1.0f / 60.0f);

        var operation = new DrainEventsOperation { World = world };

        AssertNoAllocation("event enumeration", ref operation);

        Assert.True(operation.Seen > 0, "there must actually be events, or nothing is being measured");
    }

    private struct StepAndDrainOperation : IOperation
    {
        public PhysicsWorld World;
        public int Seen;

        public void Run()
        {
            World.Step(1.0f / 60.0f);

            foreach (BodyMoveEvent moved in World.Events.BodyMoves)
            {
                Seen += moved.Body.IsValid ? 1 : 0;
            }
        }
    }

    /// <summary>A whole frame: step, then push the transforms out.</summary>
    [NativeFact]
    public void AWholeFrameAllocatesNothing()
    {
        using var world = BuildScene();
        var operation = new StepAndDrainOperation { World = world };

        AssertNoAllocation("step and drain", ref operation);

        Assert.True(operation.Seen > 0, "bodies must actually be moving, or nothing is being measured");
    }

    // -------------------------------------------------------------- joints

    private struct JointAccessOperation : IOperation
    {
        public RevoluteJoint Hinge;
        public float Sink;

        public void Run()
        {
            Sink += Hinge.Angle;
            Sink += Hinge.MotorSpeed;
            Sink += Hinge.MaxMotorTorque;
            Sink += Hinge.MotorTorque;
            Sink += Hinge.AsJoint.ConstraintForce.X;
            Sink += Hinge.AsJoint.LinearSeparation;
            Sink += (float)Hinge.AsJoint.Type;
            Hinge.MotorSpeed = 1.0f;
        }
    }

    [NativeFact]
    public void JointAccessAllocatesNothing()
    {
        using var world = BuildScene();

        Body anchor = world.CreateStaticBody(new Vector3(0.0f, 25.0f, 0.0f));
        Body arm = world.CreateDynamicBody(new Vector3(1.0f, 25.0f, 0.0f));
        arm.AddSphere(new Sphere(0.3f));

        RevoluteJoint hinge = world.CreateRevoluteJoint(
            RevoluteJointDefinition.Hinge(anchor, arm, new Vector3(0.0f, 25.0f, 0.0f), Vector3.UnitZ));

        world.Step(1.0f / 60.0f);

        var operation = new JointAccessOperation { Hinge = hinge };

        AssertNoAllocation("joint access", ref operation);
    }

    // ----------------------------------------------------- character mover

    private struct GatherPlanes : ICharacterCollisionCallback
    {
        public int Count;

        public bool OnContact(in CharacterContact contact)
        {
            Count++;
            return true;
        }
    }

    private struct CharacterOperation : IOperation
    {
        public PhysicsWorld World;
        public int Contacts;

        public void Run()
        {
            GatherPlanes gather = default;
            World.CollideCapsule(Capsule.Upright(1.0f, 0.3f), new Vector3(0.0f, 0.45f, 0.0f), ref gather);
            Contacts += gather.Count;

            _ = World.CastCapsule(
                Capsule.Upright(1.0f, 0.3f),
                new Vector3(0.0f, 0.45f, 0.0f),
                new Vector3(1.0f, 0.0f, 0.0f));
        }
    }

    [NativeFact]
    public void CharacterCollisionAllocatesNothing()
    {
        using var world = BuildScene();
        var operation = new CharacterOperation { World = world };

        AssertNoAllocation("character capsule collide and cast", ref operation);

        Assert.True(operation.Contacts > 0, "the capsule must actually touch the ground, or nothing is measured");
    }

    // ---------------------------------------------------------------- scene

    /// <summary>
    /// A scene with ground, a spread of dynamic bodies and contact events on, so
    /// that every path above has something real to work on.
    /// </summary>
    private static PhysicsWorld BuildScene()
    {
        var world = new PhysicsWorld(WorldSettings.Default with
        {
            Gravity = new Vector3(0.0f, -10.0f, 0.0f),

            // Sleeping bodies raise no move events and are skipped by the step,
            // which would leave several of these tests measuring nothing.
            EnableSleep = false,
        });

        Body ground = world.CreateStaticBody(new Vector3(0.0f, -0.5f, 0.0f));
        ground.AddBox(
            new Box(new Vector3(50.0f, 0.5f, 50.0f)),
            ShapeDefinition.Default with { EnableContactEvents = true });

        for (int i = 0; i < 40; i++)
        {
            Body body = world.CreateDynamicBody(new Vector3((i * 1.2f) - 20.0f, 1.0f, 0.0f));
            body.AddBox(Box.Cube(0.5f), ShapeDefinition.Default with { EnableContactEvents = true });
        }

        for (int frame = 0; frame < 30; frame++)
        {
            world.Step(1.0f / 60.0f);
        }

        return world;
    }
}
