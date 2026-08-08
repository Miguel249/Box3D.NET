// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using Box3D.Visualizer.Rendering;

namespace Box3D.Visualizer.Scenes;

/// <summary>
/// A colonnade baked into a single shape.
/// </summary>
/// <remarks>
/// <para>
/// Thirty-seven children - a mesh plinth, twelve hull columns, twelve capsule
/// lintels and twelve sphere capitals - and one shape. The broad-phase box drawn
/// around the whole structure is the point of the picture: a baked compound is
/// one proxy no matter how many pieces it holds, which is why it exists and also
/// why it is restricted to static bodies.
/// </para>
/// <para>
/// It is also the one shape a renderer cannot draw on its own. Every other kind
/// can be read back from its handle; a compound has no accessor in the C API, so
/// this scene hands the baked geometry to the factory through
/// <see cref="ShapeMeshFactory.Supply"/>. Without that call the columns would
/// simply not be in the picture, and the balls would bounce off nothing.
/// </para>
/// </remarks>
internal sealed class CompoundScene : Scene
{
    private const int Columns = 12;
    private const float Ring = 2.35f;
    private const float PlinthHalf = 3.1f;
    private const float PlinthTop = 0.24f;
    private const float ColumnHeight = 1.45f;
    private const float ColumnRadius = 0.2f;

    // The compound gets a category of its own so that the pass drawing
    // broad-phase bounds shows one box - its own - and not the floor's, which is
    // eighty metres across, nor one per falling ball. A shape belongs to every
    // category unless it is told otherwise, so the other two need naming as
    // well: leaving them on the default would put them in this one too.
    private const ulong BallCategory = 1UL << 0;
    private const ulong GroundCategory = 1UL << 1;
    private const ulong CompoundCategory = 1UL << 2;

    private ConvexHull? _column;
    private CollisionMesh? _plinth;
    private CompoundGeometry? _colonnade;

    /// <inheritdoc/>
    public override string Name => "compound";

    /// <inheritdoc/>
    public override string Caption =>
        "Thirty-seven children baked into one static shape, inside one broad-phase box.";

    /// <inheritdoc/>
    public override DebugDrawOptions? OverlayOptions => DebugDrawOptions.Default with
    {
        DrawBounds = true,
        CategoryMask = CompoundCategory,
    };

    /// <inheritdoc/>
    public override int FrameCount => 240;

    /// <inheritdoc/>
    public override int HeroFrame => 130;

    /// <inheritdoc/>
    public override void Build(PhysicsWorld world, ShapeMeshFactory visuals)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(visuals);

        SceneKit.Ground(world, visuals, categories: GroundCategory);

        // Built once and shared by every instance: Box3D stores one copy of the
        // hull inside the compound and points all twelve columns at it.
        _column = ConvexHull.Cylinder(ColumnHeight, ColumnRadius, sides: 14);
        _plinth = CollisionMesh.BoxMesh(Vector3.Zero, new Vector3(PlinthHalf, PlinthTop * 0.5f, PlinthHalf));

        var builder = new CompoundBuilder();
        builder.AddMesh(_plinth, new Vector3(0.0f, PlinthTop * 0.5f, 0.0f));

        for (int i = 0; i < Columns; i++)
        {
            Vector3 foot = Foot(i);
            Vector3 head = foot + new Vector3(0.0f, ColumnHeight, 0.0f);

            builder.AddHull(_column, foot);
            builder.AddSphere(new Sphere(head, ColumnRadius * 1.25f));

            // A lintel from this capital to the next one round, which is what
            // turns twelve separate posts into something that reads as built.
            builder.AddCapsule(new Capsule(head, Foot((i + 1) % Columns) + new Vector3(0.0f, ColumnHeight, 0.0f), 0.085f));
        }

        _colonnade = builder.Build();

        Body scenery = world.CreateStaticBody();
        Shape shape = scenery.AddCompound(
            _colonnade,
            ShapeDefinition.Default with
            {
                Material = PhysicsMaterial.Default with { Friction = 0.7f, Restitution = 0.25f },
                Filter = new CollisionFilter(CompoundCategory, ulong.MaxValue),
            });

        // One shape, so one colour: the children cannot be told apart from the
        // outside, which is the trade a baked compound makes.
        visuals.Paint(shape, new Appearance(Palette.Static));
        visuals.Supply(shape, _colonnade);
    }

    /// <inheritdoc/>
    public override void Update(PhysicsWorld world, ShapeMeshFactory visuals, int frame)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(visuals);

        const int Total = 18;
        const int StepsBetweenDrops = 5;

        if (frame % StepsBetweenDrops != 0 || frame / StepsBetweenDrops >= Total)
        {
            return;
        }

        int index = frame / StepsBetweenDrops;

        // Dropped off centre and given a shove, so they run into the colonnade
        // rather than settling in the middle of the plinth.
        float angle = index * 2.3999632f;
        Vector3 position = new(MathF.Cos(angle) * 0.8f, 2.6f, MathF.Sin(angle) * 0.8f);

        Body ball = world.CreateDynamicBody(position);
        Shape shape = ball.AddSphere(
            new Sphere(0.21f),
            ShapeDefinition.Default with
            {
                Material = PhysicsMaterial.Default with { Restitution = 0.35f },
                Filter = new CollisionFilter(BallCategory, ulong.MaxValue),
            });

        ball.LinearVelocity = new Vector3(MathF.Cos(angle) * 1.7f, 0.0f, MathF.Sin(angle) * 1.7f);

        visuals.Paint(shape, new Appearance(Palette.Cycle(index)));
    }

    /// <inheritdoc/>
    public override void Decorate(PhysicsWorld world, Renderer renderer, int frame) =>
        SceneKit.Grid(renderer, Vector3.Zero, 7.0f);

    /// <inheritdoc/>
    public override Camera GetCamera(int frame) =>
        Camera.Orbit(new Vector3(0.0f, 1.05f, 0.0f), 8.4f, 28.0f + (frame * 0.055f), 17.0f);

    /// <inheritdoc/>
    public override void ReleaseGeometry()
    {
        // The compound is borrowed by its shape, so it goes after the world. The
        // hull and the mesh were cloned into it when it was baked and could have
        // gone as soon as Build returned; they are released here so that the
        // order reads the same for all three.
        _colonnade?.Dispose();
        _colonnade = null;

        _plinth?.Dispose();
        _plinth = null;

        _column?.Dispose();
        _column = null;
    }

    private static Vector3 Foot(int index)
    {
        float angle = index / (float)Columns * MathF.Tau;

        return new Vector3(MathF.Cos(angle) * Ring, PlinthTop, MathF.Sin(angle) * Ring);
    }
}
