// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using Box3D.Visualizer.Rendering;

namespace Box3D.Visualizer.Scenes;

/// <summary>
/// The few pieces every scene builds the same way.
/// </summary>
internal static class SceneKit
{
    /// <summary>Adds a static floor whose top face sits at y = 0.</summary>
    /// <param name="world">The world.</param>
    /// <param name="visuals">Where to record the appearance.</param>
    /// <param name="extent">Half the width of the floor.</param>
    /// <param name="friction">The surface friction.</param>
    /// <param name="categories">
    /// The collision categories the floor belongs to. Worth setting to
    /// something of its own when the scene draws broad-phase bounds: the
    /// floor's box is the size of the world and swamps the picture, and a
    /// category is how a draw pass leaves it out.
    /// </param>
    /// <returns>The floor body.</returns>
    public static Body Ground(
        PhysicsWorld world,
        ShapeMeshFactory visuals,
        float extent = 40.0f,
        float friction = 0.6f,
        ulong categories = ulong.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(visuals);

        Body ground = world.CreateStaticBody(new Vector3(0.0f, -0.5f, 0.0f));

        Shape shape = ground.AddBox(
            new Box(new Vector3(extent, 0.5f, extent)),
            ShapeDefinition.Default with
            {
                Material = PhysicsMaterial.Default with { Friction = friction },
                Filter = new CollisionFilter(categories, ulong.MaxValue),
            });

        // The floor is what catches the shadows, so it must not cast one: its
        // own top face projects onto itself and turns the whole plane grey.
        visuals.Paint(shape, new Appearance(Palette.Ground, CastsShadow: false));

        return ground;
    }

    /// <summary>Draws the reference grid the scenes share.</summary>
    /// <param name="renderer">The renderer.</param>
    /// <param name="center">Where the grid is centred.</param>
    /// <param name="extent">How far it reaches.</param>
    /// <param name="spacing">The distance between lines.</param>
    public static void Grid(Renderer renderer, Vector3 center, float extent = 9.0f, float spacing = 1.0f)
    {
        ArgumentNullException.ThrowIfNull(renderer);

        renderer.DrawGrid(0.006f, center, extent, spacing, Rgb.FromHex(0xB9C7DA));
    }
}
