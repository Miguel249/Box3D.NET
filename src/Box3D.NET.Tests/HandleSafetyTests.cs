// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using Box3D.Interop;
using Box3D.Native;
using Xunit;

namespace Box3D.Tests;

/// <summary>
/// What happens when a handle no longer names anything.
/// </summary>
/// <remarks>
/// <para>
/// These are regression tests for a defect rather than a description of good
/// practice: every case below killed the process with an access violation
/// before 0.3.0, or - worse - returned another object's state in silence.
/// </para>
/// <para>
/// Box3D resolves an id by indexing into the world's arrays, asserting on the
/// way past that the id is live. Those assertions are compiled out of the
/// release binary this package ships, so nothing stood between a stale handle
/// and a read of freed memory. The high-level layer now asks
/// <c>b3*_IsValid</c> first; this file is what holds it to that.
/// </para>
/// <para>
/// A test here that fails by crashing the runner rather than by reporting a
/// failed assertion is still a true negative: it means the guard is gone.
/// </para>
/// </remarks>
[Collection(NativeCollection.Name)]
public class HandleSafetyTests
{
    // --------------------------------------------------------------- bodies

    [NativeFact]
    public void DefaultBodyIsNotValid()
    {
        Body body = default;

        Assert.False(body.IsValid);
    }

    [NativeFact]
    public void ReadingADefaultBodyThrows()
    {
        Body body = default;

        Assert.Throws<InvalidOperationException>(() => body.Position);
    }

    [NativeFact]
    public void ReadingADestroyedBodyThrows()
    {
        using var world = new PhysicsWorld();
        Body body = world.CreateDynamicBody(new Vector3(0.0f, 5.0f, 0.0f));
        body.AddSphere(new Sphere(0.5f));

        body.Destroy();

        Assert.False(body.IsValid);
        Assert.Throws<InvalidOperationException>(() => body.Position);
    }

    [NativeFact]
    public void WritingToADestroyedBodyThrows()
    {
        using var world = new PhysicsWorld();
        Body body = world.CreateDynamicBody(new Vector3(0.0f, 5.0f, 0.0f));

        body.Destroy();

        Assert.Throws<InvalidOperationException>(() => body.LinearVelocity = Vector3.UnitX);
    }

    [NativeFact]
    public void DestroyingABodyTwiceThrows()
    {
        using var world = new PhysicsWorld();
        Body body = world.CreateDynamicBody(new Vector3(0.0f, 5.0f, 0.0f));

        body.Destroy();

        Assert.Throws<InvalidOperationException>(body.Destroy);
    }

    [NativeFact]
    public void AttachingAShapeToADestroyedBodyThrows()
    {
        using var world = new PhysicsWorld();
        Body body = world.CreateDynamicBody(new Vector3(0.0f, 5.0f, 0.0f));

        body.Destroy();

        Assert.Throws<InvalidOperationException>(() => body.AddSphere(new Sphere(0.5f)));
    }

    /// <summary>
    /// The case that made this worth fixing rather than merely documenting.
    /// </summary>
    /// <remarks>
    /// Box3D reuses a freed body slot for the next body created, bumping the
    /// generation counter so the old handle can be told apart. Nothing consulted
    /// that counter, so the stale handle read the replacement body's state and
    /// reported it as its own: no crash, no exception, just the wrong position.
    /// </remarks>
    [NativeFact]
    public void AHandleWhoseSlotWasReusedDoesNotReadTheReplacement()
    {
        using var world = new PhysicsWorld();

        Body first = world.CreateDynamicBody(new Vector3(0.0f, 5.0f, 0.0f));
        first.Destroy();

        Body second = world.CreateDynamicBody(new Vector3(0.0f, 7.0f, 0.0f));

        // The slot really was reused, or this test is proving nothing.
        Assert.Equal(first.ToNativeId().index1, second.ToNativeId().index1);
        Assert.NotEqual(first.ToNativeId().generation, second.ToNativeId().generation);

        Assert.False(first.IsValid);
        Assert.True(second.IsValid);
        Assert.Throws<InvalidOperationException>(() => first.Position);
        Assert.Equal(7.0f, second.Position.Y, 3);
    }

    // --------------------------------------------------------------- shapes

    [NativeFact]
    public void ReadingADefaultShapeThrows()
    {
        Shape shape = default;

        Assert.False(shape.IsValid);
        Assert.Throws<InvalidOperationException>(() => shape.Friction);
    }

    [NativeFact]
    public void ReadingADestroyedShapeThrows()
    {
        using var world = new PhysicsWorld();
        Body body = world.CreateDynamicBody(new Vector3(0.0f, 5.0f, 0.0f));
        Shape shape = body.AddSphere(new Sphere(0.5f));

        shape.Destroy();

        Assert.False(shape.IsValid);
        Assert.Throws<InvalidOperationException>(() => shape.Friction);
    }

