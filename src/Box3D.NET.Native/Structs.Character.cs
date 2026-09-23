// SPDX-License-Identifier: MIT
// Mirror of the character mover types in include/box3d/types.h.

using System.Numerics;
using System.Runtime.InteropServices;

namespace Box3D.Native;

/*
 * The character mover is a plane solver rather than a rigid body. The usual
 * sequence per frame is:
 *
 *   1. b3World_CollideMover gathers the planes touching the capsule.
 *   2. The application assembles b3CollisionPlane values from the results,
 *      choosing a push limit and whether each plane clips velocity.
 *   3. b3SolvePlanes finds the translation satisfying all of them.
 *   4. b3ClipVector projects the velocity onto the surviving planes.
 *
 * b3World_CastMover exists for sweeping the capsule, but it is a poor source of
 * information about what the mover is touching; use the planes for that.
 */

/// <summary>
/// A plane between a character mover and a shape. Mirror of <c>b3PlaneResult</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct b3PlaneResult
{
    /// <summary>The outward pointing plane.</summary>
    public b3Plane plane;

    /// <summary>The closest point on the shape. Not necessarily unique.</summary>
    public Vector3 point;

    /// <summary>The index of the mesh or height field triangle hit. Zero for other shapes.</summary>
    public int triangleIndex;

    /// <summary>The index of the compound child shape. Zero for other shapes.</summary>
    public int childIndex;

    /// <summary>The material index, clamped to the materials of the shape.</summary>
    public int materialIndex;
}

/// <summary>
/// A collision plane fed to the mover plane solver. Mirror of <c>b3CollisionPlane</c>.
/// </summary>
/// <remarks>
/// Normally assembled by the application from the <see cref="b3PlaneResult"/>
/// values gathered by <c>b3World_CollideMover</c>.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public struct b3CollisionPlane
{
    /// <summary>The plane between the mover and some shape.</summary>
    public b3Plane plane;

    /// <summary>
    /// The maximum distance the mover may be pushed out of this plane, usually in metres.
    /// </summary>
    /// <remarks>
    /// Setting this to <see cref="float.MaxValue"/> makes the plane as rigid as
    /// possible; lower values make the collision soft.
    /// </remarks>
    public float pushLimit;

    /// <summary>The push determined by the solver, usually in metres. Written by <c>b3SolvePlanes</c>.</summary>
    public float push;

    /// <summary>
    /// Whether <c>b3ClipVector</c> clips against this plane. Should be false for soft collision.
    /// </summary>
    public NativeBool clipVelocity;
}

/// <summary>
/// The result of the mover plane solver. Mirror of <c>b3PlaneSolverResult</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct b3PlaneSolverResult
{
    /// <summary>The final relative translation.</summary>
    public Vector3 delta;

    /// <summary>The number of iterations used. Diagnostic.</summary>
    public int iterationCount;
}

/// <summary>
/// A plane result together with the shape that produced it. Mirror of <c>b3BodyPlaneResult</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct b3BodyPlaneResult
{
    /// <summary>The shape on the body.</summary>
    public b3ShapeId shapeId;

    /// <summary>The plane result.</summary>
    public b3PlaneResult result;
}

/// <summary>
/// The time of impact between a character mover and a body. Mirror of <c>b3BodyTOIResult</c>.
/// </summary>
/// <remarks>Returned by <c>b3Body_TimeOfImpactMover</c>.</remarks>
[StructLayout(LayoutKind.Sequential)]
public struct b3BodyTOIResult
{
    /// <summary>The hit point in world space.</summary>
    public Vector3 point;

    /// <summary>The hit normal, pointing from the body towards the mover.</summary>
    public Vector3 normal;

    /// <summary>The fraction of the sweep at which the hit occurs. One when nothing is hit.</summary>
    public float fraction;

    /// <summary>The shape that was hit. Null when nothing is hit.</summary>
    public b3ShapeId shapeId;
}
