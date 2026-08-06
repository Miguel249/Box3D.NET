// SPDX-License-Identifier: MIT
// Mirror of the enumerations in include/box3d/types.h and include/box3d/box3d.h.

using System;

namespace Box3D.Native;

/// <summary>
/// The body simulation type. Mirror of <c>b3BodyType</c>.
/// </summary>
public enum b3BodyType
{
    /// <summary>Zero mass and zero velocity. May be moved manually.</summary>
    b3_staticBody = 0,

    /// <summary>Zero mass, velocity set by the user, moved by the solver.</summary>
    b3_kinematicBody = 1,

    /// <summary>Positive mass, velocity determined by forces, moved by the solver.</summary>
    b3_dynamicBody = 2,

    /// <summary>The number of body types.</summary>
    b3_bodyTypeCount = 3,
}

/// <summary>
/// The shape type. Mirror of <c>b3ShapeType</c>.
/// </summary>
/// <remarks>The ordering is alphabetical in the C header and is load-bearing for the collision dispatch tables.</remarks>
public enum b3ShapeType
{
    /// <summary>A capsule, which is an extruded sphere.</summary>
    b3_capsuleShape = 0,

    /// <summary>A baked compound composed of spheres, capsules, hulls and meshes.</summary>
    b3_compoundShape = 1,

    /// <summary>A height field, useful for terrain.</summary>
    b3_heightShape = 2,

    /// <summary>A convex hull.</summary>
    b3_hullShape = 3,

    /// <summary>A triangle soup.</summary>
    b3_meshShape = 4,

    /// <summary>A sphere with an offset centre.</summary>
    b3_sphereShape = 5,

    /// <summary>The number of shape types.</summary>
    b3_shapeTypeCount = 6,
}

/// <summary>
/// The joint type. Mirror of <c>b3JointType</c>.
/// </summary>
/// <remarks>Useful because every joint shares the <see cref="b3JointId"/> type.</remarks>
public enum b3JointType
{
    /// <summary>Constrains the angle between the z axes of the two joint frames.</summary>
    b3_parallelJoint = 0,

    /// <summary>Connects a point on one body to a point on another by a segment.</summary>
    b3_distanceJoint = 1,

    /// <summary>Disables collision between two specific bodies.</summary>
    b3_filterJoint = 2,

    /// <summary>Controls the relative position and velocity between two bodies.</summary>
    b3_motorJoint = 3,

    /// <summary>Allows translation along a single axis with no relative rotation.</summary>
    b3_prismaticJoint = 4,

    /// <summary>Allows relative rotation about a single axis. Also called a hinge.</summary>
    b3_revoluteJoint = 5,

    /// <summary>Allows rotation about a shared point. Also called a ball-in-socket.</summary>
    b3_sphericalJoint = 6,

    /// <summary>Rigidly constrains the relative transform between two bodies.</summary>
    b3_weldJoint = 7,

    /// <summary>Models a wheel with suspension, spin and optional steering.</summary>
    b3_wheelJoint = 8,
}

/// <summary>
/// The outcome of a time of impact query. Mirror of <c>b3TOIState</c>.
/// </summary>
public enum b3TOIState
{
    /// <summary>The query has not been run.</summary>
    b3_toiStateUnknown = 0,

    /// <summary>The query failed to converge.</summary>
    b3_toiStateFailed = 1,

    /// <summary>The shapes were already overlapped at the start of the sweep.</summary>
    b3_toiStateOverlapped = 2,

    /// <summary>The shapes touch within the sweep interval.</summary>
    b3_toiStateHit = 3,

    /// <summary>The shapes remain separated for the whole sweep.</summary>
    b3_toiStateSeparated = 4,
}

/// <summary>
/// Flags on a dynamic tree node. Mirror of <c>b3TreeNodeFlags</c>.
/// </summary>
/// <remarks>Internal to the tree implementation; exposed because <c>b3TreeNode</c> is public.</remarks>
[Flags]
public enum b3TreeNodeFlags
{
    /// <summary>No flags.</summary>
    None = 0,

    /// <summary>The node is allocated rather than on the free list.</summary>
    b3_allocatedNode = 0x0001,

    /// <summary>The node bounding box was enlarged and needs a refit.</summary>
    b3_enlargedNode = 0x0002,

    /// <summary>The node is a leaf and carries user data instead of children.</summary>
    b3_leafNode = 0x0004,
}

/// <summary>
/// Adjacency flags on a mesh triangle. Mirror of <c>b3MeshEdgeFlags</c>.
/// </summary>
/// <remarks>
/// Used to suppress ghost collisions on internal edges of a triangle mesh.
/// The edge numbering follows the triangle winding.
/// </remarks>
[Flags]
public enum b3MeshEdgeFlags
{
    /// <summary>No flags.</summary>
    None = 0,

    /// <summary>The first edge is concave.</summary>
    b3_concaveEdge1 = 0x01,

    /// <summary>The second edge is concave.</summary>
    b3_concaveEdge2 = 0x02,

    /// <summary>The third edge is concave.</summary>
    b3_concaveEdge3 = 0x04,

