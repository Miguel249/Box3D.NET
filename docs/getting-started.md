# Getting started

Install the package, run a simulation, get the results back into your game.
Five minutes.

## Install

```sh
dotnet add package Box3D.NET
```

The native Box3D binary for your platform comes with it. Nothing else to
install. .NET 8 or later, on Windows, Linux and macOS, x64 and arm64.

Android and iOS are supported too, with two caveats worth knowing up front:

- **Android** works exactly like the desktop platforms — add the package and
  the right `libbox3d.so` is packed into your `.apk`. Only 64-bit ABIs are
  shipped (`arm64-v8a` and `x86_64`), which covers every publishable device and
  the emulator.
- **iOS** requires .NET 10 or later. Apple does not allow an application to load
  a dynamic library that is not a signed framework, so Box3D is linked into your
  application instead of loaded from a file, and that needs a target framework
  the .NET 8 iOS workload can no longer provide.

Neither is exercised on a real device in CI — see
[Platforms](../README.md#platforms) for exactly what is and is not verified. If
you ship on a phone, test on a phone.

## Your first simulation

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

That is the whole model:

| | |
| --- | --- |
| [`PhysicsWorld`](api/Box3D.PhysicsWorld.yml) | Contains everything and is the thing you step. Dispose it when finished. |
| [`Body`](api/Box3D.Body.yml) | Position, orientation, velocity. No shape of its own. |
| [`Shape`](api/Box3D.Shape.yml) | Collision geometry attached to a body. A body can carry several. |
| `Step` | Advances time. Everything happens here. |

A body with no shapes has no mass and no geometry, so create the body and then
attach to it.

## Three kinds of body

```csharp
world.CreateStaticBody(position);      // never moves. Level geometry.
world.CreateDynamicBody(position);     // falls, collides, responds to forces.
world.CreateKinematicBody(position);   // you move it; it pushes others aside.
```

Static bodies are effectively free, so use them for everything that does not
move. See [Bodies](guides/bodies.md).

## Step at a fixed rate

```csharp
world.Step(1.0f / 60.0f);   // yes
world.Step(deltaTime);      // no
```

A varying step makes the simulation irreproducible and hurts stability. Decouple
physics from your frame rate with an accumulator:

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

[The simulation step](concepts/step.md) covers sub-steps, sleeping and
continuous collision.

## Get the results back

Every body, shape and joint carries a `ulong` that Box3D stores and never reads.
Put an entity id or an array index in it, then read results back through it:

```csharp
Body body = world.CreateDynamicBody(spawn);
body.AddBox(Box.Cube(0.5f));
body.UserData = entityId;

world.Step(FixedStep);

foreach (BodyMoveEvent moved in world.Events.BodyMoves)
{
    ref Transform transform = ref transforms[moved.Body.UserData];
    transform.Position = moved.Position;
    transform.Rotation = moved.Rotation;
}
```

`BodyMoves` is one contiguous list of what actually moved, which beats asking
every body every frame. See [Events](guides/events.md).

## Where next

| | |
| --- | --- |
| [Bodies](guides/bodies.md) · [Shapes](guides/shapes.md) | The two types you will use most |
| [Queries](guides/queries.md) | Ray casts, overlaps, shape casts |
| [Events](guides/events.md) | Contacts, sensors, what moved |
| [Collision filtering](guides/collision-filtering.md) | What collides with what |
| [Joints](guides/joints.md) | Hinges, sliders, wheels, and six more |
| [Memory and ownership](concepts/ownership.md) | What to dispose, and in which order |
| [Examples](examples.md) | Sixteen runnable samples |

## Three things that catch people out

**Non-finite values throw.** `body.LinearVelocity = new Vector3(float.NaN, 0, 0)`
raises an `ArgumentException` instead of being accepted. Box3D validates with
assertions that release builds compile out, so without the check one NaN spreads
until every body in the world reads NaN.

**Create bodies where they belong.** Creating at the origin and moving afterwards
costs nearly twice as much, and more once shapes are attached.

**Meshes, height fields and baked compounds are static only.** Box3D only
generates their contacts against static bodies. See
[Terrain and meshes](guides/terrain.md).
