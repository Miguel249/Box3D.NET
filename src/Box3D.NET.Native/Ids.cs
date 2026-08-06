// SPDX-License-Identifier: MIT
// Mirror of include/box3d/id.h

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Box3D.Native;

/*
 * Box3D identifiers are small value structs, not pointers. A body id is twelve
 * bytes of index and generation counter that the world resolves internally.
 *
 * This rules out SafeHandle for them: a SafeHandle is a finalizable reference
 * type, so wrapping every body in one would put a heap object and a finalizer
 * queue entry behind each of the thousands of bodies a simulation holds. The
 * generation counter already provides what SafeHandle would buy - stale ids are
 * detected by b3Body_IsValid rather than by recycling a raw pointer.
 *
 * All ids are null when zero-initialized, so `default` is the null id.
 */

/// <summary>References a world instance. Mirror of <c>b3WorldId</c>.</summary>
/// <remarks>Treat this as an opaque handle; the layout is subject to change upstream.</remarks>
[StructLayout(LayoutKind.Sequential)]
public struct b3WorldId : IEquatable<b3WorldId>
{
    /// <summary>The one-based world index. Zero means null.</summary>
    public ushort index1;

    /// <summary>The generation counter used to detect stale identifiers.</summary>
    public ushort generation;

    /// <summary>Gets a value indicating whether this identifier is null.</summary>
    public readonly bool IsNull => index1 == 0;

    /// <inheritdoc/>
    public readonly bool Equals(b3WorldId other) => index1 == other.index1 && generation == other.generation;

    /// <inheritdoc/>
    public readonly override bool Equals(object? obj) => obj is b3WorldId other && Equals(other);

    /// <inheritdoc/>
    public readonly override int GetHashCode() => HashCode.Combine(index1, generation);

    /// <summary>Determines whether two identifiers are equal.</summary>
    /// <param name="left">The first identifier.</param>
    /// <param name="right">The second identifier.</param>
    public static bool operator ==(b3WorldId left, b3WorldId right) => left.Equals(right);

    /// <summary>Determines whether two identifiers are unequal.</summary>
    /// <param name="left">The first identifier.</param>
    /// <param name="right">The second identifier.</param>
    public static bool operator !=(b3WorldId left, b3WorldId right) => !left.Equals(right);
}

/// <summary>References a body instance. Mirror of <c>b3BodyId</c>.</summary>
/// <remarks>Treat this as an opaque handle; the layout is subject to change upstream.</remarks>
[StructLayout(LayoutKind.Sequential)]
public struct b3BodyId : IEquatable<b3BodyId>
{
    /// <summary>The one-based body index. Zero means null.</summary>
    public int index1;

    /// <summary>The index of the owning world.</summary>
    public ushort world0;

    /// <summary>The generation counter used to detect stale identifiers.</summary>
    public ushort generation;

    /// <summary>Gets a value indicating whether this identifier is null.</summary>
    public readonly bool IsNull => index1 == 0;

    /// <inheritdoc/>
    public readonly bool Equals(b3BodyId other) =>
        index1 == other.index1 && world0 == other.world0 && generation == other.generation;

    /// <inheritdoc/>
    public readonly override bool Equals(object? obj) => obj is b3BodyId other && Equals(other);

    /// <inheritdoc/>
    public readonly override int GetHashCode() => HashCode.Combine(index1, world0, generation);

    /// <summary>Determines whether two identifiers are equal.</summary>
    /// <param name="left">The first identifier.</param>
    /// <param name="right">The second identifier.</param>
    public static bool operator ==(b3BodyId left, b3BodyId right) => left.Equals(right);

    /// <summary>Determines whether two identifiers are unequal.</summary>
    /// <param name="left">The first identifier.</param>
    /// <param name="right">The second identifier.</param>
    public static bool operator !=(b3BodyId left, b3BodyId right) => !left.Equals(right);
}

/// <summary>References a shape instance. Mirror of <c>b3ShapeId</c>.</summary>
/// <remarks>Treat this as an opaque handle; the layout is subject to change upstream.</remarks>
[StructLayout(LayoutKind.Sequential)]
public struct b3ShapeId : IEquatable<b3ShapeId>
{
    /// <summary>The one-based shape index. Zero means null.</summary>
    public int index1;

    /// <summary>The index of the owning world.</summary>
    public ushort world0;

