// SPDX-License-Identifier: MIT

using System;
using Box3D;
using Box3D.Native;

namespace Box3D.Benchmarks;

/// <summary>
/// Checks that a benchmark scene is doing the work it claims to be doing,
/// before anything is measured.
/// </summary>
/// <remarks>
/// <para>
/// A physics benchmark fails quietly rather than loudly. Box3D skips a sleeping
/// body entirely, so a scene that settled during warm-up steps in almost no time
/// and the figure stops responding to body count: measured here, 100 bodies and
/// 10,000 bodies both came out around 210 ns, which is the sleep check and
/// nothing else. That result looks excellent and means nothing.
/// </para>
/// <para>
/// The same goes for a scene whose bodies drifted apart, or a ray aimed past
/// everything: the benchmark still runs, still reports a tidy number, and is
/// measuring an empty solver.
/// </para>
/// <para>
/// So every scene states what it expects to be true of itself and is held to it
/// at setup time. A benchmark that stops doing real work fails instead of
/// quietly getting faster.
/// </para>
/// </remarks>
internal static class Workload
{
    /// <summary>
    /// Asserts that the world has at least the expected number of awake bodies.
    /// </summary>
    /// <param name="world">The world about to be measured.</param>
    /// <param name="expected">The number of bodies the scene was built with.</param>
    /// <param name="what">What the scene is, for the failure message.</param>
    /// <exception cref="InvalidOperationException">
    /// Fewer bodies are awake than the scene was built with, so the measurement
    /// would be of a solver with nothing to do.
    /// </exception>
    public static void RequireAwake(PhysicsWorld world, int expected, string what)
    {
        int awake = world.AwakeBodyCount;

        if (awake < expected)
        {
            throw new InvalidOperationException(
                $"The {what} scene has {awake} awake bodies but was built with {expected}. Box3D skips a " +
                "sleeping body entirely, so this measurement would be of the sleep check rather than of the " +
                "simulation. Either the settle loop put the scene to sleep, or EnableSleep was left on.");
        }
    }

    /// <summary>Asserts the same thing about a world held as a raw identifier.</summary>
    /// <param name="world">The world about to be measured.</param>
    /// <param name="expected">The number of bodies the scene was built with.</param>
    /// <param name="what">What the scene is, for the failure message.</param>
    /// <exception cref="InvalidOperationException">Fewer bodies are awake than expected.</exception>
    public static void RequireAwake(b3WorldId world, int expected, string what)
    {
        int awake = B3.b3World_GetAwakeBodyCount(world);

        if (awake < expected)
        {
            throw new InvalidOperationException(
                $"The {what} scene has {awake} awake bodies but was built with {expected}. This measurement " +
                "would be of the sleep check rather than of the simulation.");
        }
    }

    /// <summary>
    /// Asserts that a query actually reaches something.
    /// </summary>
    /// <param name="hits">How many results the query produced.</param>
    /// <param name="expected">The fewest results the scene should produce.</param>
    /// <param name="what">What the query is, for the failure message.</param>
    /// <exception cref="InvalidOperationException">The query found less than expected.</exception>
    public static void RequireHits(int hits, int expected, string what)
    {
        if (hits < expected)
        {
            throw new InvalidOperationException(
                $"The {what} query found {hits} results but should find at least {expected}. A query that " +
                "reaches nothing measures the broad phase rejecting it, not the work it is supposed to do.");
        }
    }
}
