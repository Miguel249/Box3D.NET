// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using Box3D.Visualizer.Rendering;

namespace Box3D.Visualizer.Scenes;

/// <summary>
/// A chain of revolute joints, swinging under its own weight.
/// </summary>
internal sealed class ChainScene : Scene
{
    private const int LinkCount = 9;
    private const float LinkLength = 0.42f;
    private const float LinkRadius = 0.09f;
    private const float AnchorHeight = 4.6f;

    /// <inheritdoc/>
    public override string Name => "chain";

    /// <inheritdoc/>
    public override string Caption => "Nine links and a weight on revolute joints. The joint frames are the engine's own.";

    /// <inheritdoc/>
    public override DebugDrawOptions DrawOptions => DebugDrawOptions.Default with
    {
        DrawShapes = true,
        DrawJoints = true,
        JointScale = 1.2f,
    };

    /// <inheritdoc/>
    public override int FrameCount => 300;

    /// <inheritdoc/>
    public override int HeroFrame => 38;

    /// <inheritdoc/>
    public override void Build(PhysicsWorld world, ShapeMeshFactory visuals)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(visuals);

        SceneKit.Ground(world, visuals);

        Body anchor = world.CreateStaticBody(new Vector3(0.0f, AnchorHeight + 0.15f, 0.0f));
        visuals.Paint(anchor.AddBox(new Box(new Vector3(0.3f, 0.15f, 0.3f))), new Appearance(Palette.Static));

        // The chain starts hanging straight down and is set turning about the
        // anchor, rather than being placed at an angle: a chain built already
        // displaced has every joint violated on the first step and snaps.
        const float AngularSpeed = 1.7f;

        Body previous = anchor;

        for (int i = 0; i < LinkCount; i++)
        {
            float centre = AnchorHeight - ((i + 0.5f) * LinkLength);

            Body link = world.CreateDynamicBody(new Vector3(0.0f, centre, 0.0f));
            Shape shape = link.AddCapsule(
                Capsule.Upright(LinkLength + (LinkRadius * 2.0f), LinkRadius),
                ShapeDefinition.Default with { Density = 1200.0f });

            visuals.Paint(shape, new Appearance(Palette.Cycle(i)));

            // The hinge sits at the joint between the two links and turns about
            // z, so the whole chain swings in the plane facing the camera.
            Vector3 hinge = new(0.0f, AnchorHeight - (i * LinkLength), 0.0f);
            world.CreateRevoluteJoint(RevoluteJointDefinition.Hinge(previous, link, hinge, Vector3.UnitZ));

            link.LinearVelocity = new Vector3(-AngularSpeed * (AnchorHeight - centre), 0.0f, 0.0f);
            link.AngularVelocity = new Vector3(0.0f, 0.0f, AngularSpeed);

            previous = link;
        }

        // A weight on the end, which is what makes the swing read as a swing
        // rather than as a rope flapping.
        float bobHeight = AnchorHeight - (LinkCount * LinkLength) - 0.28f;

        Body bob = world.CreateDynamicBody(new Vector3(0.0f, bobHeight, 0.0f));
        visuals.Paint(
            bob.AddSphere(new Sphere(0.3f), ShapeDefinition.Default with { Density = 2600.0f }),
            new Appearance(Palette.Accent));

        world.CreateRevoluteJoint(RevoluteJointDefinition.Hinge(
            previous,
            bob,
            new Vector3(0.0f, AnchorHeight - (LinkCount * LinkLength), 0.0f),
            Vector3.UnitZ));

        bob.LinearVelocity = new Vector3(-AngularSpeed * (AnchorHeight - bobHeight), 0.0f, 0.0f);
    }

    /// <inheritdoc/>
    public override Camera GetCamera(int frame) =>
        Camera.Orbit(new Vector3(0.0f, 2.5f, 0.0f), 7.6f, 8.0f + (frame * 0.05f), 7.0f);
}

/// <summary>
/// A cart on wheel joints, driving over broken ground.
/// </summary>
internal sealed class VehicleScene : Scene
{
    private Body _chassis;
    private CollisionMesh? _road;

    /// <inheritdoc/>
    public override string Name => "vehicle";

    /// <inheritdoc/>
    public override string Caption => "A cart built from two wheel joints: suspension, a drive motor, and a rough road.";

