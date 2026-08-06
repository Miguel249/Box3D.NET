// SPDX-License-Identifier: MIT
// Mirror of the world types in include/box3d/types.h.

using System.Numerics;
using System.Runtime.InteropServices;

namespace Box3D.Native;

/*
 * Callback fields are declared as `delegate* unmanaged[Cdecl]<...>` rather than
 * as managed delegate types. A managed delegate field would make the structure
 * non-blittable, would require the caller to keep the delegate alive by hand to
 * avoid a collected thunk, and would need a reverse P/Invoke stub that NativeAOT
 * cannot always generate. Function pointers cost nothing and are AOT-safe: a
 * callback is a static method marked [UnmanagedCallersOnly], and per-instance
 * state travels through the context pointer that every Box3D callback carries.
 */

/// <summary>
/// Optional initial capacities used to avoid run-time allocations. Mirror of <c>b3Capacity</c>.
/// </summary>
/// <remarks>
/// Obtain realistic numbers for an existing simulation from <c>b3World_GetMaxCapacity</c>
/// and feed them back into <see cref="b3WorldDef"/> on the next run.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public struct b3Capacity
{
    /// <summary>The expected number of static shapes.</summary>
    public int staticShapeCount;

    /// <summary>The expected number of dynamic and kinematic shapes.</summary>
    public int dynamicShapeCount;

    /// <summary>The expected number of static bodies.</summary>
    public int staticBodyCount;

    /// <summary>The expected number of dynamic and kinematic bodies.</summary>
    public int dynamicBodyCount;

    /// <summary>The expected number of contacts.</summary>
    public int contactCount;
}

