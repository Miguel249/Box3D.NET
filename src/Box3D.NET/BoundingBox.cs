// SPDX-License-Identifier: MIT

using System.Numerics;
using Box3D.Native;

namespace Box3D;

/// <summary>
/// An axis-aligned bounding box.
/// </summary>
/// <example>
/// <code>
/// BoundingBox bounds = body.Bounds;
/// Console.WriteLine($"{bounds.Center} +/- {bounds.Extents}");
///
/// if (bounds.Overlaps(cameraFrustumBounds))
/// {
///     Draw(body);
/// }
/// </code>
/// </example>
public readonly record struct BoundingBox
{
    /// <summary>Initializes a new instance of the <see cref="BoundingBox"/> struct.</summary>
    /// <param name="min">The corner with the smallest coordinates.</param>
    /// <param name="max">The corner with the largest coordinates.</param>
    public BoundingBox(Vector3 min, Vector3 max)
    {
        Min = min;
        Max = max;
    }

    /// <summary>Gets the corner with the smallest coordinates.</summary>
    public Vector3 Min { get; }

    /// <summary>Gets the corner with the largest coordinates.</summary>
    public Vector3 Max { get; }

    /// <summary>Gets the centre.</summary>
    public Vector3 Center => (Min + Max) * 0.5f;

    /// <summary>Gets the half-widths along each axis.</summary>
    public Vector3 Extents => (Max - Min) * 0.5f;

    /// <summary>Gets the full size along each axis.</summary>
    public Vector3 Size => Max - Min;

    /// <summary>Gets the total surface area.</summary>
    public float SurfaceArea
    {
        get
        {
            Vector3 d = Size;
            return 2.0f * ((d.X * d.Y) + (d.Y * d.Z) + (d.Z * d.X));
        }
    }

    /// <summary>Creates a box centred on a point.</summary>
    /// <param name="center">The centre.</param>
    /// <param name="halfExtents">The half-widths along each axis.</param>
    /// <returns>The box.</returns>
    public static BoundingBox FromCenter(Vector3 center, Vector3 halfExtents) =>
        new(center - halfExtents, center + halfExtents);

    /// <summary>Determines whether this box intersects another.</summary>
    /// <param name="other">The other box.</param>
    /// <returns><see langword="true"/> when they intersect.</returns>
    /// <remarks>Boxes that merely touch along a face count as intersecting, matching the broad phase.</remarks>
    public bool Overlaps(BoundingBox other) =>
        B3Math.b3AABB_Overlaps(ToNative(), other.ToNative());

    /// <summary>Determines whether this box fully contains another.</summary>
    /// <param name="other">The other box.</param>
    /// <returns><see langword="true"/> when <paramref name="other"/> lies entirely inside this box.</returns>
    public bool Contains(BoundingBox other) =>
        B3Math.b3AABB_Contains(ToNative(), other.ToNative());

    /// <summary>Returns the smallest box containing both this box and another.</summary>
    /// <param name="other">The other box.</param>
    /// <returns>The union.</returns>
    public BoundingBox Union(BoundingBox other) =>
        FromNative(B3Math.b3AABB_Union(ToNative(), other.ToNative()));

    /// <summary>Returns this box expanded uniformly on every side.</summary>
    /// <param name="amount">The distance to expand by.</param>
    /// <returns>The expanded box.</returns>
    public BoundingBox Inflate(float amount) =>
        FromNative(B3Math.b3AABB_Inflate(ToNative(), amount));

    /// <summary>Returns the closest point inside this box to a target.</summary>
    /// <param name="point">The target point.</param>
    /// <returns>The closest point, which is the target itself when it lies inside.</returns>
    public Vector3 ClosestPoint(Vector3 point) =>
        B3Math.b3ClosestPointToAABB(point, ToNative());

    internal b3AABB ToNative() => new() { lowerBound = Min, upperBound = Max };

    internal static BoundingBox FromNative(in b3AABB box) => new(box.lowerBound, box.upperBound);
}
