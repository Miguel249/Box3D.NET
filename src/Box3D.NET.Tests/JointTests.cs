// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using Xunit;

namespace Box3D.Tests;

/// <summary>
/// Exercises the joint API against the real library.
/// </summary>
/// <remarks>
/// These assert on behaviour rather than on values coming back unchanged: a
/// limit that is set but never enforced, or a motor that reports a speed it is
/// not driving, would pass a round-trip test and fail in a game.
/// </remarks>
public class JointTests : IDisposable
{
    private readonly PhysicsWorld _world;

    public JointTests()
    {
        _world = new PhysicsWorld(WorldSettings.Default with
        {
            Gravity = new Vector3(0.0f, -10.0f, 0.0f),
        });
    }

    public void Dispose()
    {
        _world.Dispose();
        GC.SuppressFinalize(this);
    }

    private Body CreateAnchor(Vector3 position)
    {
        Body body = _world.CreateBody(BodyDefinition.Static(position));
        body.AddBox(Box.Cube(0.25f));
        return body;
    }

    private Body CreateArm(Vector3 position)
    {
        Body body = _world.CreateBody(BodyDefinition.Dynamic(position));
        body.AddBox(new Box(new Vector3(0.5f, 0.1f, 0.1f)));
        return body;
    }

    private void Simulate(int steps = 120)
    {
        for (int i = 0; i < steps; i++)
        {
            _world.Step(1.0f / 60.0f);
        }
    }

    // ------------------------------------------------------------- revolute

    [NativeFact]
    public void A_revolute_joint_pins_the_bodies_together()
    {
        Body anchor = CreateAnchor(Vector3.Zero);
        Body arm = CreateArm(new Vector3(1.0f, 0.0f, 0.0f));

        RevoluteJoint hinge = _world.CreateRevoluteJoint(
            RevoluteJointDefinition.Hinge(anchor, arm, Vector3.Zero, Vector3.UnitZ));

        Assert.True(hinge.AsJoint.IsValid);
        Assert.Equal(JointType.Revolute, hinge.AsJoint.Type);

        Simulate();

        // Gravity swings the arm down, but the hinge holds it at a fixed radius
        // from the anchor. Without the constraint it would simply fall away.
        float radius = Vector3.Distance(arm.Position, anchor.Position);

        Assert.True(radius is > 0.8f and < 1.2f, $"the arm drifted to a radius of {radius}");
        Assert.True(arm.Position.Y < -0.5f, $"the arm should have swung down, Y was {arm.Position.Y}");
    }

    [NativeFact]
    public void A_revolute_limit_stops_the_arm_swinging_past_it()
    {
        Body anchor = CreateAnchor(Vector3.Zero);
        Body arm = CreateArm(new Vector3(1.0f, 0.0f, 0.0f));

        RevoluteJoint hinge = _world.CreateRevoluteJoint(
            RevoluteJointDefinition.Hinge(anchor, arm, Vector3.Zero, Vector3.UnitZ) with
            {
                LimitsEnabled = true,

                // Allow a small swing either side of the starting pose.
                LowerAngle = -0.3f,
                UpperAngle = 0.3f,
            });

        Simulate(240);

        Assert.True(hinge.LimitsEnabled);

        // The limit is what keeps the arm nearly level despite gravity.
        Assert.True(
            MathF.Abs(hinge.Angle) <= 0.35f,
            $"the joint angle {hinge.Angle} escaped the limit of 0.3");
        Assert.True(
            arm.Position.Y > -0.5f,
            $"the arm fell past its limit, Y was {arm.Position.Y}");
    }

    [NativeFact]
    public void A_revolute_motor_drives_the_arm_against_gravity()
    {
        Body anchor = CreateAnchor(Vector3.Zero);
        Body arm = CreateArm(new Vector3(1.0f, 0.0f, 0.0f));

        RevoluteJoint hinge = _world.CreateRevoluteJoint(
            RevoluteJointDefinition.Hinge(anchor, arm, Vector3.Zero, Vector3.UnitZ) with
            {
                MotorEnabled = true,
                MotorSpeed = 4.0f,
                MaxMotorTorque = 1000.0f,
            });

        Simulate(60);

        Assert.True(hinge.MotorEnabled);

        // A motor strong enough to lift the arm turns it right around, so the
        // angle keeps growing rather than settling where gravity wants it.
        Assert.True(MathF.Abs(hinge.Angle) > 0.5f, $"the motor did not turn the arm, angle was {hinge.Angle}");
        Assert.True(MathF.Abs(hinge.MotorTorque) > 0.0f, "a working motor reports a torque");
    }