/// <summary>
/// The definition used to create a simulation world. Mirror of <c>b3WorldDef</c>.
/// </summary>
/// <remarks>
/// Always start from <c>B3.b3DefaultWorldDef()</c>. The structure carries an
/// <see cref="internalValue"/> that Box3D uses to reject definitions that were
/// not initialized, so a zero-initialized instance will be refused.
/// </remarks>
/// <example>
/// <code>
/// b3WorldDef def = B3.b3DefaultWorldDef();
/// def.gravity = new Vector3(0.0f, -9.8f, 0.0f);
/// b3WorldId world = B3.b3CreateWorld(in def);
/// </code>
/// </example>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct b3WorldDef
{
    /// <summary>The gravity vector. Box3D defines no up axis of its own.</summary>
    public Vector3 gravity;

    /// <summary>The speed above which collisions bounce, usually in metres per second.</summary>
    public float restitutionThreshold;

    /// <summary>The speed above which a collision can raise a hit event, usually in metres per second.</summary>
    public float hitEventThreshold;

    /// <summary>The contact stiffness in cycles per second.</summary>
    public float contactHertz;

    /// <summary>The contact damping ratio. Non-dimensional.</summary>
    public float contactDampingRatio;

    /// <summary>The cap on the overlap resolution speed, usually in metres per second.</summary>
    public float contactSpeed;

    /// <summary>The maximum linear speed, usually in metres per second.</summary>
    public float maximumLinearSpeed;

    /// <summary>
    /// An optional callback mixing the friction of two surfaces. The default is
    /// <c>sqrt(frictionA * frictionB)</c>.
    /// </summary>
    /// <remarks>
    /// Signature: <c>float (float frictionA, ulong userMaterialIdA, float frictionB, ulong userMaterialIdB)</c>.
    /// Called from worker threads; it must be thread-safe and must not touch world state.
    /// </remarks>
    public delegate* unmanaged[Cdecl]<float, ulong, float, ulong, float> frictionCallback;

    /// <summary>
    /// An optional callback mixing the restitution of two surfaces. The default is
    /// <c>max(restitutionA, restitutionB)</c>.
    /// </summary>
    /// <remarks>
    /// Signature: <c>float (float restitutionA, ulong userMaterialIdA, float restitutionB, ulong userMaterialIdB)</c>.
    /// Called from worker threads; it must be thread-safe and must not touch world state.
    /// </remarks>
    public delegate* unmanaged[Cdecl]<float, ulong, float, ulong, float> restitutionCallback;

    /// <summary>Whether bodies may sleep.</summary>
    public NativeBool enableSleep;

    /// <summary>Whether continuous collision is enabled.</summary>
    public NativeBool enableContinuous;

    /// <summary>
    /// The number of workers to use, clamped to one through <see cref="Constants.B3_MAX_WORKERS"/>.
    /// </summary>
    /// <remarks>
    /// A value above one enables multithreading. If <see cref="enqueueTask"/> and
    /// <see cref="finishTask"/> are supplied Box3D drives the application's task
    /// system; otherwise it starts its own threads.
    /// </remarks>
    public uint workerCount;

    /// <summary>
    /// The callback that spawns a task.
    /// </summary>
    /// <remarks>
    /// Signature: <c>void* (delegate* unmanaged[Cdecl]&lt;void*, void&gt; task, void* taskContext, void* userContext, byte* taskName)</c>.
    /// Returning null tells Box3D the work ran serially and <see cref="finishTask"/> will not be called for it.
    /// </remarks>
    public delegate* unmanaged[Cdecl]<delegate* unmanaged[Cdecl]<void*, void>, void*, void*, byte*, void*> enqueueTask;

    /// <summary>
    /// The callback that waits for a task to finish. It must block until the task has completed.
    /// </summary>
    /// <remarks>
    /// Signature: <c>void (void* userTask, void* userContext)</c>.
    /// The world step holds its stack across every fork and join, so do not call
    /// <c>b3World_Step</c> from inside a job that cannot park its thread.
    /// </remarks>
    public delegate* unmanaged[Cdecl]<void*, void*, void> finishTask;

    /// <summary>The context passed to <see cref="enqueueTask"/> and <see cref="finishTask"/>.</summary>
    public void* userTaskContext;

    /// <summary>Application data associated with the world.</summary>
    public void* userData;

    /// <summary>
    /// The callback that creates a debug draw shape, invoked the first time a shape is drawn.
    /// </summary>
    /// <remarks>Signature: <c>void* (b3DebugShape* debugShape, void* userContext)</c>.</remarks>
    public delegate* unmanaged[Cdecl]<b3DebugShape*, void*, void*> createDebugShape;

    /// <summary>
    /// The callback that destroys a debug draw shape when the shape is modified or destroyed.
    /// </summary>
    /// <remarks>Signature: <c>void (void* userShape, void* userContext)</c>.</remarks>
    public delegate* unmanaged[Cdecl]<void*, void*, void> destroyDebugShape;

    /// <summary>The context passed to the debug shape callbacks.</summary>
    public void* userDebugShapeContext;

    /// <summary>Optional initial capacities.</summary>
    public b3Capacity capacity;

    /// <summary>Used internally to detect a valid definition. Do not set.</summary>
    public int internalValue;
}

/// <summary>
/// Timing breakdown of a world step, in milliseconds. Mirror of <c>b3Profile</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct b3Profile
{
    /// <summary>Total time for the step.</summary>
    public float step;

    /// <summary>Time spent updating broad-phase pairs.</summary>
    public float pairs;

    /// <summary>Time spent in narrow-phase collision.</summary>
    public float collide;

    /// <summary>Time spent in the solver.</summary>
    public float solve;

    /// <summary>Time spent setting up solver sets.</summary>
    public float solverSetup;

    /// <summary>Time spent on constraints.</summary>
    public float constraints;

    /// <summary>Time spent preparing constraints.</summary>
    public float prepareConstraints;

    /// <summary>Time spent integrating velocities.</summary>
    public float integrateVelocities;

    /// <summary>Time spent warm starting.</summary>
    public float warmStart;

    /// <summary>Time spent solving impulses.</summary>
    public float solveImpulses;

    /// <summary>Time spent integrating positions.</summary>
    public float integratePositions;

    /// <summary>Time spent relaxing impulses.</summary>
    public float relaxImpulses;

    /// <summary>Time spent applying restitution.</summary>
    public float applyRestitution;

    /// <summary>Time spent storing impulses.</summary>
    public float storeImpulses;

    /// <summary>Time spent splitting islands.</summary>
    public float splitIslands;

    /// <summary>Time spent updating transforms.</summary>
    public float transforms;

    /// <summary>Time spent computing sensor hits.</summary>
    public float sensorHits;

    /// <summary>Time spent gathering joint events.</summary>
    public float jointEvents;

    /// <summary>Time spent gathering hit events.</summary>
    public float hitEvents;

    /// <summary>Time spent refitting the broad-phase tree.</summary>
    public float refit;

    /// <summary>Time spent on bullet bodies.</summary>
    public float bullets;

    /// <summary>Time spent putting islands to sleep.</summary>
    public float sleepIslands;

    /// <summary>Time spent processing sensors.</summary>
    public float sensors;
}

