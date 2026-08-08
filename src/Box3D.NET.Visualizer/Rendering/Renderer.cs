// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Numerics;

namespace Box3D.Visualizer.Rendering;

/*
 * A software rasterizer, in one file.
 *
 * There is no GPU here and no graphics dependency: the whole point of the
 * exercise is that Box3D.NET hands out geometry in world space and has no
 * opinion about what draws it, so the thing that draws it can be four hundred
 * lines of C# that runs anywhere `dotnet run` does.
 *
 * The pipeline is the textbook one. Vertices go to clip space through the
 * camera transform, get clipped against the near plane, are projected to
 * screen space, and are filled with barycentric coordinates against a depth
 * buffer. Attributes are interpolated with a perspective correction, because
 * interpolating a normal linearly in screen space bends it visibly across a
 * large ground quad seen at a shallow angle.
 *
 * Everything renders at an integer multiple of the output size and is box
 * filtered down at the end. That is the entire anti-aliasing strategy, and at
 * a supersample of three it is indistinguishable from anything cleverer at
 * these resolutions.
 *
 * Drawing is deferred rather than immediate. Shadows need the whole depth
 * buffer before they can be projected, and the debug overlay has to land on
 * top of solid geometry, so calls are recorded and the passes run in order in
 * Resolve.
 */

/// <summary>
/// Draws the world into an image.
/// </summary>
internal sealed class Renderer
{
    private const float Background = 1.0f;

    private readonly int _supersample;
    private readonly int _width;
    private readonly int _height;
    private readonly float[] _color;
    private readonly float[] _depth;
    private readonly float[] _shadow;
    private readonly List<Instance> _instances = new();
    private readonly List<Segment> _segments = new();
    private readonly List<Sprite> _sprites = new();
    private readonly List<Label> _labels = new();

    private Matrix4x4 _viewProjection;
    private Vector3 _eye;

    /// <summary>Creates a renderer for a given output size.</summary>
    /// <param name="width">The output width in pixels.</param>
    /// <param name="height">The output height in pixels.</param>
    /// <param name="supersample">How many samples per output pixel along each axis.</param>
    public Renderer(int width, int height, int supersample = 3)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfLessThan(supersample, 1);

        Width = width;
        Height = height;

