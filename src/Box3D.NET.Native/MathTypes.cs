// SPDX-License-Identifier: MIT
// Mirror of include/box3d/math_functions.h (types only).

using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Box3D.Native;

/*
 * Mapping of the Box3D vector types onto the base class library
 * ------------------------------------------------------------
 * b3Vec3 is { float x, y, z } and System.Numerics.Vector3 is { float X, Y, Z }.
 * b3Quat is { b3Vec3 v; float s }, laid out x, y, z, s, and
 * System.Numerics.Quaternion is { float X, Y, Z, W }. Both pairs agree byte for
 * byte, so the BCL types are used directly rather than being redeclared here.
 *
 * The payoff is that no conversion is ever needed at the boundary with a
 * renderer or engine, and the vectors get the BCL's SIMD paths for free.
 * Box3DLayoutTests asserts the sizes and field offsets so a future BCL change
 * cannot silently break the ABI.
 *
 * b3Pos and b3WorldTransform are only distinct types when Box3D is built with
 * BOX3D_DOUBLE_PRECISION. This binding targets the default single precision
 * build, where the C header itself declares them as typedefs of b3Vec3 and
 * b3Transform. The aliases below preserve that relationship, so a signature
 * reads the same here as it does in the header.
 */

/// <summary>A 2D vector. Mirror of <c>b3Vec2</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct b3Vec2
{
    /// <summary>The x coordinate.</summary>
    public float x;

    /// <summary>The y coordinate.</summary>
    public float y;
}

/// <summary>
/// A cosine and sine pair. Mirror of <c>b3CosSin</c>.
/// </summary>
/// <remarks>
/// Box3D computes these with a hand written implementation for cross-platform
/// determinism rather than calling into libm.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public struct b3CosSin
{
    /// <summary>The cosine of the angle.</summary>
    public float cosine;

    /// <summary>The sine of the angle.</summary>
    public float sine;
}

/// <summary>A rigid transform: a rotation followed by a translation. Mirror of <c>b3Transform</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct b3Transform : IEquatable<b3Transform>
{
    /// <summary>The translation.</summary>
    public Vector3 p;

    /// <summary>The rotation.</summary>
    public Quaternion q;

    /// <summary>The identity transform: no rotation, no translation.</summary>
    public static b3Transform Identity => new() { p = Vector3.Zero, q = Quaternion.Identity };

    /// <inheritdoc/>
    public readonly bool Equals(b3Transform other) => p.Equals(other.p) && q.Equals(other.q);

    /// <inheritdoc/>
    public readonly override bool Equals(object? obj) => obj is b3Transform other && Equals(other);

    /// <inheritdoc/>
    public readonly override int GetHashCode() => HashCode.Combine(p, q);

    /// <summary>Determines whether two transforms are equal.</summary>
    /// <param name="left">The first transform.</param>
    /// <param name="right">The second transform.</param>
    public static bool operator ==(b3Transform left, b3Transform right) => left.Equals(right);

    /// <summary>Determines whether two transforms are unequal.</summary>
    /// <param name="left">The first transform.</param>
    /// <param name="right">The second transform.</param>
    public static bool operator !=(b3Transform left, b3Transform right) => !left.Equals(right);
}

