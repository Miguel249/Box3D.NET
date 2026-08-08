// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using Box3D.Native;

namespace Box3D;

/// <summary>
/// The surface appearance Box3D suggests for a debug primitive.
/// </summary>
/// <remarks>
/// A hint, not an instruction. A renderer that draws flat wireframe can ignore
/// it entirely; one that shades solid geometry can use it to tell a sleeping
/// body from an awake one without inspecting the simulation.
/// </remarks>
public enum DebugMaterial
{
    /// <summary>Use the renderer's own appearance for this body type.</summary>
    Default = 0,

    /// <summary>A matte, non-reflective surface.</summary>
    Matte = 1,

    /// <summary>A soft-looking surface.</summary>
    Soft = 2,

    /// <summary>The appearance used for sleeping or inactive bodies.</summary>
    Dead = 3,

    /// <summary>A glossy surface.</summary>
    Glossy = 4,

    /// <summary>A metallic surface.</summary>
    Metallic = 5,
}

/// <summary>
/// A colour handed to a debug drawer, as 24-bit RGB plus a material hint.
/// </summary>
/// <remarks>
/// <para>
/// Box3D names its colours through an enum of 145 constants. That enum belongs
/// to the C API and says nothing a renderer can use, so it is not what this
/// layer hands out: what a renderer needs is the channels. The packed value is
/// kept as well, because the high byte carries the <see cref="Material"/> hint
/// and dropping it would lose information the engine went to the trouble of
/// sending.
/// </para>
/// </remarks>
public readonly record struct DebugColor
{
    private readonly uint _packed;

    /// <summary>Wraps a packed value, low 24 bits RGB and high byte the material.</summary>
    /// <param name="packed">The value as Box3D encodes it.</param>
    public DebugColor(uint packed) => _packed = packed;

    /// <summary>Builds a colour from channels, with the default material.</summary>
    /// <param name="r">The red channel.</param>
    /// <param name="g">The green channel.</param>
    /// <param name="b">The blue channel.</param>
    public DebugColor(byte r, byte g, byte b) =>
        _packed = ((uint)r << 16) | ((uint)g << 8) | b;

    /// <summary>Gets the value exactly as Box3D encoded it.</summary>
    public uint Packed => _packed;

    /// <summary>Gets the red channel.</summary>
    public byte R => (byte)((_packed >> 16) & 0xFF);

    /// <summary>Gets the green channel.</summary>
    public byte G => (byte)((_packed >> 8) & 0xFF);

    /// <summary>Gets the blue channel.</summary>
    public byte B => (byte)(_packed & 0xFF);

    /// <summary>Gets the surface appearance Box3D suggests.</summary>
    public DebugMaterial Material => (DebugMaterial)((_packed >> 24) & 0xFF);

    /// <summary>Gets the RGB channels as floats in 0..1, for a renderer that wants them that way.</summary>
    /// <returns>Red, green and blue, each scaled to 0..1.</returns>
    public (float R, float G, float B) ToUnitRgb() =>
        (R * (1.0f / 255.0f), G * (1.0f / 255.0f), B * (1.0f / 255.0f));

    /// <summary>Returns the colour as <c>#RRGGBB</c>.</summary>
    /// <returns>The hex form, without a material suffix.</returns>
    public override string ToString() => $"#{_packed & 0xFFFFFF:X6}";
}

