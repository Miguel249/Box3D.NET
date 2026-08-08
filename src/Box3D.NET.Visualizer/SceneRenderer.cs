// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Box3D.Visualizer.Encoding;
using Box3D.Visualizer.Rendering;
using Box3D.Visualizer.Scenes;

namespace Box3D.Visualizer;

/// <summary>
/// How large the pictures are and how fast the animation plays.
/// </summary>
internal sealed record RenderOptions
{
    /// <summary>Gets the width of the still image.</summary>
    public int StillWidth { get; init; } = 1280;

    /// <summary>Gets the height of the still image.</summary>
    public int StillHeight { get; init; } = 720;

    /// <summary>Gets how many samples per output pixel the still image uses.</summary>
    public int StillSupersample { get; init; } = 3;

    /// <summary>Gets the width of the animation.</summary>
    public int AnimationWidth { get; init; } = 560;

    /// <summary>Gets the height of the animation.</summary>
    public int AnimationHeight { get; init; } = 315;

    /// <summary>Gets how many samples per output pixel the animation uses.</summary>
    public int AnimationSupersample { get; init; } = 2;

    /// <summary>Gets the fewest simulation steps that may pass between animation frames.</summary>
    public int MinimumStride { get; init; } = 3;

    /// <summary>Gets the most frames an animation may be made of.</summary>
    /// <remarks>
    /// A GIF frame costs around thirty kilobytes at these sizes, so the length
    /// of an animation is the one number that decides what the gallery weighs.
    /// The scenes run for anything from three to five and a half seconds, and
    /// capturing every third step of all of them made the long ones twice the
    /// size of the short ones for no more information.
    /// </remarks>
    public int MaxAnimationFrames { get; init; } = 70;

    /// <summary>Gets whether to write the animation at all.</summary>
    public bool Animate { get; init; } = true;

    /// <summary>Gets where the files go.</summary>
    public string OutputDirectory { get; init; } = ".";

    /// <summary>Works out how many steps pass between the frames of one scene.</summary>
    /// <param name="frameCount">How many steps the scene runs for.</param>
    /// <returns>The stride.</returns>
    /// <remarks>
    /// Coarse enough to keep the animation inside
    /// <see cref="MaxAnimationFrames"/>, and never finer than
    /// <see cref="MinimumStride"/>. A longer scene therefore samples itself more
    /// coarsely rather than producing a longer file, and because
    /// <see cref="DelayFor"/> derives the frame delay from the same stride, it
    /// still plays back at the speed the simulation ran at.
    /// </remarks>
    public int StrideFor(int frameCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frameCount);

        int needed = ((frameCount + MaxAnimationFrames) - 1) / MaxAnimationFrames;

        return Math.Max(MinimumStride, needed);
    }

    /// <summary>
    /// Gets how long each animation frame is shown, in hundredths of a second.
    /// </summary>
    /// <remarks>
    /// Derived from the step and the stride so that the animation plays at the
    /// speed the simulation ran at, rounded to the hundredths GIF counts in.
    /// </remarks>
    /// <param name="timeStep">The simulation step.</param>
    /// <param name="stride">The stride, from <see cref="StrideFor"/>.</param>
    /// <returns>The delay.</returns>
    public static int DelayFor(float timeStep, int stride) =>
        Math.Max(2, (int)MathF.Round(timeStep * stride * 100.0f));
}

/// <summary>What one scene produced.</summary>
/// <param name="Scene">The scene.</param>
/// <param name="StillPath">Where the still image went.</param>
/// <param name="AnimationPath">Where the animation went, if there is one.</param>
/// <param name="FrameCount">How many frames the animation has.</param>
/// <param name="DrawablesBuilt">How many shapes were tessellated.</param>
/// <param name="ShapesSkipped">How many shapes could not be tessellated.</param>
/// <param name="Elapsed">How long the whole thing took.</param>
internal readonly record struct RenderReport(
    string Scene,
    string StillPath,
    string? AnimationPath,
    int FrameCount,
    int DrawablesBuilt,
    int ShapesSkipped,
    TimeSpan Elapsed);

/// <summary>
/// Runs a scene and writes its picture and its animation.
/// </summary>
internal static class SceneRenderer
{
    /// <summary>Renders one scene.</summary>
    /// <param name="scene">The scene.</param>
    /// <param name="options">The sizes and speeds.</param>
    /// <returns>What was written.</returns>
    public static RenderReport Render(Scene scene, RenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(options);

        var clock = Stopwatch.StartNew();

        int stride = options.StrideFor(scene.FrameCount);

        var factory = new ShapeMeshFactory();
        var still = new Renderer(options.StillWidth, options.StillHeight, options.StillSupersample)
        {
            Style = scene.Style,
        };

        Renderer? motion = options.Animate
            ? new Renderer(options.AnimationWidth, options.AnimationHeight, options.AnimationSupersample)
            {
                Style = scene.Style,
            }
            : null;

        var frames = new List<Image>();
        Image? hero = null;

        // The world is scoped so that it is disposed before the scene releases
        // any mesh or height field the shapes borrowed from it.
        using (var world = new PhysicsWorld(scene.Settings, factory))
        {
            scene.Build(world, factory);

            for (int i = 0; i < scene.WarmUpSteps; i++)
            {
                world.Step(scene.TimeStep);
            }

            for (int frame = 0; frame < scene.FrameCount; frame++)
            {
                scene.Update(world, factory, frame);
                world.Step(scene.TimeStep);

                if (motion is not null && frame % stride == 0)
                {
                    frames.Add(Capture(motion, scene, world, factory, frame));
                }

                // The last frame stands in when a scene names a hero frame past
                // the end of its own run, so there is always a picture.
                if (frame == scene.HeroFrame || (hero is null && frame == scene.FrameCount - 1))
                {
                    hero = Capture(still, scene, world, factory, frame);
                }
            }
        }

        scene.ReleaseGeometry();

        Directory.CreateDirectory(options.OutputDirectory);

        string stillPath = Path.Combine(options.OutputDirectory, scene.Name + ".png");
        PngWriter.Write(hero!, stillPath);

        string? animationPath = null;
        if (motion is not null && frames.Count > 0)
        {
            animationPath = Path.Combine(options.OutputDirectory, scene.Name + ".gif");
            GifWriter.Write(frames, RenderOptions.DelayFor(scene.TimeStep, stride), animationPath);
        }

        return new RenderReport(
            scene.Name,
            stillPath,
            animationPath,
            frames.Count,
            factory.Built,
            factory.Skipped,
            clock.Elapsed);
    }

    private static Image Capture(
        Renderer renderer,
        Scene scene,
        PhysicsWorld world,
        ShapeMeshFactory factory,
        int frame)
    {
        renderer.Begin(scene.GetCamera(frame));

        // Everything the engine knows how to draw. The drawer is a struct
        // passed by reference, which is what keeps this allocation free on the
        // engine's side of the call.
        var drawer = new SceneDrawer(renderer, factory);
        world.Draw(ref drawer, scene.DrawOptions);

        if (scene.OverlayOptions is DebugDrawOptions overlay)
        {
            world.Draw(ref drawer, overlay);
        }

        // And then whatever only the scene knows about: rays it cast, a
        // character it is moving itself.
        scene.Decorate(world, renderer, frame);

        return renderer.Resolve();
    }
}
