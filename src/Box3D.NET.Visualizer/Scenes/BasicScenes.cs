// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using Box3D.Visualizer.Rendering;

namespace Box3D.Visualizer.Scenes;

/// <summary>
/// A stacked pyramid of crates and the ball that ends it.
/// </summary>
internal sealed class StackScene : Scene
{
    private const float Half = 0.3f;
    private const int Rows = 5;

    /// <inheritdoc/>
    public override string Name => "stack";

    /// <inheritdoc/>
    public override string Caption => "A pyramid of crates, and the ball that ends it.";

    /// <inheritdoc/>
    public override int FrameCount => 180;

    /// <inheritdoc/>
    public override int HeroFrame => 62;

    /// <inheritdoc/>
    public override void Build(PhysicsWorld world, ShapeMeshFactory visuals)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(visuals);

        SceneKit.Ground(world, visuals);

        for (int row = 0; row < Rows; row++)
        {
            int count = Rows - row;

            for (int i = 0; i < count; i++)
            {
                float x = (i - ((count - 1) * 0.5f)) * ((Half * 2.0f) + 0.02f);
                float y = Half + (row * ((Half * 2.0f) + 0.004f));

                Body crate = world.CreateDynamicBody(new Vector3(x, y, 0.0f));
                Shape shape = crate.AddBox(Box.Cube(Half));

                visuals.Paint(shape, new Appearance(Palette.Cycle(row + (i * 3))));
            }
        }

        // Heavy, and thrown flat rather than dropped, so that it arrives with
        // the momentum to go through the stack instead of landing on it.
        Body ball = world.CreateDynamicBody(new Vector3(-6.5f, 2.4f, 0.0f));
        Shape ballShape = ball.AddSphere(new Sphere(0.55f), ShapeDefinition.Default with { Density = 3000.0f });
        ball.LinearVelocity = new Vector3(11.0f, 0.0f, 0.0f);

        visuals.Paint(ballShape, new Appearance(Palette.Accent));
    }

    /// <inheritdoc/>
    public override void Decorate(PhysicsWorld world, Renderer renderer, int frame) =>
        SceneKit.Grid(renderer, Vector3.Zero, 8.0f);

    /// <inheritdoc/>
    public override Camera GetCamera(int frame) =>
        Camera.Orbit(new Vector3(0.45f, 1.0f, 0.0f), 9.6f, 34.0f + (frame * 0.045f), 14.0f);
}

/// <summary>
/// Spheres, capsules and boxes poured into a pen.
/// </summary>
internal sealed class PourScene : Scene
{
    private const int Total = 54;
    private const int StepsBetweenDrops = 4;

    private readonly Random _random = new(20260808);

    /// <inheritdoc/>
    public override string Name => "pour";

    /// <inheritdoc/>
    public override string Caption => "Fifty-four bodies poured into a pen: spheres, capsules and boxes.";

    /// <inheritdoc/>
    public override int FrameCount => 300;

    /// <inheritdoc/>
    public override int HeroFrame => 260;

    /// <inheritdoc/>
    public override void Build(PhysicsWorld world, ShapeMeshFactory visuals)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(visuals);

        SceneKit.Ground(world, visuals);

        // Four low walls. Without them the pile spreads out and there is
        // nothing left to look at by the end.
        const float Reach = 2.5f;
        const float Height = 0.85f;

        Span<Vector3> centres =
        [
            new Vector3(Reach, Height * 0.5f, 0.0f),
            new Vector3(-Reach, Height * 0.5f, 0.0f),
            new Vector3(0.0f, Height * 0.5f, Reach),
            new Vector3(0.0f, Height * 0.5f, -Reach),
        ];