    // ------------------------------------------------------------ prismatic

    [NativeFact]
    public void A_prismatic_joint_confines_motion_to_its_axis()
    {
        Body anchor = CreateAnchor(new Vector3(0.0f, 5.0f, 0.0f));
        Body slider = _world.CreateBody(BodyDefinition.Dynamic(new Vector3(0.0f, 5.0f, 0.0f)));
        slider.AddBox(Box.Cube(0.25f));

        PrismaticJoint joint = _world.CreatePrismaticJoint(
            PrismaticJointDefinition.Slider(anchor, slider, new Vector3(0.0f, 5.0f, 0.0f), Vector3.UnitY) with
            {
                LimitsEnabled = true,
                LowerTranslation = -2.0f,
                UpperTranslation = 0.0f,
            });

        Simulate(240);

        // Gravity pulls it down the axis until the lower limit stops it, and it
        // must not have moved off the axis at all.
        Assert.True(joint.Translation <= 0.05f, $"translation {joint.Translation} exceeded the upper limit");
        Assert.True(joint.Translation >= -2.05f, $"translation {joint.Translation} escaped the lower limit");
        Assert.Equal(0.0f, slider.Position.X, 2);
        Assert.Equal(0.0f, slider.Position.Z, 2);
        Assert.True(slider.Position.Y is > 2.9f and < 3.1f, $"expected the slider to rest at y=3, was {slider.Position.Y}");
    }

    // ------------------------------------------------------------- distance

    [NativeFact]
    public void A_distance_joint_holds_its_length()
    {
        Body anchor = CreateAnchor(new Vector3(0.0f, 10.0f, 0.0f));
        Body weight = _world.CreateBody(BodyDefinition.Dynamic(new Vector3(0.0f, 8.0f, 0.0f)));
        weight.AddSphere(new Sphere(0.25f));

        DistanceJoint rope = _world.CreateDistanceJoint(
            DistanceJointDefinition.Between(
                anchor,
                weight,
                new Vector3(0.0f, 10.0f, 0.0f),
                new Vector3(0.0f, 8.0f, 0.0f)));

        // Between takes the current separation as the rest length, so the joint
        // starts satisfied rather than snapping the bodies together.
        Assert.Equal(2.0f, rope.Length, 2);

        Simulate(240);

        Assert.Equal(2.0f, rope.CurrentLength, 1);
        Assert.True(weight.Position.Y is > 7.8f and < 8.2f, $"the weight hangs at {weight.Position.Y}");
    }

    // ------------------------------------------------------------ spherical

    [NativeFact]
    public void A_spherical_cone_limit_contains_the_swing()
    {
        Body anchor = CreateAnchor(new Vector3(0.0f, 10.0f, 0.0f));
        Body pendulum = _world.CreateBody(BodyDefinition.Dynamic(new Vector3(0.0f, 9.0f, 0.0f)));
        pendulum.AddSphere(new Sphere(0.25f));

        SphericalJoint joint = _world.CreateSphericalJoint(
            SphericalJointDefinition.BallAndSocket(
                anchor,
                pendulum,
                new Vector3(0.0f, 10.0f, 0.0f),
                Vector3.UnitY) with
            {
                ConeLimitEnabled = true,
                ConeAngle = 0.4f,
            });

        pendulum.ApplyImpulseToCenter(new Vector3(20.0f, 0.0f, 0.0f));
        Simulate(240);

        Assert.True(joint.ConeLimitEnabled);

        // The pendulum is pinned one metre below the anchor and may swing within
        // the cone, so its horizontal offset cannot exceed sin(cone angle).
        Vector3 offset = pendulum.Position - anchor.Position;
        float horizontal = new Vector3(offset.X, 0.0f, offset.Z).Length();
        float allowed = MathF.Sin(0.4f) * offset.Length();

        Assert.True(
            horizontal <= allowed + 0.15f,
            $"swung {horizontal} horizontally, the cone allows about {allowed}");
    }