    /// <summary>The first edge is concave when the triangle is inverted.</summary>
    b3_inverseConcaveEdge1 = 0x10,

    /// <summary>The second edge is concave when the triangle is inverted.</summary>
    b3_inverseConcaveEdge2 = 0x20,

    /// <summary>The third edge is concave when the triangle is inverted.</summary>
    b3_inverseConcaveEdge3 = 0x40,

    /// <summary>All three edges are concave.</summary>
    b3_allConcaveEdges = b3_concaveEdge1 | b3_concaveEdge2 | b3_concaveEdge3,

    /// <summary>The first edge is flat: concave in both winding directions.</summary>
    b3_flatEdge1 = b3_concaveEdge1 | b3_inverseConcaveEdge1,

    /// <summary>The second edge is flat: concave in both winding directions.</summary>
    b3_flatEdge2 = b3_concaveEdge2 | b3_inverseConcaveEdge2,

    /// <summary>The third edge is flat: concave in both winding directions.</summary>
    b3_flatEdge3 = b3_concaveEdge3 | b3_inverseConcaveEdge3,

    /// <summary>All three edges are flat.</summary>
    b3_allFlatEdges = b3_flatEdge1 | b3_flatEdge2 | b3_flatEdge3,
}

/// <summary>
/// The cached separating axis feature. Mirror of the anonymous enum typedef'd as <c>b3SeparatingFeature</c>.
/// </summary>
public enum b3SeparatingFeature
{
    /// <summary>No cached axis.</summary>
    b3_invalidAxis = 0,

    /// <summary>The back side of a face.</summary>
    b3_backsideAxis = 1,

    /// <summary>A face on shape A.</summary>
    b3_faceAxisA = 2,

    /// <summary>A face on shape B.</summary>
    b3_faceAxisB = 3,

    /// <summary>An edge pair spanning both shapes.</summary>
    b3_edgePairAxis = 4,

    /// <summary>The axis between the closest points.</summary>
    b3_closestPointsAxis = 5,

    /// <summary>A manually selected face on shape A. For testing.</summary>
    b3_manualFaceAxisA = 6,

    /// <summary>A manually selected face on shape B. For testing.</summary>
    b3_manualFaceAxisB = 7,

    /// <summary>A manually selected edge pair. For testing.</summary>
    b3_manualEdgePairAxis = 8,
}

/// <summary>
/// The cached triangle feature involved in a contact. Mirror of the anonymous enum typedef'd as <c>b3TriangleFeature</c>.
/// </summary>
public enum b3TriangleFeature
{
    /// <summary>No feature.</summary>
    b3_featureNone = 0,

    /// <summary>The face of the triangle.</summary>
    b3_featureTriangleFace = 1,

    /// <summary>A face of the hull.</summary>
    b3_featureHullFace = 2,

    /// <summary>The edge from vertex 1 to vertex 2.</summary>
    b3_featureEdge1 = 3,

    /// <summary>The edge from vertex 2 to vertex 3.</summary>
    b3_featureEdge2 = 4,

    /// <summary>The edge from vertex 3 to vertex 1.</summary>
    b3_featureEdge3 = 5,

    /// <summary>The first vertex.</summary>
    b3_featureVertex1 = 6,

    /// <summary>The second vertex.</summary>
    b3_featureVertex2 = 7,

    /// <summary>The third vertex.</summary>
    b3_featureVertex3 = 8,
}

/// <summary>
/// A debug draw material preset. Mirror of <c>b3DebugMaterial</c>.
/// </summary>
/// <remarks>
/// Packed into the unused high byte of a colour so the low 24 bits stay RGB.
/// See <c>b3MakeDebugColor</c>.
/// </remarks>
public enum b3DebugMaterial
{
    /// <summary>Use the renderer's per-body-type appearance.</summary>
    b3_debugMaterialDefault = 0,

    /// <summary>A matte, non-reflective surface.</summary>
    b3_debugMaterialMatte = 1,

    /// <summary>A soft-looking surface.</summary>
    b3_debugMaterialSoft = 2,

    /// <summary>The appearance used for sleeping or inactive bodies.</summary>
    b3_debugMaterialDead = 3,

    /// <summary>A glossy surface.</summary>
    b3_debugMaterialGlossy = 4,

    /// <summary>A metallic surface.</summary>
    b3_debugMaterialMetallic = 5,
}

/// <summary>
/// The kind of a recorded spatial query. Mirror of <c>b3RecQueryType</c>.
/// </summary>
public enum b3RecQueryType
{
    /// <summary>An axis-aligned box overlap query.</summary>
    b3_recQueryOverlapAABB = 0,

    /// <summary>A shape overlap query.</summary>
    b3_recQueryOverlapShape = 1,

    /// <summary>A ray cast.</summary>
    b3_recQueryCastRay = 2,

    /// <summary>A shape cast.</summary>
    b3_recQueryCastShape = 3,

    /// <summary>A closest-hit ray cast.</summary>
    b3_recQueryCastRayClosest = 4,

    /// <summary>A character mover cast.</summary>
    b3_recQueryCastMover = 5,

    /// <summary>A character mover collision query.</summary>
    b3_recQueryCollideMover = 6,
}
