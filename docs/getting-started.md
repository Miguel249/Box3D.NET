# Getting started

Five minutes from nothing to a simulation.

## Install

```sh
dotnet add package Box3D.NET
```

The native Box3D binary for your platform comes with it. Nothing else to install.

## The smallest program that simulates something

```csharp
using System.Numerics;
using Box3D;

using var world = new PhysicsWorld();

Body ground = world.CreateStaticBody(new Vector3(0.0f, -0.5f, 0.0f));
ground.AddBox(new Box(new Vector3(50.0f, 0.5f, 50.0f)));

Body ball = world.CreateDynamicBody(new Vector3(0.0f, 10.0f, 0.0f));
ball.AddSphere(new Sphere(0.5f));

for (int frame = 0; frame < 120; frame++)
{
    world.Step(1.0f / 60.0f);
}

Console.WriteLine(ball.Position);   // resting on the ground
```

Four ideas, and that is the whole model:

| | |
| --- | --- |
| **World** | Contains everything and is the thing you step. Dispose it when finished. |
| **Body** | Has a position, an orientation and a velocity. No shape of its own. |
| **Shape** | Collision geometry attached to a body. A body can carry several. |
| **Step** | Advances time. Everything happens here. |

## Body types

```csharp
world.CreateStaticBody(position);      // never moves. Level geometry.
world.CreateDynamicBody(position);     // falls, collides, responds to forces.
world.CreateKinematicBody(position);   // you move it; it pushes others aside.
```

Static bodies are effectively free, so use them for everything that does not
move. A kinematic body is what a moving platform or a character controller wants:
drive it with `LinearVelocity` or `MoveTowards`, not `SetTransform`, so that
contacts push other bodies correctly.

## Use a fixed time step

```csharp
world.Step(1.0f / 60.0f);   // yes
world.Step(deltaTime);      // no
```

A varying step makes the simulation irreproducible and hurts stability. Decouple
it from your frame rate with an accumulator:

```csharp
const float FixedStep = 1.0f / 60.0f;
float accumulator = 0.0f;

void Update(float deltaTime)
{
    // Clamp, or a long frame spirals into a hundred catch-up steps.
    accumulator += MathF.Min(deltaTime, 0.25f);

    while (accumulator >= FixedStep)
    {
        world.Step(FixedStep);
        accumulator -= FixedStep;
    }
}
```

## Connect physics to your game

Every body, shape and joint carries a `ulong` that Box3D stores and never reads.
Put an entity id or an array index in it:

```csharp
Body body = world.CreateDynamicBody(spawn);
body.AddBox(Box.Cube(0.5f));
body.UserData = entityId;
```

Then read results back through it. This is the efficient way — one contiguous
array holding only what actually moved, rather than asking every body every
frame:

```csharp
world.Step(FixedStep);

foreach (BodyMoveEvent moved in world.Events.BodyMoves)
{
    ref Transform transform = ref transforms[moved.Body.UserData];
    transform.Position = moved.Position;
    transform.Rotation = moved.Rotation;
}
```

Shapes carry their own identifier, separate from the body's, which is what lets
a hit be attributed to a part rather than to the whole object:

```csharp
head.UserData = (ulong)HitZone.Head;
torso.UserData = (ulong)HitZone.Torso;
```

## Shoot something

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

`Fraction` is how far along the ray the hit was, from zero to one. Multiply by
the length of the vector you passed to get a distance.

For anything more selective — ignoring the shooter, collecting every hit —
implement `IRaycastCallback` on a **struct**:

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

A struct rather than a delegate so the query allocates nothing. See
[architecture](architecture.md#why-query-callbacks-are-structs).

## React to collisions

Events are opt-in per shape, because collecting them is not free:

```csharp
var reporting = ShapeDefinition.Default with
{
    EnableContactEvents = true,
    EnableHitEvents = true,
};

body.AddBox(Box.Cube(0.5f), reporting);
```

Then, after stepping:

```csharp
foreach (ContactHitEvent hit in world.Events.ContactHits)
{
    PlayImpactSound(hit.Point, volume: hit.ApproachSpeed / 20.0f);
}
```

Events are valid only until the next step, so read what you need before stepping
again. It is safe to create and destroy bodies while walking them — that is why
Box3D buffers events instead of calling back mid-step.

## Trigger volumes

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

The visitor needs `EnableSensorEvents` too.

## Filter what collides with what

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

For a one-off pair that must not collide — a projectile and the turret that
fired it — a filter joint is cheaper than spending a category bit:

```csharp
world.CreateFilterJoint(FilterJointDefinition.Between(turret, shell));
```

## Join things together

```csharp
// A door that opens ninety degrees and swings shut.
RevoluteJoint hinge = world.CreateRevoluteJoint(
    RevoluteJointDefinition.Hinge(frame, door, hingePoint, Vector3.UnitY) with
    {
        LimitsEnabled = true,
        LowerAngle = 0.0f,
        UpperAngle = MathF.PI * 0.5f,
        MotorEnabled = true,
        MotorSpeed = -1.0f,
        MaxMotorTorque = 50.0f,
    });
```

The factory methods matter more than they look. A joint needs a *pair* of local
frames describing the same world pose from each body's point of view; get that
wrong and the joint starts out violated and snaps on the first step. `Hinge`,
`Slider`, `Between`, `BallAndSocket`, `Weld` and `Suspension` derive that pair
from a world-space anchor and axis.

## Terrain

For anything large and static, a height field stores far less than the
equivalent mesh — 20 KB against 289 KB for a 64 by 64 grid:

```csharp
using var terrain = HeightField.FromHeights(
    heights,
    columnCount: 256,
    rowCount: 256,
    scale: new Vector3(1.0f, 100.0f, 1.0f));

Body ground = world.CreateStaticBody();
ground.AddHeightField(terrain);
```

**Order matters here.** A height field and a mesh are *borrowed* by the shapes
built from them, not copied. Dispose the world before disposing them. See
[architecture](architecture.md#what-owns-what).

## Things that will trip you up

**Bad numbers throw.** Passing NaN or infinity raises an `ArgumentException`
rather than being accepted:

```csharp
body.LinearVelocity = new Vector3(float.NaN, 0, 0);   // ArgumentException
```

This is deliberate. Box3D validates with assertions that release builds compile
out, so without the check a single NaN spreads until every body in the world
reads NaN, and nothing can remove it.

**Create bodies where they belong.** Creating at the origin and moving afterwards
costs nearly twice as much, and more once shapes are attached.

**A body with no shapes has no mass and no geometry.** Create the body, then
attach shapes.

**Meshes and height fields are static only.** Box3D only generates their
contacts against static bodies. Use a convex hull or several primitives for
something that moves.

**Sleeping is a feature.** A settled scene costs almost nothing because bodies
fall asleep. `world.AwakeBodyCount` reaching zero means everything has settled.

## Where next

- [Architecture](architecture.md) — layers, ownership, and why the API is shaped this way
- [Benchmarks](benchmarks.md) — what the wrapper costs, measured
- `src/Box3D.NET.Samples` — sixteen runnable samples, each demonstrating one feature