        for (int i = 0; i < centres.Length; i++)
        {
            Vector3 extents = i < 2
                ? new Vector3(0.12f, Height * 0.5f, Reach + 0.12f)
                : new Vector3(Reach + 0.12f, Height * 0.5f, 0.12f);

            Body wall = world.CreateStaticBody(centres[i]);
            visuals.Paint(wall.AddBox(new Box(extents)), new Appearance(Palette.Static));
        }
    }

    /// <inheritdoc/>
    public override void Update(PhysicsWorld world, ShapeMeshFactory visuals, int frame)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(visuals);

        // One body every few steps. Dropping them all at once produces a single
        // collapsing block rather than something that reads as pouring.
        if (frame % StepsBetweenDrops != 0 || frame / StepsBetweenDrops >= Total)
        {
            return;
        }

        int index = frame / StepsBetweenDrops;

        // The golden angle, which is what keeps successive drops from landing
        // on top of one another.
        float angle = index * 2.3999632f;
        float radius = 1.4f * MathF.Sqrt((index % 13) / 13.0f);

        Body body = world.CreateDynamicBody(
            new Vector3(MathF.Cos(angle) * radius, 4.0f, MathF.Sin(angle) * radius),
            Quaternion.CreateFromYawPitchRoll(
                (float)_random.NextDouble() * MathF.Tau,
                (float)_random.NextDouble() * MathF.Tau,
                (float)_random.NextDouble() * MathF.Tau));

        Shape shape = (index % 3) switch
        {
            0 => body.AddSphere(new Sphere(0.23f)),
            1 => body.AddCapsule(Capsule.Upright(0.60f, 0.17f)),
            _ => body.AddBox(Box.Cube(0.20f)),
        };

        visuals.Paint(shape, new Appearance(Palette.Cycle(index)));
    }

    /// <inheritdoc/>
    public override void Decorate(PhysicsWorld world, Renderer renderer, int frame) =>
        SceneKit.Grid(renderer, Vector3.Zero, 7.0f);

    /// <inheritdoc/>
    public override Camera GetCamera(int frame) =>
        Camera.Orbit(new Vector3(0.0f, 0.85f, 0.0f), 8.8f, 20.0f + (frame * 0.05f), 21.0f);
}

/// <summary>
/// The same simulation with the engine's own diagnostics turned on.
/// </summary>
/// <remarks>
/// This is the picture that shows what debug draw is actually for: the contact
/// points the solver is working with, the normal at each one, and the broad
/// phase box around every shape. All of it comes out of the same
/// <c>world.Draw</c> call as the bodies themselves, selected by flags.
/// </remarks>
internal sealed class ContactsScene : Scene
{
    // The floor gets a category of its own so the pass that draws broad-phase
    // bounds can leave it out. Its box is eighty metres across and would be the
    // only thing in the picture.
    private const ulong GroundCategory = 1UL << 1;
    private const ulong BodyCategory = 1UL << 0;

    /// <inheritdoc/>
    public override string Name => "contacts";

    /// <inheritdoc/>
    public override string Caption => "Contact points, contact normals and broad-phase bounds, straight from the engine.";

    /// <inheritdoc/>
    public override WorldSettings Settings => base.Settings with
    {
        // Box3D stops solving a body that has gone to sleep, and a sleeping
        // body has no contacts to report. The pile would settle and the
        // annotations would vanish halfway through the animation.
        EnableSleep = false,
    };

    /// <inheritdoc/>
    public override DebugDrawOptions DrawOptions => DebugDrawOptions.Default with { DrawShapes = true };

    /// <inheritdoc/>
    public override DebugDrawOptions? OverlayOptions => DebugDrawOptions.Default with
    {
        DrawContacts = true,
        DrawContactNormals = true,
        DrawBounds = true,
        CategoryMask = BodyCategory,
    };

    /// <inheritdoc/>
    public override int FrameCount => 200;

    /// <inheritdoc/>
    public override int HeroFrame => 150;

    /// <inheritdoc/>
    public override void Build(PhysicsWorld world, ShapeMeshFactory visuals)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(visuals);

        SceneKit.Ground(world, visuals, categories: GroundCategory);

        ShapeDefinition definition = ShapeDefinition.Default with
        {
            Filter = new CollisionFilter(BodyCategory, ulong.MaxValue),
        };

        // A deliberately small pile: every contact is drawn, and thirty bodies
        // would bury the shapes under their own annotations.
        Span<Vector3> layout =
        [
            new Vector3(-0.55f, 0.36f, -0.2f),
            new Vector3(0.5f, 0.36f, 0.25f),
            new Vector3(-0.05f, 1.08f, 0.0f),
        ];

        for (int i = 0; i < layout.Length; i++)
        {
            Body crate = world.CreateDynamicBody(
                layout[i],
                Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.22f * i));

            visuals.Paint(crate.AddBox(Box.Cube(0.35f), definition), new Appearance(Palette.Cycle(i * 2)));
        }

        Body ball = world.CreateDynamicBody(new Vector3(-0.05f, 2.1f, 0.0f));
        visuals.Paint(ball.AddSphere(new Sphere(0.4f), definition), new Appearance(Palette.Cool));

        Body capsule = world.CreateDynamicBody(new Vector3(1.25f, 3.2f, -0.5f));
        visuals.Paint(
            capsule.AddCapsule(Capsule.Upright(0.9f, 0.24f), definition),
            new Appearance(Palette.Accent));
    }

    /// <inheritdoc/>
    public override Camera GetCamera(int frame) =>
        Camera.Orbit(new Vector3(0.1f, 0.95f, 0.0f), 4.9f, 26.0f + (frame * 0.07f), 12.0f);
}
