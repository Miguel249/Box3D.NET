# Collision filtering

Two shapes collide when each one's `Categories` appears in the other's
`CollidesWith`. The test is symmetric, so making one side ignore the other is
enough to disable the pair.

```csharp
[Flags]
enum Layers : ulong
{
    World  = 1 << 0,
    Player = 1 << 1,
    Enemy  = 1 << 2,
    Debris = 1 << 3,
}

var playerFilter = new CollisionFilter
{
    Categories = (ulong)Layers.Player,
    CollidesWith = (ulong)(Layers.World | Layers.Enemy),
};

body.AddCapsule(capsule, ShapeDefinition.Default with { Filter = playerFilter });
```

A shape usually belongs to one category and collides with several.
[`CollisionFilter.Default`](../api/Box3D.CollisionFilter.yml) belongs to every
category and collides with everything, which is what a shape gets if you say
nothing.

The same filter applies to [queries](queries.md), not only to shape-versus-shape
collision, so debris that ignores the player is also invisible to a ray cast
filtered to the player's categories.

## Groups, for pairs and ragdolls

A non-zero `Group` overrides the masks entirely:

| Group | Effect |
| ---: | --- |
| negative | Shapes sharing it **never** collide |
| positive | Shapes sharing it **always** collide |
| zero | No effect; the masks decide |

```csharp
// Every limb of one ragdoll ignores every other limb of the same ragdoll.
var filter = CollisionFilter.Default with { Group = -ragdollIndex };
```

This is the cheap way to say "these objects never collide with each other"
without spending a category bit on them, and it scales: each ragdoll gets its
own negative number.

## One pair, without spending anything

For a single pair that must not collide — a projectile and the turret that fired
it — a filter joint is more direct than either mechanism:

```csharp
world.CreateFilterJoint(FilterJointDefinition.Between(turret, shell));
```

As a side effect the two bodies stay in the same simulation island, so they
sleep and wake together.

## Changing a filter later

```csharp
shape.SetFilter(newFilter);                          // recomputes contacts
shape.SetFilter(newFilter, recomputeContacts: false);
```

Recomputing is what makes a change take effect on contacts that already exist.
Skip it only when you are about to move the shape anyway.

## Choosing between the three

| You want | Use |
| --- | --- |
| Layers: players, enemies, world, debris | `Categories` and `CollidesWith` |
| A set of objects that ignore each other | Negative `Group` |
| Exactly two bodies that ignore each other | [`FilterJoint`](joints.md) |
| A shape that detects but never pushes | [Sensor](shapes.md#sensors) |