    /// <summary>The generation counter used to detect stale identifiers.</summary>
    public ushort generation;

    /// <summary>Gets a value indicating whether this identifier is null.</summary>
    public readonly bool IsNull => index1 == 0;

    /// <inheritdoc/>
    public readonly bool Equals(b3ShapeId other) =>
        index1 == other.index1 && world0 == other.world0 && generation == other.generation;

    /// <inheritdoc/>
    public readonly override bool Equals(object? obj) => obj is b3ShapeId other && Equals(other);

    /// <inheritdoc/>
    public readonly override int GetHashCode() => HashCode.Combine(index1, world0, generation);

    /// <summary>Determines whether two identifiers are equal.</summary>
    /// <param name="left">The first identifier.</param>
    /// <param name="right">The second identifier.</param>
    public static bool operator ==(b3ShapeId left, b3ShapeId right) => left.Equals(right);

    /// <summary>Determines whether two identifiers are unequal.</summary>
    /// <param name="left">The first identifier.</param>
    /// <param name="right">The second identifier.</param>
    public static bool operator !=(b3ShapeId left, b3ShapeId right) => !left.Equals(right);
}

/// <summary>References a joint instance. Mirror of <c>b3JointId</c>.</summary>
/// <remarks>Treat this as an opaque handle; the layout is subject to change upstream.</remarks>
[StructLayout(LayoutKind.Sequential)]
public struct b3JointId : IEquatable<b3JointId>
{
    /// <summary>The one-based joint index. Zero means null.</summary>
    public int index1;

    /// <summary>The index of the owning world.</summary>
    public ushort world0;

    /// <summary>The generation counter used to detect stale identifiers.</summary>
    public ushort generation;

    /// <summary>Gets a value indicating whether this identifier is null.</summary>
    public readonly bool IsNull => index1 == 0;

    /// <inheritdoc/>
    public readonly bool Equals(b3JointId other) =>
        index1 == other.index1 && world0 == other.world0 && generation == other.generation;

    /// <inheritdoc/>
    public readonly override bool Equals(object? obj) => obj is b3JointId other && Equals(other);

    /// <inheritdoc/>
    public readonly override int GetHashCode() => HashCode.Combine(index1, world0, generation);

    /// <summary>Determines whether two identifiers are equal.</summary>
    /// <param name="left">The first identifier.</param>
    /// <param name="right">The second identifier.</param>
    public static bool operator ==(b3JointId left, b3JointId right) => left.Equals(right);

    /// <summary>Determines whether two identifiers are unequal.</summary>
    /// <param name="left">The first identifier.</param>
    /// <param name="right">The second identifier.</param>
    public static bool operator !=(b3JointId left, b3JointId right) => !left.Equals(right);
}

/// <summary>References a contact instance. Mirror of <c>b3ContactId</c>.</summary>
/// <remarks>
/// Contacts are transient. A contact identifier can be orphaned by any world
/// mutation or time step, so validate it with <c>b3Contact_IsValid</c> before use.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public struct b3ContactId : IEquatable<b3ContactId>
{
    /// <summary>The one-based contact index. Zero means null.</summary>
    public int index1;

    /// <summary>The index of the owning world.</summary>
    public ushort world0;

    /// <summary>Explicit padding present in the C layout.</summary>
    public short padding;

    /// <summary>The generation counter used to detect stale identifiers.</summary>
    public uint generation;

    /// <summary>Gets a value indicating whether this identifier is null.</summary>
    public readonly bool IsNull => index1 == 0;

    /// <inheritdoc/>
    public readonly bool Equals(b3ContactId other) =>
        index1 == other.index1 && world0 == other.world0 && generation == other.generation;

    /// <inheritdoc/>
    public readonly override bool Equals(object? obj) => obj is b3ContactId other && Equals(other);

    /// <inheritdoc/>
    public readonly override int GetHashCode() => HashCode.Combine(index1, world0, generation);

    /// <summary>Determines whether two identifiers are equal.</summary>
    /// <param name="left">The first identifier.</param>
    /// <param name="right">The second identifier.</param>
    public static bool operator ==(b3ContactId left, b3ContactId right) => left.Equals(right);

    /// <summary>Determines whether two identifiers are unequal.</summary>
    /// <param name="left">The first identifier.</param>
    /// <param name="right">The second identifier.</param>
    public static bool operator !=(b3ContactId left, b3ContactId right) => !left.Equals(right);
}

