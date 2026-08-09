// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using Box3D.Native;

namespace Box3D;

/*
 * Why the library rejects NaN and infinity
 * ----------------------------------------
 * Box3D checks its inputs with B3_ASSERT, which compiles to nothing in the
 * release builds this package ships. So a non-finite value is accepted in
 * silence, and it does not stay where it was put.
 *
 * Measured: setting one body's velocity to NaN and stepping thirty times left a
 * second body, twenty metres away and never touched, with a position of
 * (NaN, NaN, NaN). The solver couples bodies through islands and the broad
 * phase, so one bad number reaches everything. The simulation does not fail, it
 * silently becomes garbage, and the first sign of it is usually a renderer
 * drawing nothing.
 *
 * Recovering is impossible: there is no way to remove a NaN from a world once it
 * is in. So the value is rejected at the boundary, where the caller still has
 * the stack frame that produced it.
 *
 * The cost was measured before this went in rather than assumed. See
 * docs/benchmarks.md.
 */

/// <summary>
/// Argument checks applied at the boundary between application code and the
/// simulation.
/// </summary>
internal static class Validate
{
    /// <summary>Rejects a vector holding NaN or an infinity.</summary>
    /// <param name="value">The vector to check.</param>
    /// <param name="paramName">The parameter being checked.</param>
    /// <exception cref="ArgumentException">The vector is not finite.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Finite(Vector3 value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        // Three explicit checks rather than testing the sum: adding the
        // components first would report a false positive for a legitimately
        // large vector whose sum overflows to infinity.
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z))
        {
            ThrowNotFinite(value, paramName);
        }
    }

    /// <summary>Rejects a quaternion holding NaN or an infinity.</summary>
    /// <param name="value">The quaternion to check.</param>
    /// <param name="paramName">The parameter being checked.</param>
    /// <exception cref="ArgumentException">The quaternion is not finite.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Finite(Quaternion value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) ||
            !float.IsFinite(value.Z) || !float.IsFinite(value.W))
        {
            ThrowNotFinite(value, paramName);
        }
    }

    /// <summary>Rejects a number that is NaN or infinite.</summary>
    /// <param name="value">The value to check.</param>
    /// <param name="paramName">The parameter being checked.</param>
    /// <exception cref="ArgumentException">The value is not finite.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Finite(float value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!float.IsFinite(value))
        {
            ThrowNotFinite(value, paramName);
        }
    }

    /*
     * Why the library rejects stale handles
     * -------------------------------------
     * Box3D resolves an id by indexing straight into the world's arrays and
     * asserting the id is live on the way past. Those assertions compile to
     * nothing in the release builds this package ships, so a handle to a
     * destroyed body reaches b3Array_Get unchecked. Measured on win-x64 against
     * the shipped Release binary:
     *
     *   default(Body).Position                access violation, 0xC0000005
     *   destroyed body .Position              access violation
     *   body .Position after world.Dispose()  access violation
     *   destroyed body .Destroy()             access violation
     *   destroyed body .AddSphere()           access violation
     *   handle whose index was reused         returned the *replacement* body's
     *                                         position, silently
     *
     * A managed property read is not allowed to kill the process, and the last
     * case is worse than a crash: it is wrong data with no symptom. Box3D
     * exports b3Body_IsValid, b3Shape_IsValid and b3Joint_IsValid precisely so
     * a host can ask before it dereferences, and those are safe against any bit
     * pattern - they bounds-check the world index, the array index and the
     * generation counter before touching anything.
     *
     * So every high-level member that dereferences a handle asks first. The
     * check is one extra call into the same library: 2.07 ns against the
     * 2.66 ns of the b3Body_GetPosition it guards, measured over 20 million
     * iterations on the machine described in docs/benchmarks.md. Attaching
     * [SuppressGCTransition] to a second, private declaration of the predicate
     * was measured too - it brought the check to 1.65 ns - and rejected: 0.4 ns
     * does not pay for a hand-written duplicate of a generated binding.
     *
     * Nothing here applies to Box3D.NET.Native or to Box3D.Interop. Those are
     * the raw C API and are documented as validating nothing.
     */

    /// <summary>Rejects a handle that does not refer to a live body.</summary>
    /// <param name="id">The identifier to check.</param>
    /// <returns>The identifier, unchanged.</returns>
    /// <exception cref="InvalidOperationException">The handle is null or stale.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static b3BodyId Handle(b3BodyId id)
    {
        if (id.IsNull || !B3.b3Body_IsValid(id))
        {
            ThrowStaleHandle("body", nameof(Body), "by disposing the world");
        }

        return id;
    }

    /// <summary>Rejects a handle that does not refer to a live shape.</summary>
    /// <param name="id">The identifier to check.</param>
    /// <returns>The identifier, unchanged.</returns>
    /// <exception cref="InvalidOperationException">The handle is null or stale.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static b3ShapeId Handle(b3ShapeId id)
    {
        if (id.IsNull || !B3.b3Shape_IsValid(id))
        {
            ThrowStaleHandle("shape", nameof(Shape), "by destroying the body it is attached to, or by disposing the world");
        }

        return id;
    }

    /// <summary>Rejects a handle that does not refer to a live joint.</summary>
    /// <param name="id">The identifier to check.</param>
    /// <returns>The identifier, unchanged.</returns>
    /// <exception cref="InvalidOperationException">The handle is null or stale.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static b3JointId Handle(b3JointId id)
    {
        if (id.IsNull || !B3.b3Joint_IsValid(id))
        {
            ThrowStaleHandle("joint", nameof(Joint), "by destroying either body it connects, or by disposing the world");
        }

        return id;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowStaleHandle(string what, string type, string destroyedBy) =>
        throw new InvalidOperationException(
            $"This {what} handle does not refer to a live {what}. Either it was default-constructed, or the " +
            $"{what} has been destroyed - directly, or {destroyedBy}. Handles are values, so a copy left in " +
            $"a list or a field outlives what it names; check {type}.IsValid before using one whose {what} " +
            "may be gone. The usual source is an end-touch or sensor-end event, which is raised precisely " +
            "because a shape was destroyed or stopped colliding.");

    // Kept out of the checking methods so that they stay small enough to inline.
    // The throw path is cold and its size does not matter.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowNotFinite(object value, string? paramName) =>
        throw new ArgumentException(
            $"The value {value} is not finite. Box3D validates its inputs with assertions that release " +
            "builds compile out, so a NaN or infinity is accepted in silence and then spreads: the solver " +
            "couples bodies through islands and the broad phase, so one bad number can leave every body in " +
            "the world at NaN within a few steps, and there is no way to remove it afterwards.",
            paramName);
}
