// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using Box3D.Native;
using Xunit;

namespace Box3D.Tests;

/// <summary>
/// Checks the math ported from the <c>B3_INLINE</c> functions of
/// <c>math_functions.h</c>.
/// </summary>
/// <remarks>
/// These verify algebraic identities and agreement with the base class library
/// where the two are meant to agree. They deliberately do not call into the
/// native library, so they run without a native binary; the functions that
/// delegate to <c>b3Atan2</c> and <c>b3ComputeCosSin</c> are covered by
/// <c>NativeInteropTests</c> instead.
/// </remarks>
public class MathTests
{
    private const float Tolerance = 1e-5f;

    private static readonly Quaternion SampleRotation =
        Quaternion.Normalize(new Quaternion(0.3f, -0.5f, 0.2f, 0.8f));

    private static void AssertClose(Vector3 expected, Vector3 actual, float tolerance = Tolerance)
    {
        Assert.True(
            MathF.Abs(expected.X - actual.X) < tolerance &&
            MathF.Abs(expected.Y - actual.Y) < tolerance &&
            MathF.Abs(expected.Z - actual.Z) < tolerance,
            $"expected {expected}, got {actual}");
    }

    // ------------------------------------------------------------- vectors

    [Fact]
    public void Cross_follows_the_right_hand_rule()
    {
        AssertClose(Vector3.UnitZ, B3Math.b3Cross(Vector3.UnitX, Vector3.UnitY));
        AssertClose(Vector3.UnitX, B3Math.b3Cross(Vector3.UnitY, Vector3.UnitZ));
        AssertClose(Vector3.UnitY, B3Math.b3Cross(Vector3.UnitZ, Vector3.UnitX));
    }

    [Fact]
    public void Cross_agrees_with_the_base_class_library()
    {
        Vector3 a = new(1.0f, 2.0f, 3.0f);
        Vector3 b = new(-4.0f, 5.0f, 0.5f);

        AssertClose(Vector3.Cross(a, b), B3Math.b3Cross(a, b));
    }

    [Fact]
    public void Normalize_returns_zero_for_a_degenerate_vector()
    {
        // The guard uses FLT_MIN, the smallest normalized float. Getting this
        // wrong would produce infinities instead of a zero vector.
        Vector3 tiny = new(1e-25f, 0.0f, 0.0f);

        Assert.Equal(Vector3.Zero, B3Math.b3Normalize(tiny));
        Assert.Equal(Vector3.Zero, B3Math.b3Normalize(Vector3.Zero));
    }

    [Fact]
    public void Normalize_produces_a_unit_vector()
    {
        Vector3 v = new(3.0f, -4.0f, 12.0f);
        Vector3 n = B3Math.b3Normalize(v);

        Assert.True(B3Math.b3IsNormalized(n));
        Assert.Equal(1.0f, B3Math.b3Length(n), 5);
    }

    [Fact]
    public void Perp_is_perpendicular_and_unit_length()
    {
        foreach (Vector3 v in new[]
        {
            Vector3.UnitX,
            Vector3.UnitY,
            Vector3.UnitZ,
            B3Math.b3Normalize(new Vector3(1.0f, 1.0f, 1.0f)),
            B3Math.b3Normalize(new Vector3(-0.9f, 0.1f, 0.4f)),
        })
        {
            Vector3 p = B3Math.b3Perp(v);

            Assert.True(B3Math.b3IsNormalized(p));
            Assert.Equal(0.0f, B3Math.b3Dot(v, p), 5);
        }
    }

    [Fact]
    public void SafeScale_keeps_the_sign_and_clamps_the_magnitude()
    {
        Vector3 scaled = B3Math.b3SafeScale(new Vector3(-0.0001f, 2.0f, 0.0f));

        Assert.Equal(-Constants.B3_MIN_SCALE, scaled.X, 6);
        Assert.Equal(2.0f, scaled.Y, 6);
        Assert.Equal(Constants.B3_MIN_SCALE, scaled.Z, 6);
    }

    // --------------------------------------------------------- quaternions

    [Fact]
    public void MulQuat_agrees_with_the_base_class_library()
    {
        Quaternion a = Quaternion.Normalize(new Quaternion(0.1f, 0.2f, 0.3f, 0.9f));
        Quaternion b = Quaternion.Normalize(new Quaternion(-0.4f, 0.1f, 0.5f, 0.7f));

        Quaternion expected = Quaternion.Multiply(a, b);
        Quaternion actual = B3Math.b3MulQuat(a, b);

        Assert.Equal(expected.X, actual.X, 5);
        Assert.Equal(expected.Y, actual.Y, 5);
        Assert.Equal(expected.Z, actual.Z, 5);
        Assert.Equal(expected.W, actual.W, 5);
    }

