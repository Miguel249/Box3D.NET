// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using Box3D.Visualizer.Rendering;

namespace Box3D.Visualizer.Scenes;

/// <summary>
/// A height field, and everything dropped on it rolling to the middle.
/// </summary>
/// <remarks>
/// The terrain is drawn from the engine's own compressed grid rather than from
/// the array it was built out of, which is the only way to be sure the picture
/// shows what the simulation is actually colliding against - quantization
/// included.
/// </remarks>
internal sealed class TerrainScene : Scene
{
    private const int Side = 41;
    private const float Cell = 0.36f;
    private const float Depth = 3.0f;

    private HeightField? _terrain;

    /// <inheritdoc/>
    public override string Name => "terrain";

    /// <inheritdoc/>
    public override string Caption => "A 41 by 41 height field, drawn from the engine's own grid.";

    /// <inheritdoc/>
    public override RenderStyle Style => base.Style with
    {
        // Terrain fills the frame, so it needs to sit apart from the backdrop
        // rather than fading into it, and the haze that gives the smaller
        // scenes their depth only flattens this one.
        FogDensity = 0.008f,
    };

    /// <inheritdoc/>
    public override int FrameCount => 300;

    /// <inheritdoc/>
    public override int HeroFrame => 110;

    /// <inheritdoc/>
    public override void Build(PhysicsWorld world, ShapeMeshFactory visuals)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(visuals);

        float[] heights = new float[Side * Side];

        for (int row = 0; row < Side; row++)
        {
            for (int column = 0; column < Side; column++)
            {
                float x = ((column / (float)(Side - 1)) - 0.5f) * 2.0f;
                float z = ((row / (float)(Side - 1)) - 0.5f) * 2.0f;

                // A bowl, with a ripple on it so that the surface has something
                // for the light to describe.
                float bowl = Depth * ((x * x) + (z * z));
                float ripple = 0.22f * MathF.Sin(x * 6.0f) * MathF.Cos(z * 6.0f);

                heights[(row * Side) + column] = bowl + ripple;
            }
        }

        _terrain = HeightField.FromHeights(
            heights,
            columnCount: Side,
            rowCount: Side,
            scale: new Vector3(Cell, 1.0f, Cell),
            minimumHeight: -0.5f,
            maximumHeight: (Depth * 2.0f) + 0.5f);

        // A height field grows in positive x and z from its body origin, so the
        // body goes back by half its extent to put the bottom of the bowl on
        // the world origin.
        float half = (Side - 1) * Cell * 0.5f;

        Body ground = world.CreateStaticBody(new Vector3(-half, 0.0f, -half));
        Shape shape = ground.AddHeightField(
            _terrain,
            ShapeDefinition.Default with
            {
                Material = PhysicsMaterial.Default with { Friction = 0.5f },
            });

        visuals.Paint(shape, new Appearance(Palette.Terrain, CastsShadow: false));

        for (int i = 0; i < 12; i++)
        {
            float angle = (i / 12.0f) * MathF.Tau;
            float radius = 4.6f;

            Body ball = world.CreateDynamicBody(new Vector3(
                MathF.Cos(angle) * radius,
                5.0f + (i * 0.22f),
                MathF.Sin(angle) * radius));

            visuals.Paint(ball.AddSphere(new Sphere(0.4f)), new Appearance(Palette.Cycle(i)));
        }
    }

    /// <inheritdoc/>
    public override Camera GetCamera(int frame) =>
        Camera.Orbit(new Vector3(0.0f, 1.0f, 0.0f), 19.0f, 12.0f + (frame * 0.06f), 33.0f);

    /// <inheritdoc/>
    public override void ReleaseGeometry()
    {
        // After the world, never before: the shape holds a borrowed pointer
        // into this and freeing it first is a use-after-free in the solver.
        _terrain?.Dispose();
        _terrain = null;
    }
}
