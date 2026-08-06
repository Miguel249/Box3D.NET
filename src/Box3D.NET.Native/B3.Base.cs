// SPDX-License-Identifier: MIT
// Mirror of include/box3d/base.h, include/box3d/constants.h and the exported
// functions of include/box3d/math_functions.h.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Box3D.Native;

/// <summary>
/// The Box3D C API, one method per exported function.
/// </summary>
/// <remarks>
/// <para>
/// Every method here corresponds to a <c>B3_API</c> declaration in the Box3D
/// headers and keeps its C name, so the binding can be checked against the
/// header by inspection and so Box3D's own documentation applies verbatim.
/// </para>
/// <para>
/// The <c>B3_INLINE</c> helpers in the headers are not exported from the shared
/// library and therefore cannot be bound. They are reimplemented in
/// <see cref="B3Math"/> and <see cref="Ids"/>.
/// </para>
/// <para>
/// This class performs no validation and manages no lifetimes. Passing an
/// invalid identifier or violating an ownership rule is undefined behaviour that
/// will usually crash the process rather than raise an exception. Prefer the
/// <c>Box3D</c> namespace of the Box3D.NET package unless you need something it
/// does not yet expose.
/// </para>
/// </remarks>
public static unsafe partial class B3
{
    // ---------------------------------------------------------------- base.h