    [Fact]
    public void RotateVector_and_InvRotateVector_are_inverses()
    {
        Vector3 v = new(1.0f, -2.0f, 0.5f);

        Vector3 rotated = B3Math.b3RotateVector(SampleRotation, v);
        Vector3 back = B3Math.b3InvRotateVector(SampleRotation, rotated);

        AssertClose(v, back);
    }

    [Fact]
    public void RotateVector_agrees_with_the_base_class_library()
    {
        Vector3 v = new(1.0f, -2.0f, 0.5f);

        AssertClose(Vector3.Transform(v, SampleRotation), B3Math.b3RotateVector(SampleRotation, v));
    }

    [Fact]
    public void RotateVector_preserves_length()
    {
        Vector3 v = new(3.0f, -4.0f, 12.0f);

        Assert.Equal(
            B3Math.b3Length(v),
            B3Math.b3Length(B3Math.b3RotateVector(SampleRotation, v)),
            4);
    }

    [Fact]
    public void InvMulQuat_undoes_MulQuat()
    {
        Quaternion b = Quaternion.Normalize(new Quaternion(-0.4f, 0.1f, 0.5f, 0.7f));

        Quaternion composed = B3Math.b3MulQuat(SampleRotation, b);
        Quaternion recovered = B3Math.b3InvMulQuat(SampleRotation, composed);

        Assert.Equal(b.X, recovered.X, 5);
        Assert.Equal(b.Y, recovered.Y, 5);
        Assert.Equal(b.Z, recovered.Z, 5);
        Assert.Equal(b.W, recovered.W, 5);
    }

    [Fact]
    public void NormalizeQuat_returns_identity_for_a_degenerate_quaternion()
    {
        Assert.Equal(Quaternion.Identity, B3Math.b3NormalizeQuat(default));
    }

    [Fact]
    public void MakeMatrixFromQuat_rotates_the_same_way_as_the_quaternion()
    {
        Vector3 v = new(1.0f, -2.0f, 0.5f);

        b3Matrix3 m = B3Math.b3MakeMatrixFromQuat(SampleRotation);

        AssertClose(B3Math.b3RotateVector(SampleRotation, v), B3Math.b3MulMV(m, v));
    }

    // ---------------------------------------------------------- transforms

    [Fact]
    public void InvertTransform_undoes_TransformPoint()
    {
        b3Transform t = new() { p = new Vector3(5.0f, -3.0f, 2.0f), q = SampleRotation };
        Vector3 point = new(1.0f, 2.0f, 3.0f);

        Vector3 world = B3Math.b3TransformPoint(t, point);
        Vector3 local = B3Math.b3TransformPoint(B3Math.b3InvertTransform(t), world);

        AssertClose(point, local);
    }

    [Fact]
    public void InvTransformPoint_matches_transforming_by_the_inverse()
    {
        b3Transform t = new() { p = new Vector3(5.0f, -3.0f, 2.0f), q = SampleRotation };
        Vector3 world = new(1.0f, 2.0f, 3.0f);

        AssertClose(
            B3Math.b3TransformPoint(B3Math.b3InvertTransform(t), world),
            B3Math.b3InvTransformPoint(t, world));
    }

    [Fact]
    public void MulTransforms_composes_in_the_same_order_as_applying_them()
    {
        b3Transform a = new() { p = new Vector3(1.0f, 0.0f, 0.0f), q = SampleRotation };
        b3Transform b = new() { p = new Vector3(0.0f, 2.0f, 0.0f), q = Quaternion.Identity };
        Vector3 point = new(0.5f, 0.5f, 0.5f);

        AssertClose(
            B3Math.b3TransformPoint(a, B3Math.b3TransformPoint(b, point)),
            B3Math.b3TransformPoint(B3Math.b3MulTransforms(a, b), point));
    }

    [Fact]
    public void InvMulTransforms_produces_the_relative_pose()
    {
        b3Transform a = new() { p = new Vector3(1.0f, 0.0f, 0.0f), q = SampleRotation };
        b3Transform b = new() { p = new Vector3(0.0f, 2.0f, 0.0f), q = Quaternion.Identity };

        b3Transform relative = B3Math.b3InvMulTransforms(a, b);

        // Composing a with the relative pose must reproduce b.
        b3Transform composed = B3Math.b3MulTransforms(a, relative);

        AssertClose(b.p, composed.p);
    }