/// <summary>A 3x3 matrix stored as three column vectors. Mirror of <c>b3Matrix3</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct b3Matrix3 : IEquatable<b3Matrix3>
{
    /// <summary>The first column.</summary>
    public Vector3 cx;

    /// <summary>The second column.</summary>
    public Vector3 cy;

    /// <summary>The third column.</summary>
    public Vector3 cz;

    /// <summary>The zero matrix.</summary>
    public static b3Matrix3 Zero => default;

    /// <summary>The identity matrix.</summary>
    public static b3Matrix3 Identity => new()
    {
        cx = new Vector3(1.0f, 0.0f, 0.0f),
        cy = new Vector3(0.0f, 1.0f, 0.0f),
        cz = new Vector3(0.0f, 0.0f, 1.0f),
    };

    /// <inheritdoc/>
    public readonly bool Equals(b3Matrix3 other) => cx.Equals(other.cx) && cy.Equals(other.cy) && cz.Equals(other.cz);

    /// <inheritdoc/>
    public readonly override bool Equals(object? obj) => obj is b3Matrix3 other && Equals(other);

    /// <inheritdoc/>
    public readonly override int GetHashCode() => HashCode.Combine(cx, cy, cz);

    /// <summary>Determines whether two matrices are equal.</summary>
    /// <param name="left">The first matrix.</param>
    /// <param name="right">The second matrix.</param>
    public static bool operator ==(b3Matrix3 left, b3Matrix3 right) => left.Equals(right);

    /// <summary>Determines whether two matrices are unequal.</summary>
    /// <param name="left">The first matrix.</param>
    /// <param name="right">The second matrix.</param>
    public static bool operator !=(b3Matrix3 left, b3Matrix3 right) => !left.Equals(right);
}

/// <summary>An axis-aligned bounding box. Mirror of <c>b3AABB</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct b3AABB : IEquatable<b3AABB>
{
    /// <summary>The corner with the smallest coordinates.</summary>
    public Vector3 lowerBound;

    /// <summary>The corner with the largest coordinates.</summary>
    public Vector3 upperBound;

    /// <inheritdoc/>
    public readonly bool Equals(b3AABB other) => lowerBound.Equals(other.lowerBound) && upperBound.Equals(other.upperBound);

    /// <inheritdoc/>
    public readonly override bool Equals(object? obj) => obj is b3AABB other && Equals(other);

    /// <inheritdoc/>
    public readonly override int GetHashCode() => HashCode.Combine(lowerBound, upperBound);

    /// <summary>Determines whether two boxes are equal.</summary>
    /// <param name="left">The first box.</param>
    /// <param name="right">The second box.</param>
    public static bool operator ==(b3AABB left, b3AABB right) => left.Equals(right);

    /// <summary>Determines whether two boxes are unequal.</summary>
    /// <param name="left">The first box.</param>
    /// <param name="right">The second box.</param>
    public static bool operator !=(b3AABB left, b3AABB right) => !left.Equals(right);
}

/// <summary>
/// A plane, where <c>separation = dot(normal, point) - offset</c>. Mirror of <c>b3Plane</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct b3Plane : IEquatable<b3Plane>
{
    /// <summary>The unit normal of the plane.</summary>
    public Vector3 normal;

    /// <summary>The signed distance from the origin along the normal.</summary>
    public float offset;

    /// <inheritdoc/>
    public readonly bool Equals(b3Plane other) => normal.Equals(other.normal) && offset.Equals(other.offset);

    /// <inheritdoc/>
    public readonly override bool Equals(object? obj) => obj is b3Plane other && Equals(other);

    /// <inheritdoc/>
    public readonly override int GetHashCode() => HashCode.Combine(normal, offset);

    /// <summary>Determines whether two planes are equal.</summary>
    /// <param name="left">The first plane.</param>
    /// <param name="right">The second plane.</param>
    public static bool operator ==(b3Plane left, b3Plane right) => left.Equals(right);

    /// <summary>Determines whether two planes are unequal.</summary>
    /// <param name="left">The first plane.</param>
    /// <param name="right">The second plane.</param>
    public static bool operator !=(b3Plane left, b3Plane right) => !left.Equals(right);
}

/// <summary>
/// The closest points between two segments or infinite lines.
/// Mirror of <c>b3SegmentDistanceResult</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct b3SegmentDistanceResult
{
    /// <summary>The closest point on the first segment or line.</summary>
    public Vector3 point1;

    /// <summary>The parametric coordinate of <see cref="point1"/> along the first segment or line.</summary>
    public float fraction1;

    /// <summary>The closest point on the second segment or line.</summary>
    public Vector3 point2;

    /// <summary>The parametric coordinate of <see cref="point2"/> along the second segment or line.</summary>
    public float fraction2;
}
