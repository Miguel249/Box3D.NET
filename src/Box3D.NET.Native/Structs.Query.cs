// SPDX-License-Identifier: MIT
// Mirror of the query types in include/box3d/types.h.

using System.Numerics;
using System.Runtime.InteropServices;

namespace Box3D.Native;

/// <summary>
/// Filters collisions between a query and the shapes it visits.
/// Mirror of <c>b3QueryFilter</c>.
/// </summary>
/// <remarks>
/// Start from <c>B3.b3DefaultQueryFilter()</c>. The <see cref="id"/> and
/// <see cref="name"/> fields only matter while recording.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct b3QueryFilter
{
    /// <summary>The categories this query belongs to. Normally a single bit.</summary>
    public ulong categoryBits;

    /// <summary>The shape categories this query accepts.</summary>
    public ulong maskBits;

    /// <summary>
    /// An optional identifier used to track this query in a recording, such as an
    /// entity id. Zero together with a null name means untagged.
    /// </summary>
    public ulong id;

    /// <summary>
    /// An optional label used together with <see cref="id"/> to identify the query
    /// in a recording, as a null-terminated UTF-8 string.
    /// </summary>
    /// <remarks>
    /// The recorder hashes the pair into one stable key, so the same pair tracks
    /// the same query across frames.
    /// </remarks>
    public byte* name;
}

/// <summary>Low-level ray cast input. Mirror of <c>b3RayCastInput</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct b3RayCastInput
{
    /// <summary>The start point of the ray.</summary>
    public Vector3 origin;

    /// <summary>The translation of the ray, so that the end point is origin plus translation.</summary>
    public Vector3 translation;

    /// <summary>The maximum fraction of the translation to consider. Usually one.</summary>
    public float maxFraction;
}

/// <summary>
/// The result of a closest-hit world ray cast. Mirror of <c>b3RayResult</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct b3RayResult
{
    /// <summary>The shape that was hit.</summary>
    public b3ShapeId shapeId;

    /// <summary>The world-space hit point.</summary>
    public Vector3 point;

    /// <summary>The world-space surface normal at the hit point.</summary>
    public Vector3 normal;

    /// <summary>
    /// The user material identifier at the hit point, which may be per triangle
    /// on a mesh, height field, or compound containing a mesh.
    /// </summary>
    public ulong userMaterialId;

    /// <summary>The fraction along the input ray at which the hit occurred.</summary>
    public float fraction;

    /// <summary>The triangle index for mesh, height field or compound-with-mesh shapes.</summary>
    public int triangleIndex;

    /// <summary>The child index when the shape is a compound.</summary>
    public int childIndex;

    /// <summary>The number of hierarchy nodes visited. Diagnostic.</summary>
    public int nodeVisits;

    /// <summary>The number of hierarchy leaves visited. Diagnostic.</summary>
    public int leafVisits;

    /// <summary>Whether the ray hit anything. When false every other field is meaningless.</summary>
    public NativeBool hit;
}

/// <summary>
/// A convex shape expressed as a point cloud with a radius, used by GJK.
/// Mirror of <c>b3ShapeProxy</c>.
/// </summary>
/// <remarks>
/// A sphere is one point with a non-zero radius, a capsule is two points with a
/// non-zero radius, and a box is eight points with zero radius.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct b3ShapeProxy
{
    /// <summary>The points of the cloud.</summary>
    public Vector3* points;

    /// <summary>
    /// The number of points. Must not exceed <see cref="Constants.B3_MAX_SHAPE_CAST_POINTS"/>.
    /// </summary>
    public int count;

    /// <summary>The radius by which the point cloud is inflated.</summary>
    public float radius;
}

/// <summary>Low-level shape cast input. Mirror of <c>b3ShapeCastInput</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct b3ShapeCastInput
{
    /// <summary>The shape being cast.</summary>
    public b3ShapeProxy proxy;

    /// <summary>The translation of the cast.</summary>
    public Vector3 translation;

    /// <summary>The maximum fraction of the translation to consider. Usually one.</summary>
    public float maxFraction;

    /// <summary>
    /// Whether the cast may encroach when the shapes start out touching.
    /// Only has an effect when the proxy radius is greater than zero.
    /// </summary>
    public NativeBool canEncroach;
}

