---
_layout: landing
---

# Box3D.NET

An idiomatic, allocation-free C# binding for [Box3D](https://github.com/erincatto/box3d),
the 3D physics engine by Erin Catto.

```csharp
using var world = new PhysicsWorld();

Body ground = world.CreateStaticBody(new Vector3(0.0f, -0.5f, 0.0f));
ground.AddBox(new Box(new Vector3(50.0f, 0.5f, 50.0f)));

Body ball = world.CreateDynamicBody(new Vector3(0.0f, 10.0f, 0.0f));
ball.AddSphere(new Sphere(0.5f));

for (int frame = 0; frame < 120; frame++)
{
    world.Step(1.0f / 60.0f);
}
```

## Start here

- **[Getting started](getting-started.md)** — from nothing to a simulation, and the
  handful of things that will otherwise trip you up.
- **[Architecture](architecture.md)** — the layers, what owns what, and why the API
  is shaped the way it is.
- **[Benchmarks](benchmarks.md)** — what the wrapper costs against calling the C API
  directly. Measured, not claimed.
- **[API reference](api/index.md)** — every public type.

## What it is

| Package | What it is |
| --- | --- |
| `Box3D.NET` | The idiomatic surface. This is what you want. |
| `Box3D.NET.Native` | The raw P/Invoke layer, a one-to-one mirror of the C API. |

Reading a body position through the wrapper costs what calling
`b3Body_GetPosition` costs. Ray casts allocate nothing, including the callback
forms. .NET 8 or later, Windows, Linux and macOS, x64 and arm64, NativeAOT and
trimming.