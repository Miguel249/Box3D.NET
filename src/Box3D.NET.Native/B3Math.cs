// SPDX-License-Identifier: MIT
// Port of the B3_INLINE functions in include/box3d/math_functions.h.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Box3D.Native;

/// <summary>
/// The Box3D math helpers that are not exported from the native library.
/// </summary>
/// <remarks>
/// <para>
/// Most of the functions in <c>math_functions.h</c> are declared
/// <c>B3_INLINE</c>, which expands to <c>static inline</c>. They have internal
/// linkage in every translation unit and are therefore absent from the shared
/// library's export table: they cannot be bound with a P/Invoke and are
/// reimplemented here instead.
/// </para>
/// <para>
/// The implementations follow the C source operation by operation rather than
/// delegating to <see cref="System.Numerics"/>. Box3D guarantees cross-platform
/// determinism, and floating point addition is not associative, so reordering
/// the terms of a dot product or a normalization can change the last bit of the
/// result. Where Box3D calls its own deterministic <c>b3Atan2</c> or
/// <c>b3ComputeCosSin</c>, this code calls into the native library for the same
/// reason.
/// </para>
/// </remarks>
public static class B3Math
{
    /*
     * The C limits, which do not have exact BCL equivalents.
     *
     * FLT_MIN is the smallest *normalized* positive float. It is not
     * float.Epsilon, which is the smallest subnormal and is around 10^-45.
     * Confusing the two turns "is this vector long enough to normalize?" into a
     * test that essentially never fires, so a near-zero vector would be
     * normalized into infinities.
     */

    /// <summary>The smallest normalized positive <see cref="float"/>, equivalent to C's <c>FLT_MIN</c>.</summary>
    public const float FltMin = 1.17549435e-38f;

    /// <summary>The difference between one and the next representable <see cref="float"/>, equivalent to C's <c>FLT_EPSILON</c>.</summary>
    public const float FltEpsilon = 1.19209290e-07f;

    // ------------------------------------------------------------- scalars

    /// <summary>Returns the smaller of two integers.</summary>
    /// <param name="a">The first value.</param>
    /// <param name="b">The second value.</param>
    /// <returns>The minimum.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int b3MinInt(int a, int b) => a < b ? a : b;

    /// <summary>Returns the larger of two integers.</summary>
    /// <param name="a">The first value.</param>
    /// <param name="b">The second value.</param>
    /// <returns>The maximum.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int b3MaxInt(int a, int b) => a > b ? a : b;

    /// <summary>Clamps an integer to a range.</summary>
    /// <param name="a">The value to clamp.</param>
    /// <param name="lower">The lower bound.</param>
    /// <param name="upper">The upper bound.</param>
    /// <returns>The clamped value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int b3ClampInt(int a, int lower, int upper) => a < lower ? lower : (upper < a ? upper : a);

    /// <summary>Returns the absolute value of a number.</summary>
    /// <param name="a">The value.</param>
    /// <returns>The absolute value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float b3AbsFloat(float a) => a < 0 ? -a : a;

    /// <summary>Returns the smaller of two numbers.</summary>
    /// <param name="a">The first value.</param>
    /// <param name="b">The second value.</param>
    /// <returns>The minimum.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float b3MinFloat(float a, float b) => a < b ? a : b;

    /// <summary>Returns the larger of two numbers.</summary>
    /// <param name="a">The first value.</param>
    /// <param name="b">The second value.</param>
    /// <returns>The maximum.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float b3MaxFloat(float a, float b) => a > b ? a : b;

    /// <summary>Clamps a number to a range.</summary>
    /// <param name="a">The value to clamp.</param>
    /// <param name="lower">The lower bound.</param>
    /// <param name="upper">The upper bound.</param>
    /// <returns>The clamped value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float b3ClampFloat(float a, float lower, float upper) => a < lower ? lower : (upper < a ? upper : a);

    /// <summary>Linearly interpolates between two numbers.</summary>
    /// <param name="a">The value at <paramref name="alpha"/> zero.</param>
    /// <param name="b">The value at <paramref name="alpha"/> one.</param>
    /// <param name="alpha">The interpolation parameter.</param>
    /// <returns>The interpolated value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float b3LerpFloat(float a, float b, float alpha) => ((1.0f - alpha) * a) + (alpha * b);

    /// <summary>Computes the sine of an angle.</summary>
    /// <param name="radians">The angle in radians.</param>
    /// <returns>The sine.</returns>
    /// <remarks>Deprecated in Box3D in favour of <see cref="B3.b3ComputeCosSin"/>.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float b3Sin(float radians) => B3.b3ComputeCosSin(radians).sine;

    /// <summary>Computes the cosine of an angle.</summary>
    /// <param name="radians">The angle in radians.</param>
    /// <returns>The cosine.</returns>
    /// <remarks>Deprecated in Box3D in favour of <see cref="B3.b3ComputeCosSin"/>.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float b3Cos(float radians) => B3.b3ComputeCosSin(radians).cosine;

    /// <summary>Wraps an angle into the range minus pi to pi.</summary>
    /// <param name="radians">The angle in radians.</param>
    /// <returns>The wrapped angle.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float b3UnwindAngle(float radians) =>
        (float)Math.IEEERemainder(radians, 2.0f * Constants.B3_PI);

    // ------------------------------------------------------------- vectors