        _supersample = supersample;
        _width = width * supersample;
        _height = height * supersample;
        _color = new float[_width * _height * 3];
        _depth = new float[_width * _height];
        _shadow = new float[_width * _height];
    }

    /// <summary>Gets the output width in pixels.</summary>
    public int Width { get; }

    /// <summary>Gets the output height in pixels.</summary>
    public int Height { get; }

    /// <summary>Gets or sets the lighting and backdrop.</summary>
    public RenderStyle Style { get; set; } = RenderStyle.Default;

    /// <summary>Starts a frame.</summary>
    /// <param name="camera">Where the picture is taken from.</param>
    public void Begin(in Camera camera)
    {
        _instances.Clear();
        _segments.Clear();
        _sprites.Clear();
        _labels.Clear();

        _eye = camera.Eye;
        _viewProjection = camera.ViewProjection(_width / (float)_height);
    }

    /// <summary>Draws a mesh at a transform.</summary>
    /// <param name="mesh">The mesh.</param>
    /// <param name="position">Where it is, in world space.</param>
    /// <param name="rotation">Its orientation.</param>
    /// <param name="albedo">Its linear surface colour.</param>
    /// <param name="castsShadow">Whether it casts a shadow onto the shadow plane.</param>
    public void DrawMesh(Mesh mesh, Vector3 position, Quaternion rotation, Vector3 albedo, bool castsShadow = true)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        _instances.Add(new Instance(mesh, position, rotation, albedo, castsShadow));
    }

    /// <summary>Draws a line segment on top of the solid geometry.</summary>
    /// <param name="from">The start, in world space.</param>
    /// <param name="to">The end, in world space.</param>
    /// <param name="color">The linear colour.</param>
    /// <param name="thickness">The width in output pixels.</param>
    /// <param name="alpha">The opacity.</param>
    /// <param name="onTop">
    /// Whether to ignore the depth buffer. For annotations that describe
    /// something inside a body - a contact point under a character's feet -
    /// being hidden by the very thing they annotate makes them useless.
    /// </param>
    public void DrawLine(
        Vector3 from,
        Vector3 to,
        Vector3 color,
        float thickness = 1.6f,
        float alpha = 1.0f,
        bool onTop = false) =>
        _segments.Add(new Segment(from, to, color, thickness, alpha, onTop));

    /// <summary>Draws a round dot on top of the solid geometry.</summary>
    /// <param name="position">Where it is, in world space.</param>
    /// <param name="size">The diameter in output pixels.</param>
    /// <param name="color">The linear colour.</param>
    /// <param name="alpha">The opacity.</param>
    /// <param name="onTop">Whether to ignore the depth buffer.</param>
    public void DrawPoint(Vector3 position, float size, Vector3 color, float alpha = 1.0f, bool onTop = false) =>
        _sprites.Add(new Sprite(position, size, color, alpha, onTop));

    /// <summary>Draws a line of text anchored to a point in the world.</summary>
    /// <param name="position">Where the text belongs, in world space.</param>
    /// <param name="utf8Text">The text, UTF-8 encoded and not NUL-terminated.</param>
    /// <param name="color">The linear colour.</param>
    /// <param name="scale">How many output pixels wide one glyph pixel is.</param>
    /// <remarks>
    /// The bytes belong to whoever called this and may not outlive the call, so
    /// the text is transcribed here rather than when the label is drawn.
    /// </remarks>
    public void DrawText(Vector3 position, ReadOnlySpan<byte> utf8Text, Vector3 color, float scale = 2.0f)
    {
        if (utf8Text.IsEmpty)
        {
            return;
        }

        string text = GlyphFont.Transcribe(utf8Text);
        if (text.Length == 0)
        {
            return;
        }

        _labels.Add(new Label(position, text, color, scale));
    }

    /// <summary>Draws a line of text anchored to a point in the world.</summary>
    /// <param name="position">Where the text belongs, in world space.</param>
    /// <param name="text">The text. Anything outside printable ASCII is drawn as a question mark.</param>
    /// <param name="color">The linear colour.</param>
    /// <param name="scale">How many output pixels wide one glyph pixel is.</param>
    public void DrawText(Vector3 position, string text, Vector3 color, float scale = 2.0f)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.Length > 0)
        {
            _labels.Add(new Label(position, text, color, scale));
        }
    }

    /// <summary>Draws a circle in the plane perpendicular to an axis.</summary>
    /// <param name="center">The centre, in world space.</param>
    /// <param name="axis">The axis the circle is perpendicular to.</param>
    /// <param name="radius">The radius.</param>
    /// <param name="color">The linear colour.</param>
    /// <param name="thickness">The width in output pixels.</param>
    /// <param name="alpha">The opacity.</param>
    /// <param name="segments">How many straight pieces the circle is made of.</param>
    public void DrawCircle(
        Vector3 center,
        Vector3 axis,
        float radius,
        Vector3 color,
        float thickness = 1.4f,
        float alpha = 1.0f,
        int segments = 40)
    {
        Tessellate.Basis(Vector3.Normalize(axis), out Vector3 u, out Vector3 v);

        Vector3 previous = center + (u * radius);
        for (int i = 1; i <= segments; i++)
        {
            float theta = i / (float)segments * MathF.Tau;
            Vector3 current = center + (((u * MathF.Cos(theta)) + (v * MathF.Sin(theta))) * radius);

            DrawLine(previous, current, color, thickness, alpha);
            previous = current;
        }
    }

    /// <summary>Draws a sphere as three great circles, the way a debug view does.</summary>
    /// <param name="center">The centre, in world space.</param>
    /// <param name="radius">The radius.</param>
    /// <param name="color">The linear colour.</param>
    /// <param name="alpha">The opacity.</param>
    public void DrawWireSphere(Vector3 center, float radius, Vector3 color, float alpha = 1.0f)
    {
        DrawCircle(center, Vector3.UnitX, radius, color, 1.3f, alpha);
        DrawCircle(center, Vector3.UnitY, radius, color, 1.3f, alpha);
        DrawCircle(center, Vector3.UnitZ, radius, color, 1.3f, alpha);
    }

    /// <summary>Draws a capsule as two rings and four rails.</summary>
    /// <param name="from">The centre of one end cap.</param>
    /// <param name="to">The centre of the other end cap.</param>
    /// <param name="radius">The radius.</param>
    /// <param name="color">The linear colour.</param>
    /// <param name="alpha">The opacity.</param>
    public void DrawWireCapsule(Vector3 from, Vector3 to, float radius, Vector3 color, float alpha = 1.0f)
    {
        Vector3 axis = to - from;
        if (axis.LengthSquared() <= 1e-12f)
        {
            DrawWireSphere(from, radius, color, alpha);
            return;
        }

        Vector3 w = Vector3.Normalize(axis);
        Tessellate.Basis(w, out Vector3 u, out Vector3 v);

        DrawCircle(from, w, radius, color, 1.3f, alpha);
        DrawCircle(to, w, radius, color, 1.3f, alpha);

        Span<Vector3> rails = [u * radius, -u * radius, v * radius, -v * radius];
        foreach (Vector3 offset in rails)
        {
            DrawLine(from + offset, to + offset, color, 1.3f, alpha);
        }
    }

    /// <summary>Draws the twelve edges of an oriented box.</summary>
    /// <param name="halfExtents">The half-widths along each local axis.</param>
    /// <param name="position">The centre, in world space.</param>
    /// <param name="rotation">The orientation.</param>
    /// <param name="color">The linear colour.</param>
    /// <param name="thickness">The width in output pixels.</param>
    /// <param name="alpha">The opacity.</param>
    public void DrawWireBox(
        Vector3 halfExtents,
        Vector3 position,
        Quaternion rotation,
        Vector3 color,
        float thickness = 1.4f,
        float alpha = 1.0f)
    {
        Span<Vector3> corners = stackalloc Vector3[8];

        for (int i = 0; i < 8; i++)
        {
            Vector3 local = new(
                (i & 1) == 0 ? -halfExtents.X : halfExtents.X,
                (i & 2) == 0 ? -halfExtents.Y : halfExtents.Y,
                (i & 4) == 0 ? -halfExtents.Z : halfExtents.Z);

            corners[i] = position + Vector3.Transform(local, rotation);
        }

        // Every pair of corner indices differing in exactly one bit is an edge.
        for (int i = 0; i < 8; i++)
        {
            for (int bit = 1; bit <= 4; bit <<= 1)
            {
                int j = i | bit;
                if (j != i)
                {
                    DrawLine(corners[i], corners[j], color, thickness, alpha);
                }
            }
        }
    }

    /// <summary>Draws an axis-aligned box.</summary>
    /// <param name="bounds">The box, in world space.</param>
    /// <param name="color">The linear colour.</param>
    /// <param name="thickness">The width in output pixels.</param>
    /// <param name="alpha">The opacity.</param>
    public void DrawWireBounds(BoundingBox bounds, Vector3 color, float thickness = 1.2f, float alpha = 1.0f) =>
        DrawWireBox(bounds.Size * 0.5f, bounds.Center, Quaternion.Identity, color, thickness, alpha);

    /// <summary>Draws a line with a head on it.</summary>
    /// <param name="from">The tail, in world space.</param>
    /// <param name="to">The point, in world space.</param>
    /// <param name="color">The linear colour.</param>
    /// <param name="thickness">The width in output pixels.</param>
    public void DrawArrow(Vector3 from, Vector3 to, Vector3 color, float thickness = 1.8f)
    {
        DrawLine(from, to, color, thickness);

        Vector3 axis = to - from;
        float length = axis.Length();
        if (length <= 1e-6f)
        {
            return;
        }

        Vector3 w = axis / length;
        Tessellate.Basis(w, out Vector3 u, out Vector3 v);

        float head = MathF.Min(0.25f * length, 0.35f);
        Vector3 baseCentre = to - (w * head);

        for (int i = 0; i < 4; i++)
        {
            float theta = i * (MathF.Tau / 4.0f);
            Vector3 rim = baseCentre + (((u * MathF.Cos(theta)) + (v * MathF.Sin(theta))) * head * 0.45f);
            DrawLine(rim, to, color, thickness);
        }
    }

    /// <summary>Draws a coordinate frame in the usual red, green and blue.</summary>
    /// <param name="position">The origin, in world space.</param>
    /// <param name="rotation">The orientation.</param>
    /// <param name="size">The length of each axis.</param>
    public void DrawTransform(Vector3 position, Quaternion rotation, float size = 0.5f)
    {
        DrawLine(position, position + (Vector3.Transform(Vector3.UnitX, rotation) * size), Rgb.FromHex(0xFF5555), 1.6f);
        DrawLine(position, position + (Vector3.Transform(Vector3.UnitY, rotation) * size), Rgb.FromHex(0x66DD66), 1.6f);
        DrawLine(position, position + (Vector3.Transform(Vector3.UnitZ, rotation) * size), Rgb.FromHex(0x6699FF), 1.6f);
    }

    /// <summary>Draws a grid on a horizontal plane, fading with distance.</summary>
    /// <param name="planeY">The height of the plane.</param>
    /// <param name="center">The point the grid is centred on.</param>
    /// <param name="extent">How far the grid reaches from that point.</param>
    /// <param name="spacing">The distance between lines.</param>
    /// <param name="color">The linear colour.</param>
    public void DrawGrid(float planeY, Vector3 center, float extent, float spacing, Vector3 color)
    {
        float x0 = MathF.Floor((center.X - extent) / spacing) * spacing;
        float x1 = center.X + extent;
        float z0 = MathF.Floor((center.Z - extent) / spacing) * spacing;
        float z1 = center.Z + extent;

        for (float x = x0; x <= x1; x += spacing)
        {
            float fade = 1.0f - (MathF.Abs(x - center.X) / extent);
            DrawLine(new Vector3(x, planeY, z0), new Vector3(x, planeY, z1), color, 1.0f, 0.22f * Math.Clamp(fade, 0.0f, 1.0f));
        }

        for (float z = z0; z <= z1; z += spacing)
        {
            float fade = 1.0f - (MathF.Abs(z - center.Z) / extent);
            DrawLine(new Vector3(x0, planeY, z), new Vector3(x1, planeY, z), color, 1.0f, 0.22f * Math.Clamp(fade, 0.0f, 1.0f));
        }
    }

    /// <summary>Runs every pass and produces the finished frame.</summary>
    /// <returns>The image, at the output size.</returns>
    public Image Resolve()
    {
        ClearBuffers();

        foreach (Instance instance in _instances)
        {
            RasterizeInstance(instance);
        }

        if (Style.ShadowPlaneY is float planeY)
        {
            ShadowPass(planeY);
        }

        foreach (Segment segment in _segments)
        {
            RasterizeSegment(segment);
        }

        foreach (Sprite sprite in _sprites)
        {
            RasterizeSprite(sprite);
        }

        // Text last, and over everything. A label is the one primitive that is
        // unreadable if anything is drawn across it.
        foreach (Label label in _labels)
        {
            RasterizeLabel(label);
        }

        return Downsample();
    }

    // ----------------------------------------------------------- the passes

    private void ClearBuffers()
    {
        Array.Clear(_shadow);

        for (int y = 0; y < _height; y++)
        {
            // The backdrop is a plain vertical gradient. Anything busier
            // competes with the geometry, which is what the picture is of.
            float t = _height == 1 ? 0.0f : y / (float)(_height - 1);
            Vector3 sky = Vector3.Lerp(Style.BackgroundTop, Style.BackgroundBottom, t);

            int row = y * _width;
            for (int x = 0; x < _width; x++)
            {
                int index = row + x;
                _depth[index] = Background;

                int channel = index * 3;
                _color[channel] = sky.X;
                _color[channel + 1] = sky.Y;
                _color[channel + 2] = sky.Z;
            }
        }
    }

    private void RasterizeInstance(in Instance instance)
    {
        Mesh mesh = instance.Mesh;
        int[] indices = mesh.Indices;

        Span<RasterVertex> triangle = stackalloc RasterVertex[3];
        Span<RasterVertex> clipped = stackalloc RasterVertex[8];

        for (int i = 0; i < indices.Length; i += 3)
        {
            for (int corner = 0; corner < 3; corner++)
            {
                int index = indices[i + corner];

                Vector3 world = instance.Position + Vector3.Transform(mesh.Positions[index], instance.Rotation);
                Vector3 normal = Vector3.Transform(mesh.Normals[index], instance.Rotation);

                triangle[corner] = new RasterVertex(Vector4.Transform(new Vector4(world, 1.0f), _viewProjection), world, normal);
            }

            int count = ClipNear(triangle, clipped);
            for (int fan = 2; fan < count; fan++)
            {
                RasterizeTriangle(clipped[0], clipped[fan - 1], clipped[fan], instance.Albedo);
            }
        }
    }

    private void RasterizeTriangle(in RasterVertex a, in RasterVertex b, in RasterVertex c, Vector3 albedo)
    {
        Project(a.Clip, out float x0, out float y0, out float z0, out float w0);
        Project(b.Clip, out float x1, out float y1, out float z1, out float w1);
        Project(c.Clip, out float x2, out float y2, out float z2, out float w2);

        float determinant = ((y1 - y2) * (x0 - x2)) + ((x2 - x1) * (y0 - y2));
        if (MathF.Abs(determinant) < 1e-9f)
        {
            return;
        }

        float inverse = 1.0f / determinant;

        int minX = Math.Max(0, (int)MathF.Floor(MathF.Min(x0, MathF.Min(x1, x2))));
        int maxX = Math.Min(_width - 1, (int)MathF.Ceiling(MathF.Max(x0, MathF.Max(x1, x2))));
        int minY = Math.Max(0, (int)MathF.Floor(MathF.Min(y0, MathF.Min(y1, y2))));
        int maxY = Math.Min(_height - 1, (int)MathF.Ceiling(MathF.Max(y0, MathF.Max(y1, y2))));

        for (int y = minY; y <= maxY; y++)
        {
            float py = y + 0.5f;
            int row = y * _width;

            for (int x = minX; x <= maxX; x++)
            {
                float px = x + 0.5f;

                float l0 = (((y1 - y2) * (px - x2)) + ((x2 - x1) * (py - y2))) * inverse;
                if (l0 < 0.0f)
                {
                    continue;
                }

                float l1 = (((y2 - y0) * (px - x2)) + ((x0 - x2) * (py - y2))) * inverse;
                if (l1 < 0.0f)
                {
                    continue;
                }

                float l2 = 1.0f - l0 - l1;
                if (l2 < 0.0f)
                {
                    continue;
                }

                float depth = (l0 * z0) + (l1 * z1) + (l2 * z2);
                int index = row + x;
                if (depth < 0.0f || depth >= _depth[index])
                {
                    continue;
                }

                // Perspective correction. Screen-space linear interpolation of a
                // normal is visibly wrong on a ground plane seen edge on.
                float invW = (l0 * w0) + (l1 * w1) + (l2 * w2);
                float k0 = l0 * w0 / invW;
                float k1 = l1 * w1 / invW;
                float k2 = l2 * w2 / invW;

                Vector3 world = (a.World * k0) + (b.World * k1) + (c.World * k2);
                Vector3 normal = (a.Normal * k0) + (b.Normal * k1) + (c.Normal * k2);

                _depth[index] = depth;
                Store(index, Shade(world, normal, albedo));
            }
        }
    }

    private void ShadowPass(float planeY)
    {
        Vector3 travel = -Style.LightDirection;

        // A light at the horizon casts shadows to infinity, which is neither
        // useful nor representable.
        if (travel.Y > -0.15f)
        {
            return;
        }

        Span<RasterVertex> triangle = stackalloc RasterVertex[3];
        Span<RasterVertex> clipped = stackalloc RasterVertex[8];

        foreach (Instance instance in _instances)
        {
            if (!instance.CastsShadow)
            {
                continue;
            }

            Mesh mesh = instance.Mesh;
            int[] indices = mesh.Indices;

            for (int i = 0; i < indices.Length; i += 3)
            {
                bool skip = false;

                for (int corner = 0; corner < 3; corner++)
                {
                    int index = indices[i + corner];
                    Vector3 world = instance.Position + Vector3.Transform(mesh.Positions[index], instance.Rotation);

                    // A vertex already below the plane would project backwards
                    // along the light and stretch the shadow the wrong way.
                    if (world.Y < planeY)
                    {
                        skip = true;
                        break;
                    }

                    Vector3 onPlane = world + (travel * ((planeY - world.Y) / travel.Y));
                    triangle[corner] = new RasterVertex(
                        Vector4.Transform(new Vector4(onPlane, 1.0f), _viewProjection),
                        onPlane,
                        Vector3.UnitY);
                }

                if (skip)
                {
                    continue;
                }

                int count = ClipNear(triangle, clipped);
                for (int fan = 2; fan < count; fan++)
                {
                    RasterizeShadowTriangle(clipped[0], clipped[fan - 1], clipped[fan]);
                }
            }
        }

        CompositeShadow();
    }

    private void RasterizeShadowTriangle(in RasterVertex a, in RasterVertex b, in RasterVertex c)
    {
        Project(a.Clip, out float x0, out float y0, out float z0, out _);
        Project(b.Clip, out float x1, out float y1, out float z1, out _);
        Project(c.Clip, out float x2, out float y2, out float z2, out _);

        float determinant = ((y1 - y2) * (x0 - x2)) + ((x2 - x1) * (y0 - y2));
        if (MathF.Abs(determinant) < 1e-9f)
        {
            return;
        }

        float inverse = 1.0f / determinant;

        int minX = Math.Max(0, (int)MathF.Floor(MathF.Min(x0, MathF.Min(x1, x2))));
        int maxX = Math.Min(_width - 1, (int)MathF.Ceiling(MathF.Max(x0, MathF.Max(x1, x2))));
        int minY = Math.Max(0, (int)MathF.Floor(MathF.Min(y0, MathF.Min(y1, y2))));
        int maxY = Math.Min(_height - 1, (int)MathF.Ceiling(MathF.Max(y0, MathF.Max(y1, y2))));

        for (int y = minY; y <= maxY; y++)
        {
            float py = y + 0.5f;
            int row = y * _width;

            for (int x = minX; x <= maxX; x++)
            {
                float px = x + 0.5f;

                float l0 = (((y1 - y2) * (px - x2)) + ((x2 - x1) * (py - y2))) * inverse;
                if (l0 < 0.0f)
                {
                    continue;
                }

                float l1 = (((y2 - y0) * (px - x2)) + ((x0 - x2) * (py - y2))) * inverse;
                if (l1 < 0.0f)
                {
                    continue;
                }

                float l2 = 1.0f - l0 - l1;
                if (l2 < 0.0f)
                {
                    continue;
                }

                int index = row + x;

                // The shadow lies on the plane, so it only survives where the
                // plane is what the camera can see: the depth test rejects it
                // wherever a body is standing in front.
                float depth = (l0 * z0) + (l1 * z1) + (l2 * z2);
                if (depth > _depth[index] + 1e-5f || _depth[index] >= Background)
                {
                    continue;
                }

                _shadow[index] = 1.0f;
            }
        }
    }

    private void CompositeShadow()
    {
        for (int index = 0; index < _shadow.Length; index++)
        {
            float coverage = _shadow[index];
            if (coverage <= 0.0f)
            {
                continue;
            }

            float factor = 1.0f - (coverage * Style.ShadowStrength);
            int channel = index * 3;

            _color[channel] *= factor;
            _color[channel + 1] *= factor;
            _color[channel + 2] *= factor;
        }
    }

    private void RasterizeSegment(in Segment segment)
    {
        Vector4 a = Vector4.Transform(new Vector4(PullTowardsEye(segment.From), 1.0f), _viewProjection);
        Vector4 b = Vector4.Transform(new Vector4(PullTowardsEye(segment.To), 1.0f), _viewProjection);

        if (!ClipSegmentToNearPlane(ref a, ref b))
        {
            return;
        }

        Project(a, out float x0, out float y0, out float z0, out _);
        Project(b, out float x1, out float y1, out float z1, out _);

        // Clipped to the viewport before it is walked, and not only to save
        // work: a segment pointing almost straight away from the camera
        // projects to screen coordinates in the millions, and stepping that at
        // a bounded number of steps draws a dotted line.
        if (!ClipToViewport(ref x0, ref y0, ref z0, ref x1, ref y1, ref z1))
        {
            return;
        }

        float dx = x1 - x0;
        float dy = y1 - y0;
        int steps = Math.Clamp((int)MathF.Ceiling(MathF.Max(MathF.Abs(dx), MathF.Abs(dy))), 1, 1 << 14);
        float radius = MathF.Max(0.5f, segment.Thickness * _supersample * 0.5f);

        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            Splat(
                x0 + (dx * t),
                y0 + (dy * t),
                z0 + ((z1 - z0) * t),
                radius,
                segment.Color,
                segment.Alpha,
                segment.OnTop);
        }
    }

    private bool ClipToViewport(
        ref float x0,
        ref float y0,
        ref float z0,
        ref float x1,
        ref float y1,
        ref float z1)
    {
        float dx = x1 - x0;
        float dy = y1 - y0;
        float dz = z1 - z0;

        // Liang-Barsky, with a margin so that a line just off the edge still
        // contributes the half of its width that is on screen.
        const float Margin = 4.0f;

        float enter = 0.0f;
        float exit = 1.0f;

        if (!Narrow(-dx, x0 + Margin, ref enter, ref exit) ||
            !Narrow(dx, _width + Margin - x0, ref enter, ref exit) ||
            !Narrow(-dy, y0 + Margin, ref enter, ref exit) ||
            !Narrow(dy, _height + Margin - y0, ref enter, ref exit))
        {
            return false;
        }

        x1 = x0 + (dx * exit);
        y1 = y0 + (dy * exit);
        z1 = z0 + (dz * exit);

        x0 += dx * enter;
        y0 += dy * enter;
        z0 += dz * enter;

        return true;
    }

    private static bool Narrow(float direction, float distance, ref float enter, ref float exit)
    {
        if (direction == 0.0f)
        {
            return distance >= 0.0f;
        }

        float t = distance / direction;

        if (direction < 0.0f)
        {
            if (t > exit)
            {
                return false;
            }

            enter = MathF.Max(enter, t);
        }
        else
        {
            if (t < enter)
            {
                return false;
            }

            exit = MathF.Min(exit, t);
        }

        return true;
    }

    private void RasterizeSprite(in Sprite sprite)
    {
        Vector4 clip = Vector4.Transform(new Vector4(PullTowardsEye(sprite.Position, 0.03f), 1.0f), _viewProjection);
        if (clip.Z < 0.0f)
        {
            return;
        }

        Project(clip, out float x, out float y, out float z, out _);
        Splat(
            x,
            y,
            z,
            MathF.Max(0.5f, sprite.Size * _supersample * 0.5f),
            sprite.Color,
            sprite.Alpha,
            sprite.OnTop);
    }

    private void Splat(
        float centreX,
        float centreY,
        float depth,
        float radius,
        Vector3 color,
        float alpha,
        bool onTop)
    {
        if (depth < 0.0f || depth > Background)
        {
            return;
        }

        int minX = Math.Max(0, (int)MathF.Floor(centreX - radius - 1.0f));
        int maxX = Math.Min(_width - 1, (int)MathF.Ceiling(centreX + radius + 1.0f));
        int minY = Math.Max(0, (int)MathF.Floor(centreY - radius - 1.0f));
        int maxY = Math.Min(_height - 1, (int)MathF.Ceiling(centreY + radius + 1.0f));

        for (int y = minY; y <= maxY; y++)
        {
            int row = y * _width;

            for (int x = minX; x <= maxX; x++)
            {
                int index = row + x;

                // No depth write: overlay primitives are allowed to overlap one
                // another, and the order they arrive in is not meaningful.
                if (!onTop && depth > _depth[index])
                {
                    continue;
                }

                float dx = x + 0.5f - centreX;
                float dy = y + 0.5f - centreY;
                float coverage = Math.Clamp(radius - MathF.Sqrt((dx * dx) + (dy * dy)) + 0.5f, 0.0f, 1.0f) * alpha;

                if (coverage <= 0.0f)
                {
                    continue;
                }

                int channel = index * 3;
                _color[channel] += (color.X - _color[channel]) * coverage;
                _color[channel + 1] += (color.Y - _color[channel + 1]) * coverage;
                _color[channel + 2] += (color.Z - _color[channel + 2]) * coverage;
            }
        }
    }

    private void RasterizeLabel(in Label label)
    {
        Vector4 clip = Vector4.Transform(new Vector4(label.Position, 1.0f), _viewProjection);

        // Behind the camera. A label has no extent in the world, so there is
        // nothing to clip against the near plane: it is either in front or not.
        if (clip.Z < 0.0f || clip.W <= 0.0f)
        {
            return;
        }

        Project(clip, out float x, out float y, out _, out _);

        // A point just in front of the eye projects to screen coordinates in the
        // millions, and casting one of those to an int does not give a large int
        // - it gives whatever the conversion happens to produce, which then
        // overflows the bounds arithmetic below. Rejected here instead: a label
        // that far off screen has nothing to contribute anyway.
        const float Limit = 1e6f;
        if (MathF.Abs(x) > Limit || MathF.Abs(y) > Limit)
        {
            return;
        }

        // Glyph pixels are whole buffer pixels. A fractional cell would let the
        // same label land on different pixels from one frame to the next and
        // shimmer, which at this size is all anyone would see.
        int cell = Math.Max(1, (int)MathF.Round(label.Scale * _supersample));

        // Anchored to the left of the point and centred on it vertically, which
        // is where a label reads as belonging to the thing under it. The gap
        // keeps the first stroke off whatever the anchor is marking.
        int originX = (int)MathF.Round(x) + (cell * 2);
        int originY = (int)MathF.Round(y) - (GlyphFont.Height * cell / 2);

        if (originX > _width || originY > _height ||
            originX + (GlyphFont.Measure(label.Text) * cell) < 0 ||
            originY + (GlyphFont.Height * cell) < 0)
        {
            return;
        }

        // The shadow first, offset by one output pixel. Without it white text
        // vanishes against a pale body and dark text against the backdrop, and
        // the scenes cannot promise which a label will land on.
        Blit(label.Text, originX + _supersample, originY + _supersample, cell, Vector3.Zero, 0.55f);
        Blit(label.Text, originX, originY, cell, label.Color, 1.0f);
    }

    private void Blit(string text, int originX, int originY, int cell, Vector3 color, float alpha)
    {
        int pen = originX;

        foreach (char character in text)
        {
            ReadOnlySpan<byte> glyph = GlyphFont.Glyph(character);

            for (int column = 0; column < GlyphFont.Width; column++)
            {
                int bits = glyph[column];
                if (bits == 0)
                {
                    continue;
                }

                for (int row = 0; row < GlyphFont.Height; row++)
                {
                    if ((bits & (1 << row)) != 0)
                    {
                        FillCell(pen + (column * cell), originY + (row * cell), cell, color, alpha);
                    }
                }
            }

            pen += GlyphFont.Advance * cell;
        }
    }

    private void FillCell(int left, int top, int cell, Vector3 color, float alpha)
    {
        int minX = Math.Max(0, left);
        int maxX = Math.Min(_width - 1, left + cell - 1);
        int minY = Math.Max(0, top);
        int maxY = Math.Min(_height - 1, top + cell - 1);

        for (int y = minY; y <= maxY; y++)
        {
            int row = y * _width;

            for (int x = minX; x <= maxX; x++)
            {
                // No depth test and no depth write: a label describes something
                // rather than being somewhere.
                int channel = (row + x) * 3;
                _color[channel] += (color.X - _color[channel]) * alpha;
                _color[channel + 1] += (color.Y - _color[channel + 1]) * alpha;
                _color[channel + 2] += (color.Z - _color[channel + 2]) * alpha;
            }
        }
    }

    private Image Downsample()
    {
        var image = new Image(Width, Height);
        float weight = 1.0f / (_supersample * _supersample);
        float halfWidth = Width * 0.5f;
        float halfHeight = Height * 0.5f;
        float radius = MathF.Sqrt((halfWidth * halfWidth) + (halfHeight * halfHeight));

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                float r = 0.0f;
                float g = 0.0f;
                float b = 0.0f;

                for (int sy = 0; sy < _supersample; sy++)
                {
                    int row = ((y * _supersample) + sy) * _width;

                    for (int sx = 0; sx < _supersample; sx++)
                    {
                        int channel = (row + (x * _supersample) + sx) * 3;
                        r += _color[channel];
                        g += _color[channel + 1];
                        b += _color[channel + 2];
                    }
                }

                // The vignette goes on last, over the whole image rather than
                // only the backdrop, which is what makes it read as a lens
                // rather than as a painted corner.
                float dx = (x + 0.5f - halfWidth) / radius;
                float dy = (y + 0.5f - halfHeight) / radius;
                float falloff = 1.0f - (Style.Vignette * MathF.Pow((dx * dx) + (dy * dy), 1.4f));

                int destination = ((y * Width) + x) * 3;
                image.Pixels[destination] = ToByte(r * weight * falloff);
                image.Pixels[destination + 1] = ToByte(g * weight * falloff);
                image.Pixels[destination + 2] = ToByte(b * weight * falloff);
            }
        }

        return image;
    }

    // ------------------------------------------------------------- the maths

    private Vector3 Shade(Vector3 world, Vector3 normal, Vector3 albedo)
    {
        Vector3 n = Normalize(normal);

        Vector3 toEye = _eye - world;
        float distance = MathF.Max(1e-6f, toEye.Length());
        Vector3 view = toEye / distance;

        // Two-sided. The hulls and meshes that come out of the engine are not
        // guaranteed to be wound the way this rasterizer would prefer, and a
        // back face shaded with an inward normal is a black hole in the image.
        if (Vector3.Dot(n, view) < 0.0f)
        {
            n = -n;
        }

        Vector3 light = Style.LightDirection;
        float diffuse = MathF.Max(0.0f, Vector3.Dot(n, light));

        Vector3 ambient = Vector3.Lerp(Style.GroundBounce, Style.SkyLight, (n.Y * 0.5f) + 0.5f);
        Vector3 halfway = Normalize(light + view);
        float specular = MathF.Pow(MathF.Max(0.0f, Vector3.Dot(n, halfway)), Style.Shininess) * Style.SpecularStrength;

        Vector3 lit = (albedo * (ambient + (Style.LightColor * diffuse))) + (Style.LightColor * specular);

        // Distance fade. Without it a ground plane forty metres across is as
        // bright at the horizon as it is under the camera, and the picture has
        // no depth to it at all.
        float fog = 1.0f - MathF.Exp(-distance * Style.FogDensity);

        return Vector3.Lerp(lit, Style.FogColor, fog);
    }

    private Vector3 PullTowardsEye(Vector3 point, float metres = 0.0f)
    {
        // Overlay lines are meant to be seen on the surfaces they describe, and
        // a contact normal drawn exactly on a face z-fights with it. Nudging
        // towards the camera by a fraction of the distance is a bias that works
        // at every scale, unlike a constant added to the depth value.
        Vector3 toEye = _eye - point;
        float distance = MathF.Max(1e-6f, toEye.Length());

        // Dots get an absolute nudge as well. A contact point sits at the
        // middle of the overlap between two shapes, which is a few millimetres
        // *inside* both of them, so a relative bias alone leaves it buried.
        return point + (toEye * 0.0015f) + (toEye * (metres / distance));
    }

    private void Project(in Vector4 clip, out float x, out float y, out float depth, out float invW)
    {
        invW = 1.0f / clip.W;
        x = ((clip.X * invW * 0.5f) + 0.5f) * _width;
        y = (0.5f - (clip.Y * invW * 0.5f)) * _height;
        depth = clip.Z * invW;
    }

    private void Store(int index, Vector3 color)
    {
        int channel = index * 3;
        _color[channel] = color.X;
        _color[channel + 1] = color.Y;
        _color[channel + 2] = color.Z;
    }

    private static int ClipNear(ReadOnlySpan<RasterVertex> input, Span<RasterVertex> output)
    {
        int count = 0;

        for (int i = 0; i < input.Length; i++)
        {
            RasterVertex current = input[i];
            RasterVertex previous = input[(i + input.Length - 1) % input.Length];

            bool currentInside = current.Clip.Z >= 0.0f;
            bool previousInside = previous.Clip.Z >= 0.0f;

            if (currentInside != previousInside)
            {
                float t = previous.Clip.Z / (previous.Clip.Z - current.Clip.Z);
                output[count++] = RasterVertex.Lerp(previous, current, t);
            }

            if (currentInside)
            {
                output[count++] = current;
            }
        }

        return count;
    }

    private static bool ClipSegmentToNearPlane(ref Vector4 a, ref Vector4 b)
    {
        bool aInside = a.Z >= 0.0f;
        bool bInside = b.Z >= 0.0f;

        if (!aInside && !bInside)
        {
            return false;
        }

        if (!aInside)
        {
            a = Vector4.Lerp(a, b, a.Z / (a.Z - b.Z));
        }
        else if (!bInside)
        {
            b = Vector4.Lerp(b, a, b.Z / (b.Z - a.Z));
        }

        return true;
    }

    private static Vector3 Normalize(Vector3 value)
    {
        float length = value.Length();
        return length <= 1e-9f ? Vector3.UnitY : value / length;
    }

    private static byte ToByte(float linear) => (byte)MathF.Round(Rgb.ToSrgb(linear) * 255.0f);

    // ------------------------------------------------------- recorded calls

    private readonly struct Instance(Mesh mesh, Vector3 position, Quaternion rotation, Vector3 albedo, bool castsShadow)
    {
        public Mesh Mesh { get; } = mesh;

        public Vector3 Position { get; } = position;

        public Quaternion Rotation { get; } = rotation;

        public Vector3 Albedo { get; } = albedo;

        public bool CastsShadow { get; } = castsShadow;
    }

    private readonly struct Segment(
        Vector3 from,
        Vector3 to,
        Vector3 color,
        float thickness,
        float alpha,
        bool onTop)
    {
        public Vector3 From { get; } = from;

        public Vector3 To { get; } = to;

        public Vector3 Color { get; } = color;

        public float Thickness { get; } = thickness;

        public float Alpha { get; } = alpha;

        public bool OnTop { get; } = onTop;
    }

    private readonly struct Sprite(Vector3 position, float size, Vector3 color, float alpha, bool onTop)
    {
        public Vector3 Position { get; } = position;

        public float Size { get; } = size;

        public Vector3 Color { get; } = color;

        public float Alpha { get; } = alpha;

        public bool OnTop { get; } = onTop;
    }

    private readonly struct Label(Vector3 position, string text, Vector3 color, float scale)
    {
        public Vector3 Position { get; } = position;

        public string Text { get; } = text;

        public Vector3 Color { get; } = color;

        public float Scale { get; } = scale;
    }

    private readonly struct RasterVertex(Vector4 clip, Vector3 world, Vector3 normal)
    {
        public Vector4 Clip { get; } = clip;

        public Vector3 World { get; } = world;

        public Vector3 Normal { get; } = normal;

        public static RasterVertex Lerp(in RasterVertex a, in RasterVertex b, float t) => new(
            Vector4.Lerp(a.Clip, b.Clip, t),
            Vector3.Lerp(a.World, b.World, t),
            Vector3.Lerp(a.Normal, b.Normal, t));
    }
}
