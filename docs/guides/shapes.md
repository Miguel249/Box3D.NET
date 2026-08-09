# Shapes

A [`Shape`](../api/Box3D.Shape.yml) is collision geometry attached to a body. A body
can carry as many as it needs, and each one has its own material, filter and
events.

```csharp
Body body = world.CreateDynamicBody(spawn);

body.AddSphere(new Sphere(0.5f));
body.AddBox(Box.Cube(0.5f));
body.AddCapsule(Capsule.Upright(height: 1.8f, radius: 0.3f));
```

## The geometry kinds

| Geometry | Built with | Copied on attach | Body types | Dispose |
| --- | --- | --- | --- | --- |
| [`Sphere`](../api/Box3D.Sphere.yml) | value | yes | any | — |
| [`Capsule`](../api/Box3D.Capsule.yml) | value | yes | any | — |
| [`Box`](../api/Box3D.Box.yml) | value | yes | any | — |
| [`ConvexHull`](../api/Box3D.ConvexHull.yml) | `FromPoints`, `Cylinder`, `Cone`, `Rock` | yes, interned in the world | any | any time |
| [`CollisionMesh`](../api/Box3D.CollisionMesh.yml) | `FromTriangles`, `Grid`, `Wave` | **no, borrowed** | static only | **after the world** |
| [`HeightField`](../api/Box3D.HeightField.yml) | `FromHeights`, `Grid`, `Wave` | **no, borrowed** | static only | **after the world** |
| [`CompoundGeometry`](../api/Box3D.CompoundGeometry.yml) | `CompoundBuilder` | **no, borrowed** | static only | **after the world** |

The first four are the ones to reach for on anything that moves. The last three
are covered in [Terrain and meshes](terrain.md), and their lifetime rules in
[Memory and ownership](../concepts/ownership.md).

## Primitives

The three value types have constructors and a few factories for the shapes
people actually build:

```csharp
new Sphere(0.5f);                             // centred on the body origin
new Sphere(offset, 0.5f);

Box.Cube(0.5f);                               // half-extent, so a 1m cube
Box.FromSize(new Vector3(2.0f, 0.1f, 2.0f));  // full size instead
new Box(halfExtents, centre);

Capsule.Upright(height: 1.8f, radius: 0.3f);  // a character
new Capsule(start, end, radius);              // any orientation
```

`Box` is described by half-extents, which is a common source of a level that
comes out twice the intended size. `FromSize` takes the measurement you would
read off a model.

## Convex hulls

A hull is how a moving body gets a shape that is not a primitive: meshes and
height fields are static only, so a rock, a wheel or a crate with a chamfered
edge is a hull.

```csharp
using ConvexHull rock = ConvexHull.Rock(0.4f);
using ConvexHull wheel = ConvexHull.Cylinder(height: 0.2f, radius: 0.35f);
using ConvexHull custom = ConvexHull.FromPoints(vertices, maxVertexCount: 32);

body.AddHull(rock);
```

A hull is interned into the world when it is attached, so it may be disposed as
soon as the shape exists. That is the one disposable geometry with no ordering
rule attached to it.

## Density and material

Density gives the body its mass; the material decides how the surface behaves.

```csharp
body.AddBox(Box.Cube(0.5f), ShapeDefinition.Default with
{
    Density = 500.0f,
    Material = PhysicsMaterial.Default with
    {
        Friction = 0.9f,
        Restitution = 0.4f,
    },
});
```

[`PhysicsMaterial`](../api/Box3D.PhysicsMaterial.yml) carries five things:

| | |
| --- | --- |
| `Friction` | Resistance to sliding |
| `Restitution` | Bounciness |
| `RollingResistance` | Resistance to rolling, which is what stops a ball rolling forever |
| `SurfaceVelocity` | A surface that moves without the body moving — a conveyor |
| `UserMaterialId` | Your own identifier, handed back by ray casts and hit events |

`UserMaterialId` is the way to answer "what did this hit sound like": tag the ice
and the gravel when you build them, then read
[`RaycastHit.UserMaterialId`](../api/Box3D.RaycastHit.yml) or the `UserMaterialIdA`
and `UserMaterialIdB` on a [hit event](events.md#impacts).

Density can be changed after the fact, and a shape can be told not to recompute
the body's mass while several change at once:

```csharp
shape.SetDensity(200.0f, updateBodyMass: false);
// ... more changes ...
body.RecomputeMass();
```

## Several shapes on one body

Attaching more than one shape is a run-time compound, and it works on any body
type. This is how you build an L-shaped piece, a character with a separate head
volume, or a vehicle chassis with a bumper.

```csharp
Body character = world.CreateDynamicBody(spawn);

Shape torso = character.AddCapsule(Capsule.Upright(1.4f, 0.3f));
Shape head = character.AddSphere(new Sphere(new Vector3(0.0f, 1.5f, 0.0f), 0.2f));

torso.UserData = (ulong)HitZone.Torso;
head.UserData = (ulong)HitZone.Head;
```

Each shape keeps its own identifier, filter, material and events, which is what
lets a hit be attributed to a part rather than to the whole object. The cost is
one broad-phase proxy per shape.

When the geometry is large, static and never needs to be told apart — a
colonnade, a rock field — bake it into a single
[`CompoundGeometry`](terrain.md#baked-compounds) instead and pay for one proxy.

## Sensors

A sensor reports overlaps and never pushes anything:

```csharp
Body trigger = world.CreateStaticBody(doorway);

trigger.AddBox(new Box(new Vector3(1.0f, 1.0f, 0.2f)), ShapeDefinition.Default with
{
    IsSensor = true,
    EnableSensorEvents = true,
    Density = 0.0f,          // a sensor still weighs something otherwise
});
```

The visitor needs `EnableSensorEvents` too. See [Events](events.md#sensors).

## Changing a shape later

```csharp
shape.Friction = 0.2f;
shape.Restitution = 0.6f;
shape.SetFilter(newFilter);              // recomputes contacts by default
shape.SetDensity(300.0f);
shape.Destroy();                         // updates the body's mass by default
```

`ApplyWind` is there too, for shapes that should be pushed by air rather than by
contacts.
