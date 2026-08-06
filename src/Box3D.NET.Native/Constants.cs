// SPDX-License-Identifier: MIT
// Mirror of the macros in include/box3d/constants.h, base.h and math_functions.h.

namespace Box3D.Native;

/// <summary>
/// The compile-time constants of Box3D.
/// </summary>
/// <remarks>
/// <para>
/// Only macros with a fixed value appear here. Several Box3D macros expand to an
/// expression involving <c>b3GetLengthUnitsPerMeter()</c> and therefore change
/// when the application chooses different units; those live on
/// <see cref="ScaledConstants"/> as properties, because a <c>const</c> would bake
/// in the wrong value.
/// </para>
/// <para>
/// The values assume a default build of Box3D. Constants guarded by
/// <c>#ifndef</c> in the header (<c>B3_MAX_WORLDS</c>, <c>B3_GYROSCOPIC_ITERATIONS</c>,
/// <c>B3_RESTITUTION_ITERATIONS</c>) can be overridden when compiling the native
/// library, in which case these no longer match.
/// </para>
/// </remarks>
public static class Constants
{
    /// <summary>Indicates null for interfaces that use indices rather than pointers.</summary>
    public const int B3_NULL_INDEX = -1;

    /// <summary>The default collision category bits: every category.</summary>
    public const ulong B3_DEFAULT_CATEGORY_BITS = ulong.MaxValue;

    /// <summary>The default collision mask bits: collide with everything.</summary>
    public const ulong B3_DEFAULT_MASK_BITS = ulong.MaxValue;

    /// <summary>The maximum number of parallel workers.</summary>
    public const int B3_MAX_WORKERS = 32;

    /// <summary>The maximum number of tasks queued per world step.</summary>
    public const int B3_MAX_TASKS = 256;

    /// <summary>The number of colours in the constraint graph. The last one is the overflow set.</summary>
    public const int B3_GRAPH_COLOR_COUNT = 24;

    /// <summary>The number of buckets used to report contact points per shape pair.</summary>
    public const int B3_CONTACT_MANIFOLD_COUNT_BUCKETS = 8;

    /// <summary>The lower bound on the friction weight of a speculative contact point.</summary>
    public const float B3_MIN_FRICTION_WEIGHT = 1e-10f;

    /// <summary>The maximum number of simultaneous worlds. May be overridden at compile time.</summary>
    public const int B3_MAX_WORLDS = 128;

    /// <summary>The maximum rotation of a body per time step, in radians.</summary>
    /// <remarks>Raising this to half pi or more breaks continuous collision.</remarks>
    public const float B3_MAX_ROTATION = 0.25f * B3_PI;

    /// <summary>The per-shape bounding box margin as a fraction of the shape extent.</summary>
    public const float B3_AABB_MARGIN_FRACTION = 0.125f;

    /// <summary>The time a body must be still before it sleeps, in seconds.</summary>
    public const float B3_TIME_TO_SLEEP = 0.5f;

    /// <summary>The maximum number of contact points between two touching shapes.</summary>
    public const int B3_MAX_MANIFOLD_POINTS = 4;

    /// <summary>The number of iterations used for gyroscopic torques. May be overridden at compile time.</summary>
    public const int B3_GYROSCOPIC_ITERATIONS = 1;

    /// <summary>The number of restitution iterations. May be overridden at compile time.</summary>
    public const int B3_RESTITUTION_ITERATIONS = 1;

    /// <summary>The maximum number of convex hull vertices. Fixed for performance.</summary>
    public const int B3_MAX_HULL_VERTICES = 128;

    /// <summary>The maximum number of convex hull faces.</summary>
    public const int B3_MAX_HULL_FACES = 128;

    /// <summary>The maximum number of convex hull edges. Full edges, not half-edges.</summary>
    public const int B3_MAX_HULL_EDGES = 128;

    /// <summary>The relative tolerance used to decide whether two edges are parallel.</summary>
    public const float B3_PARALLEL_EDGE_TOL = 0.005f;

    /// <summary>The maximum number of points in a shape cast proxy.</summary>
    public const int B3_MAX_SHAPE_CAST_POINTS = B3_MAX_HULL_VERTICES;

    /// <summary>The contact recycling angular threshold, stored as cos(angle/2) squared for ten degrees.</summary>
    public const float B3_CONTACT_RECYCLE_ANGULAR_DISTANCE = 0.99240388f;

    /// <summary>The number of bits used to index shapes in a shape pair key.</summary>
    public const int B3_SHAPE_POWER = 22;

    /// <summary>The number of bits used to index child shapes in a shape pair key.</summary>
    public const int B3_CHILD_POWER = 64 - (2 * B3_SHAPE_POWER);

    /// <summary>The maximum number of shapes in a world.</summary>
    public const int B3_MAX_SHAPES = 1 << B3_SHAPE_POWER;