    // ------------------------------------------------------------ matrices

    [Fact]
    public void InvertMatrix_produces_the_identity_when_multiplied_back()
    {
        b3Matrix3 m = new()
        {
            cx = new Vector3(2.0f, 1.0f, 0.0f),
            cy = new Vector3(0.0f, 3.0f, 1.0f),
            cz = new Vector3(1.0f, 0.0f, 4.0f),
        };

        b3Matrix3 product = B3Math.b3MulMM(m, B3Math.b3InvertMatrix(m));

        AssertClose(Vector3.UnitX, product.cx, 1e-4f);
        AssertClose(Vector3.UnitY, product.cy, 1e-4f);
        AssertClose(Vector3.UnitZ, product.cz, 1e-4f);
    }

    [Fact]
    public void InvertMatrix_returns_zero_for_a_singular_matrix()
    {
        b3Matrix3 singular = new()
        {
            cx = new Vector3(1.0f, 2.0f, 3.0f),
            cy = new Vector3(2.0f, 4.0f, 6.0f), // twice the first column
            cz = new Vector3(0.0f, 0.0f, 0.0f),
        };

        Assert.Equal(b3Matrix3.Zero, B3Math.b3InvertMatrix(singular));
    }

    [Fact]
    public void Solve3_solves_the_linear_system()
    {
        b3Matrix3 m = new()
        {
            cx = new Vector3(2.0f, 1.0f, 0.0f),
            cy = new Vector3(0.0f, 3.0f, 1.0f),
            cz = new Vector3(1.0f, 0.0f, 4.0f),
        };
        Vector3 x = new(1.0f, -2.0f, 3.0f);
        Vector3 rhs = B3Math.b3MulMV(m, x);

        AssertClose(x, B3Math.b3Solve3(m, rhs), 1e-4f);
    }

    [Fact]
    public void Transpose_is_its_own_inverse()
    {
        b3Matrix3 m = new()
        {
            cx = new Vector3(1.0f, 2.0f, 3.0f),
            cy = new Vector3(4.0f, 5.0f, 6.0f),
            cz = new Vector3(7.0f, 8.0f, 9.0f),
        };

        Assert.Equal(m, B3Math.b3Transpose(B3Math.b3Transpose(m)));
    }

    [Fact]
    public void Det_of_the_identity_is_one()
    {
        Assert.Equal(1.0f, B3Math.b3Det(b3Matrix3.Identity), 6);
    }

    // ------------------------------------------------------ bounding boxes

    [Fact]
    public void MakeAABB_bounds_every_point_and_applies_the_radius()
    {
        Span<Vector3> points =
        [
            new Vector3(0.0f, 0.0f, 0.0f),
            new Vector3(1.0f, -2.0f, 3.0f),
            new Vector3(-4.0f, 5.0f, 0.0f),
        ];

        b3AABB box = B3Math.b3MakeAABB(points, 0.5f);

        AssertClose(new Vector3(-4.5f, -2.5f, -0.5f), box.lowerBound);
        AssertClose(new Vector3(1.5f, 5.5f, 3.5f), box.upperBound);
    }

    [Fact]
    public void MakeAABB_rejects_an_empty_point_cloud()
    {
        Assert.Throws<ArgumentException>(() => B3Math.b3MakeAABB(default, 0.0f));
    }

    [Fact]
    public void Overlaps_and_Contains_agree_with_geometry()
    {
        b3AABB outer = new() { lowerBound = new Vector3(-1.0f), upperBound = new Vector3(1.0f) };
        b3AABB inner = new() { lowerBound = new Vector3(-0.5f), upperBound = new Vector3(0.5f) };
        b3AABB away = new() { lowerBound = new Vector3(2.0f), upperBound = new Vector3(3.0f) };

        Assert.True(B3Math.b3AABB_Contains(outer, inner));
        Assert.False(B3Math.b3AABB_Contains(inner, outer));
        Assert.True(B3Math.b3AABB_Overlaps(outer, inner));
        Assert.False(B3Math.b3AABB_Overlaps(outer, away));
    }

    [Fact]
    public void Touching_boxes_overlap()
    {
        // Box3D treats a shared face as an overlap, which matters for the broad phase.
        b3AABB a = new() { lowerBound = Vector3.Zero, upperBound = new Vector3(1.0f) };
        b3AABB b = new() { lowerBound = new Vector3(1.0f, 0.0f, 0.0f), upperBound = new Vector3(2.0f, 1.0f, 1.0f) };

        Assert.True(B3Math.b3AABB_Overlaps(a, b));
    }

