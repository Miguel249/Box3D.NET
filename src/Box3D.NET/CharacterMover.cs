// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Box3D.Native;

namespace Box3D;

/*
 * The character mover
 * -------------------
 * Box3D does not ship a character controller, and neither does this library.
 * What it ships is the hard part: given a capsule and the world around it, find
 * the planes it is touching, and solve for a translation that satisfies all of
 * them at once.
 *
 * Everything above that - how fast the character walks, whether it can jump,
 * what counts as a slope it can climb, how it behaves on a moving platform - is
 * game design, and every game answers it differently. Wrapping an opinionated
 * controller here would be inventing physics policy that Box3D deliberately
 * left to the caller, and would be wrong for most callers.
 *
 * So the primitives are exposed idiomatically and allocation-free, and
 * CharacterControllerSample shows how to assemble a controller from them.
 *
 * The loop is:
 *   1. CollideCapsule gathers the planes the capsule is touching.
 *   2. The caller turns them into CollisionPlane values, choosing a push limit
 *      and whether each one should clip velocity.
 *   3. SolvePlanes finds the translation that satisfies them.
 *   4. ClipVelocity projects the velocity onto the surviving planes.
 */

/// <summary>
/// A plane between a character capsule and something it is touching.
/// </summary>
/// <remarks>
/// Reported by <see cref="PhysicsWorld.CollideCapsule{TCallback}"/>. Turn it
/// into a <see cref="CollisionPlane"/> to feed the solver, which is where you
/// decide how hard the surface pushes back.
/// </remarks>
public readonly record struct CharacterContact
{
    /// <summary>Gets the shape the capsule is touching.</summary>
    public Shape Shape { get; init; }

    /// <summary>Gets the outward normal of the surface.</summary>
    /// <remarks>
    /// Compare against your up axis to tell floor from wall from ceiling: a
    /// normal whose dot product with up exceeds the cosine of your maximum walk
    /// angle is ground the character can stand on.
    /// </remarks>
    public Vector3 Normal { get; init; }

    /// <summary>
    /// Gets the signed distance from the capsule centre to the plane, along the
    /// normal.
    /// </summary>
    /// <remarks>Negative means the capsule is already past the plane.</remarks>
    public float Offset { get; init; }

    /// <summary>Gets the closest point on the touched shape, relative to the query origin.</summary>
    public Vector3 Point { get; init; }
}