/// <summary>
/// Input for sweeping a bounding box through a dynamic tree. Mirror of <c>b3BoxCastInput</c>.
/// </summary>
/// <remarks>
/// The caller folds the cast shape radius into the box, so the traversal stays a
/// conservative box sweep and the precise narrow phase happens per shape in the callback.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public struct b3BoxCastInput
{
    /// <summary>The box to cast, in the tree's frame.</summary>
    public b3AABB box;

    /// <summary>The sweep translation.</summary>
    public Vector3 translation;

    /// <summary>The maximum fraction of the translation to consider. Usually one.</summary>
    public float maxFraction;
}

/// <summary>
/// Low-level ray cast or shape cast output. Mirror of <c>b3CastOutput</c>.
/// </summary>
/// <remarks>
/// In the single-precision build this is also <c>b3WorldCastOutput</c>.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public struct b3CastOutput
{
    /// <summary>The surface normal at the hit point.</summary>
    public Vector3 normal;

    /// <summary>The hit point.</summary>
    public Vector3 point;

    /// <summary>The fraction of the input translation at which contact occurred.</summary>
    public float fraction;

    /// <summary>The number of iterations used. Diagnostic.</summary>
    public int iterations;

    /// <summary>The index of the mesh or height field triangle hit.</summary>
    public int triangleIndex;

    /// <summary>The index of the compound child hit.</summary>
    public int childIndex;

    /// <summary>The material index, or minus one for none.</summary>
    public int materialIndex;

    /// <summary>Whether the cast hit anything.</summary>
    public NativeBool hit;
}

/// <summary>
/// The result of casting against a single body. Mirror of <c>b3BodyCastResult</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct b3BodyCastResult
{
    /// <summary>The shape that was hit.</summary>
    public b3ShapeId shapeId;

    /// <summary>The world-space point on the shape surface.</summary>
    public Vector3 point;

    /// <summary>The world-space normal on the shape surface.</summary>
    public Vector3 normal;

    /// <summary>
    /// The fraction along the cast, so that the hit point is origin plus fraction times translation.
    /// </summary>
    public float fraction;

    /// <summary>The triangle index for mesh and height field shapes.</summary>
    public int triangleIndex;

    /// <summary>The user material identifier at the hit point.</summary>
    public ulong userMaterialId;

    /// <summary>The number of iterations used. Diagnostic.</summary>
    public int iterations;

    /// <summary>Whether the cast hit. When false every other field is meaningless.</summary>
    public NativeBool hit;
}

/// <summary>
/// A warm-start cache for the GJK simplex. Mirror of <c>b3SimplexCache</c>.
/// </summary>
/// <remarks>
/// Reusing a cache across calls with nearby transforms improves performance.
/// Zero-initialize it for the first call, or whenever the shapes change.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct b3SimplexCache
{
    /// <summary>A value used to compare the length, area or volume of two simplexes.</summary>
    public float metric;

    /// <summary>The number of stored simplex points.</summary>
    public ushort count;

    /// <summary>The cached simplex vertex indices on shape A.</summary>
    public fixed byte indexA[4];

    /// <summary>The cached simplex vertex indices on shape B.</summary>
    public fixed byte indexB[4];
}

/// <summary>Input for a pairwise shape cast. Mirror of <c>b3ShapeCastPairInput</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct b3ShapeCastPairInput
{
    /// <summary>The proxy for shape A, which stays fixed.</summary>
    public b3ShapeProxy proxyA;

    /// <summary>The proxy for shape B, which moves.</summary>
    public b3ShapeProxy proxyB;

    /// <summary>The pose of shape B in shape A's frame.</summary>
    public b3Transform transform;

    /// <summary>The translation of shape B, in shape A's frame.</summary>
    public Vector3 translationB;

    /// <summary>The maximum fraction of the translation to consider. Usually one.</summary>
    public float maxFraction;

    /// <summary>Whether shapes with a radius may move slightly closer when already touching.</summary>
    public NativeBool canEncroach;
}

/// <summary>Input for a closest-points query. Mirror of <c>b3DistanceInput</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct b3DistanceInput
{
    /// <summary>The proxy for shape A.</summary>
    public b3ShapeProxy proxyA;

    /// <summary>The proxy for shape B.</summary>
    public b3ShapeProxy proxyB;

    /// <summary>
    /// The pose of shape B in shape A's frame.
    /// </summary>
    /// <remarks>The query is origin independent and runs entirely in frame A.</remarks>
    public b3Transform transform;

    /// <summary>Whether the proxy radii are taken into account.</summary>
    public NativeBool useRadii;
}