    /// <inheritdoc/>
    public override int FrameCount => 330;

    /// <inheritdoc/>
    public override int HeroFrame => 190;

    /// <inheritdoc/>
    public override void Build(PhysicsWorld world, ShapeMeshFactory visuals)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(visuals);

        SceneKit.Ground(world, visuals, 60.0f, friction: 1.0f);

        // Something to drive over. Shallow enough that the suspension is what
        // absorbs it rather than the cart being launched.
        Span<float> bumps = [6.0f, 9.5f, 13.0f, 17.5f, 22.0f];

        for (int i = 0; i < bumps.Length; i++)
        {
            Body bump = world.CreateStaticBody(
                new Vector3(bumps[i], 0.0f, 0.0f),
                Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI * 0.25f));

            visuals.Paint(bump.AddBox(Box.Cube(0.16f + (0.02f * i))), new Appearance(Palette.Static));
        }

        // The take-off ramp is a triangle mesh rather than a box, so that the
        // gallery covers that path too: a mesh is borrowed by the shape built
        // from it rather than copied, which is why it is released separately.
        Span<Vector3> vertices =
        [
            new Vector3(26.0f, 0.0f, -2.0f),
            new Vector3(26.0f, 0.0f, 2.0f),
            new Vector3(34.0f, 2.2f, -2.0f),
            new Vector3(34.0f, 2.2f, 2.0f),
        ];

        // Counter-clockwise seen from above, which is the side the surface is
        // solid from.
        Span<int> indices = [0, 1, 2, 2, 1, 3];

        _road = CollisionMesh.FromTriangles(vertices, indices);

        Body level = world.CreateStaticBody();
        visuals.Paint(level.AddMesh(_road), new Appearance(Palette.Static));

        _chassis = world.CreateDynamicBody(new Vector3(0.0f, 0.95f, 0.0f));

        ShapeDefinition body = ShapeDefinition.Default with { Density = 500.0f };

        visuals.Paint(
            _chassis.AddBox(new Box(new Vector3(1.0f, 0.20f, 0.55f)), body),
            new Appearance(Palette.Accent));

        // A cabin, so that the cart has a front and the pitch under
        // acceleration is something you can see.
        visuals.Paint(
            _chassis.AddBox(new Box(new Vector3(0.42f, 0.24f, 0.48f), new Vector3(-0.2f, 0.42f, 0.0f)), body),
            new Appearance(Palette.Accent * 0.75f));

        WheelJoint rear = AttachWheel(world, visuals, _chassis, new Vector3(-0.72f, 0.55f, 0.0f));
        AttachWheel(world, visuals, _chassis, new Vector3(0.72f, 0.55f, 0.0f));

        // Rear wheel drive, which is enough to show the chassis pitching under
        // acceleration.
        rear.SpinMotorEnabled = true;
        rear.SpinMotorSpeed = -16.0f;
        rear.MaxSpinTorque = 700.0f;
    }

    /// <inheritdoc/>
    public override void Decorate(PhysicsWorld world, Renderer renderer, int frame) =>
        SceneKit.Grid(renderer, new Vector3(_chassis.Position.X, 0.0f, 0.0f), 12.0f, 2.0f);

    /// <inheritdoc/>
    public override Camera GetCamera(int frame)
    {
        // The camera rides along rather than orbiting, or the cart drives out
        // of the picture after a second and a half.
        Vector3 target = new(_chassis.IsValid ? _chassis.Position.X : 0.0f, 0.95f, 0.0f);
        return Camera.Orbit(target, 7.2f, 22.0f, 12.0f);
    }

    /// <inheritdoc/>
    public override void ReleaseGeometry()
    {
        _road?.Dispose();
        _road = null;
    }

    private static WheelJoint AttachWheel(PhysicsWorld world, ShapeMeshFactory visuals, Body chassis, Vector3 hub)
    {
        Body wheel = world.CreateBody(BodyDefinition.Dynamic(hub) with { AllowFastRotation = true });

        Shape shape = wheel.AddSphere(
            new Sphere(0.35f),
            ShapeDefinition.Default with
            {
                Density = 800.0f,
                Material = PhysicsMaterial.Default with { Friction = 1.5f },
            });

        visuals.Paint(shape, new Appearance(Rgb.FromHex(0x39404A)));

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