    // ----------------------------------------------------------------- weld

    [NativeFact]
    public void A_weld_joint_carries_a_body_that_would_otherwise_fall()
    {
        Body anchor = CreateAnchor(new Vector3(0.0f, 10.0f, 0.0f));
        Body welded = _world.CreateBody(BodyDefinition.Dynamic(new Vector3(1.0f, 10.0f, 0.0f)));
        welded.AddBox(Box.Cube(0.25f));

        Vector3 start = welded.Position;

        WeldJoint joint = _world.CreateWeldJoint(
            WeldJointDefinition.Weld(anchor, welded, new Vector3(0.5f, 10.0f, 0.0f)));

        Simulate(240);

        Assert.Equal(JointType.Weld, joint.AsJoint.Type);

        // A rigid weld to a static body holds it in place. Some sag is expected
        // because the solver is approximate, but it must not fall away.
        Assert.True(
            Vector3.Distance(welded.Position, start) < 0.2f,
            $"the welded body moved to {welded.Position} from {start}");
    }

    // --------------------------------------------------------------- filter

    [NativeFact]
    public void A_filter_joint_stops_two_bodies_colliding()
    {
        // A floor to catch whatever falls.
        Body floor = _world.CreateBody(BodyDefinition.Static(new Vector3(0.0f, -0.5f, 0.0f)));
        floor.AddBox(new Box(new Vector3(20.0f, 0.5f, 20.0f)));

        Body lower = _world.CreateBody(BodyDefinition.Dynamic(new Vector3(0.0f, 1.0f, 0.0f)));
        lower.AddBox(Box.Cube(0.5f));

        Body upper = _world.CreateBody(BodyDefinition.Dynamic(new Vector3(0.0f, 4.0f, 0.0f)));
        upper.AddBox(Box.Cube(0.5f));

        _world.CreateFilterJoint(FilterJointDefinition.Between(lower, upper));

        Simulate(240);

        // Without the filter the upper box would stack on the lower one and rest
        // near y = 1.5. With it, the two pass through each other and both end up
        // on the floor.
        Assert.True(upper.Position.Y < 1.2f, $"the upper box did not pass through, Y was {upper.Position.Y}");
    }

    // --------------------------------------------------------------- shared

    [NativeFact]
    public void A_joint_reports_the_bodies_it_connects()
    {
        Body anchor = CreateAnchor(Vector3.Zero);
        Body arm = CreateArm(new Vector3(1.0f, 0.0f, 0.0f));

        Joint joint = _world.CreateRevoluteJoint(
            RevoluteJointDefinition.Hinge(anchor, arm, Vector3.Zero, Vector3.UnitZ)).AsJoint;

        Assert.Equal(anchor, joint.BodyA);
        Assert.Equal(arm, joint.BodyB);
        Assert.False(joint.CollideConnected);
    }

    [NativeFact]
    public void A_loaded_joint_reports_a_constraint_force()
    {
        Body anchor = CreateAnchor(new Vector3(0.0f, 10.0f, 0.0f));
        Body weight = _world.CreateBody(BodyDefinition.Dynamic(new Vector3(0.0f, 8.0f, 0.0f)));
        weight.AddSphere(new Sphere(0.5f), ShapeDefinition.Default with { Density = 5000.0f });

        Joint rope = _world.CreateDistanceJoint(
            DistanceJointDefinition.Between(
                anchor,
                weight,
                new Vector3(0.0f, 10.0f, 0.0f),
                new Vector3(0.0f, 8.0f, 0.0f))).AsJoint;

        Simulate(120);

        // A heavy weight hanging on the joint puts it under measurable load,
        // which is how a game would detect a rope about to snap.
        Assert.True(rope.ConstraintForce.Length() > 0.0f, "a loaded joint reports a non-zero constraint force");
    }

