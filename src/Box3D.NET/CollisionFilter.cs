// SPDX-License-Identifier: MIT

using System;
using Box3D.Native;

namespace Box3D;

/// <summary>
/// Controls which shapes are allowed to collide with each other.
/// </summary>
/// <remarks>
/// <para>
/// Two shapes collide when each one's <see cref="Categories"/> appears in the
/// other's <see cref="CollidesWith"/> mask. The test is symmetric: both
/// directions must agree, so making one shape ignore another is enough to
/// disable the pair.
/// </para>
/// <para>
/// <see cref="Group"/> overrides the masks entirely when it is non-zero, which
/// is the cheap way to express "these objects never collide with each other"
/// without spending a category bit on them.
/// </para>
/// <para>
/// The same filter applies to queries such as ray casts, not only to
/// shape-versus-shape collision.
/// </para>
/// </remarks>
/// <example>
/// Giving a player its own category and letting it collide only with the world
/// and with enemies:
/// <code>
/// [Flags]
/// enum Layers : ulong
/// {
///     World  = 1 &lt;&lt; 0,
///     Player = 1 &lt;&lt; 1,
///     Enemy  = 1 &lt;&lt; 2,
///     Debris = 1 &lt;&lt; 3,
/// }
///
/// var filter = new CollisionFilter
/// {
///     Categories  = (ulong)Layers.Player,
///     CollidesWith = (ulong)(Layers.World | Layers.Enemy),
/// };
/// </code>
/// Stopping a ragdoll from colliding with itself, without touching any masks:
/// <code>
/// var filter = CollisionFilter.Default with { Group = -ragdollIndex };
/// </code>
/// </example>
public readonly record struct CollisionFilter
{
    /// <summary>Initializes a new instance of the <see cref="CollisionFilter"/> struct.</summary>
    /// <param name="categories">The categories this shape belongs to.</param>
    /// <param name="collidesWith">The categories this shape collides with.</param>
    /// <param name="group">The collision group, or zero for none.</param>
    public CollisionFilter(ulong categories, ulong collidesWith, int group = 0)
    {
        Categories = categories;
        CollidesWith = collidesWith;
        Group = group;
    }

    /// <summary>
    /// Gets the categories this shape belongs to. Usually a single bit.
    /// </summary>
    public ulong Categories { get; init; }

    /// <summary>
    /// Gets the categories this shape is willing to collide with.
    /// </summary>
    public ulong CollidesWith { get; init; }

    /// <summary>
    /// Gets the collision group. Shapes sharing a negative group never collide;
    /// shapes sharing a positive group always collide. Zero has no effect.
    /// </summary>
    /// <remarks>A non-zero group wins over <see cref="CollidesWith"/>.</remarks>
    public int Group { get; init; }

    /// <summary>
    /// Gets a filter that belongs to every category and collides with everything.
    /// </summary>
    public static CollisionFilter Default => new(ulong.MaxValue, ulong.MaxValue);

    internal b3Filter ToNative() => new()
    {
        categoryBits = Categories,
        maskBits = CollidesWith,
        groupIndex = Group,
    };

    internal static CollisionFilter FromNative(in b3Filter filter) =>
        new(filter.categoryBits, filter.maskBits, filter.groupIndex);
}

/// <summary>
/// Restricts which shapes a query such as a ray cast is allowed to hit.
/// </summary>
/// <remarks>
/// The same category and mask rules as <see cref="CollisionFilter"/> apply, with
/// the query standing in for one of the two shapes.
/// </remarks>
/// <example>
/// A bullet that hits the world and enemies but passes through debris:
/// <code>
/// var filter = new QueryFilter
/// {
///     Categories  = (ulong)Layers.Bullet,
///     CollidesWith = (ulong)(Layers.World | Layers.Enemy),
/// };
///
/// if (world.CastRayClosest(muzzle, direction * range, filter) is { Hit: true } hit)
/// {
///     ApplyDamage(hit.Shape, hit.Point);
/// }
/// </code>
/// </example>
public readonly record struct QueryFilter
{
    /// <summary>Initializes a new instance of the <see cref="QueryFilter"/> struct.</summary>
    /// <param name="categories">The categories this query belongs to.</param>
    /// <param name="collidesWith">The categories this query can hit.</param>
    public QueryFilter(ulong categories, ulong collidesWith)
    {
        Categories = categories;
        CollidesWith = collidesWith;
    }

    /// <summary>Gets the categories this query belongs to. Usually a single bit.</summary>
    public ulong Categories { get; init; }

    /// <summary>Gets the shape categories this query can hit.</summary>
    public ulong CollidesWith { get; init; }

    /// <summary>Gets a filter that can hit everything.</summary>
    public static QueryFilter Default => new(ulong.MaxValue, ulong.MaxValue);

    internal unsafe b3QueryFilter ToNative() => new()
    {
        categoryBits = Categories,
        maskBits = CollidesWith,
        id = 0,
        name = null,
    };
}
