// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using Box3D.Visualizer.Rendering;

namespace Box3D.Visualizer;

/// <summary>
/// The other half of debug draw: what the engine emits, and where it goes.
/// </summary>
/// <remarks>
/// <para>
/// A struct, and passed by <c>ref</c>, because that is what
/// <see cref="PhysicsWorld.Draw{T}(ref T, in DebugDrawOptions)"/> asks for: the
/// generic constraint keeps the calls devirtualised and nothing is boxed. The
/// renderer it forwards to is a class, and holding a reference to it inside the
/// struct is exactly the intended use.
/// </para>
/// <para>
/// Every method here is a translation and nothing more. Box3D hands over
/// positions, segments and boxes in world space; this decides that a segment is
/// a line one and a half pixels wide. No decision about what the simulation
/// means is taken on this side of the boundary.
/// </para>
/// </remarks>
internal struct SceneDrawer(Renderer renderer, ShapeMeshFactory factory) : IDebugDrawer
{
    private readonly Renderer _renderer = renderer;
    private readonly ShapeMeshFactory _factory = factory;

    /// <summary>Gets how many primitives the engine emitted this frame.</summary>
    public int Primitives { get; private set; }

    /// <inheritdoc/>
    public void DrawShape(nint handle, Vector3 position, Quaternion rotation, DebugColor color)
    {
        if (!_factory.TryGet(handle, out ShapeMeshFactory.Drawable drawable))
        {
            return;
        }

        Primitives++;

        Appearance appearance = drawable.Appearance
            ?? new Appearance(Palette.FromDebugColor(color));

        _renderer.DrawMesh(drawable.Mesh, position, rotation, appearance.Albedo, appearance.CastsShadow);
    }

    /// <inheritdoc/>
    public void DrawSegment(Vector3 p1, Vector3 p2, DebugColor color)
    {
        Primitives++;
        _renderer.DrawLine(p1, p2, Palette.FromDebugColor(color), 1.8f);
    }

    /// <inheritdoc/>
    public void DrawPoint(Vector3 position, float size, DebugColor color)
    {
        Primitives++;

        // The engine gives a size in pixels for a window it knows nothing
        // about. Clamped, or a contact point on a settled stack covers the
        // bodies it is meant to annotate.
        _renderer.DrawPoint(position, Math.Clamp(size * 0.7f, 5.0f, 10.0f), Palette.FromDebugColor(color));
    }

    /// <inheritdoc/>
    public void DrawTransform(Vector3 position, Quaternion rotation)
    {
        Primitives++;
        _renderer.DrawTransform(position, rotation, 0.4f);
    }

    /// <inheritdoc/>
    public void DrawSphere(Vector3 center, float radius, DebugColor color, float alpha)
    {
        Primitives++;
        _renderer.DrawWireSphere(center, radius, Palette.FromDebugColor(color), Math.Clamp(alpha, 0.35f, 1.0f));
    }

    /// <inheritdoc/>
    public void DrawCapsule(Vector3 p1, Vector3 p2, float radius, DebugColor color, float alpha)
    {
        Primitives++;
        _renderer.DrawWireCapsule(p1, p2, radius, Palette.FromDebugColor(color), Math.Clamp(alpha, 0.35f, 1.0f));
    }

    /// <inheritdoc/>
    public void DrawBounds(BoundingBox bounds, DebugColor color)
    {
        Primitives++;
        _renderer.DrawWireBounds(bounds, Palette.FromDebugColor(color), 1.1f, 0.75f);
    }

    /// <inheritdoc/>
    public void DrawBox(Vector3 extents, Vector3 position, Quaternion rotation, DebugColor color)
    {
        Primitives++;
        _renderer.DrawWireBox(extents, position, rotation, Palette.FromDebugColor(color), 1.3f);
    }

    /// <inheritdoc/>
    public void DrawString(Vector3 position, ReadOnlySpan<byte> utf8Text, DebugColor color)
    {
        // Deliberately dropped. This renderer has no font, and a label rendered
        // as a smear of pixels is worse than no label; the captions in the
        // documentation say what each picture is.
        Primitives++;
    }
}