/// <summary>
/// Receives the primitives Box3D emits while drawing a world.
/// </summary>
/// <remarks>
/// <para>
/// Implement this over whatever renderer the application already has. Box3D.NET
/// deliberately knows nothing about OpenGL, Vulkan, Unity or anything else: it
/// hands over points, segments and boxes in world space and the application
/// decides what a line looks like.
/// </para>
/// <para>
/// Every method is called synchronously from inside
/// <see cref="PhysicsWorld.Draw{T}(ref T, in DebugDrawOptions)"/>, on the thread
/// that called it, and none of them may touch the world being drawn. Reading or
/// writing the simulation from here is the same race as doing it mid-step.
/// </para>
/// <para>
/// A frame's worth of drawing allocates nothing, provided the implementation
/// does not allocate: the calls arrive through function pointers with no
/// delegate, no closure and no boxing. Implementing this on a <c>struct</c>
/// passed by <c>ref</c> keeps it that way and lets the JIT inline the calls.
/// </para>
/// </remarks>
/// <example>
/// A drawer that collects line segments for a renderer to upload:
/// <code>
/// struct SegmentCollector : IDebugDrawer
/// {
///     public List&lt;(Vector3, Vector3, DebugColor)&gt; Lines;
///
///     public void DrawSegment(Vector3 p1, Vector3 p2, DebugColor color)
///         =&gt; Lines.Add((p1, p2, color));
///
///     // ... the rest may be left empty; Box3D skips nothing, but a drawer may.
/// }
///
/// var collector = new SegmentCollector { Lines = lines };
/// world.Draw(ref collector, DebugDrawOptions.Default with { DrawJoints = true });
/// </code>
/// </example>
public interface IDebugDrawer
{
    /// <summary>Draws a line segment.</summary>
    /// <param name="p1">The start, in world space.</param>
    /// <param name="p2">The end, in world space.</param>
    /// <param name="color">The suggested colour.</param>
    void DrawSegment(Vector3 p1, Vector3 p2, DebugColor color);

    /// <summary>Draws a point.</summary>
    /// <param name="position">The position, in world space.</param>
    /// <param name="size">The size in pixels the renderer should use.</param>
    /// <param name="color">The suggested colour.</param>
    void DrawPoint(Vector3 position, float size, DebugColor color);

    /// <summary>Draws a coordinate frame, the application choosing the axis length.</summary>
    /// <param name="position">The origin of the frame, in world space.</param>
    /// <param name="rotation">The orientation of the frame.</param>
    void DrawTransform(Vector3 position, Quaternion rotation);

    /// <summary>Draws a sphere.</summary>
    /// <param name="center">The centre, in world space.</param>
    /// <param name="radius">The radius.</param>
    /// <param name="color">The suggested colour.</param>
    /// <param name="alpha">The suggested opacity, 0 to 1.</param>
    void DrawSphere(Vector3 center, float radius, DebugColor color, float alpha);

    /// <summary>Draws a capsule between two points.</summary>
    /// <param name="p1">The centre of one end cap, in world space.</param>
    /// <param name="p2">The centre of the other end cap, in world space.</param>
    /// <param name="radius">The radius.</param>
    /// <param name="color">The suggested colour.</param>
    /// <param name="alpha">The suggested opacity, 0 to 1.</param>
    void DrawCapsule(Vector3 p1, Vector3 p2, float radius, DebugColor color, float alpha);

    /// <summary>Draws an axis-aligned bounding box.</summary>
    /// <param name="bounds">The box, in world space.</param>
    /// <param name="color">The suggested colour.</param>
    void DrawBounds(BoundingBox bounds, DebugColor color);

    /// <summary>Draws an oriented box.</summary>
    /// <param name="extents">The half-extents along each local axis.</param>
    /// <param name="position">The centre, in world space.</param>
    /// <param name="rotation">The orientation.</param>
    /// <param name="color">The suggested colour.</param>
    void DrawBox(Vector3 extents, Vector3 position, Quaternion rotation, DebugColor color);

    /// <summary>Draws text anchored to a world position.</summary>
    /// <param name="position">Where the text belongs, in world space.</param>
    /// <param name="utf8Text">The text, UTF-8 encoded and not NUL-terminated.</param>
    /// <param name="color">The suggested colour.</param>
    /// <remarks>
    /// The span points at memory Box3D owns and is valid only for the duration
    /// of the call. Copy it if the renderer defers drawing to later in the
    /// frame. It arrives as UTF-8 bytes rather than a string because turning it
    /// into one would allocate on every label, every frame.
    /// </remarks>
    void DrawString(Vector3 position, ReadOnlySpan<byte> utf8Text, DebugColor color);
}

