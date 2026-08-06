// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Box3D.Native;
using Xunit;

namespace Box3D.Tests;

/// <summary>
/// Verifies that the managed structs have the same memory layout as their C
/// counterparts.
/// </summary>
/// <remarks>
/// <para>
/// A layout mismatch is the worst class of bug a binding can have. It does not
/// fail to compile and it does not throw: the call succeeds and silently reads
/// the wrong bytes, so a body ends up with its restitution in the friction slot.
/// These tests pin the sizes and offsets that the whole binding rests on.
/// </para>
/// <para>
/// The expected values are derived from the C declarations under the standard
/// x86-64 and AArch64 alignment rules, which agree for every type here.
/// <c>NativeInteropTests</c> checks the same thing end to end against the real
/// library, but these run without any native binary present.
/// </para>
/// </remarks>
public class LayoutTests
{
    /*
     * The whole binding rests on this: b3Vec3 is three floats and b3Quat is a
     * b3Vec3 followed by a scalar, which is exactly System.Numerics.Vector3 and
     * Quaternion. If a future runtime ever padded these, every call taking a
     * vector would corrupt its arguments, so the assumption is asserted rather
     * than assumed.
     */

    [Fact]
    public void Vector3_matches_b3Vec3()
    {
        Assert.Equal(12, Unsafe.SizeOf<Vector3>());
    }

    [Fact]
    public void Quaternion_matches_b3Quat()
    {
        // b3Quat is { b3Vec3 v; float s }, laid out x, y, z, s.
        Assert.Equal(16, Unsafe.SizeOf<Quaternion>());

        Quaternion q = new(1.0f, 2.0f, 3.0f, 4.0f);
        ReadOnlySpan<float> raw = MemoryMarshal.Cast<Quaternion, float>(
            MemoryMarshal.CreateReadOnlySpan(ref q, 1));

        Assert.Equal(1.0f, raw[0]);
        Assert.Equal(2.0f, raw[1]);
        Assert.Equal(3.0f, raw[2]);
        Assert.Equal(4.0f, raw[3]);
    }

    [Theory]
    [InlineData(typeof(b3Transform), 28)]
    [InlineData(typeof(b3Matrix3), 36)]
    [InlineData(typeof(b3AABB), 24)]
    [InlineData(typeof(b3Plane), 16)]
    [InlineData(typeof(b3CosSin), 8)]
    [InlineData(typeof(b3Vec2), 8)]
    [InlineData(typeof(b3SegmentDistanceResult), 32)]
    public void Math_types_have_the_C_size(Type type, int expected)
    {
        Assert.Equal(expected, SizeOf(type));
    }

    [Theory]
    [InlineData(typeof(b3WorldId), 4)]
    [InlineData(typeof(b3BodyId), 8)]
    [InlineData(typeof(b3ShapeId), 8)]
    [InlineData(typeof(b3JointId), 8)]
    [InlineData(typeof(b3ContactId), 12)]
    public void Id_types_have_the_C_size(Type type, int expected)
    {
        Assert.Equal(expected, SizeOf(type));
    }

    [Theory]
    [InlineData(typeof(b3Capacity), 20)]
    [InlineData(typeof(b3Filter), 24)]
    [InlineData(typeof(b3SurfaceMaterial), 40)]
    [InlineData(typeof(b3MotionLocks), 6)]
    [InlineData(typeof(b3MassData), 52)]
    [InlineData(typeof(b3Sphere), 16)]
    [InlineData(typeof(b3Capsule), 28)]
    [InlineData(typeof(b3RayCastInput), 28)]
    [InlineData(typeof(b3TreeStats), 8)]
    [InlineData(typeof(b3Version), 12)]
    [InlineData(typeof(b3ManifoldPoint), 56)]
    [InlineData(typeof(b3Manifold), 268)]
    [InlineData(typeof(b3FeaturePair), 4)]
    [InlineData(typeof(b3SATCache), 8)]
    [InlineData(typeof(b3TreeNode), 48)]
    [InlineData(typeof(b3ChildShape), 80)]
    public void Public_structs_have_the_C_size(Type type, int expected)
    {
        Assert.Equal(expected, SizeOf(type));
    }

    /*
     * NativeBool exists so that structures holding booleans stay blittable. A
     * managed bool would marshal as a four-byte Win32 BOOL and shift every field
     * after it.
     */

    [Fact]
    public void NativeBool_is_one_byte()
    {
        Assert.Equal(1, Unsafe.SizeOf<NativeBool>());
    }

    [Fact]
    public void NativeBool_round_trips_through_bool()
    {
        NativeBool t = true;
        NativeBool f = false;

        Assert.True(t);
        Assert.False(f);
        Assert.True(t.Value);
        Assert.False(f.Value);
    }

    [Fact]
    public void NativeBool_treats_any_non_zero_byte_as_true()
    {
        // C evaluates any non-zero value as true, and the native side is free to
        // hand back something other than 1.
        byte raw = 42;
        NativeBool value = Unsafe.As<byte, NativeBool>(ref raw);

        Assert.True(value);
    }

    /*
     * b3MotionLocks is six adjacent bools in C. If NativeBool were anything
     * other than one byte this would be the first thing to break, and it would
     * break silently by locking the wrong axes.
     */

    [Fact]
    public void MotionLocks_packs_one_byte_per_axis()
    {
        b3MotionLocks locks = default;
        locks.angularY = true;

        ReadOnlySpan<byte> raw = MemoryMarshal.AsBytes(
            MemoryMarshal.CreateReadOnlySpan(ref locks, 1));

        Assert.Equal(6, raw.Length);
        Assert.Equal(0, raw[0]); // linearX
        Assert.Equal(0, raw[1]); // linearY
        Assert.Equal(0, raw[2]); // linearZ
        Assert.Equal(0, raw[3]); // angularX
        Assert.Equal(1, raw[4]); // angularY
        Assert.Equal(0, raw[5]); // angularZ
    }

    /*
     * The inline arrays must occupy exactly the space of the C arrays they
     * replace, with no header of their own.
     */

    [Fact]
    public void ManifoldPointArray_is_four_contiguous_points()
    {
        Assert.Equal(
            4 * Unsafe.SizeOf<b3ManifoldPoint>(),
            Unsafe.SizeOf<b3ManifoldPointArray>());
    }

    [Fact]
    public void SimplexVertexArray_is_four_contiguous_vertices()
    {
        Assert.Equal(
            4 * Unsafe.SizeOf<b3SimplexVertex>(),
            Unsafe.SizeOf<b3SimplexVertexArray>());
    }

    /*
     * b3ChildShape is a union of four shape types followed by three fields. The
     * union is 28 bytes of capsule but has 8-byte alignment because of the hull
     * pointer, so it occupies 32 and the fields after it start there.
     */

    [Fact]
    public void ChildShape_union_members_all_start_at_offset_zero()
    {
        b3ChildShape child = default;
        child.sphere = new b3Sphere { center = new Vector3(1.0f, 2.0f, 3.0f), radius = 4.0f };

        // The capsule overlaps the sphere, so its first centre reads the sphere centre.
        Assert.Equal(new Vector3(1.0f, 2.0f, 3.0f), child.capsule.center1);
    }

    private static int SizeOf(Type type)
    {
        // Unsafe.SizeOf<T> is generic, so it is reached reflectively for the
        // table-driven cases. This runs in a test, never at simulation time.
        return (int)typeof(Unsafe)
            .GetMethod(nameof(Unsafe.SizeOf))!
            .MakeGenericMethod(type)
            .Invoke(null, null)!;
    }
}
