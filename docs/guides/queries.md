# Queries

Asking the world what is there, without stepping it. Every query on this page
allocates nothing, including the callback forms.

| Question | Call |
| --- | --- |
| What does this ray hit first? | `world.RaycastClosest` |
| What does this ray pass through? | `world.Raycast` with a callback |
| What is inside this box? | `world.OverlapBox`, `world.OverlapBounds` |
| Does this ray hit *that* shape? | `shape.Raycast` |
| How far can this capsule move? | `world.CastCapsule` |
| What is this capsule touching? | `world.CollideCapsule` — see [Characters](characters.md) |

## The closest hit

```csharp
RaycastHit hit = world.RaycastClosest(muzzle, aim * 100.0f);

if (hit.Hit)
{
    ulong entity = hit.Shape.Body.UserData;
    var zone = (HitZone)hit.Shape.UserData;

    Damage(entity, zone == HitZone.Head ? 100 : 25);
    SpawnDecal(hit.Point, hit.Normal);
}
```

The second argument is a vector, not a direction: its length is the range.
`Fraction` is how far along it the hit was, from zero to one, so multiply by that
vector's length to get a distance.

[`RaycastHit`](../api/Box3D.RaycastHit.yml) also carries `TriangleIndex` and
`ChildIndex` for hits against meshes and compounds, and `UserMaterialId` for
whatever you tagged the surface with.

## Ray casts that need a decision per hit

Ignoring the shooter, collecting every hit, stopping early — implement
[`IRaycastCallback`](../api/Box3D.IRaycastCallback.yml) on a **struct**:

```csharp
struct IgnoreSelf : IRaycastCallback
{
    public Body Self;
    public RaycastHit Nearest;

    public RaycastAction OnHit(in RaycastHit hit)
    {
        if (hit.Shape.Body == Self)
        {
            return RaycastAction.Ignore;
        }

        Nearest = hit;
        return RaycastAction.ClipTo(hit.Fraction);   // keep only closer hits
    }
}

var callback = new IgnoreSelf { Self = player };
world.Raycast(muzzle, aim * 100.0f, ref callback);
```

Shapes arrive in no particular order. What you return decides what happens next:

| [`RaycastAction`](../api/Box3D.RaycastAction.yml) | Effect |
| --- | --- |
| `Ignore` | Pretend the shape is not there and carry on |
| `ClipTo(hit.Fraction)` | Shorten the ray to end here; only closer shapes are reported afterwards |
| `Continue` | Record it and carry on, near and far |
| `Stop` | End the cast now, keeping what was gathered |

Returning `ClipTo` from every hit is a closest-hit search, which is what
`RaycastClosest` does for you.

A struct rather than a delegate, because the query is generic over the callback
type: the JIT specialises it and inlines your `OnHit` into the dispatcher, so
nothing is allocated and nothing has to be kept alive across the native
transition. See
[the native layer](../concepts/native-layer.md#why-callbacks-are-structs).

The world is locked while a query runs. Collect what you need and create or
destroy bodies after it returns.

## Overlaps

```csharp
struct CountBodies : IOverlapCallback
{
    public int Count;

    public bool OnOverlap(Shape shape)
    {
        Count++;
        return true;   // false stops the search
    }
}

var callback = new CountBodies();
world.OverlapBox(explosionCentre, new Vector3(5.0f), ref callback);
```

This is a broad-phase query: it tests bounding boxes, not geometry, so it can
report a shape whose box overlaps and whose geometry does not. Narrow it
yourself when you need exactness — a distance test against
`shape.ClosestPointTo` is usually enough.

`OverlapBounds` takes a [`BoundingBox`](../api/Box3D.BoundingBox.yml) directly, which
is the form to use when you already have one from `body.Bounds` or
`shape.Bounds`.

## Filtering a query

Every query takes an optional [`QueryFilter`](../api/Box3D.QueryFilter.yml), which
works exactly like a [collision filter](collision-filtering.md): the query sees a
shape when the categories agree in both directions.

```csharp
var visibleToBullets = new QueryFilter(
    categories: (ulong)Layers.Bullet,
    collidesWith: (ulong)(Layers.World | Layers.Enemy));

world.RaycastClosest(muzzle, aim * 100.0f, visibleToBullets);
```

Passing `null`, which is the default, tests against everything.

## Casting against one shape

```csharp
RaycastHit hit = shape.Raycast(origin, direction * 10.0f);
```

No broad phase, no filter — just this shape. Useful when you already know what
you are testing against, such as re-checking the shape a previous query
returned.

## Explosions

Not a query, but it belongs with them: `Explode` applies a radial impulse to
everything within reach, scaled by how much of each shape faces the blast.

```csharp
world.Explode(centre, radius: 5.0f, impulsePerArea: 300.0f, falloff: 2.0f);
```

Only spheres, capsules and hulls respond. A negative `impulsePerArea` implodes.