/// <summary>
/// A constraint handed to the character plane solver.
/// </summary>
/// <remarks>
/// Built from a <see cref="CharacterContact"/> once you have decided how the
/// surface should behave. <see cref="PushLimit"/> is what separates a solid wall
/// from something soft; <see cref="ClipsVelocity"/> is what stops the character
/// accumulating speed into a surface it cannot pass.
/// </remarks>
public readonly record struct CollisionPlane
{
    /// <summary>Initializes a new instance of the <see cref="CollisionPlane"/> struct.</summary>
    /// <param name="normal">The outward plane normal.</param>
    /// <param name="offset">The signed distance from the capsule centre to the plane.</param>
    /// <param name="pushLimit">
    /// The furthest the solver may push the capsule out of this plane. Use
    /// <see cref="float.MaxValue"/> for a solid surface.
    /// </param>
    /// <param name="clipsVelocity">Whether velocity is projected onto this plane.</param>
    public CollisionPlane(Vector3 normal, float offset, float pushLimit = float.MaxValue, bool clipsVelocity = true)
    {
        Normal = normal;
        Offset = offset;
        PushLimit = pushLimit;
        ClipsVelocity = clipsVelocity;
    }

    /// <summary>Gets the outward plane normal.</summary>
    public Vector3 Normal { get; init; }

    /// <summary>Gets the plane offset along its normal.</summary>
    /// <remarks>
    /// <para>
    /// The plane is <c>dot(Normal, point) = Offset</c>, so a point's separation
    /// from it is <c>dot(Normal, point) - Offset</c>: positive on the outside,
    /// negative once past it.
    /// </para>
    /// <para>
    /// The sign is worth pausing over when building planes by hand. For a
    /// capsule at the origin, a surface it is clear of needs a <em>negative</em>
    /// offset; a positive one puts the capsule inside the plane and the solver
    /// will push it out. Planes that come from
    /// <see cref="PhysicsWorld.CollideCapsule{TCallback}"/> already have the
    /// right sign.
    /// </para>
    /// </remarks>
    public float Offset { get; init; }

    /// <summary>Gets how far the solver may push the capsule out of this plane.</summary>
    /// <remarks>
    /// <see cref="float.MaxValue"/> makes the surface as rigid as the solver can
    /// manage. Smaller values make it soft, which is how you model something the
    /// character can partly sink into.
    /// </remarks>
    public float PushLimit { get; init; }

    /// <summary>Gets a value indicating whether velocity is clipped against this plane.</summary>
    /// <remarks>Should be false for soft planes, or the character loses speed to a surface that never stopped it.</remarks>
    public bool ClipsVelocity { get; init; }

    /// <summary>Gets how far the solver actually pushed along this plane.</summary>
    /// <remarks>
    /// Written by <see cref="CharacterMover.SolvePlanes"/>. A non-zero push is
    /// how you tell which planes were actually load-bearing this frame, which is
    /// the usual way to decide whether the character is standing on something.
    /// </remarks>
    public float Push { get; init; }

    /// <summary>Builds a solver plane from a reported contact.</summary>
    /// <param name="contact">The contact.</param>
    /// <param name="pushLimit">The push limit, defaulting to a solid surface.</param>
    /// <param name="clipsVelocity">Whether velocity is clipped against it.</param>
    /// <returns>The plane.</returns>
    public static CollisionPlane From(
        CharacterContact contact,
        float pushLimit = float.MaxValue,
        bool clipsVelocity = true) =>
        new(contact.Normal, contact.Offset, pushLimit, clipsVelocity);

    internal b3CollisionPlane ToNative() => new()
    {
        plane = new b3Plane { normal = Normal, offset = Offset },
        pushLimit = PushLimit,
        push = Push,
        clipVelocity = ClipsVelocity,
    };

    internal static CollisionPlane FromNative(in b3CollisionPlane plane) => new()
    {
        Normal = plane.plane.normal,
        Offset = plane.plane.offset,
        PushLimit = plane.pushLimit,
        ClipsVelocity = plane.clipVelocity,
        Push = plane.push,
    };
}

/// <summary>The outcome of solving a set of character collision planes.</summary>
public readonly record struct PlaneSolverResult
{
    /// <summary>Gets the translation that satisfies every plane.</summary>
    public Vector3 Translation { get; init; }

    /// <summary>Gets the number of iterations the solver used. Diagnostic.</summary>
    public int IterationCount { get; init; }
}

/// <summary>
/// Receives the surfaces a character capsule is touching.
/// </summary>
/// <remarks>
/// Implement this on a <see langword="struct"/>, as with the query callbacks, so
/// that gathering contacts every frame allocates nothing.
/// </remarks>
/// <example>
/// <code>
/// struct GatherPlanes : ICharacterCollisionCallback
/// {
///     public CollisionPlane[] Planes;
///     public int Count;
///
///     public bool OnContact(in CharacterContact contact)
///     {
///         if (Count &lt; Planes.Length)
///         {
///             Planes[Count++] = CollisionPlane.From(contact);
///         }
///
///         return true;
///     }
/// }
/// </code>
/// </example>
public interface ICharacterCollisionCallback
{
    /// <summary>Called once for each surface the capsule is touching.</summary>
    /// <param name="contact">The contact.</param>
    /// <returns><see langword="true"/> to keep gathering, <see langword="false"/> to stop.</returns>
    bool OnContact(in CharacterContact contact);
}