    /// <summary>Adds two vectors.</summary>
    /// <param name="a">The first vector.</param>
    /// <param name="b">The second vector.</param>
    /// <returns>The sum.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 b3Add(Vector3 a, Vector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    /// <summary>Subtracts one vector from another.</summary>
    /// <param name="a">The vector to subtract from.</param>
    /// <param name="b">The vector to subtract.</param>
    /// <returns>The difference.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 b3Sub(Vector3 a, Vector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    /// <summary>Multiplies two vectors component-wise.</summary>
    /// <param name="a">The first vector.</param>
    /// <param name="b">The second vector.</param>
    /// <returns>The component-wise product.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 b3Mul(Vector3 a, Vector3 b) => new(a.X * b.X, a.Y * b.Y, a.Z * b.Z);

    /// <summary>Negates a vector.</summary>
    /// <param name="a">The vector.</param>
    /// <returns>The negated vector.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 b3Neg(Vector3 a) => new(-a.X, -a.Y, -a.Z);

    /// <summary>Computes the dot product of two vectors.</summary>
    /// <param name="a">The first vector.</param>
    /// <param name="b">The second vector.</param>
    /// <returns>The dot product.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float b3Dot(Vector3 a, Vector3 b) => (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z);

    /// <summary>Computes the length of a vector.</summary>
    /// <param name="v">The vector.</param>
    /// <returns>The length.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float b3Length(Vector3 v) => MathF.Sqrt(b3Dot(v, v));

    /// <summary>Computes the squared length of a vector.</summary>
    /// <param name="a">The vector.</param>
    /// <returns>The squared length.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float b3LengthSquared(Vector3 a) => (a.X * a.X) + (a.Y * a.Y) + (a.Z * a.Z);

    /// <summary>Computes the distance between two points.</summary>
    /// <param name="a">The first point.</param>
    /// <param name="b">The second point.</param>
    /// <returns>The distance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float b3Distance(Vector3 a, Vector3 b) =>
        b3Length(new Vector3(b.X - a.X, b.Y - a.Y, b.Z - a.Z));

    /// <summary>Computes the squared distance between two points.</summary>
    /// <param name="a">The first point.</param>
    /// <param name="b">The second point.</param>
    /// <returns>The squared distance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float b3DistanceSquared(Vector3 a, Vector3 b)
    {
        Vector3 dv = new(b.X - a.X, b.Y - a.Y, b.Z - a.Z);
        return (dv.X * dv.X) + (dv.Y * dv.Y) + (dv.Z * dv.Z);
    }

    /// <summary>Normalizes a vector.</summary>
    /// <param name="a">The vector.</param>
    /// <returns>The unit vector, or the zero vector if the input is very small.</returns>
    public static Vector3 b3Normalize(Vector3 a)
    {
        float lengthSquared = (a.X * a.X) + (a.Y * a.Y) + (a.Z * a.Z);

        if (lengthSquared > 1000.0f * FltMin)
        {
            float s = 1.0f / MathF.Sqrt(lengthSquared);
            return new Vector3(s * a.X, s * a.Y, s * a.Z);
        }

        return Vector3.Zero;
    }

    /// <summary>Normalizes a vector and reports its original length.</summary>
    /// <param name="length">Receives the original length.</param>
    /// <param name="a">The vector.</param>
    /// <returns>The unit vector, or the zero vector if the input is very small.</returns>
    public static Vector3 b3GetLengthAndNormalize(out float length, Vector3 a)
    {
        length = b3Length(a);
        if (length < FltEpsilon)
        {
            return Vector3.Zero;
        }

        float invLength = 1.0f / length;
        return new Vector3(invLength * a.X, invLength * a.Y, invLength * a.Z);
    }

    /// <summary>Computes a unit vector perpendicular to the given vector.</summary>
    /// <param name="a">The vector.</param>
    /// <returns>A perpendicular unit vector.</returns>
    public static Vector3 b3Perp(Vector3 a)
    {
        // At least one component of a unit vector is at least sqrt(1/3), so
        // selecting on the x component always leaves a well-conditioned result.
        Vector3 p = (a.X < -0.5f || 0.5f < a.X)
            ? new Vector3(a.Y, -a.X, 0.0f)
            : new Vector3(0.0f, a.Z, -a.Y);

        return b3Normalize(p);
    }

    /// <summary>Determines whether a vector has unit length.</summary>
    /// <param name="a">The vector.</param>
    /// <returns><see langword="true"/> when the vector is normalized.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool b3IsNormalized(Vector3 a)
    {
        float aa = b3Dot(a, a);
        return b3AbsFloat(1.0f - aa) < 100.0f * FltEpsilon;
    }

    /// <summary>Computes <c>a + s * b</c>.</summary>
    /// <param name="a">The base vector.</param>
    /// <param name="s">The scale applied to <paramref name="b"/>.</param>
    /// <param name="b">The scaled vector.</param>
    /// <returns>The result.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 b3MulAdd(Vector3 a, float s, Vector3 b) =>
        new(a.X + (s * b.X), a.Y + (s * b.Y), a.Z + (s * b.Z));

    /// <summary>Computes <c>a - s * b</c>.</summary>
    /// <param name="a">The base vector.</param>
    /// <param name="s">The scale applied to <paramref name="b"/>.</param>
    /// <param name="b">The scaled vector.</param>
    /// <returns>The result.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 b3MulSub(Vector3 a, float s, Vector3 b) =>
        new(a.X - (s * b.X), a.Y - (s * b.Y), a.Z - (s * b.Z));

    /// <summary>Scales a vector.</summary>
    /// <param name="s">The scale.</param>
    /// <param name="a">The vector.</param>
    /// <returns>The scaled vector.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 b3MulSV(float s, Vector3 a) => new(s * a.X, s * a.Y, s * a.Z);