    /// <summary>The maximum number of child shapes in a compound.</summary>
    public const int B3_MAX_CHILD_SHAPES = 1 << B3_CHILD_POWER;

    /// <summary>The material index that designates a hole in a height field.</summary>
    public const byte B3_HEIGHT_FIELD_HOLE = 0xFF;

    /// <summary>The maximum number of materials on a mesh inside a compound.</summary>
    public const int B3_MAX_COMPOUND_MESH_MATERIALS = 4;

    /// <summary>The seed for the djb2 hash used in determinism testing.</summary>
    public const uint B3_HASH_INIT = 5381;

    /// <summary>Pi.</summary>
    public const float B3_PI = 3.14159265359f;

    /// <summary>Multiplier converting degrees to radians.</summary>
    public const float B3_DEG_TO_RAD = 0.01745329251f;

    /// <summary>Multiplier converting radians to degrees.</summary>
    public const float B3_RAD_TO_DEG = 57.2957795131f;

    /// <summary>The minimum scale usable for collision meshes.</summary>
    public const float B3_MIN_SCALE = 0.01f;

    /// <summary>The dynamic tree version, for validating serialized data.</summary>
    public const ulong B3_DYNAMIC_TREE_VERSION = 0x93EDAF889FD30B4AUL;

    /// <summary>The convex hull version, for validating serialized data.</summary>
    public const ulong B3_HULL_VERSION = 0xDA5150191B994C01UL;

    /// <summary>The triangle mesh version, for validating serialized data.</summary>
    public const ulong B3_MESH_VERSION = 0xABD11AB62A6E886DUL;

    /// <summary>The height field version, for validating serialized data.</summary>
    public const ulong B3_HEIGHT_FIELD_VERSION = 0x8B18CBD138A6BC84UL;

    /// <summary>The baked compound version, derived from the tree, mesh and hull versions.</summary>
    public const ulong B3_COMPOUND_VERSION =
        0xB11DCE70FAD5622BUL ^ B3_DYNAMIC_TREE_VERSION ^ B3_MESH_VERSION ^ B3_HULL_VERSION;
}

/// <summary>
/// Box3D constants that scale with the application's length units.
/// </summary>
/// <remarks>
/// <para>
/// In the C headers these are macros that expand to a call to
/// <c>b3GetLengthUnitsPerMeter()</c>, so their value depends on what the
/// application passed to <c>b3SetLengthUnitsPerMeter</c> at startup. They are
/// properties here for the same reason: capturing them in a <c>const</c> would
/// silently freeze the default of one unit per metre.
/// </para>
/// <para>
/// Reading any of these calls into the native library.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// B3.b3SetLengthUnitsPerMeter(100.0f); // centimetres, before any other call
/// float slop = ScaledConstants.B3_LINEAR_SLOP; // 0.5, not 0.005
/// </code>
/// </example>
public static class ScaledConstants
{
    /// <summary>
    /// The bound used to detect bad values. Positions beyond this are treated as errors.
    /// </summary>
    /// <remarks>In single precision this is 100km at the default scale.</remarks>
    public static float B3_HUGE => 1.0e5f * B3.b3GetLengthUnitsPerMeter();

    /// <summary>
    /// A small length used as a collision and constraint tolerance, in metres.
    /// </summary>
    /// <remarks>Chosen to be numerically significant but visually insignificant.</remarks>
    public static float B3_LINEAR_SLOP => 0.005f * B3.b3GetLengthUnitsPerMeter();

    /// <summary>
    /// The minimum length of a capsule. Shorter capsules should be spheres.
    /// </summary>
    public static float B3_MIN_CAPSULE_LENGTH => B3_LINEAR_SLOP;

    /// <summary>
    /// The distance at which shapes are considered overlapped.
    /// </summary>
    public static float B3_OVERLAP_SLOP => 0.1f * B3_LINEAR_SLOP;

    /// <summary>
    /// The distance within which speculative contacts are generated.
    /// </summary>
    public static float B3_SPECULATIVE_DISTANCE => 4.0f * B3_LINEAR_SLOP;

    /// <summary>
    /// The rest offset applied to mesh contacts to reduce ghost collisions.
    /// </summary>
    public static float B3_MESH_REST_OFFSET => 1.0f * B3_LINEAR_SLOP;

    /// <summary>
    /// The default distance at which contact points are recycled.
    /// </summary>
    public static float B3_CONTACT_RECYCLE_DISTANCE => 10.0f * B3_LINEAR_SLOP;

    /// <summary>
    /// The upper bound on the margin used to fatten bounding boxes in the dynamic tree.
    /// </summary>
    public static float B3_MAX_AABB_MARGIN => 0.05f * B3.b3GetLengthUnitsPerMeter();
}
