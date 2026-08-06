// SPDX-License-Identifier: MIT
// Mirror of b3Version in include/box3d/base.h and the recording types in include/box3d/box3d.h.

using System.Numerics;
using System.Runtime.InteropServices;

namespace Box3D.Native;

/// <summary>
/// The Box3D version, following semantic versioning. Mirror of <c>b3Version</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct b3Version
{
    /// <summary>The major version, incremented for significant changes.</summary>
    public int major;

    /// <summary>The minor version, incremented for incremental changes.</summary>
    public int minor;

    /// <summary>The revision, incremented for bug fixes.</summary>
    public int revision;
}

/// <summary>
/// A summary of a recording, read once when it is opened. Mirror of <c>b3RecPlayerInfo</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct b3RecPlayerInfo
{
    /// <summary>The total number of recorded steps.</summary>
    public int frameCount;

    /// <summary>The worker count requested for the replay world.</summary>
    public int workerCount;

    /// <summary>The time step of the recorded steps.</summary>
    public float timeStep;

    /// <summary>The sub-step count used while recording.</summary>
    public int subStepCount;

    /// <summary>The length units per metre in effect while recording.</summary>
    public float lengthScale;

    /// <summary>
    /// The world bounds accumulated over the recording, or a zero-extent box when unavailable.
    /// </summary>
    public b3AABB bounds;
}

/// <summary>
/// A spatial query captured during a replayed frame. Mirror of <c>b3RecQueryInfo</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct b3RecQueryInfo
{
    /// <summary>The kind of query.</summary>
    public b3RecQueryType type;

    /// <summary>The filter the query used.</summary>
    public b3QueryFilter filter;

    /// <summary>The world-space bounds of the query, swept for casts.</summary>
    public b3AABB aabb;

    /// <summary>The query origin, zero for a box overlap.</summary>
    public Vector3 origin;

    /// <summary>The ray or cast translation.</summary>
    public Vector3 translation;

    /// <summary>The number of recorded results.</summary>
    public int hitCount;

    /// <summary>The identity key, a hash of the id and name pair. Zero when untagged.</summary>
    public ulong key;

    /// <summary>The query id, or zero for none.</summary>
    public ulong id;

    /// <summary>The query label as a null-terminated UTF-8 string, or null for none.</summary>
    public byte* name;
}

/// <summary>
/// One result of a recorded spatial query. Mirror of <c>b3RecQueryHit</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct b3RecQueryHit
{
    /// <summary>The shape that was hit.</summary>
    public b3ShapeId shape;

    /// <summary>The world-space hit point.</summary>
    public Vector3 point;

    /// <summary>The world-space surface normal at the hit point.</summary>
    public Vector3 normal;

    /// <summary>The fraction along the query at which the hit occurred.</summary>
    public float fraction;
}
