// SPDX-License-Identifier: MIT

using System;
using System.Numerics;

namespace Box3D.Samples;

/// <summary>
/// A hinged door with limits and a closer, which is the joint most games need first.
/// </summary>
internal static class HingedDoorSample
{
    public static void Run()
    {
        using var world = new PhysicsWorld(WorldSettings.Default with
        {
            Gravity = new Vector3(0.0f, -10.0f, 0.0f),
        });

        // The frame is static, so the hinge has something immovable to pull against.
        Body frame = world.CreateBody(BodyDefinition.Static(new Vector3(0.0f, 1.0f, 0.0f)));
        frame.AddBox(new Box(new Vector3(0.05f, 1.0f, 0.05f)));

        // The door hangs to one side of the hinge, so its centre is offset.
        Body door = world.CreateBody(BodyDefinition.Dynamic(new Vector3(0.5f, 1.0f, 0.0f)));
        door.AddBox(new Box(new Vector3(0.5f, 1.0f, 0.03f)), ShapeDefinition.Default with
        {
            Density = 400.0f,
        });

        Vector3 hingePoint = new(0.0f, 1.0f, 0.0f);

        // The door turns about the world up axis. Hinge places that axis on the
        // joint frames and expresses the anchor in each body's local space, which
        // is the calculation that is easy to get wrong by hand.
        RevoluteJoint hinge = world.CreateRevoluteJoint(
            RevoluteJointDefinition.Hinge(frame, door, hingePoint, Vector3.UnitY) with
            {
                // A door that opens one way, ninety degrees.
                LimitsEnabled = true,
                LowerAngle = 0.0f,
                UpperAngle = MathF.PI * 0.5f,
            });

        // Push it open.
        door.ApplyImpulse(new Vector3(0.0f, 0.0f, 60.0f), new Vector3(1.0f, 1.0f, 0.0f));

        float widestOpening = 0.0f;

        for (int i = 0; i < 180; i++)
        {
            world.Step(1.0f / 60.0f);
            widestOpening = MathF.Max(widestOpening, MathF.Abs(hinge.Angle));
        }

        Console.WriteLine($"   swung open to : {widestOpening * 180.0f / MathF.PI:F1} degrees");
        Console.WriteLine($"   resting at    : {hinge.Angle * 180.0f / MathF.PI:F1} degrees");

        SampleRunner.Expect(widestOpening > 0.2f, "the impulse pushed the door open");

        // The limit is the point: without it the door would spin right round.
        SampleRunner.Expect(
            widestOpening <= (MathF.PI * 0.5f) + 0.1f,
            "the upper limit stopped the door at ninety degrees");

        // Now add a closer: a motor that always pulls towards shut.
        hinge.MotorEnabled = true;
        hinge.MotorSpeed = -1.0f;
        hinge.MaxMotorTorque = 50.0f;

        for (int i = 0; i < 300; i++)
        {
            world.Step(1.0f / 60.0f);
        }

        Console.WriteLine($"   after closer  : {hinge.Angle * 180.0f / MathF.PI:F1} degrees");

        SampleRunner.Expect(MathF.Abs(hinge.Angle) < 0.1f, "the door closer pulled it shut");
    }
}

/// <summary>
/// A hanging chain built from distance joints, and what happens when a link breaks.
/// </summary>
internal static class ChainSample
{
    public static void Run()
    {
        using var world = new PhysicsWorld(WorldSettings.Default with
        {
            Gravity = new Vector3(0.0f, -10.0f, 0.0f),
        });

        const int LinkCount = 8;
        const float LinkSpacing = 0.5f;

        Body ceiling = world.CreateBody(BodyDefinition.Static(new Vector3(0.0f, 10.0f, 0.0f)));
        ceiling.AddBox(Box.Cube(0.1f));

        Span<Body> links = stackalloc Body[LinkCount];
        Body previous = ceiling;
        Vector3 previousAnchor = new(0.0f, 10.0f, 0.0f);

        DistanceJoint topJoint = default;

        for (int i = 0; i < LinkCount; i++)
        {
            Vector3 position = new(0.0f, 10.0f - ((i + 1) * LinkSpacing), 0.0f);

            Body link = world.CreateBody(BodyDefinition.Dynamic(position));
            link.AddSphere(new Sphere(0.12f), ShapeDefinition.Default with { Density = 2000.0f });
            links[i] = link;

            DistanceJoint joint = world.CreateDistanceJoint(
                DistanceJointDefinition.Between(previous, link, previousAnchor, position));

            if (i == 0)
            {
                topJoint = joint;
            }

            previous = link;
            previousAnchor = position;
        }

        // Give the chain a shove so it swings rather than hanging dead straight.
        links[LinkCount - 1].ApplyImpulseToCenter(new Vector3(40.0f, 0.0f, 0.0f));

        for (int i = 0; i < 240; i++)
        {
            world.Step(1.0f / 60.0f);
        }

        Body tip = links[LinkCount - 1];
        float span = 10.0f - tip.Position.Y;

        Console.WriteLine($"   links         : {LinkCount}");
        Console.WriteLine($"   tip at        : {tip.Position}");
        Console.WriteLine($"   hangs down    : {span:F2} m");
        Console.WriteLine($"   tension       : {topJoint.AsJoint.ConstraintForce.Length():F0} N at the top link");

        // The chain cannot stretch beyond the sum of its rest lengths.
        SampleRunner.Expect(span <= (LinkCount * LinkSpacing) + 0.5f, "the chain did not stretch");
        SampleRunner.Expect(span > 2.0f, "the chain hangs under its own weight");

        // The top link carries the whole chain, so it is the one under load.
        SampleRunner.Expect(
            topJoint.AsJoint.ConstraintForce.Length() > 0.0f,
            "the top joint reports the tension it is carrying");

        // Cut the top link and the whole chain falls.
        topJoint.Destroy();

        for (int i = 0; i < 120; i++)
        {
            world.Step(1.0f / 60.0f);
        }

        Console.WriteLine($"   after cutting : tip at y = {tip.Position.Y:F2}");

        SampleRunner.Expect(tip.Position.Y < -1.0f, "cutting the top link drops the chain");
    }
}