/// <summary>
/// Counters describing the size of the simulation. Mirror of <c>b3Counters</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct b3Counters
{
    /// <summary>The number of bodies.</summary>
    public int bodyCount;

    /// <summary>The number of shapes.</summary>
    public int shapeCount;

    /// <summary>The number of contacts.</summary>
    public int contactCount;

    /// <summary>The number of joints.</summary>
    public int jointCount;

    /// <summary>The number of simulation islands.</summary>
    public int islandCount;

    /// <summary>The bytes of arena currently in use.</summary>
    public int stackUsed;

    /// <summary>The arena capacity in bytes.</summary>
    public int arenaCapacity;

    /// <summary>The height of the static broad-phase tree.</summary>
    public int staticTreeHeight;

    /// <summary>The height of the dynamic broad-phase tree.</summary>
    public int treeHeight;

    /// <summary>The number of separating axis tests performed.</summary>
    public int satCallCount;

    /// <summary>The number of separating axis cache hits.</summary>
    public int satCacheHitCount;

    /// <summary>The total bytes allocated.</summary>
    public int byteCount;

    /// <summary>The number of tasks enqueued during the step.</summary>
    public int taskCount;

    /// <summary>The number of constraints in each graph colour.</summary>
    /// <remarks>Fixed-size array of <see cref="Constants.B3_GRAPH_COLOR_COUNT"/> entries.</remarks>
    public fixed int colorCounts[Constants.B3_GRAPH_COLOR_COUNT];

    /// <summary>Histogram of contact points per shape pair.</summary>
    /// <remarks>Fixed-size array of <see cref="Constants.B3_CONTACT_MANIFOLD_COUNT_BUCKETS"/> entries.</remarks>
    public fixed int manifoldCounts[Constants.B3_CONTACT_MANIFOLD_COUNT_BUCKETS];

    /// <summary>The number of contacts touched by the collide pass.</summary>
    public int awakeContactCount;

    /// <summary>The number of contacts recycled in the most recent step.</summary>
    public int recycledContactCount;

    /// <summary>The maximum number of distance iterations used by time of impact.</summary>
    public int distanceIterations;

    /// <summary>The maximum number of push-back iterations used by time of impact.</summary>
    public int pushBackIterations;

    /// <summary>The maximum number of root-finding iterations used by time of impact.</summary>
    public int rootIterations;
}

/// <summary>
/// Configuration for a radial explosion. Mirror of <c>b3ExplosionDef</c>.
/// </summary>
/// <remarks>Explosions apply only to spheres, capsules and hulls.</remarks>
[StructLayout(LayoutKind.Sequential)]
public struct b3ExplosionDef
{
    /// <summary>The mask bits used to filter affected shapes.</summary>
    public ulong maskBits;

    /// <summary>The centre of the explosion in world space.</summary>
    public Vector3 position;

    /// <summary>The radius of the explosion.</summary>
    public float radius;

    /// <summary>The distance beyond the radius over which the impulse falls to zero.</summary>
    public float falloff;

    /// <summary>The impulse per unit of area facing the explosion. Negative values implode.</summary>
    public float impulsePerArea;
}