/// <summary>
/// The plane solver behind a kinematic character controller.
/// </summary>
/// <remarks>
/// <para>
/// These are the two steps that make character movement work: given the planes a
/// capsule is touching, find a translation that satisfies all of them, then clip
/// the velocity so the character does not accumulate speed into a wall.
/// </para>
/// <para>
/// What sits above them - walk speed, jumping, slope limits, step height - is
/// game design, and is left to you. <c>CharacterControllerSample</c> shows one
/// way to assemble it.
/// </para>
/// </remarks>
public static class CharacterMover
{
    /// <summary>
    /// Finds the translation closest to the one requested that satisfies every plane.
    /// </summary>
    /// <param name="targetTranslation">The movement the character wants to make.</param>
    /// <param name="planes">
    /// The planes to satisfy. Updated in place: each plane's
    /// <see cref="CollisionPlane.Push"/> reports how far the solver moved along it.
    /// </param>
    /// <returns>The translation to apply, and the iteration count.</returns>
    /// <remarks>
    /// This is what makes a character slide along a wall rather than stop dead
    /// against it, and what keeps it out of a corner where two walls meet.
    /// </remarks>
    /// <example>
    /// <code>
    /// Span&lt;CollisionPlane&gt; planes = stackalloc CollisionPlane[8];
    /// // ... fill from CollideCapsule ...
    ///
    /// PlaneSolverResult result = CharacterMover.SolvePlanes(desiredMove, planes[..count]);
    /// position += result.Translation;
    /// </code>
    /// </example>
    public static unsafe PlaneSolverResult SolvePlanes(Vector3 targetTranslation, Span<CollisionPlane> planes)
    {
        if (planes.IsEmpty)
        {
            // Nothing to satisfy, so the character moves exactly as asked.
            return new PlaneSolverResult { Translation = targetTranslation, IterationCount = 0 };
        }

        // The solver writes the resolved push back into each plane, so the native
        // copies have to travel in both directions.
        Span<b3CollisionPlane> native = planes.Length <= 16
            ? stackalloc b3CollisionPlane[planes.Length]
            : new b3CollisionPlane[planes.Length];

        for (int i = 0; i < planes.Length; i++)
        {
            native[i] = planes[i].ToNative();
        }

        b3PlaneSolverResult result;
        fixed (b3CollisionPlane* p = native)
        {
            result = B3.b3SolvePlanes(targetTranslation, p, planes.Length);
        }

        for (int i = 0; i < planes.Length; i++)
        {
            planes[i] = CollisionPlane.FromNative(native[i]);
        }

        return new PlaneSolverResult
        {
            Translation = result.delta,
            IterationCount = result.iterationCount,
        };
    }

    /// <summary>Projects a velocity onto the planes that resisted the movement.</summary>
    /// <param name="velocity">The velocity to clip.</param>
    /// <param name="planes">
    /// The planes, as returned by <see cref="SolvePlanes"/>. Planes with no push,
    /// or with <see cref="CollisionPlane.ClipsVelocity"/> false, are skipped.
    /// </param>
    /// <returns>The clipped velocity.</returns>
    /// <remarks>
    /// Without this a character walking into a wall keeps building up velocity
    /// into it, and shoots sideways the moment the wall ends.
    /// </remarks>
    public static unsafe Vector3 ClipVelocity(Vector3 velocity, ReadOnlySpan<CollisionPlane> planes)
    {
        if (planes.IsEmpty)
        {
            return velocity;
        }

        Span<b3CollisionPlane> native = planes.Length <= 16
            ? stackalloc b3CollisionPlane[planes.Length]
            : new b3CollisionPlane[planes.Length];

        for (int i = 0; i < planes.Length; i++)
        {
            native[i] = planes[i].ToNative();
        }

        fixed (b3CollisionPlane* p = native)
        {
            return B3.b3ClipVector(velocity, p, planes.Length);
        }
    }
}