    [NativeFact]
    public void A_destroyed_joint_stops_constraining()
    {
        Body anchor = CreateAnchor(new Vector3(0.0f, 10.0f, 0.0f));
        Body weight = _world.CreateBody(BodyDefinition.Dynamic(new Vector3(0.0f, 8.0f, 0.0f)));
        weight.AddSphere(new Sphere(0.25f));

        DistanceJoint rope = _world.CreateDistanceJoint(
            DistanceJointDefinition.Between(
                anchor,
                weight,
                new Vector3(0.0f, 10.0f, 0.0f),
                new Vector3(0.0f, 8.0f, 0.0f)));

        Simulate(60);
        Assert.True(rope.AsJoint.IsValid);

        rope.Destroy();

        Assert.False(rope.AsJoint.IsValid);

        Simulate(120);

        // Cutting the rope lets the weight fall.
        Assert.True(weight.Position.Y < 6.0f, $"the weight should have fallen, Y was {weight.Position.Y}");
    }

    [NativeFact]
    public void Destroying_a_body_destroys_its_joints()
    {
        Body anchor = CreateAnchor(Vector3.Zero);
        Body arm = CreateArm(new Vector3(1.0f, 0.0f, 0.0f));

        Joint joint = _world.CreateRevoluteJoint(
            RevoluteJointDefinition.Hinge(anchor, arm, Vector3.Zero, Vector3.UnitZ)).AsJoint;

        Assert.True(joint.IsValid);

        arm.Destroy();

        Assert.False(joint.IsValid);
    }

    [NativeFact]
    public void Creating_a_joint_rejects_a_body_from_a_destroyed_pair()
    {
        Body anchor = CreateAnchor(Vector3.Zero);
        Body arm = CreateArm(new Vector3(1.0f, 0.0f, 0.0f));

        RevoluteJointDefinition definition =
            RevoluteJointDefinition.Hinge(anchor, arm, Vector3.Zero, Vector3.UnitZ);

        arm.Destroy();

        // Box3D asserts on this, and asserts are compiled out of a release build,
        // so the check has to happen on this side to produce a diagnosable error.
        Assert.Throws<ArgumentException>(() => _world.CreateRevoluteJoint(definition));
    }

    [NativeFact]
    public void Creating_a_joint_rejects_a_body_joined_to_itself()
    {
        Body arm = CreateArm(Vector3.Zero);

        RevoluteJointDefinition definition =
            RevoluteJointDefinition.Hinge(arm, arm, Vector3.Zero, Vector3.UnitZ);

        Assert.Throws<ArgumentException>(() => _world.CreateRevoluteJoint(definition));
    }

    [NativeFact]
    public void Joint_frames_from_a_world_anchor_agree_on_the_same_point()
    {
        Body a = CreateAnchor(new Vector3(2.0f, 3.0f, 4.0f));
        Body b = CreateArm(new Vector3(-1.0f, 5.0f, 0.0f));
        Vector3 anchor = new(0.5f, 4.0f, 2.0f);

        (JointFrame frameA, JointFrame frameB) = Joint.FramesFromWorldAnchor(a, b, anchor, Vector3.UnitY);

        // Both frames must describe the same world point, or the joint starts out
        // violated and snaps on the first step.
        Assert.Equal(anchor.X, a.ToWorldPoint(frameA.Position).X, 3);
        Assert.Equal(anchor.Y, a.ToWorldPoint(frameA.Position).Y, 3);
        Assert.Equal(anchor.Z, a.ToWorldPoint(frameA.Position).Z, 3);

        Assert.Equal(anchor.X, b.ToWorldPoint(frameB.Position).X, 3);
        Assert.Equal(anchor.Y, b.ToWorldPoint(frameB.Position).Y, 3);
        Assert.Equal(anchor.Z, b.ToWorldPoint(frameB.Position).Z, 3);
    }

    [NativeFact]
    public void Joint_frames_reject_a_zero_axis()
    {
        Body a = CreateAnchor(Vector3.Zero);
        Body b = CreateArm(new Vector3(1.0f, 0.0f, 0.0f));

        Assert.Throws<ArgumentException>(() =>
            Joint.FramesFromWorldAnchor(a, b, Vector3.Zero, Vector3.Zero));
    }
}