/// <summary>
/// A cart on wheel joints, showing suspension and a drive motor.
/// </summary>
internal static class VehicleSample
{
    public static void Run()
    {
        using var world = new PhysicsWorld(WorldSettings.Default with
        {
            Gravity = new Vector3(0.0f, -10.0f, 0.0f),
        });

        Body ground = world.CreateBody(BodyDefinition.Static(new Vector3(0.0f, -0.5f, 0.0f)));
        ground.AddBox(new Box(new Vector3(100.0f, 0.5f, 20.0f)), ShapeDefinition.Default with
        {
            Material = PhysicsMaterial.Default with { Friction = 1.0f },
        });

        Body chassis = world.CreateBody(BodyDefinition.Dynamic(new Vector3(0.0f, 1.0f, 0.0f)));
        chassis.AddBox(new Box(new Vector3(1.0f, 0.25f, 0.5f)), ShapeDefinition.Default with
        {
            Density = 500.0f,
        });

        // Two wheels, each on its own suspension. The suspension travels along
        // the world up axis; the wheel spins about the joint frame z axis.
        WheelJoint rear = AttachWheel(world, chassis, new Vector3(-0.7f, 0.6f, 0.0f));
        WheelJoint front = AttachWheel(world, chassis, new Vector3(0.7f, 0.6f, 0.0f));

        // Drive the rear wheel only.
        rear.SpinMotorEnabled = true;
        rear.SpinMotorSpeed = -20.0f;
        rear.MaxSpinTorque = 600.0f;

        float startX = chassis.Position.X;

        for (int i = 0; i < 300; i++)
        {
            world.Step(1.0f / 60.0f);
        }

        float travelled = chassis.Position.X - startX;

        Console.WriteLine($"   travelled     : {travelled:F2} m");
        Console.WriteLine($"   chassis height: {chassis.Position.Y:F2} m");
        Console.WriteLine($"   rear spin     : {rear.SpinSpeed:F1} rad/s");
        Console.WriteLine($"   front spin    : {front.SpinSpeed:F1} rad/s");

        SampleRunner.Expect(MathF.Abs(travelled) > 1.0f, "the driven wheel moved the cart");

        // The suspension holds the chassis clear of the ground.
        SampleRunner.Expect(chassis.Position.Y > 0.3f, "the suspension carries the chassis");

        // The undriven wheel turns because it is rolling, which is the sign the
        // cart is actually moving over the ground rather than sliding.
        SampleRunner.Expect(MathF.Abs(front.SpinSpeed) > 0.1f, "the front wheel rolls along");
    }

    private static WheelJoint AttachWheel(PhysicsWorld world, Body chassis, Vector3 hub)
    {
        Body wheel = world.CreateBody(BodyDefinition.Dynamic(hub) with
        {
            // Wheels are symmetric about their spin axis, so the usual rotation
            // clamp is unnecessary and would only slow them down.
            AllowFastRotation = true,
        });

        wheel.AddSphere(new Sphere(0.35f), ShapeDefinition.Default with
        {
            Density = 800.0f,
            Material = PhysicsMaterial.Default with { Friction = 1.5f },
        });

        return world.CreateWheelJoint(
            WheelJointDefinition.Suspension(chassis, wheel, hub, Vector3.UnitY) with
            {
                SuspensionEnabled = true,
                SuspensionHertz = 4.0f,
                SuspensionDampingRatio = 0.7f,
                SuspensionLimitEnabled = true,
                LowerSuspensionLimit = -0.2f,
                UpperSuspensionLimit = 0.2f,
            });
    }
}
