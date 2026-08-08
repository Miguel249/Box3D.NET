// SPDX-License-Identifier: MIT

using System.Numerics;
using Box3D.Visualizer.Rendering;

namespace Box3D.Visualizer.Scenes;

/// <summary>
/// One picture in the gallery: a world, a camera and a few seconds of time.
/// </summary>
/// <remarks>
/// A scene is deliberately not allowed to draw the bodies itself. It builds the
/// world and says what colour things are; the frames come out of
/// <see cref="PhysicsWorld.Draw{T}(ref T, in DebugDrawOptions)"/> like any other
/// application's would. What a scene may add through <see cref="Decorate"/> is
/// the part the engine cannot know about - a ray it cast, a character it is
/// moving itself.
/// </remarks>
internal abstract class Scene
{
    /// <summary>Gets the name used on the command line and for the file names.</summary>
    public abstract string Name { get; }

    /// <summary>Gets the one-line description used in the documentation.</summary>
    public abstract string Caption { get; }

    /// <summary>Gets the world tuning.</summary>
    public virtual WorldSettings Settings => WorldSettings.Default with
    {
        Gravity = new Vector3(0.0f, -9.81f, 0.0f),
    };

    /// <summary>Gets what the engine should emit each frame.</summary>
    public virtual DebugDrawOptions DrawOptions => DebugDrawOptions.Default with { DrawShapes = true };

    /// <summary>
    /// Gets a second draw pass, or <see langword="null"/> for scenes that need
    /// only one.
    /// </summary>
    /// <remarks>
    /// Two passes exist because the options that select what is drawn apply to
    /// the whole call, and <see cref="DebugDrawOptions.CategoryMask"/> along
    /// with them. Drawing every shape but annotating only some of them is
    /// therefore two calls, not one.
    /// </remarks>
    public virtual DebugDrawOptions? OverlayOptions => null;

    /// <summary>Gets the lighting and backdrop.</summary>
    public virtual RenderStyle Style => RenderStyle.Default with { ShadowPlaneY = 0.004f };

    /// <summary>Gets the simulation step.</summary>
    public virtual float TimeStep => 1.0f / 60.0f;

    /// <summary>Gets how many steps to run before the first frame is captured.</summary>
    public virtual int WarmUpSteps => 0;

    /// <summary>Gets how many steps the scene runs for.</summary>
    public virtual int FrameCount => 180;

    /// <summary>Gets the step the still image is taken at.</summary>
    public virtual int HeroFrame => FrameCount - 1;

    /// <summary>Builds the world.</summary>
    /// <param name="world">The world to fill.</param>
    /// <param name="visuals">Where to record what each shape should look like.</param>
    public abstract void Build(PhysicsWorld world, ShapeMeshFactory visuals);

    /// <summary>Does whatever the scene wants done before a step.</summary>
    /// <param name="world">The world.</param>
    /// <param name="visuals">Where to record the appearance of anything created here.</param>
    /// <param name="frame">The step about to run, counted from zero.</param>
    public virtual void Update(PhysicsWorld world, ShapeMeshFactory visuals, int frame)
    {
    }

    /// <summary>Adds anything the engine does not know it should be drawing.</summary>
    /// <param name="world">The world.</param>
    /// <param name="renderer">The renderer to draw into.</param>
    /// <param name="frame">The step that has just run.</param>
    public virtual void Decorate(PhysicsWorld world, Renderer renderer, int frame)
    {
    }

    /// <summary>Gets where the camera stands for a given step.</summary>
    /// <param name="frame">The step.</param>
    /// <returns>The camera.</returns>
    public abstract Camera GetCamera(int frame);

    /// <summary>
    /// Releases geometry the scene owns, after the world that borrowed it has
    /// been disposed.
    /// </summary>
    /// <remarks>
    /// Meshes and height fields are borrowed by the shapes built from them
    /// rather than copied, so this cannot be a <c>using</c> inside
    /// <see cref="Build"/>: the order is world first, geometry second.
    /// </remarks>
    public virtual void ReleaseGeometry()
    {
    }
}