    [Fact]
    public void Area_of_the_unit_cube_is_six()
    {
        b3AABB unit = new() { lowerBound = Vector3.Zero, upperBound = new Vector3(1.0f) };

        Assert.Equal(6.0f, B3Math.b3AABB_Area(unit), 5);
    }

    [Fact]
    public void ClosestPointToAABB_clamps_outside_points_and_keeps_inside_ones()
    {
        b3AABB box = new() { lowerBound = new Vector3(-1.0f), upperBound = new Vector3(1.0f) };

        AssertClose(new Vector3(1.0f, 0.0f, -1.0f), B3Math.b3ClosestPointToAABB(new Vector3(5.0f, 0.0f, -3.0f), box));
        AssertClose(new Vector3(0.25f, 0.5f, -0.5f), B3Math.b3ClosestPointToAABB(new Vector3(0.25f, 0.5f, -0.5f), box));
    }

    [Fact]
    public void AABB_Transform_contains_the_transformed_corners()
    {
        b3AABB box = new() { lowerBound = new Vector3(-1.0f), upperBound = new Vector3(1.0f) };
        b3Transform t = new() { p = new Vector3(3.0f, 0.0f, 0.0f), q = SampleRotation };

        b3AABB transformed = B3Math.b3AABB_Transform(t, box);

        foreach (Vector3 corner in Corners(box))
        {
            Vector3 moved = B3Math.b3TransformPoint(t, corner);

            Assert.True(
                moved.X >= transformed.lowerBound.X - Tolerance &&
                moved.Y >= transformed.lowerBound.Y - Tolerance &&
                moved.Z >= transformed.lowerBound.Z - Tolerance &&
                moved.X <= transformed.upperBound.X + Tolerance &&
                moved.Y <= transformed.upperBound.Y + Tolerance &&
                moved.Z <= transformed.upperBound.Z + Tolerance,
                $"corner {moved} escaped the transformed box");
        }
    }

    private static Vector3[] Corners(b3AABB box) =>
    [
        new Vector3(box.lowerBound.X, box.lowerBound.Y, box.lowerBound.Z),
        new Vector3(box.upperBound.X, box.lowerBound.Y, box.lowerBound.Z),
        new Vector3(box.lowerBound.X, box.upperBound.Y, box.lowerBound.Z),
        new Vector3(box.upperBound.X, box.upperBound.Y, box.lowerBound.Z),
        new Vector3(box.lowerBound.X, box.lowerBound.Y, box.upperBound.Z),
        new Vector3(box.upperBound.X, box.lowerBound.Y, box.upperBound.Z),
        new Vector3(box.lowerBound.X, box.upperBound.Y, box.upperBound.Z),
        new Vector3(box.upperBound.X, box.upperBound.Y, box.upperBound.Z),
    ];

    // ------------------------------------------------------------------ ids

    [Fact]
    public void Body_ids_round_trip_through_a_ulong()
    {
        b3BodyId id = new() { index1 = 12345, world0 = 7, generation = 99 };

        b3BodyId restored = Ids.b3LoadBodyId(Ids.b3StoreBodyId(id));

        Assert.Equal(id, restored);
    }

    [Fact]
    public void World_ids_round_trip_through_a_uint()
    {
        b3WorldId id = new() { index1 = 3, generation = 4242 };

        Assert.Equal(id, Ids.b3LoadWorldId(Ids.b3StoreWorldId(id)));
    }

    [Fact]
    public void Contact_ids_round_trip_through_three_uints()
    {
        b3ContactId id = new() { index1 = 999, world0 = 2, generation = 123456 };

        Span<uint> packed = stackalloc uint[3];
        Ids.b3StoreContactId(id, packed);

        Assert.Equal(id, Ids.b3LoadContactId(packed));
    }

    [Fact]
    public void A_default_id_is_null()
    {
        Assert.True(default(b3BodyId).IsNull);
        Assert.True(default(b3WorldId).IsNull);
        Assert.True(default(b3ShapeId).IsNull);
        Assert.True(default(b3JointId).IsNull);
        Assert.True(default(b3ContactId).IsNull);
    }

    [Fact]
    public void Storing_a_contact_id_rejects_a_short_buffer()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            Span<uint> tooSmall = stackalloc uint[2];
            Ids.b3StoreContactId(default, tooSmall);
        });
    }
}