    /// <summary>Computes the cross product of two vectors.</summary>
    /// <param name="a">The first vector.</param>
    /// <param name="b">The second vector.</param>
    /// <returns>The cross product.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 b3Cross(Vector3 a, Vector3 b) => new(
        (a.Y * b.Z) - (a.Z * b.Y),
        (a.Z * b.X) - (a.X * b.Z),
        (a.X * b.Y) - (a.Y * b.X));

    /// <summary>Linearly interpolates between two vectors.</summary>
    /// <param name="a">The vector at <paramref name="alpha"/> zero.</param>
    /// <param name="b">The vector at <paramref name="alpha"/> one.</param>
    /// <param name="alpha">The interpolation parameter, from zero to one.</param>
    /// <returns>The interpolated vector.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 b3Lerp(Vector3 a, Vector3 b, float alpha) => new(
        ((1.0f - alpha) * a.X) + (alpha * b.X),
        ((1.0f - alpha) * a.Y) + (alpha * b.Y),
        ((1.0f - alpha) * a.Z) + (alpha * b.Z));

    /// <summary>Computes <c>s * a + t * b</c>.</summary>
    /// <param name="s">The scale applied to <paramref name="a"/>.</param>
    /// <param name="a">The first vector.</param>
    /// <param name="t">The scale applied to <paramref name="b"/>.</param>
    /// <param name="b">The second vector.</param>
    /// <returns>The blended vector.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 b3Blend2(float s, Vector3 a, float t, Vector3 b) => new(
        (s * a.X) + (t * b.X),
        (s * a.Y) + (t * b.Y),
        (s * a.Z) + (t * b.Z));

    /// <summary>Computes the component-wise absolute value of a vector.</summary>
    /// <param name="a">The vector.</param>
    /// <returns>The component-wise absolute value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 b3Abs(Vector3 a) => new(b3AbsFloat(a.X), b3AbsFloat(a.Y), b3AbsFloat(a.Z));

    /// <summary>Computes the component-wise sign of a vector, treating zero as positive.</summary>
    /// <param name="a">The vector.</param>
    /// <returns>A vector of minus one and one values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 b3Sign(Vector3 a) => new(
        a.X >= 0.0f ? 1.0f : -1.0f,
        a.Y >= 0.0f ? 1.0f : -1.0f,
        a.Z >= 0.0f ? 1.0f : -1.0f);

    /// <summary>Computes the component-wise minimum of two vectors.</summary>
    /// <param name="a">The first vector.</param>
    /// <param name="b">The second vector.</param>
    /// <returns>The component-wise minimum.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 b3Min(Vector3 a, Vector3 b) =>
        new(b3MinFloat(a.X, b.X), b3MinFloat(a.Y, b.Y), b3MinFloat(a.Z, b.Z));

    /// <summary>Computes the component-wise maximum of two vectors.</summary>
    /// <param name="a">The first vector.</param>
    /// <param name="b">The second vector.</param>
    /// <returns>The component-wise maximum.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 b3Max(Vector3 a, Vector3 b) =>
        new(b3MaxFloat(a.X, b.X), b3MaxFloat(a.Y, b.Y), b3MaxFloat(a.Z, b.Z));

    /// <summary>Clamps a vector component-wise to a range.</summary>
    /// <param name="a">The vector to clamp.</param>
    /// <param name="lower">The lower bound.</param>
    /// <param name="upper">The upper bound.</param>
    /// <returns>The clamped vector.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 b3Clamp(Vector3 a, Vector3 lower, Vector3 upper) => new(
        b3ClampFloat(a.X, lower.X, upper.X),
        b3ClampFloat(a.Y, lower.Y, upper.Y),
        b3ClampFloat(a.Z, lower.Z, upper.Z));

    /// <summary>
    /// Makes a scale value safe for scaling collision geometry.
    /// </summary>
    /// <param name="a">The requested scale.</param>
    /// <returns>The scale, with each component pushed away from zero.</returns>
    /// <remarks>Negative scale is preserved; magnitudes are clamped up to <see cref="Constants.B3_MIN_SCALE"/>.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 b3SafeScale(Vector3 a)
    {
        Vector3 absScale = b3Abs(a);
        Vector3 minScale = new(Constants.B3_MIN_SCALE, Constants.B3_MIN_SCALE, Constants.B3_MIN_SCALE);
        return b3Mul(b3Sign(a), b3Max(absScale, minScale));
    }

    // --------------------------------------------------------- quaternions

    /// <summary>Determines whether a quaternion has unit length.</summary>
    /// <param name="q">The quaternion.</param>
    /// <returns><see langword="true"/> when the quaternion is normalized.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool b3IsNormalizedQuat(Quaternion q)
    {
        float qq = (q.X * q.X) + (q.Y * q.Y) + (q.Z * q.Z) + (q.W * q.W);
        return 1.0f - (20.0f * FltEpsilon) < qq && qq < 1.0f + (20.0f * FltEpsilon);
    }

    /// <summary>Rotates a vector by a quaternion.</summary>
    /// <param name="q">The rotation.</param>
    /// <param name="v">The vector.</param>
    /// <returns>The rotated vector.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 b3RotateVector(Quaternion q, Vector3 v)
    {
        // v + 2 * cross(q.v, cross(q.v, v) + q.s * v)
        Vector3 qv = new(q.X, q.Y, q.Z);
        Vector3 t1 = b3Cross(qv, v);
        Vector3 t2 = b3MulAdd(t1, q.W, v);
        Vector3 t3 = b3Cross(qv, t2);
        return b3MulAdd(v, 2.0f, t3);
    }

    /// <summary>Rotates a vector by the inverse of a quaternion.</summary>
    /// <param name="q">The rotation.</param>
    /// <param name="v">The vector.</param>
    /// <returns>The inverse-rotated vector.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 b3InvRotateVector(Quaternion q, Vector3 v)
    {
        // v + 2 * cross(q.v, cross(q.v, v) - q.s * v)
        Vector3 qv = new(q.X, q.Y, q.Z);
        Vector3 t1 = b3Cross(qv, v);
        Vector3 t2 = b3MulSub(t1, q.W, v);
        Vector3 t3 = b3Cross(qv, t2);
        return b3MulAdd(v, 2.0f, t3);
    }

    /// <summary>Computes the dot product of two quaternions, which is useful for polarity tests.</summary>
    /// <param name="a">The first quaternion.</param>
    /// <param name="b">The second quaternion.</param>
    /// <returns>The dot product.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float b3DotQuat(Quaternion a, Quaternion b) =>
        (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z) + (a.W * b.W);

    /// <summary>Multiplies two quaternions.</summary>
    /// <param name="q1">The first rotation.</param>
    /// <param name="q2">The second rotation.</param>
    /// <returns>The composed rotation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Quaternion b3MulQuat(Quaternion q1, Quaternion q2)
    {
        Vector3 v1 = new(q1.X, q1.Y, q1.Z);
        Vector3 v2 = new(q2.X, q2.Y, q2.Z);
        Vector3 t1 = b3Cross(v1, v2);
        Vector3 t2 = b3MulAdd(t1, q1.W, v2);
        Vector3 t3 = b3MulAdd(t2, q2.W, v1);
        return new Quaternion(t3.X, t3.Y, t3.Z, (q1.W * q2.W) - b3Dot(v1, v2));
    }

    /// <summary>Computes the relative rotation <c>inv(q1) * q2</c>.</summary>
    /// <param name="q1">The reference rotation.</param>
    /// <param name="q2">The target rotation.</param>
    /// <returns>The relative rotation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Quaternion b3InvMulQuat(Quaternion q1, Quaternion q2)
    {
        Vector3 v1 = new(q1.X, q1.Y, q1.Z);
        Vector3 v2 = new(q2.X, q2.Y, q2.Z);
        Vector3 t1 = b3Cross(v2, v1);
        Vector3 t2 = b3MulAdd(t1, q1.W, v2);
        Vector3 t3 = b3MulSub(t2, q2.W, v1);
        return new Quaternion(t3.X, t3.Y, t3.Z, (q1.W * q2.W) + b3Dot(v1, v2));
    }

    /// <summary>Computes the conjugate of a quaternion, which is a cheap inverse for unit quaternions.</summary>
    /// <param name="q">The quaternion.</param>
    /// <returns>The conjugate.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Quaternion b3Conjugate(Quaternion q) => new(-q.X, -q.Y, -q.Z, q.W);

    /// <summary>Negates every component of a quaternion.</summary>
    /// <param name="q">The quaternion.</param>
    /// <returns>The negated quaternion, which represents the same rotation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Quaternion b3NegateQuat(Quaternion q) => new(-q.X, -q.Y, -q.Z, -q.W);

    /// <summary>Normalizes a quaternion.</summary>
    /// <param name="q">The quaternion.</param>
    /// <returns>The unit quaternion, or the identity if the input is very small.</returns>
    public static Quaternion b3NormalizeQuat(Quaternion q)
    {
        float lengthSq = b3DotQuat(q, q);
        if (lengthSq > 1000.0f * FltMin)
        {
            float s = 1.0f / MathF.Sqrt(lengthSq);
            return new Quaternion(s * q.X, s * q.Y, s * q.Z, s * q.W);
        }

        return Quaternion.Identity;
    }

    /// <summary>Builds a rotation about an axis.</summary>
    /// <param name="axis">The rotation axis, which must be normalized.</param>
    /// <param name="radians">The rotation angle in radians.</param>
    /// <returns>The rotation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Quaternion b3MakeQuatFromAxisAngle(Vector3 axis, float radians)
    {
        b3CosSin cs = B3.b3ComputeCosSin(0.5f * radians);
        return new Quaternion(cs.sine * axis.X, cs.sine * axis.Y, cs.sine * axis.Z, cs.cosine);
    }

    /// <summary>Extracts the axis and angle of a rotation.</summary>
    /// <param name="radians">Receives the rotation angle in radians.</param>
    /// <param name="q">The rotation, which is assumed to be normalized.</param>
    /// <returns>The rotation axis, or the zero vector for the identity rotation.</returns>
    public static Vector3 b3GetAxisAngle(out float radians, Quaternion q)
    {
        float length = MathF.Sqrt((q.X * q.X) + (q.Y * q.Y) + (q.Z * q.Z));
        radians = 2.0f * B3.b3Atan2(length, q.W);
        if (length > 0.0f)
        {
            float invLength = 1.0f / length;
            return new Vector3(invLength * q.X, invLength * q.Y, invLength * q.Z);
        }

        return Vector3.Zero;
    }

    /// <summary>Gets the rotation angle of a quaternion.</summary>
    /// <param name="q">The rotation.</param>
    /// <returns>The angle in radians.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float b3GetQuatAngle(Quaternion q)
    {
        float length = MathF.Sqrt((q.X * q.X) + (q.Y * q.Y) + (q.Z * q.Z));
        return 2.0f * B3.b3Atan2(length, q.W);
    }

    /// <summary>
    /// Gets the twist angle about the z axis, used by twist limits and the
    /// revolute joint angle limit.
    /// </summary>
    /// <param name="q">The rotation.</param>
    /// <returns>The twist angle in radians, in the range minus pi to pi.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float b3GetTwistAngle(Quaternion q)
    {
        // Polarity is folded in here so callers do not have to unwind the result.
        float twist = q.W < 0.0f ? B3.b3Atan2(-q.Z, -q.W) : B3.b3Atan2(q.Z, q.W);
        return twist * 2.0f;
    }

    /// <summary>Gets the swing angle away from the z axis, used by cone limits.</summary>
    /// <param name="q">The rotation.</param>
    /// <returns>The swing angle in radians, in the range zero to pi.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float b3GetSwingAngle(Quaternion q)
    {
        // Polarity does not matter because every term is squared.
        float x = MathF.Sqrt((q.Z * q.Z) + (q.W * q.W));
        float y = MathF.Sqrt((q.X * q.X) + (q.Y * q.Y));
        return 2.0f * B3.b3Atan2(y, x);
    }

    /// <summary>Interpolates between two rotations and normalizes the result.</summary>
    /// <param name="q1">The rotation at <paramref name="alpha"/> zero.</param>
    /// <param name="q2">The rotation at <paramref name="alpha"/> one.</param>
    /// <param name="alpha">The interpolation parameter, from zero to one.</param>
    /// <returns>The interpolated rotation.</returns>
    public static Quaternion b3NLerp(Quaternion q1, Quaternion q2, float alpha)
    {
        if (b3DotQuat(q1, q2) < 0.0f)
        {
            q1 = new Quaternion(-q1.X, -q1.Y, -q1.Z, -q1.W);
        }

        Vector3 v = b3Lerp(new Vector3(q1.X, q1.Y, q1.Z), new Vector3(q2.X, q2.Y, q2.Z), alpha);
        float s = ((1.0f - alpha) * q1.W) + (alpha * q2.W);

        return b3NormalizeQuat(new Quaternion(v.X, v.Y, v.Z, s));
    }

    // ---------------------------------------------------------- transforms

    /// <summary>
    /// Composes two transforms, so that the result maps a point in frame B into
    /// the frame that <paramref name="a"/> maps into.
    /// </summary>
    /// <param name="a">The outer transform.</param>
    /// <param name="b">The inner transform.</param>
    /// <returns>The composed transform.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static b3Transform b3MulTransforms(b3Transform a, b3Transform b) => new()
    {
        p = b3Add(b3RotateVector(a.q, b.p), a.p),
        q = b3MulQuat(a.q, b.q),
    };

    /// <summary>Builds the transform that converts a point local to frame B into a point local to frame A.</summary>
    /// <param name="a">The first frame.</param>
    /// <param name="b">The second frame.</param>
    /// <returns>The relative transform.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static b3Transform b3InvMulTransforms(b3Transform a, b3Transform b) => new()
    {
        p = b3InvRotateVector(a.q, b3Sub(b.p, a.p)),
        q = b3InvMulQuat(a.q, b.q),
    };

    /// <summary>Inverts a transform.</summary>
    /// <param name="t">The transform.</param>
    /// <returns>The inverse transform.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static b3Transform b3InvertTransform(b3Transform t) => new()
    {
        p = b3InvRotateVector(t.q, b3Neg(t.p)),
        q = b3Conjugate(t.q),
    };

    /// <summary>Applies a transform to a point.</summary>
    /// <param name="t">The transform.</param>
    /// <param name="v">The point.</param>
    /// <returns>The transformed point.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 b3TransformPoint(b3Transform t, Vector3 v) => b3Add(b3RotateVector(t.q, v), t.p);

    /// <summary>Applies the inverse of a transform to a point.</summary>
    /// <param name="t">The transform.</param>
    /// <param name="v">The point.</param>
    /// <returns>The inverse-transformed point.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 b3InvTransformPoint(b3Transform t, Vector3 v) => b3InvRotateVector(t.q, b3Sub(v, t.p));

    /*
     * The world position boundary.
     *
     * In large world mode these functions cross between a double-precision
     * public boundary and a float interior. This binding targets the single
     * precision build, where b3Pos is b3Vec3 and b3WorldTransform is
     * b3Transform, so the conversions collapse to identity and the explicit
     * casts in the C source become no-ops. They are kept so that code written
     * against the C API translates unchanged.
     */

    /// <summary>Converts a vector to a world position.</summary>
    /// <param name="v">The vector.</param>
    /// <returns>The world position.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 b3ToPos(Vector3 v) => v;

    /// <summary>Converts a world position to a vector.</summary>
    /// <param name="p">The world position.</param>
    /// <returns>The vector.</returns>
    /// <remarks>Lossy in large world mode; exact here.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 b3ToVec3(Vector3 p) => p;

    /// <summary>Subtracts two world positions, producing a local offset.</summary>
    /// <param name="a">The position to subtract from.</param>
    /// <param name="b">The position to subtract.</param>
    /// <returns>The offset.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 b3SubPos(Vector3 a, Vector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    /// <summary>Offsets a world position by a vector.</summary>
    /// <param name="p">The position.</param>
    /// <param name="d">The offset.</param>
    /// <returns>The offset position.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 b3OffsetPos(Vector3 p, Vector3 d) => new(p.X + d.X, p.Y + d.Y, p.Z + d.Z);

    /// <summary>Interpolates between two world positions.</summary>
    /// <param name="a">The position at <paramref name="t"/> zero.</param>
    /// <param name="b">The position at <paramref name="t"/> one.</param>
    /// <param name="t">The interpolation parameter.</param>
    /// <returns>The interpolated position.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 b3LerpPosition(Vector3 a, Vector3 b, float t) => new(
        ((1.0f - t) * a.X) + (t * b.X),
        ((1.0f - t) * a.Y) + (t * b.Y),
        ((1.0f - t) * a.Z) + (t * b.Z));

    /// <summary>Transforms a local point into a world position.</summary>
    /// <param name="t">The world transform.</param>
    /// <param name="p">The local point.</param>
    /// <returns>The world position.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 b3TransformWorldPoint(b3Transform t, Vector3 p)
    {
        Vector3 r = b3RotateVector(t.q, p);
        return new Vector3(t.p.X + r.X, t.p.Y + r.Y, t.p.Z + r.Z);
    }

    /// <summary>Transforms a world position into a local point.</summary>
    /// <param name="t">The world transform.</param>
    /// <param name="p">The world position.</param>
    /// <returns>The local point.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 b3InvTransformWorldPoint(b3Transform t, Vector3 p)
    {
        Vector3 d = new(p.X - t.p.X, p.Y - t.p.Y, p.Z - t.p.Z);
        return b3InvRotateVector(t.q, d);
    }

    /// <summary>Computes the pose of world frame B relative to world frame A.</summary>
    /// <param name="a">The reference frame.</param>
    /// <param name="b">The target frame.</param>
    /// <returns>The relative transform.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static b3Transform b3InvMulWorldTransforms(b3Transform a, b3Transform b)
    {
        Vector3 d = new(b.p.X - a.p.X, b.p.Y - a.p.Y, b.p.Z - a.p.Z);
        return new b3Transform
        {
            q = b3InvMulQuat(a.q, b.q),
            p = b3InvRotateVector(a.q, d),
        };
    }

    /// <summary>Composes a world transform with a local transform.</summary>
    /// <param name="a">The world transform.</param>
    /// <param name="b">The local transform.</param>
    /// <returns>The composed world transform.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static b3Transform b3MulWorldTransforms(b3Transform a, b3Transform b)
    {
        Vector3 r = b3RotateVector(a.q, b.p);
        return new b3Transform
        {
            q = b3MulQuat(a.q, b.q),
            p = new Vector3(a.p.X + r.X, a.p.Y + r.Y, a.p.Z + r.Z),
        };
    }

    /// <summary>Shifts a world transform into the frame of a base position.</summary>
    /// <param name="t">The world transform.</param>
    /// <param name="basePosition">The base position.</param>
    /// <returns>The transform relative to the base position.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static b3Transform b3ToRelativeTransform(b3Transform t, Vector3 basePosition) => new()
    {
        q = t.q,
        p = new Vector3(t.p.X - basePosition.X, t.p.Y - basePosition.Y, t.p.Z - basePosition.Z),
    };

    /// <summary>Promotes a local transform to a world transform.</summary>
    /// <param name="t">The transform.</param>
    /// <returns>The world transform.</returns>
    /// <remarks>Lossless in both precision modes.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static b3Transform b3MakeWorldTransform(b3Transform t) => t;

    /// <summary>Narrows a world coordinate towards negative infinity.</summary>
    /// <param name="x">The coordinate.</param>
    /// <returns>The narrowed coordinate.</returns>
    /// <remarks>A plain conversion in the single-precision build.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float b3RoundDownFloat(double x) => (float)x;

    /// <summary>Narrows a world coordinate towards positive infinity.</summary>
    /// <param name="x">The coordinate.</param>
    /// <returns>The narrowed coordinate.</returns>
    /// <remarks>A plain conversion in the single-precision build.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float b3RoundUpFloat(double x) => (float)x;

    // ------------------------------------------------------------ matrices

    /// <summary>Computes the determinant of a matrix.</summary>
    /// <param name="m">The matrix.</param>
    /// <returns>The determinant.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float b3Det(b3Matrix3 m) => b3Dot(m.cx, b3Cross(m.cy, m.cz));

    /// <summary>Multiplies a matrix by a column vector.</summary>
    /// <param name="m">The matrix.</param>
    /// <param name="a">The vector.</param>
    /// <returns>The transformed vector.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 b3MulMV(b3Matrix3 m, Vector3 a) => new(
        (m.cx.X * a.X) + (m.cy.X * a.Y) + (m.cz.X * a.Z),
        (m.cx.Y * a.X) + (m.cy.Y * a.Y) + (m.cz.Y * a.Z),
        (m.cx.Z * a.X) + (m.cy.Z * a.Y) + (m.cz.Z * a.Z));

    /// <summary>Negates a matrix.</summary>
    /// <param name="a">The matrix.</param>
    /// <returns>The negated matrix.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static b3Matrix3 b3NegateMat3(b3Matrix3 a) => new()
    {
        cx = b3Neg(a.cx),
        cy = b3Neg(a.cy),
        cz = b3Neg(a.cz),
    };

    /// <summary>Adds two matrices.</summary>
    /// <param name="a">The first matrix.</param>
    /// <param name="b">The second matrix.</param>
    /// <returns>The sum.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static b3Matrix3 b3AddMM(b3Matrix3 a, b3Matrix3 b) => new()
    {
        cx = b3Add(a.cx, b.cx),
        cy = b3Add(a.cy, b.cy),
        cz = b3Add(a.cz, b.cz),
    };

    /// <summary>Subtracts one matrix from another.</summary>
    /// <param name="a">The matrix to subtract from.</param>
    /// <param name="b">The matrix to subtract.</param>
    /// <returns>The difference.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static b3Matrix3 b3SubMM(b3Matrix3 a, b3Matrix3 b) => new()
    {
        cx = b3Sub(a.cx, b.cx),
        cy = b3Sub(a.cy, b.cy),
        cz = b3Sub(a.cz, b.cz),
    };

    /// <summary>Scales a matrix.</summary>
    /// <param name="s">The scale.</param>
    /// <param name="a">The matrix.</param>
    /// <returns>The scaled matrix.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static b3Matrix3 b3MulSM(float s, b3Matrix3 a) => new()
    {
        cx = b3MulSV(s, a.cx),
        cy = b3MulSV(s, a.cy),
        cz = b3MulSV(s, a.cz),
    };

    /// <summary>Multiplies two matrices.</summary>
    /// <param name="a">The left matrix.</param>
    /// <param name="b">The right matrix.</param>
    /// <returns>The product.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static b3Matrix3 b3MulMM(b3Matrix3 a, b3Matrix3 b) => new()
    {
        cx = b3MulMV(a, b.cx),
        cy = b3MulMV(a, b.cy),
        cz = b3MulMV(a, b.cz),
    };

    /// <summary>Transposes a matrix.</summary>
    /// <param name="m">The matrix.</param>
    /// <returns>The transpose.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static b3Matrix3 b3Transpose(b3Matrix3 m) => new()
    {
        cx = new Vector3(m.cx.X, m.cy.X, m.cz.X),
        cy = new Vector3(m.cx.Y, m.cy.Y, m.cz.Y),
        cz = new Vector3(m.cx.Z, m.cy.Z, m.cz.Z),
    };

    /// <summary>Inverts a matrix.</summary>
    /// <param name="m">The matrix.</param>
    /// <returns>The inverse, or the zero matrix when the matrix is singular.</returns>
    public static b3Matrix3 b3InvertMatrix(b3Matrix3 m)
    {
        float det = b3Det(m);
        if (b3AbsFloat(det) > 1000.0f * FltMin)
        {
            float invDet = 1.0f / det;
            b3Matrix3 result = new()
            {
                cx = b3MulSV(invDet, b3Cross(m.cy, m.cz)),
                cy = b3MulSV(invDet, b3Cross(m.cz, m.cx)),
                cz = b3MulSV(invDet, b3Cross(m.cx, m.cy)),
            };

            return b3Transpose(result);
        }

        return b3Matrix3.Zero;
    }

    /// <summary>Solves <c>m * x = a</c> for x.</summary>
    /// <param name="m">The matrix.</param>
    /// <param name="a">The right-hand side.</param>
    /// <returns>The solution, or the zero vector when the matrix is singular.</returns>
    public static Vector3 b3Solve3(b3Matrix3 m, Vector3 a)
    {
        float det = b3Det(m);
        if (b3AbsFloat(det) > 1000.0f * FltMin)
        {
            float invDet = 1.0f / det;
            b3Matrix3 s = new()
            {
                cx = b3Cross(m.cy, m.cz),
                cy = b3Cross(m.cz, m.cx),
                cz = b3Cross(m.cx, m.cy),
            };

            return new Vector3(
                invDet * b3Dot(s.cx, a),
                invDet * b3Dot(s.cy, a),
                invDet * b3Dot(s.cz, a));
        }

        return Vector3.Zero;
    }

    /// <summary>Computes the inverse transpose of a matrix.</summary>
    /// <param name="m">The matrix.</param>
    /// <returns>The inverse transpose, or the zero matrix when the matrix is singular.</returns>
    public static b3Matrix3 b3InvertT(b3Matrix3 m)
    {
        float det = b3Det(m);
        if (b3AbsFloat(det) > 1000.0f * FltMin)
        {
            float invDet = 1.0f / det;
            return new b3Matrix3
            {
                cx = b3MulSV(invDet, b3Cross(m.cy, m.cz)),
                cy = b3MulSV(invDet, b3Cross(m.cz, m.cx)),
                cz = b3MulSV(invDet, b3Cross(m.cx, m.cy)),
            };
        }

        return b3Matrix3.Zero;
    }

    /// <summary>Computes the component-wise absolute value of a matrix.</summary>
    /// <param name="m">The matrix.</param>
    /// <returns>The component-wise absolute value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static b3Matrix3 b3AbsMatrix3(b3Matrix3 m) => new()
    {
        cx = b3Abs(m.cx),
        cy = b3Abs(m.cy),
        cz = b3Abs(m.cz),
    };

    /// <summary>Builds a rotation matrix from a quaternion.</summary>
    /// <param name="q">The rotation.</param>
    /// <returns>The equivalent matrix.</returns>
    /// <remarks>Worth doing when the same rotation is applied to many vectors.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static b3Matrix3 b3MakeMatrixFromQuat(Quaternion q)
    {
        float xx = q.X * q.X;
        float yy = q.Y * q.Y;
        float zz = q.Z * q.Z;
        float xy = q.X * q.Y;
        float xz = q.X * q.Z;
        float xw = q.X * q.W;
        float yz = q.Y * q.Z;
        float yw = q.Y * q.W;
        float zw = q.Z * q.W;

        return new b3Matrix3
        {
            cx = new Vector3(1.0f - (2.0f * (yy + zz)), 2.0f * (xy + zw), 2.0f * (xz - yw)),
            cy = new Vector3(2.0f * (xy - zw), 1.0f - (2.0f * (xx + zz)), 2.0f * (yz + xw)),
            cz = new Vector3(2.0f * (xz + yw), 2.0f * (yz - xw), 1.0f - (2.0f * (xx + yy))),
        };
    }

    // -------------------------------------------------------- bounding boxes

    /// <summary>Computes the bounding box of a point cloud.</summary>
    /// <param name="points">The points. Must not be empty.</param>
    /// <param name="radius">A radius by which to inflate the box.</param>
    /// <returns>The bounding box.</returns>
    /// <exception cref="ArgumentException"><paramref name="points"/> is empty.</exception>
    public static b3AABB b3MakeAABB(ReadOnlySpan<Vector3> points, float radius)
    {
        if (points.IsEmpty)
        {
            throw new ArgumentException("A bounding box requires at least one point.", nameof(points));
        }

        b3AABB a = new() { lowerBound = points[0], upperBound = points[0] };
        for (int i = 1; i < points.Length; ++i)
        {
            a.lowerBound = b3Min(a.lowerBound, points[i]);
            a.upperBound = b3Max(a.upperBound, points[i]);
        }

        Vector3 r = new(radius, radius, radius);
        a.lowerBound = b3Sub(a.lowerBound, r);
        a.upperBound = b3Add(a.upperBound, r);

        return a;
    }

    /// <summary>Determines whether one box fully contains another.</summary>
    /// <param name="a">The containing box.</param>
    /// <param name="b">The contained box.</param>
    /// <returns><see langword="true"/> when <paramref name="a"/> contains <paramref name="b"/>.</returns>
    public static bool b3AABB_Contains(b3AABB a, b3AABB b)
    {
        if (a.lowerBound.X > b.lowerBound.X || b.upperBound.X > a.upperBound.X)
        {
            return false;
        }

        if (a.lowerBound.Y > b.lowerBound.Y || b.upperBound.Y > a.upperBound.Y)
        {
            return false;
        }

        if (a.lowerBound.Z > b.lowerBound.Z || b.upperBound.Z > a.upperBound.Z)
        {
            return false;
        }

        return true;
    }

    /// <summary>Computes the surface area of a box.</summary>
    /// <param name="a">The box.</param>
    /// <returns>The surface area.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float b3AABB_Area(b3AABB a)
    {
        Vector3 delta = b3Sub(a.upperBound, a.lowerBound);
        return 2.0f * ((delta.X * delta.Y) + (delta.Y * delta.Z) + (delta.Z * delta.X));
    }

    /// <summary>Computes the centre of a box.</summary>
    /// <param name="a">The box.</param>
    /// <returns>The centre.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 b3AABB_Center(b3AABB a) => b3MulSV(0.5f, b3Add(a.upperBound, a.lowerBound));

    /// <summary>Computes the half-widths of a box.</summary>
    /// <param name="a">The box.</param>
    /// <returns>The half-widths.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 b3AABB_Extents(b3AABB a) => b3MulSV(0.5f, b3Sub(a.upperBound, a.lowerBound));

    /// <summary>Computes the smallest box containing two boxes.</summary>
    /// <param name="a">The first box.</param>
    /// <param name="b">The second box.</param>
    /// <returns>The union.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static b3AABB b3AABB_Union(b3AABB a, b3AABB b) => new()
    {
        lowerBound = b3Min(a.lowerBound, b.lowerBound),
        upperBound = b3Max(a.upperBound, b.upperBound),
    };

    /// <summary>Expands a box uniformly.</summary>
    /// <param name="a">The box.</param>
    /// <param name="extension">The amount to expand by on every side.</param>
    /// <returns>The expanded box.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static b3AABB b3AABB_Inflate(b3AABB a, float extension)
    {
        Vector3 radius = new(extension, extension, extension);
        return new b3AABB
        {
            lowerBound = b3Sub(a.lowerBound, radius),
            upperBound = b3Add(a.upperBound, radius),
        };
    }

    /// <summary>Determines whether two boxes overlap.</summary>
    /// <param name="a">The first box.</param>
    /// <param name="b">The second box.</param>
    /// <returns><see langword="true"/> when the boxes intersect.</returns>
    public static bool b3AABB_Overlaps(b3AABB a, b3AABB b)
    {
        if (a.upperBound.X < b.lowerBound.X || a.lowerBound.X > b.upperBound.X)
        {
            return false;
        }

        if (a.upperBound.Y < b.lowerBound.Y || a.lowerBound.Y > b.upperBound.Y)
        {
            return false;
        }

        if (a.upperBound.Z < b.lowerBound.Z || a.lowerBound.Z > b.upperBound.Z)
        {
            return false;
        }

        return true;
    }

    /// <summary>Transforms a bounding box.</summary>
    /// <param name="transform">The transform.</param>
    /// <param name="a">The box.</param>
    /// <returns>A box containing the transformed box.</returns>
    /// <remarks>
    /// The result can be larger than recomputing the box of the original shape
    /// under the same transform.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static b3AABB b3AABB_Transform(b3Transform transform, b3AABB a)
    {
        Vector3 center = b3TransformPoint(transform, b3AABB_Center(a));
        b3Matrix3 m = b3MakeMatrixFromQuat(transform.q);
        Vector3 extent = b3MulMV(b3AbsMatrix3(m), b3AABB_Extents(a));
        return new b3AABB { lowerBound = b3Sub(center, extent), upperBound = b3Add(center, extent) };
    }

    /// <summary>Translates a local box by a world origin.</summary>
    /// <param name="localBox">The local box.</param>
    /// <param name="origin">The world origin.</param>
    /// <returns>The translated box.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static b3AABB b3OffsetAABB(b3AABB localBox, Vector3 origin) => new()
    {
        lowerBound = b3Add(localBox.lowerBound, origin),
        upperBound = b3Add(localBox.upperBound, origin),
    };

    /// <summary>Computes the closest point on a box to a point.</summary>
    /// <param name="point">The point.</param>
    /// <param name="a">The box.</param>
    /// <returns>The closest point, which is the point itself when it lies inside the box.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 b3ClosestPointToAABB(Vector3 point, b3AABB a) => b3Clamp(point, a.lowerBound, a.upperBound);
}