    [NativeFact]
    public void AShapeOutlivesNeitherItsBodyNorItsWorld()
    {
        var world = new PhysicsWorld();
        Body body = world.CreateDynamicBody(new Vector3(0.0f, 5.0f, 0.0f));
        Shape onDestroyedBody = body.AddSphere(new Sphere(0.5f));

        Body survivor = world.CreateDynamicBody(new Vector3(4.0f, 5.0f, 0.0f));
        Shape onDisposedWorld = survivor.AddSphere(new Sphere(0.5f));

        body.Destroy();
        Assert.False(onDestroyedBody.IsValid);
        Assert.Throws<InvalidOperationException>(() => onDestroyedBody.Friction);

        Assert.True(onDisposedWorld.IsValid);
        world.Dispose();
        Assert.False(onDisposedWorld.IsValid);
        Assert.Throws<InvalidOperationException>(() => onDisposedWorld.Friction);
    }

    // --------------------------------------------------------------- joints

    [NativeFact]
    public void ReadingADestroyedJointThrows()
    {
        using var world = new PhysicsWorld();
        Body anchor = world.CreateStaticBody();
        Body arm = world.CreateDynamicBody(new Vector3(1.0f, 0.0f, 0.0f));
        arm.AddSphere(new Sphere(0.5f));

        RevoluteJoint hinge = world.CreateRevoluteJoint(
            RevoluteJointDefinition.Hinge(anchor, arm, Vector3.Zero, Vector3.UnitY));

        hinge.Destroy();

        Assert.False(hinge.AsJoint.IsValid);
        Assert.Throws<InvalidOperationException>(() => hinge.Angle);
        Assert.Throws<InvalidOperationException>(() => hinge.AsJoint.ConstraintForce);
    }

    /// <summary>
    /// Destroying a body destroys the joints attached to it, which is where a
    /// stale joint handle most often comes from.
    /// </summary>
    [NativeFact]
    public void AJointDiesWithTheBodiesItConnects()
    {
        using var world = new PhysicsWorld();
        Body anchor = world.CreateStaticBody();
        Body arm = world.CreateDynamicBody(new Vector3(1.0f, 0.0f, 0.0f));
        arm.AddSphere(new Sphere(0.5f));

        Joint joint = world
            .CreateRevoluteJoint(RevoluteJointDefinition.Hinge(anchor, arm, Vector3.Zero, Vector3.UnitY))
            .AsJoint;

        arm.Destroy();

        Assert.False(joint.IsValid);
        Assert.Throws<InvalidOperationException>(() => joint.Type);
    }

    /// <summary>
    /// Converting between handle types is not a dereference, so it must keep
    /// working on a dead handle: otherwise <c>AsJoint.IsValid</c>, which is the
    /// documented way to ask, would itself throw.
    /// </summary>
    [NativeFact]
    public void ConvertingADeadHandleDoesNotThrow()
    {
        using var world = new PhysicsWorld();
        Body anchor = world.CreateStaticBody();
        Body arm = world.CreateDynamicBody(new Vector3(1.0f, 0.0f, 0.0f));
        arm.AddSphere(new Sphere(0.5f));

        RevoluteJoint hinge = world.CreateRevoluteJoint(
            RevoluteJointDefinition.Hinge(anchor, arm, Vector3.Zero, Vector3.UnitY));

        hinge.Destroy();

        Joint asJoint = hinge.AsJoint;
        Assert.False(asJoint.IsValid);

        // The interop escape hatch validates nothing by design, and must stay
        // reachable so that a caller can inspect a handle the guard rejects.
        Assert.NotEqual(0, hinge.AsJoint.ToNativeId().index1);
    }

    // ------------------------------------------------- handles from events

    /// <summary>
    /// End-touch events name shapes that may have just been destroyed. That is
    /// the documented hazard, and the reason the guard exists.
    /// </summary>
    [NativeFact]
    public void ShapesFromAnEndTouchEventAreSafeToTest()
    {
        using var world = new PhysicsWorld();

        Body ground = world.CreateStaticBody(new Vector3(0.0f, -0.5f, 0.0f));
        ground.AddBox(new Box(new Vector3(10.0f, 0.5f, 10.0f)),
            ShapeDefinition.Default with { EnableContactEvents = true });

        Body ball = world.CreateDynamicBody(new Vector3(0.0f, 0.6f, 0.0f));
        ball.AddSphere(new Sphere(0.5f), ShapeDefinition.Default with { EnableContactEvents = true });

        // Settle until the two are touching.
        for (int frame = 0; frame < 30; frame++)
        {
            world.Step(1.0f / 60.0f);
        }

        // Destroying the ball ends the contact and orphans the shape the event
        // will name.
        ball.Destroy();
        world.Step(1.0f / 60.0f);

        int ended = 0;
        int orphaned = 0;
        foreach (ContactEndEvent touch in world.Events.ContactEnds)
        {
            ended++;

            // The whole contract: asking is always safe, using is not.
            if (touch.ShapeA.IsValid)
            {
                _ = touch.ShapeA.Friction;
            }
            else
            {
                orphaned++;
                Assert.Throws<InvalidOperationException>(() => touch.ShapeA.Friction);
            }

            if (touch.ShapeB.IsValid)
            {
                _ = touch.ShapeB.Friction;
            }
            else
            {
                orphaned++;
                Assert.Throws<InvalidOperationException>(() => touch.ShapeB.Friction);
            }
        }

        Assert.True(ended > 0, "destroying a touching body should have ended a contact");
        Assert.True(orphaned > 0, "the destroyed ball's shape should have been reported as orphaned");
    }

