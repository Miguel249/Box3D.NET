// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using Box3D.Native;

namespace Box3D;

/// <summary>
/// A sphere, positioned relative to the body it is attached to.
/// </summary>
/// <example>
/// <code>
/// // A ball of radius half a metre, centred on the body origin.
/// body.AddSphere(new Sphere(0.5f));
///
/// // A wheel hub offset from the body origin.
/// body.AddSphere(new Sphere(new Vector3(0.0f, -0.4f, 0.0f), 0.2f));
/// </code>
/// </example>
public readonly record struct Sphere
{
    /// <summary>Initializes a new instance of the <see cref="Sphere"/> struct.</summary>
    /// <param name="center">The centre, relative to the body origin.</param>
    /// <param name="radius">The radius. Must be positive.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="radius"/> is not positive.</exception>
    public Sphere(Vector3 center, float radius)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);

        Center = center;
        Radius = radius;
    }

    /// <summary>Initializes a new instance centred on the body origin.</summary>
    /// <param name="radius">The radius. Must be positive.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="radius"/> is not positive.</exception>
    public Sphere(float radius)
        : this(Vector3.Zero, radius)
    {
    }

    /// <summary>Gets the centre, relative to the body origin.</summary>
    public Vector3 Center { get; }

    /// <summary>Gets the radius.</summary>
    public float Radius { get; }

    internal b3Sphere ToNative() => new() { center = Center, radius = Radius };
}

/// <summary>
/// A capsule: a sphere swept along a segment.
/// </summary>
/// <remarks>
/// Capsules are the usual choice for characters, because they slide over small
/// steps and ledges instead of catching on them the way a box does.
/// </remarks>
/// <example>
/// <code>
/// // An upright capsule roughly the size of a person: 1.8m tall, 0.3m radius.
/// var body = Capsule.Upright(height: 1.8f, radius: 0.3f);
/// character.AddCapsule(body);
/// </code>
/// </example>
public readonly record struct Capsule
{
    /// <summary>Initializes a new instance of the <see cref="Capsule"/> struct.</summary>
    /// <param name="start">The centre of the first hemisphere, relative to the body origin.</param>
    /// <param name="end">The centre of the second hemisphere, relative to the body origin.</param>
    /// <param name="radius">The radius. Must be positive.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="radius"/> is not positive.</exception>
    public Capsule(Vector3 start, Vector3 end, float radius)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);

        Start = start;
        End = end;
        Radius = radius;
    }

    /// <summary>Gets the centre of the first hemisphere.</summary>
    public Vector3 Start { get; }

    /// <summary>Gets the centre of the second hemisphere.</summary>
    public Vector3 End { get; }

    /// <summary>Gets the radius.</summary>
    public float Radius { get; }

    /// <summary>Gets the distance between the two hemisphere centres.</summary>
    /// <remarks>
    /// A capsule whose length approaches zero is numerically awkward; use a
    /// <see cref="Sphere"/> instead of a very short capsule.
    /// </remarks>
    public float SegmentLength => Vector3.Distance(Start, End);

    /// <summary>
    /// Creates a capsule standing along the y axis, centred on the body origin.
    /// </summary>
    /// <param name="height">The total height, including both hemispherical caps.</param>
    /// <param name="radius">The radius. Must be positive and less than half the height.</param>
    /// <returns>The capsule.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="radius"/> is not positive, or the height leaves no room for the caps.
    /// </exception>
    public static Capsule Upright(float height, float radius)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);

        // The two caps account for 2 * radius of the total height, so the
        // segment gets what is left. Without room for both, the shape the caller
        // asked for is not a capsule.
        float segment = height - (2.0f * radius);
        if (segment <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(height),
                height,
                $"A capsule of radius {radius} needs a height above {2.0f * radius}. Use a Sphere for anything shorter.");
        }

        float half = 0.5f * segment;
        return new Capsule(new Vector3(0.0f, -half, 0.0f), new Vector3(0.0f, half, 0.0f), radius);
    }

    internal b3Capsule ToNative() => new() { center1 = Start, center2 = End, radius = Radius };
}

/// <summary>
/// An axis-aligned box, described by its half-widths.
/// </summary>
/// <remarks>
/// A box is a convex hull underneath. Unlike hulls built from arbitrary point
/// clouds, a box is self-contained and costs nothing to create, so there is no
/// resource to release.
/// </remarks>
/// <example>
/// <code>
/// // A one metre cube.
/// body.AddBox(Box.Cube(0.5f));
///
/// // A ground slab: 100m across, 1m thick.
/// ground.AddBox(new Box(new Vector3(50.0f, 0.5f, 50.0f)));
/// </code>
/// </example>
public readonly record struct Box
{
    /// <summary>Initializes a new instance of the <see cref="Box"/> struct.</summary>
    /// <param name="halfExtents">The half-widths along each axis. Every component must be positive.</param>
    /// <param name="center">The centre, relative to the body origin.</param>
    /// <exception cref="ArgumentOutOfRangeException">A component of <paramref name="halfExtents"/> is not positive.</exception>
    public Box(Vector3 halfExtents, Vector3 center = default)
    {
        if (halfExtents.X <= 0.0f || halfExtents.Y <= 0.0f || halfExtents.Z <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(halfExtents),
                halfExtents,
                "Every half-extent must be positive. These are half-widths, not full widths.");
        }

        HalfExtents = halfExtents;
        Center = center;
    }

    /// <summary>Gets the half-widths along each axis.</summary>
    public Vector3 HalfExtents { get; }

    /// <summary>Gets the centre, relative to the body origin.</summary>
    public Vector3 Center { get; }

    /// <summary>Gets the full size along each axis.</summary>
    public Vector3 Size => HalfExtents * 2.0f;

    /// <summary>Creates a cube.</summary>
    /// <param name="halfWidth">Half the edge length. Must be positive.</param>
    /// <returns>The cube.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="halfWidth"/> is not positive.</exception>
    public static Box Cube(float halfWidth) => new(new Vector3(halfWidth));

    /// <summary>Creates a box from its full size rather than its half-widths.</summary>
    /// <param name="size">The full size along each axis. Every component must be positive.</param>
    /// <param name="center">The centre, relative to the body origin.</param>
    /// <returns>The box.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A component of <paramref name="size"/> is not positive.</exception>
    public static Box FromSize(Vector3 size, Vector3 center = default) => new(size * 0.5f, center);

    /// <summary>
    /// Builds the native hull for this box.
    /// </summary>
    /// <remarks>
    /// The result embeds its own arrays, so it is returned by value and must
    /// never be destroyed.
    /// </remarks>
    internal b3BoxHull ToNativeHull() =>
        Center == Vector3.Zero
            ? B3.b3MakeBoxHull(HalfExtents.X, HalfExtents.Y, HalfExtents.Z)
            : B3.b3MakeOffsetBoxHull(HalfExtents.X, HalfExtents.Y, HalfExtents.Z, Center);
}