/// <summary>Output of a closest-points query, in shape A's frame. Mirror of <c>b3DistanceOutput</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct b3DistanceOutput
{
    /// <summary>The closest point on shape A.</summary>
    public Vector3 pointA;

    /// <summary>The closest point on shape B.</summary>
    public Vector3 pointB;

    /// <summary>The normal from A to B. Meaningless when the distance is zero.</summary>
    public Vector3 normal;

    /// <summary>The distance between the shapes, zero when they overlap.</summary>
    public float distance;

    /// <summary>The number of GJK iterations used.</summary>
    public int iterations;

    /// <summary>The number of simplexes written to the debug simplex array.</summary>
    public int simplexCount;
}

/// <summary>A GJK simplex vertex, exposed for debugging. Mirror of <c>b3SimplexVertex</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct b3SimplexVertex
{
    /// <summary>The support point on proxy A.</summary>
    public Vector3 wA;

    /// <summary>The support point on proxy B.</summary>
    public Vector3 wB;

    /// <summary>The Minkowski difference, wB minus wA.</summary>
    public Vector3 w;

    /// <summary>The barycentric coordinate.</summary>
    public float a;

    /// <summary>The index of wA on proxy A.</summary>
    public int indexA;

    /// <summary>The index of wB on proxy B.</summary>
    public int indexB;
}

/// <summary>
/// The four vertex slots of a GJK simplex.
/// </summary>
/// <remarks>
/// An inline array, so it has the same layout as the C array
/// <c>b3SimplexVertex[4]</c> while still supporting indexing and
/// <see cref="System.Span{T}"/> conversion.
/// </remarks>
[System.Runtime.CompilerServices.InlineArray(4)]
public struct b3SimplexVertexArray
{
    private b3SimplexVertex _element0;
}

/// <summary>A GJK simplex, exposed for debugging. Mirror of <c>b3Simplex</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct b3Simplex
{
    /// <summary>The vertex slots. Only the first <see cref="count"/> are valid.</summary>
    public b3SimplexVertexArray vertices;

    /// <summary>The number of valid vertices, one through four.</summary>
    public int count;
}

/// <summary>
/// The motion of a body over a time step, for time of impact. Mirror of <c>b3Sweep</c>.
/// </summary>
/// <remarks>
/// Shapes are defined relative to the body origin, which need not coincide with
/// the centre of mass, so the centre of mass is interpolated separately.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public struct b3Sweep
{
    /// <summary>The local centre of mass.</summary>
    public Vector3 localCenter;

    /// <summary>The world centre of mass at the start of the sweep.</summary>
    public Vector3 c1;

    /// <summary>The world centre of mass at the end of the sweep.</summary>
    public Vector3 c2;

    /// <summary>The world rotation at the start of the sweep.</summary>
    public Quaternion q1;

    /// <summary>The world rotation at the end of the sweep.</summary>
    public Quaternion q2;
}

/// <summary>Input for a time of impact query. Mirror of <c>b3TOIInput</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct b3TOIInput
{
    /// <summary>The proxy for shape A.</summary>
    public b3ShapeProxy proxyA;

    /// <summary>The proxy for shape B.</summary>
    public b3ShapeProxy proxyB;

    /// <summary>The motion of shape A.</summary>
    public b3Sweep sweepA;

    /// <summary>The motion of shape B.</summary>
    public b3Sweep sweepB;

    /// <summary>The end of the sweep interval, which runs from zero to this value.</summary>
    public float maxFraction;
}

/// <summary>Output of a time of impact query. Mirror of <c>b3TOIOutput</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct b3TOIOutput
{
    /// <summary>The kind of result.</summary>
    public b3TOIState state;

    /// <summary>The hit point.</summary>
    public Vector3 point;

    /// <summary>The hit normal.</summary>
    public Vector3 normal;

    /// <summary>The sweep fraction at which the collision occurs.</summary>
    public float fraction;

    /// <summary>The final distance between the shapes.</summary>
    public float distance;

    /// <summary>The number of outer iterations used.</summary>
    public int distanceIterations;

    /// <summary>The total number of push-back iterations used.</summary>
    public int pushBackIterations;

    /// <summary>The total number of root-finding iterations used.</summary>
    public int rootIterations;

    /// <summary>
    /// Whether initial overlap was detected and a fallback sphere was used as a
    /// last resort to prevent tunnelling.
    /// </summary>
    public NativeBool usedFallback;
}