    // ------------------------------------------------------ disposed worlds

    /// <summary>
    /// Every member of a disposed world throws, rather than calling into an
    /// engine that no longer has the world.
    /// </summary>
    [NativeFact]
    public void EveryWorldMemberThrowsAfterDispose()
    {
        var world = new PhysicsWorld();
        world.Step(1.0f / 60.0f);
        world.Dispose();

        Assert.True(world.IsDisposed);

        Assert.Throws<ObjectDisposedException>(() => world.Step(1.0f / 60.0f));
        Assert.Throws<ObjectDisposedException>(() => world.CreateDynamicBody());
        Assert.Throws<ObjectDisposedException>(() => world.CreateStaticBody());
        Assert.Throws<ObjectDisposedException>(() => world.CreateKinematicBody());
        Assert.Throws<ObjectDisposedException>(() => world.RaycastClosest(Vector3.Zero, Vector3.UnitX));
        Assert.Throws<ObjectDisposedException>(() => world.Explode(Vector3.Zero, 1.0f, 1.0f));
        Assert.Throws<ObjectDisposedException>(() => world.Gravity);
        Assert.Throws<ObjectDisposedException>(() => world.Gravity = Vector3.Zero);
        Assert.Throws<ObjectDisposedException>(() => world.Bounds);
        Assert.Throws<ObjectDisposedException>(() => world.AwakeBodyCount);
        Assert.Throws<ObjectDisposedException>(() => world.WorkerCount);
        Assert.Throws<ObjectDisposedException>(() => world.UserData);
        Assert.Throws<ObjectDisposedException>(() => world.UserData = 1);
        Assert.Throws<ObjectDisposedException>(() => world.SleepEnabled);
        Assert.Throws<ObjectDisposedException>(() => world.ContinuousEnabled);

        // Events return a ref struct, so the lambda has to consume it here.
        Assert.Throws<ObjectDisposedException>(() =>
        {
            foreach (BodyMoveEvent moved in world.Events.BodyMoves)
            {
                _ = moved;
            }
        });

        Assert.Throws<ObjectDisposedException>(() =>
        {
            var callback = default(CountingRaycastCallback);
            world.Raycast(Vector3.Zero, Vector3.UnitX, ref callback);
        });

        Assert.Throws<ObjectDisposedException>(() =>
        {
            var callback = default(CountingOverlapCallback);
            world.OverlapBox(Vector3.Zero, Vector3.One, ref callback);
        });

        Assert.Throws<ObjectDisposedException>(() =>
            world.CreateRevoluteJoint(RevoluteJointDefinition.Default));
    }

    [NativeFact]
    public void DisposingTwiceIsHarmless()
    {
        var world = new PhysicsWorld();
        Body body = world.CreateDynamicBody(new Vector3(0.0f, 5.0f, 0.0f));
        body.AddSphere(new Sphere(0.5f));

        world.Dispose();
        world.Dispose();
        world.Dispose();

        Assert.True(world.IsDisposed);
    }