/// <summary>
/// Conversions between identifiers and integers, for storing them in
/// application data structures. Port of the <c>static inline</c> helpers in
/// <c>id.h</c>, which are not exported from the shared library.
/// </summary>
/// <example>
/// Packing a body id into an entity component:
/// <code>
/// ulong stored = Ids.b3StoreBodyId(bodyId);
/// // ... later ...
/// b3BodyId restored = Ids.b3LoadBodyId(stored);
/// </code>
/// </example>
public static class Ids
{
    /// <summary>Stores a world identifier in a 32-bit integer.</summary>
    /// <param name="id">The identifier to store.</param>
    /// <returns>The packed representation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint b3StoreWorldId(b3WorldId id) => ((uint)id.index1 << 16) | id.generation;

    /// <summary>Restores a world identifier from a 32-bit integer.</summary>
    /// <param name="x">The packed representation.</param>
    /// <returns>The restored identifier.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static b3WorldId b3LoadWorldId(uint x) =>
        new() { index1 = (ushort)(x >> 16), generation = (ushort)x };

    /// <summary>Stores a body identifier in a 64-bit integer.</summary>
    /// <param name="id">The identifier to store.</param>
    /// <returns>The packed representation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong b3StoreBodyId(b3BodyId id) =>
        ((ulong)id.index1 << 32) | ((ulong)id.world0 << 16) | id.generation;

    /// <summary>Restores a body identifier from a 64-bit integer.</summary>
    /// <param name="x">The packed representation.</param>
    /// <returns>The restored identifier.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static b3BodyId b3LoadBodyId(ulong x) =>
        new() { index1 = (int)(x >> 32), world0 = (ushort)(x >> 16), generation = (ushort)x };

    /// <summary>Stores a shape identifier in a 64-bit integer.</summary>
    /// <param name="id">The identifier to store.</param>
    /// <returns>The packed representation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong b3StoreShapeId(b3ShapeId id) =>
        ((ulong)id.index1 << 32) | ((ulong)id.world0 << 16) | id.generation;

    /// <summary>Restores a shape identifier from a 64-bit integer.</summary>
    /// <param name="x">The packed representation.</param>
    /// <returns>The restored identifier.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static b3ShapeId b3LoadShapeId(ulong x) =>
        new() { index1 = (int)(x >> 32), world0 = (ushort)(x >> 16), generation = (ushort)x };

    /// <summary>Stores a joint identifier in a 64-bit integer.</summary>
    /// <param name="id">The identifier to store.</param>
    /// <returns>The packed representation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong b3StoreJointId(b3JointId id) =>
        ((ulong)id.index1 << 32) | ((ulong)id.world0 << 16) | id.generation;

    /// <summary>Restores a joint identifier from a 64-bit integer.</summary>
    /// <param name="x">The packed representation.</param>
    /// <returns>The restored identifier.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static b3JointId b3LoadJointId(ulong x) =>
        new() { index1 = (int)(x >> 32), world0 = (ushort)(x >> 16), generation = (ushort)x };

    /// <summary>Stores a contact identifier in three 32-bit integers.</summary>
    /// <param name="id">The identifier to store.</param>
    /// <param name="values">Receives the packed representation. Must hold at least three elements.</param>
    /// <exception cref="ArgumentException"><paramref name="values"/> is shorter than three elements.</exception>
    public static void b3StoreContactId(b3ContactId id, Span<uint> values)
    {
        if (values.Length < 3)
        {
            throw new ArgumentException("A contact id requires three elements of storage.", nameof(values));
        }

        values[0] = (uint)id.index1;
        values[1] = id.world0;
        values[2] = id.generation;
    }

    /// <summary>Restores a contact identifier from three 32-bit integers.</summary>
    /// <param name="values">The packed representation. Must hold at least three elements.</param>
    /// <returns>The restored identifier.</returns>
    /// <exception cref="ArgumentException"><paramref name="values"/> is shorter than three elements.</exception>
    public static b3ContactId b3LoadContactId(ReadOnlySpan<uint> values)
    {
        if (values.Length < 3)
        {
            throw new ArgumentException("A contact id requires three elements of storage.", nameof(values));
        }

        return new b3ContactId
        {
            index1 = (int)values[0],
            world0 = (ushort)values[1],
            padding = 0,
            generation = values[2],
        };
    }
}
