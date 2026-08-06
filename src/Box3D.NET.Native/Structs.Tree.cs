// SPDX-License-Identifier: MIT
// Mirror of the dynamic tree types in include/box3d/types.h.

using System.Numerics;
using System.Runtime.InteropServices;

namespace Box3D.Native;

/// <summary>
/// The child indices of an internal tree node. Mirror of <c>b3TreeNodeChildren</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct b3TreeNodeChildren
{
    /// <summary>The index of the first child node.</summary>
    public int child1;

    /// <summary>The index of the second child node.</summary>
    public int child2;
}

/// <summary>
/// A node in the dynamic tree. Mirror of <c>b3TreeNode</c>.
/// </summary>
/// <remarks>
/// This is internal data, exposed for performance. It contains two unions: the
/// children of an internal node overlap the user data of a leaf, and the parent
/// index of an allocated node overlaps the free-list link of a free node.
/// Which member is live follows from <see cref="flags"/>.
/// </remarks>
[StructLayout(LayoutKind.Explicit)]
public struct b3TreeNode
{
    /// <summary>The node bounding box.</summary>
    [FieldOffset(0)]
    public b3AABB aabb;

    /// <summary>The category bits used for collision filtering.</summary>
    [FieldOffset(24)]
    public ulong categoryBits;

    /// <summary>The child indices. Valid when the node is not a leaf.</summary>
    [FieldOffset(32)]
    public b3TreeNodeChildren children;

    /// <summary>The user data. Valid when the node is a leaf.</summary>
    [FieldOffset(32)]
    public ulong userData;

    /// <summary>The parent node index. Valid when the node is allocated.</summary>
    [FieldOffset(40)]
    public int parent;

    /// <summary>The next free node index. Valid when the node is on the free list.</summary>
    [FieldOffset(40)]
    public int next;

    /// <summary>The height of the node. Leaves have height zero.</summary>
    [FieldOffset(44)]
    public ushort height;

    /// <summary>The node flags.</summary>
    [FieldOffset(46)]
    public b3TreeNodeFlagsStorage flags;
}

/// <summary>
/// The storage form of <see cref="b3TreeNodeFlags"/> inside a <see cref="b3TreeNode"/>,
/// which the C header declares as a <c>uint16_t</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential, Size = 2)]
public struct b3TreeNodeFlagsStorage
{
    private ushort _value;

    /// <summary>Gets or sets the flags.</summary>
    public b3TreeNodeFlags Value
    {
        readonly get => (b3TreeNodeFlags)_value;
        set => _value = (ushort)value;
    }
}

/// <summary>
/// A dynamic bounding volume hierarchy over axis-aligned boxes.
/// Mirror of <c>b3DynamicTree</c>.
/// </summary>
/// <remarks>
/// <para>
/// Box3D uses this internally for the broad phase, and exposes it because it is
/// useful for organizing other spatial game data. It is private data placed in
/// the public header for performance.
/// </para>
/// <para>
/// A tree returned by <c>b3DynamicTree_Create</c> owns heap memory and must be
/// released with <c>b3DynamicTree_Destroy</c>.
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct b3DynamicTree
{
    /// <summary>
    /// The format version. Always the first field, so a serialized tree can be validated.
    /// Must equal <see cref="Constants.B3_DYNAMIC_TREE_VERSION"/>.
    /// </summary>
    public ulong version;

    /// <summary>The node pool.</summary>
    public b3TreeNode* nodes;

    /// <summary>The index of the root node.</summary>
    public int root;

    /// <summary>The number of nodes in use.</summary>
    public int nodeCount;

    /// <summary>The number of nodes allocated.</summary>
    public int nodeCapacity;

    /// <summary>The number of proxies created.</summary>
    public int proxyCount;

    /// <summary>The head of the node free list.</summary>
    public int freeList;

    /// <summary>Scratch storage: leaf indices used during a rebuild.</summary>
    public int* leafIndices;

    /// <summary>Scratch storage: leaf bounding boxes used during a rebuild.</summary>
    public b3AABB* leafBoxes;

    /// <summary>Scratch storage: leaf box centres used during a rebuild.</summary>
    public Vector3* leafCenters;

    /// <summary>Scratch storage: sort bins used during a rebuild.</summary>
    public int* binIndices;

    /// <summary>The capacity of the rebuild scratch storage.</summary>
    public int rebuildCapacity;
}

/// <summary>
/// Traversal counters returned by dynamic tree queries. Mirror of <c>b3TreeStats</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct b3TreeStats
{
    /// <summary>The number of internal nodes visited.</summary>
    public int nodeVisits;

    /// <summary>The number of leaf nodes visited.</summary>
    public int leafVisits;
}
