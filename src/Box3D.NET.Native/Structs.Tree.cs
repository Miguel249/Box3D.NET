// SPDX-License-Identifier: MIT
// Mirror of the dynamic tree types in include/box3d/types.h.

using System.Numerics;
using System.Runtime.InteropServices;

namespace Box3D.Native;

/// <summary>
/// A node in the dynamic tree. Mirror of <c>b3TreeNode</c>.
/// </summary>
/// <remarks>
/// <para>
/// This is internal data, exposed for performance. Siblings are stored as a
/// pair at an even index so that two nodes share a 64-byte cache line; the root
/// is at index zero and index one is always empty.
/// </para>
/// <para>
/// <see cref="flagIndex"/> packs a leaf bit, a moved bit and an index: the
/// first node of the child pair for an internal node, or the proxy id for a
/// leaf. The last word is a union of <see cref="height"/> and
/// <see cref="shapeIndex"/>, and <see cref="IsLeaf"/> says which is live.
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Explicit)]
public struct b3TreeNode
{
    /// <summary>The node bounding box.</summary>
    [FieldOffset(0)]
    public b3AABB aabb;

    /// <summary>
    /// Bit 31 is set for a leaf, bit 30 for a moved node, and bits 0 to 29 hold
    /// the index of the child pair or, for a leaf, the proxy id.
    /// </summary>
    [FieldOffset(24)]
    public uint flagIndex;

    /// <summary>The height of an internal node. A leaf has height zero.</summary>
    [FieldOffset(28)]
    public int height;

    /// <summary>The shape index of a leaf, truncated from the proxy user data.</summary>
    [FieldOffset(28)]
    public int shapeIndex;

    /// <summary>Gets a value indicating whether this node is a leaf.</summary>
    public readonly bool IsLeaf => (flagIndex & 0x80000000u) != 0;

    /// <summary>Gets a value indicating whether this node is flagged as moved.</summary>
    public readonly bool IsMoved => (flagIndex & 0x40000000u) != 0;

    /// <summary>
    /// Gets the index held in the low 30 bits: the first node of the child pair,
    /// or the proxy id when <see cref="IsLeaf"/> is true.
    /// </summary>
    public readonly int Index => (int)(flagIndex & 0x3FFFFFFFu);
}

/// <summary>
/// The per-proxy data of a dynamic tree, kept apart from the nodes as cold data.
/// Mirror of <c>b3TreeProxy</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct b3TreeProxy
{
    /// <summary>The user data. An integer rather than a pointer, because Box3D uses it as a shape index.</summary>
    public ulong userData;

    /// <summary>The category bits used for collision filtering.</summary>
    public ulong categoryBits;

    /// <summary>The leaf node of this proxy, or <see cref="Constants.B3_NULL_INDEX"/> for a free proxy.</summary>
    public int node;

    /// <summary>The next free proxy.</summary>
    public int next;
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

    /// <summary>
    /// The nodes. The root is at index zero and index one is empty; otherwise
    /// siblings are paired at even indices, with holes for free pairs.
    /// </summary>
    public b3TreeNode* nodes;

    /// <summary>The parent index of each node. The free list is interleaved with it.</summary>
    public int* parents;

    /// <summary>The proxies, indexed by proxy id.</summary>
    public b3TreeProxy* proxies;

    /// <summary>One past the highest allocated node index.</summary>
    public int nodeEnd;

    /// <summary>The number of nodes allocated.</summary>
    public int nodeCapacity;

    /// <summary>The head of the list of free node pairs below <see cref="nodeEnd"/>.</summary>
    public int pairFreeList;

    /// <summary>The number of proxies created.</summary>
    public int proxyCount;

    /// <summary>The number of proxies allocated.</summary>
    public int proxyCapacity;

    /// <summary>The head of the proxy free list.</summary>
    public int proxyFreeList;

    /// <summary>Scratch storage: the node array swapped in during a rebuild.</summary>
    public b3TreeNode* swapNodes;

    /// <summary>Scratch storage: leaf indices used during a rebuild.</summary>
    public int* leafIndices;

    /// <summary>Scratch storage: the leaves of a rebuild, each a proxy or a retained subtree.</summary>
    public b3TreeNode* leafNodes;

    /// <summary>Scratch storage: leaf bounding boxes used during a rebuild.</summary>
    public b3AABB* leafBoxes;

    /// <summary>Scratch storage: leaf box centres used during a rebuild.</summary>
    public Vector3* leafCenters;

    /// <summary>Scratch storage: sort bins used during a rebuild.</summary>
    public int* binIndices;

    /// <summary>The capacity of the rebuild scratch storage.</summary>
    public int rebuildCapacity;

    /// <summary>
    /// Whether the nodes are in depth-first order, children after their parent.
    /// </summary>
    /// <remarks>Set by a rebuild and disturbed by creating proxies.</remarks>
    public NativeBool dfsOrdered;
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
