// SPDX-License-Identifier: MIT

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Box3D.Native;

/// <summary>
/// Interop constants shared by every binding in this assembly.
/// </summary>
public static class Box3DLibrary
{
    /// <summary>
    /// The native library name passed to <see cref="LibraryImportAttribute"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The runtime expands this to <c>box3d.dll</c> on Windows, <c>libbox3d.so</c>
    /// on Linux and Android, and <c>libbox3d.dylib</c> on macOS, and resolves it
    /// from the <c>runtimes/&lt;rid&gt;/native</c> folder of the package.
    /// </para>
    /// <para>
    /// On iOS the value is <c>__Internal</c> instead, which names the running
    /// executable rather than a file to load. Apple requires every dynamic
    /// library inside an application to be a signed framework in the bundle, so
    /// the package ships a static archive that the iOS build links into the
    /// application: by the time a P/Invoke runs, the Box3D symbols are already
    /// part of the main image and there is nothing left to load.
    /// </para>
    /// </remarks>
#if IOS
    public const string Name = "__Internal";
#else
    public const string Name = "box3d";
#endif
}

/// <summary>
/// A one-byte boolean matching the C <c>bool</c> from <c>&lt;stdbool.h&gt;</c>.
/// </summary>
/// <remarks>
/// <para>
/// This type exists so that structures containing booleans stay <em>blittable</em>.
/// A <see cref="bool"/> field marshals as a four-byte Win32 <c>BOOL</c> by default,
/// which both corrupts the layout of every Box3D definition struct and forces the
/// runtime to copy the structure field by field on each call.
/// </para>
/// <para>
/// Annotating each field with <c>[MarshalAs(UnmanagedType.U1)]</c> would fix the
/// width but the structure would still be classified as non-blittable, so it could
/// not be passed by <c>in</c> reference without a hidden copy. A one-byte value type
/// keeps the structures copy-free and lets them be pinned and passed directly.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// b3BodyDef def = B3.b3DefaultBodyDef();
/// def.isBullet = true;              // implicit conversion from bool
/// if (def.enableSleep) { /* ... */ } // implicit conversion to bool
/// </code>
/// </example>
[StructLayout(LayoutKind.Sequential, Size = 1)]
public readonly struct NativeBool : IEquatable<NativeBool>
{
    private readonly byte _value;

    /// <summary>Initializes a new instance with the given value.</summary>
    /// <param name="value">The boolean value to represent.</param>
    public NativeBool(bool value) => _value = value ? (byte)1 : (byte)0;

    /// <summary>Gets a value indicating whether this instance represents <see langword="true"/>.</summary>
    /// <remarks>
    /// Any non-zero byte is treated as <see langword="true"/>, matching how C
    /// evaluates a <c>bool</c> in a condition.
    /// </remarks>
    public bool Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _value != 0;
    }

    /// <summary>Converts a managed boolean to its one-byte native form.</summary>
    /// <param name="value">The value to convert.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator NativeBool(bool value) => new(value);

    /// <summary>Converts a one-byte native boolean to its managed form.</summary>
    /// <param name="value">The value to convert.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator bool(NativeBool value) => value._value != 0;

    /// <inheritdoc/>
    public bool Equals(NativeBool other) => Value == other.Value;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is NativeBool other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => Value ? 1 : 0;

    /// <summary>Determines whether two values are equal.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    public static bool operator ==(NativeBool left, NativeBool right) => left.Equals(right);

    /// <summary>Determines whether two values are unequal.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    public static bool operator !=(NativeBool left, NativeBool right) => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString() => Value ? "true" : "false";
}
