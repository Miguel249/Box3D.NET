// SPDX-License-Identifier: MIT
// Mirror of the collision types in include/box3d/types.h.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Box3D.Native;

/// <summary>
/// A contact point within a manifold. Mirror of <c>b3ManifoldPoint</c>.
/// </summary>
/// <remarks>
/// Box3D uses speculative collision, so a manifold point may be separated rather
/// than touching. Use <see cref="totalNormalImpulse"/> to tell whether the point
/// actually interacted during the step.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public struct b3ManifoldPoint
{
    /// <summary>The contact point relative to body A's centre of mass, in world space.</summary>
    public Vector3 anchorA;

    /// <summary>The contact point relative to body B's centre of mass, in world space.</summary>
    public Vector3 anchorB;

    /// <summary>The separation of the contact point. Negative when penetrating.</summary>
    public float separation;

    /// <summary>The cached separation used for contact recycling.</summary>
    public float baseSeparation;

    /// <summary>
    /// The impulse along the manifold normal from the final sub-step.
    /// </summary>
    public float normalImpulse;

    /// <summary>
    /// The total normal impulse applied across all sub-steps.
    /// </summary>
    /// <remarks>
    /// This is what identifies a speculative point that actually interacted
    /// during the step; <see cref="normalImpulse"/> alone can be zero.
    /// </remarks>
    public float totalNormalImpulse;

    /// <summary>
    /// The relative normal velocity before solving, used for hit events.
    /// Negative means the shapes are approaching.
    /// </summary>
    public float normalVelocity;

    /// <summary>
    /// A stable identifier for this contact point between the two shapes,
    /// used to match points across steps for warm starting.
    /// </summary>
    public uint featureId;

    /// <summary>The triangle index when one of the shapes is a mesh or height field.</summary>
    public int triangleIndex;

    /// <summary>Whether this point also existed in the previous step.</summary>
    public NativeBool persisted;
}

/// <summary>
/// The four contact point slots of a manifold.
/// </summary>
/// <remarks>
/// An inline array, so it has the same layout as the C array
/// <c>b3ManifoldPoint[B3_MAX_MANIFOLD_POINTS]</c> while supporting indexing and
/// <see cref="System.Span{T}"/> conversion.
/// </remarks>
[InlineArray(Constants.B3_MAX_MANIFOLD_POINTS)]
public struct b3ManifoldPointArray
{
    private b3ManifoldPoint _element0;
}

/// <summary>
/// The contact points between two colliding shapes. Mirror of <c>b3Manifold</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct b3Manifold
{
    /// <summary>The contact point slots. Only the first <see cref="pointCount"/> are valid.</summary>
    public b3ManifoldPointArray points;

    /// <summary>The unit normal in world space, pointing from shape A to shape B.</summary>
    public Vector3 normal;

    /// <summary>The central friction angular impulse, applied about the normal.</summary>
    public float twistImpulse;

    /// <summary>The central friction linear impulse.</summary>
    public Vector3 frictionImpulse;

    /// <summary>The rolling resistance angular impulse.</summary>
    public Vector3 rollingImpulse;

    /// <summary>The number of valid contact points, zero through four.</summary>
    public int pointCount;
}

/// <summary>
/// A cache of the last separating axis, used to accelerate repeated collision
/// queries between the same pair. Mirror of <c>b3SATCache</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct b3SATCache
{
    /// <summary>The separation when the cache was populated. Negative for overlap.</summary>
    public float separation;

    /// <summary>The kind of feature cached, as a <see cref="b3SeparatingFeature"/>.</summary>
    public byte type;

    /// <summary>The index of the feature on shape A.</summary>
    public byte indexA;

    /// <summary>The index of the feature on shape B.</summary>
    public byte indexB;

    /// <summary>Whether the cache was reused on the last query.</summary>
    public byte hit;
}

/// <summary>
/// Identifies a contact point by the pair of edges that produced it.
/// Mirror of <c>b3FeaturePair</c>.
/// </summary>
/// <remarks>
/// A contact point is always the intersection of two edges, which may both
/// belong to the same shape (making it a vertex) or one to each shape. The pair
/// gives contact points temporal identity for warm starting.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public struct b3FeaturePair
{
    /// <summary>Which shape owns the incoming edge.</summary>
    public byte owner1;

    /// <summary>The index of the incoming edge on its shape.</summary>
    public byte index1;

    /// <summary>Which shape owns the outgoing edge.</summary>
    public byte owner2;

    /// <summary>The index of the outgoing edge on its shape.</summary>
    public byte index2;
}

/// <summary>
/// A manifold point expressed in shape A's frame, with no dynamics.
/// Mirror of <c>b3LocalManifoldPoint</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct b3LocalManifoldPoint
{
    /// <summary>The contact point in frame A.</summary>
    public Vector3 point;

    /// <summary>The separation. Negative for overlap.</summary>
    public float separation;

    /// <summary>The feature pair identifying this point.</summary>
    public b3FeaturePair pair;

    /// <summary>The triangle index when colliding with a mesh or height field.</summary>
    public int triangleIndex;
}

/// <summary>
/// The output of the low-level <c>b3Collide*</c> functions, in shape A's frame.
/// Mirror of <c>b3LocalManifold</c>.
/// </summary>
/// <remarks>
/// Unlike <see cref="b3Manifold"/>, the point array is supplied by the caller,
/// so the point count is bounded only by the buffer capacity passed alongside it.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct b3LocalManifold
{
    /// <summary>The contact normal in frame A.</summary>
    public Vector3 normal;

    /// <summary>The triangle normal, when a triangle is involved.</summary>
    public Vector3 triangleNormal;

    /// <summary>The contact points, written into the caller's buffer.</summary>
    public b3LocalManifoldPoint* points;

    /// <summary>The number of contact points written.</summary>
    public int pointCount;

    /// <summary>The index of the triangle involved.</summary>
    public int triangleIndex;

    /// <summary>The index of the triangle's first vertex.</summary>
    public int i1;

    /// <summary>The index of the triangle's second vertex.</summary>
    public int i2;

    /// <summary>The index of the triangle's third vertex.</summary>
    public int i3;

    /// <summary>The squared distance of a sphere from a triangle, used to reduce ghost collisions.</summary>
    public float squaredDistance;

    /// <summary>The triangle feature involved in the contact.</summary>
    public b3TriangleFeature feature;

    /// <summary>The triangle adjacency flags, as <see cref="b3MeshEdgeFlags"/>.</summary>
    public int triangleFlags;
}