    /// <summary>
    /// Overrides the allocation functions. Must be called during application startup.
    /// </summary>
    /// <param name="allocFcn">
    /// The allocation callback, with signature <c>void* (int size, int alignment)</c>.
    /// The alignment is guaranteed to be a power of two.
    /// </param>
    /// <param name="freeFcn">
    /// The deallocation callback, with signature <c>void (void* mem)</c>.
    /// </param>
    [LibraryImport(Box3DLibrary.Name)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void b3SetAllocator(
        delegate* unmanaged[Cdecl]<int, int, void*> allocFcn,
        delegate* unmanaged[Cdecl]<void*, void> freeFcn);

    /// <summary>Gets the total number of bytes currently allocated by Box3D.</summary>
    /// <returns>The byte count.</returns>
    [LibraryImport(Box3DLibrary.Name)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int b3GetByteCount();

    /// <summary>Overrides the assertion callback.</summary>
    /// <param name="assertFcn">
    /// A non-null callback with signature
    /// <c>int (byte* condition, byte* fileName, int lineNumber)</c>.
    /// Returning zero skips the debugger break.
    /// </param>
    [LibraryImport(Box3DLibrary.Name)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void b3SetAssertFcn(
        delegate* unmanaged[Cdecl]<byte*, byte*, int, int> assertFcn);

    /// <summary>Overrides the logging callback, which Box3D uses to report warnings.</summary>
    /// <param name="logFcn">A callback with signature <c>void (byte* message)</c>.</param>
    [LibraryImport(Box3DLibrary.Name)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void b3SetLogFcn(delegate* unmanaged[Cdecl]<byte*, void> logFcn);

    /// <summary>Gets the version of the loaded Box3D library.</summary>
    /// <returns>The version.</returns>
    [LibraryImport(Box3DLibrary.Name)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial b3Version b3GetVersion();

    /// <summary>
    /// Gets a value indicating whether the library was built with
    /// <c>BOX3D_DOUBLE_PRECISION</c>, also known as large world mode.
    /// </summary>
    /// <returns><see langword="true"/> when built in double precision.</returns>
    /// <remarks>
    /// This binding targets the single-precision build. If this returns
    /// <see langword="true"/> the structure layouts here do not match the loaded
    /// library and every call is undefined behaviour.
    /// </remarks>
    [LibraryImport(Box3DLibrary.Name)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial NativeBool b3IsDoublePrecision();

    /// <summary>Gets the absolute number of system ticks. The unit is platform specific.</summary>
    /// <returns>The tick count.</returns>
    [LibraryImport(Box3DLibrary.Name)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial ulong b3GetTicks();

    /// <summary>Gets the milliseconds elapsed since an earlier tick value.</summary>
    /// <param name="ticks">The earlier tick value.</param>
    /// <returns>The elapsed milliseconds.</returns>
    [LibraryImport(Box3DLibrary.Name)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial float b3GetMilliseconds(ulong ticks);

    /// <summary>Gets the milliseconds elapsed since an earlier tick value and resets it to now.</summary>
    /// <param name="ticks">On input the earlier tick value; on return the current tick value.</param>
    /// <returns>The elapsed milliseconds.</returns>
    [LibraryImport(Box3DLibrary.Name)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial float b3GetMillisecondsAndReset(ref ulong ticks);

    /// <summary>Yields the current thread, for use inside a busy loop.</summary>
    [LibraryImport(Box3DLibrary.Name)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void b3Yield();

    /// <summary>Sleeps the current thread.</summary>
    /// <param name="milliseconds">The duration to sleep.</param>
    [LibraryImport(Box3DLibrary.Name)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void b3Sleep(int milliseconds);

    /// <summary>Computes a djb2 hash, used for determinism testing.</summary>
    /// <param name="hash">The running hash. Seed with <see cref="Constants.B3_HASH_INIT"/>.</param>
    /// <param name="data">The bytes to hash.</param>
    /// <param name="count">The number of bytes.</param>
    /// <returns>The updated hash.</returns>
    [LibraryImport(Box3DLibrary.Name)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial uint b3Hash(uint hash, byte* data, int count);

    // ----------------------------------------------------------- constants.h

    /// <summary>
    /// Sets the number of length units per metre. The default is one.
    /// </summary>
    /// <param name="lengthUnits">The number of application length units in one metre.</param>
    /// <remarks>
    /// Box3D bases all length units on metres. Call this once during application
    /// startup, before any other Box3D call, if your game uses different units.
    /// </remarks>
    [LibraryImport(Box3DLibrary.Name)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void b3SetLengthUnitsPerMeter(float lengthUnits);

    /// <summary>Gets the current number of length units per metre.</summary>
    /// <returns>The number of length units per metre.</returns>
    [LibraryImport(Box3DLibrary.Name)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial float b3GetLengthUnitsPerMeter();

    /// <summary>Sets the threshold, in seconds, above which a stall is logged.</summary>
    /// <param name="seconds">The threshold in seconds.</param>
    [LibraryImport(Box3DLibrary.Name)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void b3SetStallThreshold(float seconds);

    /// <summary>Gets the threshold, in seconds, above which a stall is logged.</summary>
    /// <returns>The threshold in seconds.</returns>
    [LibraryImport(Box3DLibrary.Name)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial float b3GetStallThreshold();

    // ---------------------------------------------------- math_functions.h

    /// <summary>
    /// Computes an approximate arctangent in the range minus pi to pi.
    /// </summary>
    /// <param name="y">The numerator.</param>
    /// <param name="x">The denominator.</param>
    /// <returns>The angle in radians.</returns>
    /// <remarks>
    /// Hand coded for cross-platform determinism, which the standard library
    /// <c>atan2f</c> does not provide. Accurate to about 0.0023 degrees.
    /// </remarks>
    [LibraryImport(Box3DLibrary.Name)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial float b3Atan2(float y, float x);

    /// <summary>Computes the cosine and sine of an angle, deterministically across platforms.</summary>
    /// <param name="radians">The angle in radians.</param>
    /// <returns>The cosine and sine pair.</returns>
    [LibraryImport(Box3DLibrary.Name)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial b3CosSin b3ComputeCosSin(float radians);

    /// <summary>Builds a quaternion from a rotation matrix.</summary>
    /// <param name="m">The rotation matrix.</param>
    /// <returns>The equivalent quaternion.</returns>
    [LibraryImport(Box3DLibrary.Name)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial Quaternion b3MakeQuatFromMatrix(b3Matrix3* m);

    /// <summary>Computes the shortest rotation taking one unit vector to another.</summary>
    /// <param name="v1">The starting unit vector.</param>
    /// <param name="v2">The target unit vector.</param>
    /// <returns>The rotation.</returns>
    [LibraryImport(Box3DLibrary.Name)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial Quaternion b3ComputeQuatBetweenUnitVectors(Vector3 v1, Vector3 v2);

    /// <summary>Computes the parallel axis theorem term for shifting an inertia tensor.</summary>
    /// <param name="mass">The mass.</param>
    /// <param name="origin">The offset from the centre of mass.</param>
    /// <returns>The inertia contribution of the shift.</returns>
    [LibraryImport(Box3DLibrary.Name)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial b3Matrix3 b3Steiner(float mass, Vector3 origin);

    /// <summary>Computes the closest point on the segment from <paramref name="a"/> to <paramref name="b"/>.</summary>
    /// <param name="a">The start of the segment.</param>
    /// <param name="b">The end of the segment.</param>
    /// <param name="q">The target point.</param>
    /// <returns>The closest point on the segment.</returns>
    [LibraryImport(Box3DLibrary.Name)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial Vector3 b3PointToSegmentDistance(Vector3 a, Vector3 b, Vector3 q);

    /// <summary>Computes the closest points on two infinite lines.</summary>
    /// <param name="p1">A point on the first line.</param>
    /// <param name="d1">The direction of the first line.</param>
    /// <param name="p2">A point on the second line.</param>
    /// <param name="d2">The direction of the second line.</param>
    /// <returns>The closest points and their parametric coordinates.</returns>
    [LibraryImport(Box3DLibrary.Name)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial b3SegmentDistanceResult b3LineDistance(Vector3 p1, Vector3 d1, Vector3 p2, Vector3 d2);

    /// <summary>Computes the closest points on two line segments.</summary>
    /// <param name="p1">The start of the first segment.</param>
    /// <param name="q1">The end of the first segment.</param>
    /// <param name="p2">The start of the second segment.</param>
    /// <param name="q2">The end of the second segment.</param>
    /// <returns>The closest points and their parametric coordinates.</returns>
    [LibraryImport(Box3DLibrary.Name)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial b3SegmentDistanceResult b3SegmentDistance(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2);

    /// <summary>Determines whether a number is neither NaN nor infinite.</summary>
    /// <param name="a">The value to test.</param>
    /// <returns><see langword="true"/> when the value is valid.</returns>
    [LibraryImport(Box3DLibrary.Name)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial NativeBool b3IsValidFloat(float a);

    /// <summary>Determines whether a vector is neither NaN nor infinite.</summary>
    /// <param name="a">The vector to test.</param>
    /// <returns><see langword="true"/> when the vector is valid.</returns>
    [LibraryImport(Box3DLibrary.Name)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial NativeBool b3IsValidVec3(Vector3 a);

    /// <summary>Determines whether a quaternion is valid and normalized.</summary>
    /// <param name="q">The quaternion to test.</param>
    /// <returns><see langword="true"/> when the quaternion is valid.</returns>
    [LibraryImport(Box3DLibrary.Name)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial NativeBool b3IsValidQuat(Quaternion q);

    /// <summary>Determines whether a transform is valid and its rotation normalized.</summary>
    /// <param name="a">The transform to test.</param>
    /// <returns><see langword="true"/> when the transform is valid.</returns>
    [LibraryImport(Box3DLibrary.Name)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial NativeBool b3IsValidTransform(b3Transform a);

    /// <summary>Determines whether a matrix is neither NaN nor infinite.</summary>
    /// <param name="a">The matrix to test.</param>
    /// <returns><see langword="true"/> when the matrix is valid.</returns>
    [LibraryImport(Box3DLibrary.Name)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial NativeBool b3IsValidMatrix3(b3Matrix3 a);

    /// <summary>
    /// Determines whether a bounding box is valid, meaning finite with the upper
    /// bound at least the lower bound.
    /// </summary>
    /// <param name="a">The box to test.</param>
    /// <returns><see langword="true"/> when the box is valid.</returns>
    [LibraryImport(Box3DLibrary.Name)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial NativeBool b3IsValidAABB(b3AABB a);

    /// <summary>
    /// Determines whether a bounding box lies reasonably close to the origin.
    /// </summary>
    /// <param name="a">The box to test.</param>
    /// <returns><see langword="true"/> when the box is within the sanity bound.</returns>
    /// <remarks>See <see cref="ScaledConstants.B3_HUGE"/>.</remarks>
    [LibraryImport(Box3DLibrary.Name)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial NativeBool b3IsBoundedAABB(b3AABB a);

    /// <summary>Determines whether a bounding box is both valid and bounded.</summary>
    /// <param name="a">The box to test.</param>
    /// <returns><see langword="true"/> when the box is sane.</returns>
    [LibraryImport(Box3DLibrary.Name)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial NativeBool b3IsSaneAABB(b3AABB a);

    /// <summary>Determines whether a plane is valid, meaning finite with a normalized normal.</summary>
    /// <param name="a">The plane to test.</param>
    /// <returns><see langword="true"/> when the plane is valid.</returns>
    [LibraryImport(Box3DLibrary.Name)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial NativeBool b3IsValidPlane(b3Plane a);

    /// <summary>Determines whether a world position is neither NaN nor infinite.</summary>
    /// <param name="p">The position to test.</param>
    /// <returns><see langword="true"/> when the position is valid.</returns>
    [LibraryImport(Box3DLibrary.Name)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial NativeBool b3IsValidPosition(Vector3 p);

    /// <summary>Determines whether a world transform is valid and its rotation normalized.</summary>
    /// <param name="t">The transform to test.</param>
    /// <returns><see langword="true"/> when the transform is valid.</returns>
    [LibraryImport(Box3DLibrary.Name)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial NativeBool b3IsValidWorldTransform(b3Transform t);

    /// <summary>
    /// Packs an RGB colour together with a debug draw material preset.
    /// Port of the inline <c>b3MakeDebugColor</c>.
    /// </summary>
    /// <param name="rgb">The colour. Only the low 24 bits are used.</param>
    /// <param name="material">The material preset, which rides in the high byte.</param>
    /// <returns>The packed colour.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint b3MakeDebugColor(b3HexColor rgb, b3DebugMaterial material) =>
        ((uint)rgb & 0x00FFFFFFu) | ((uint)material << 24);

    /// <summary>
    /// Gets the visualization colour of a constraint graph colour slot.
    /// </summary>
    /// <param name="index">
    /// The slot index. The last one, <see cref="Constants.B3_GRAPH_COLOR_COUNT"/>
    /// minus one, is the overflow colour.
    /// </param>
    /// <returns>The colour.</returns>
    [LibraryImport(Box3DLibrary.Name)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial b3HexColor b3GetGraphColor(int index);
}