/// <summary>
/// Selects what a call to <see cref="PhysicsWorld.Draw{T}(ref T, in DebugDrawOptions)"/> emits.
/// </summary>
/// <remarks>
/// Everything is off by default. Turning on only what is being investigated
/// keeps both the emitted geometry and the cost down; drawing contacts and
/// their features across a large world is not cheap.
/// </remarks>
public readonly record struct DebugDrawOptions
{
    /// <summary>Gets the options with everything off, which is the starting point.</summary>
    /// <remarks>
    /// <see cref="Bounds"/> defaults to the whole world rather than an empty
    /// box, since a default-constructed <see cref="BoundingBox"/> would cull
    /// everything and look like a drawer that is not being called.
    /// </remarks>
    public static DebugDrawOptions Default => new()
    {
        Bounds = new BoundingBox(
            new Vector3(float.MinValue, float.MinValue, float.MinValue),
            new Vector3(float.MaxValue, float.MaxValue, float.MaxValue)),
        ForceScale = 1.0f,
        JointScale = 1.0f,
        CategoryMask = ulong.MaxValue,
    };

    /// <summary>Gets the world region outside which nothing is drawn.</summary>
    public BoundingBox Bounds { get; init; }

    /// <summary>Gets the scale applied when drawing forces.</summary>
    public float ForceScale { get; init; }

    /// <summary>Gets the scale applied when drawing joints.</summary>
    public float JointScale { get; init; }

    /// <summary>Gets the shape categories to draw. Defaults to all of them.</summary>
    public ulong CategoryMask { get; init; }

    /// <summary>
    /// Gets a value indicating whether to draw the shapes themselves.
    /// </summary>
    /// <remarks>
    /// This one needs more than a flag. Box3D draws a shape by handing back an
    /// opaque handle the application created earlier, which means registering
    /// shape callbacks when the world is built. Those are not yet surfaced by
    /// this layer, so setting this alone draws nothing. See the remarks on
    /// <see cref="PhysicsWorld.Draw{T}(ref T, in DebugDrawOptions)"/>.
    /// </remarks>
    public bool DrawShapes { get; init; }

    /// <summary>Gets a value indicating whether to draw joints.</summary>
    public bool DrawJoints { get; init; }

    /// <summary>Gets a value indicating whether to draw additional joint detail.</summary>
    public bool DrawJointExtras { get; init; }

    /// <summary>Gets a value indicating whether to draw the broad-phase bounding boxes.</summary>
    public bool DrawBounds { get; init; }

    /// <summary>Gets a value indicating whether to draw centres of mass.</summary>
    public bool DrawMass { get; init; }

    /// <summary>Gets a value indicating whether to mark sleeping bodies.</summary>
    public bool DrawSleep { get; init; }

    /// <summary>Gets a value indicating whether to draw body names.</summary>
    public bool DrawBodyNames { get; init; }

    /// <summary>Gets a value indicating whether to draw contact points.</summary>
    public bool DrawContacts { get; init; }

    /// <summary>Gets a value indicating whether to draw the anchor of each joint's first body.</summary>
    public bool DrawAnchorA { get; init; }

    /// <summary>Gets a value indicating whether to colour bodies by constraint graph colour.</summary>
    public bool DrawGraphColors { get; init; }

    /// <summary>Gets a value indicating whether to draw contact features.</summary>
    public bool DrawContactFeatures { get; init; }

    /// <summary>Gets a value indicating whether to draw contact normals.</summary>
    public bool DrawContactNormals { get; init; }

    /// <summary>Gets a value indicating whether to draw contact forces.</summary>
    public bool DrawContactForces { get; init; }

    /// <summary>Gets a value indicating whether to draw simulation islands.</summary>
    public bool DrawIslands { get; init; }

    /// <summary>Fills the native struct, leaving the callbacks to the caller.</summary>
    internal b3DebugDraw ToNative()
    {
        // Start from the engine's defaults so that any field added by a future
        // Box3D keeps its intended value rather than silently becoming zero.
        b3DebugDraw draw = B3.b3DefaultDebugDraw();

        draw.drawingBounds = new b3AABB { lowerBound = Bounds.Min, upperBound = Bounds.Max };
        draw.forceScale = ForceScale;
        draw.jointScale = JointScale;
        draw.drawShapes = DrawShapes;
        draw.drawJoints = DrawJoints;
        draw.drawJointExtras = DrawJointExtras;
        draw.drawBounds = DrawBounds;
        draw.drawMass = DrawMass;
        draw.drawSleep = DrawSleep;
        draw.drawBodyNames = DrawBodyNames;
        draw.drawContacts = DrawContacts;
        draw.drawAnchorA = DrawAnchorA;
        draw.drawGraphColors = DrawGraphColors;
        draw.drawContactFeatures = DrawContactFeatures;
        draw.drawContactNormals = DrawContactNormals;
        draw.drawContactForces = DrawContactForces;
        draw.drawIslands = DrawIslands;

        return draw;
    }
}
