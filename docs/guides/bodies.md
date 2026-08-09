# Bodies

A [`Body`](../api/Box3D.Body.yml) has a position, an orientation, a velocity and a
mass. It has no shape of its own: collision geometry is
[attached to it](shapes.md), and the mass comes from what you attach.

## Body types

| Type | Moved by | Mass | Pushed by others | Typical use |
| --- | --- | --- | --- | --- |
| `Static` | nothing | infinite | no | Level geometry, terrain |
| `Kinematic` | you | infinite | no | Platforms, lifts, characters |
| `Dynamic` | the solver | from its shapes | yes | Anything that falls over |

Static bodies are effectively free, so use them for everything that does not
move; they do not collide with each other at all. A kinematic body pushes
dynamic bodies out of the way without being pushed back.

```csharp
Body ground = world.CreateStaticBody(new Vector3(0.0f, -0.5f, 0.0f));
Body crate = world.CreateDynamicBody(new Vector3(0.0f, 5.0f, 0.0f));
Body lift = world.CreateKinematicBody(shaftBase);
```

For anything the three convenience methods do not cover, build a
[`BodyDefinition`](../api/Box3D.BodyDefinition.yml) and pass it to `CreateBody`:

```csharp
Body projectile = world.CreateBody(BodyDefinition.Dynamic(muzzle) with
{
    LinearVelocity = aim * 80.0f,
    IsBullet = true,
    GravityScale = 0.5f,
    Name = "shell",
});
```

Create bodies where they belong. Creating at the origin and moving afterwards
costs nearly twice as much, and more once shapes are attached.

## Moving a dynamic body

Forces and impulses are what the solver expects. A force accumulates over the
step; an impulse changes velocity immediately.

```csharp
crate.ApplyForceToCenter(new Vector3(0.0f, 200.0f, 0.0f));   // thruster
crate.ApplyImpulseToCenter(jump);                            // kick
crate.ApplyForce(wind, atPoint);                             // off-centre: also spins it
crate.ApplyTorque(new Vector3(0.0f, 5.0f, 0.0f));
crate.ApplyAngularImpulse(spin);
```

Each takes a `wake` argument, defaulting to `true`. Applying a force to a
sleeping body without waking it does nothing.

Setting `LinearVelocity` directly works and is sometimes what you want, but it
overrides the solver rather than cooperating with it: a body driven that way
walks through a stack instead of pushing it.

## Moving a kinematic body

Drive it with `LinearVelocity` or `MoveTowards`, not `SetTransform`:

```csharp
lift.LinearVelocity = new Vector3(0.0f, 2.0f, 0.0f);
lift.MoveTowards(nextPosition, nextRotation, FixedStep);
```

`SetTransform` is a teleport: it does not sweep, so the body can pass through
geometry, and it is expensive. `MoveTowards` sets the velocity that arrives at
the target pose after one step, so the body keeps a real velocity and its
contacts push other bodies correctly. That is what makes a platform carry what
stands on it.

## Mass

Mass, centre of mass and rotational inertia are computed from the shapes on the
body and their [density](shapes.md#density-and-material). A body with no shapes
has neither mass nor geometry, so attach before you simulate.

```csharp
Body body = world.CreateDynamicBody(spawn);
body.AddBox(Box.Cube(0.5f), ShapeDefinition.Default with { Density = 500.0f });

float kilograms = body.Mass;
Vector3 centre = body.CenterOfMass;       // world space
Vector3 local = body.LocalCenterOfMass;   // body space
```

Attaching or destroying a shape recomputes the mass by default. When several
shapes change at once, suppress it with `UpdateBodyMass = false` and call
`RecomputeMass` once at the end.

## Sleeping

A body that stops moving falls asleep and stops costing anything until something
touches it. This is on by default and is a large win: a settled scene of ten
thousand bodies steps in roughly the time an empty one does.

```csharp
world.AwakeBodyCount        // zero means the scene has settled
body.IsAwake = true;        // wake it yourself
body.CanSleep = false;      // this body is stepped every frame, always
```

Turn sleeping off only when something depends on a body being stepped every
frame. `SleepThreshold` on the definition sets the speed below which a body is
considered still.

Sleeping is also the classic way to write a benchmark that measures nothing —
see [Benchmarks](../benchmarks.md#sleeping-bodies-measure-nothing).

## Restricting motion

```csharp
Body character = world.CreateDynamicBody(spawn);
character.MotionLocks = MotionLocks.NoRotation;   // stays upright

crate.MotionLocks = new MotionLocks { LinearZ = true, AngularX = true };
```

[`MotionLocks`](../api/Box3D.MotionLocks.yml) removes degrees of freedom without
changing mass, which is how you get a body that slides but never tips.

## Body space

```csharp
Vector3 local = body.ToLocalPoint(worldPoint);
Vector3 world = body.ToWorldPoint(localPoint);
Vector3 direction = body.ToWorldVector(localDirection);   // rotation only

Vector3 velocity = body.GetVelocityAt(contactPoint);      // includes spin
```

`GetVelocityAt` is the one to use for impact sounds and damage: a point on a
spinning body moves even when the body's centre does not.

## Enabling, disabling, destroying

```csharp
body.Disable();    // out of the simulation entirely; costs almost nothing
body.Enable();
body.Destroy();    // gone, and every handle to it is now invalid
```

Disabling is the cheap way to park something you will need again. Destroying
invalidates every `Body` and `Shape` handle referring to it — see
[Handle validity](../concepts/handles.md).
