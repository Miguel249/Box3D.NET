// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

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