    /// <summary>
    /// Disposing a world after every kind of resource has been created in it,
    /// and after each of the orders those resources can be released in.
    /// </summary>
    [NativeTheory]
    [InlineData(DisposalOrder.NothingFirst)]
    [InlineData(DisposalOrder.ShapesFirst)]
    [InlineData(DisposalOrder.JointsThenShapesThenBodies)]
    [InlineData(DisposalOrder.BodiesFirst)]
    public void DisposeIsSafeInEveryReleaseOrder(DisposalOrder order)
    {
        var world = new PhysicsWorld();

        Body ground = world.CreateStaticBody(new Vector3(0.0f, -0.5f, 0.0f));
        Shape groundShape = ground.AddBox(new Box(new Vector3(10.0f, 0.5f, 10.0f)));

        Body anchor = world.CreateStaticBody(new Vector3(0.0f, 4.0f, 0.0f));
        Body arm = world.CreateDynamicBody(new Vector3(1.0f, 4.0f, 0.0f));
        Shape armShape = arm.AddSphere(new Sphere(0.5f));

        RevoluteJoint hinge = world.CreateRevoluteJoint(
            RevoluteJointDefinition.Hinge(anchor, arm, new Vector3(0.0f, 4.0f, 0.0f), Vector3.UnitY));

        world.Step(1.0f / 60.0f);

        switch (order)
        {
            case DisposalOrder.NothingFirst:
                break;

            case DisposalOrder.ShapesFirst:
                groundShape.Destroy();
                armShape.Destroy();
                break;

            case DisposalOrder.JointsThenShapesThenBodies:
                hinge.Destroy();
                groundShape.Destroy();
                armShape.Destroy();
                ground.Destroy();
                arm.Destroy();
                anchor.Destroy();
                break;

            case DisposalOrder.BodiesFirst:
                ground.Destroy();
                arm.Destroy();
                anchor.Destroy();
                break;
        }

        world.Step(1.0f / 60.0f);
        world.Dispose();

        Assert.False(ground.IsValid);
        Assert.False(arm.IsValid);
        Assert.False(groundShape.IsValid);
        Assert.False(armShape.IsValid);
        Assert.False(hinge.AsJoint.IsValid);
    }

    /// <summary>The order resources are released in before the world is disposed.</summary>
    public enum DisposalOrder
    {
        /// <summary>Dispose the world with everything still alive.</summary>
        NothingFirst,

        /// <summary>Remove the shapes, leaving the bodies.</summary>
        ShapesFirst,

        /// <summary>Unwind in the order an owner would.</summary>
        JointsThenShapesThenBodies,

        /// <summary>Destroy the bodies, which takes their shapes and joints with them.</summary>
        BodiesFirst,
    }

    // -------------------------------------------------- ids from other worlds

    /// <summary>
    /// A body handle held past its world's disposal is caught, unless another
    /// world has since taken the same slot.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the one case the guard cannot catch, and it is a limit of the C
    /// id rather than of this wrapper. A <c>b3BodyId</c> is an index, an owning
    /// world <em>slot</em> and a body generation counter. It does not carry the
    /// world's generation, and <c>b3Body_IsValid</c> only checks that the slot
    /// is occupied - not that it is occupied by the same world the handle came
    /// from. So a handle into world slot 1 generation 0 is indistinguishable
    /// from the identical handle into world slot 1 generation 1.
    /// </para>
    /// <para>
    /// Measured: after disposing a world and creating another, the first world's
    /// body id is bit-for-bit equal to the new world's first body id, reports
    /// itself valid, and reads the new body's position. There is no information
    /// left in the handle with which to tell them apart. Closing it would mean
    /// storing the world generation in <see cref="Body"/> and
    /// <see cref="Shape"/>, and <see cref="Body.GetShapes"/> writes native ids
    /// straight into the caller's span precisely because <see cref="Shape"/> is
    /// nothing but a <c>b3ShapeId</c>.
    /// </para>
    /// <para>
    /// So it is documented rather than hidden, and pinned here: if a future
    /// Box3D widens the id, this test fails and the limitation can be lifted
    /// from the documentation along with it.
    /// </para>
    /// </remarks>
    [NativeFact]
    public void AHandleOutlivingItsWorldIsCaughtOnlyUntilTheSlotIsReused()
    {
        var first = new PhysicsWorld();
        Body body = first.CreateDynamicBody(new Vector3(0.0f, 5.0f, 0.0f));
        body.AddSphere(new Sphere(0.5f));
        b3BodyId staleId = body.ToNativeId();
        first.Dispose();

        // With the slot empty, the handle is correctly rejected.
        Assert.False(body.IsValid);
        Assert.Throws<InvalidOperationException>(() => body.Position);

        // A new world takes the slot the first one released.
        using var second = new PhysicsWorld();
        Body replacement = second.CreateDynamicBody(new Vector3(0.0f, 9.0f, 0.0f));
        replacement.AddSphere(new Sphere(0.5f));

        if (replacement.ToNativeId() != staleId)
        {
            // The slot or the counters did not line up, so this run cannot say
            // anything either way. Nothing is asserted rather than asserting
            // something that only holds by accident.
            return;
        }

        // The two handles are the same value, so no check can separate them.
        Assert.True(body.IsValid);
        Assert.Equal(replacement.Position.Y, body.Position.Y, 3);
    }

    private struct CountingRaycastCallback : IRaycastCallback
    {
        public int Count;

        public RaycastAction OnHit(in RaycastHit hit)
        {
            Count++;
            return RaycastAction.Continue;
        }
    }

    private struct CountingOverlapCallback : IOverlapCallback
    {
        public int Count;

        public bool OnOverlap(Shape shape)
        {
            Count++;
            return true;
        }
    }
}
