# Architecture

How the pieces fit, and why they are arranged this way.

## The layers

```mermaid
flowchart TB
    app["Your game or application"]
    high["<b>Box3D.NET</b><br/>PhysicsWorld · Body · Shape · Joint<br/>idiomatic, validated, allocation-free"]
    interop["<b>Box3D.Interop</b><br/>ToNativeId · ToBody<br/><i>the marked door between layers</i>"]
    native["<b>Box3D.NET.Native</b><br/>579 P/Invokes · blittable structs<br/>a literal mirror of the C API"]
    c["<b>Box3D</b> (C)<br/>consumed unmodified as a submodule"]

    app -->|"the normal path"| high
    app -.->|"when you need<br/>something not wrapped"| interop
    high --> native
    interop --> native
    native -->|"P/Invoke, no marshalling"| c

    style high fill:#512BD4,color:#fff,stroke:#3a1f9e
    style native fill:#b09ef5,color:#1a1a1a,stroke:#7d68c9
    style interop fill:#fff,color:#1a1a1a,stroke:#512BD4,stroke-dasharray: 4 3
    style c fill:#d6cdfa,color:#1a1a1a,stroke:#7d68c9
    style app fill:#f4f4f5,color:#1a1a1a,stroke:#a1a1aa
```

The rule is that `Box3D.NET` never names a `Box3D.NET.Native` type in public
API. If it did, every consumer touching a handle would take a compile-time
dependency on the C ABI and the two packages could no longer version
independently. `LayeringTests` enforces it by reflection over the built
assembly, because a rule like this decays quietly.

Dropping to the native layer stays possible, because a thin wrapper should not
be a ceiling — Box3D exports around 580 functions and the idiomatic surface does
not cover all of them. But it goes through `Box3D.Interop`, so the coupling
appears as a `using` in your own source rather than happening by accident.

## What owns what

The single most important diagram, because it is the one thing you can get
wrong in a way that crashes rather than throws.

```mermaid
flowchart LR
    subgraph disposable["Owns unmanaged memory — IDisposable"]
        world["PhysicsWorld"]
        mesh["CollisionMesh"]
        field["HeightField"]
        compound["CompoundGeometry"]
        hull["ConvexHull"]
    end

    subgraph handles["Handles — copy freely, dispose nothing"]
        body["Body"]
        shape["Shape"]
        joint["Joint"]
    end

    world -->|"creates and owns"| body
    body -->|"creates and owns"| shape
    world -->|"creates and owns"| joint

    hull -.->|"<b>copied</b> on attach"| shape
    mesh -->|"<b>borrowed</b> — must outlive"| shape
    field -->|"<b>borrowed</b> — must outlive"| shape
    compound -->|"<b>borrowed</b> — must outlive"| shape

    style world fill:#512BD4,color:#fff
    style mesh fill:#dc2626,color:#fff
    style field fill:#dc2626,color:#fff
    style compound fill:#dc2626,color:#fff
    style hull fill:#16a34a,color:#fff
    style body fill:#f4f4f5,color:#1a1a1a
    style shape fill:#f4f4f5,color:#1a1a1a
    style joint fill:#f4f4f5,color:#1a1a1a
```

Read the arrow colours:

- **Green** — a hull is interned into the world when attached, so it may be
  disposed the moment the shape exists.
- **Red** — a mesh, height field or baked compound is *borrowed*. The shape holds
  a pointer into it. Disposing it while a shape is alive is a use-after-free
  inside the solver. **Dispose the world first.**
- **Grey** — bodies, shapes and joints own nothing. They die with their world.

```csharp
using var terrain = HeightField.FromHeights(heights, 256, 256, scale);

using (var world = new PhysicsWorld())
{
    world.CreateStaticBody().AddHeightField(terrain);
    Simulate(world);
}
// World disposed here, terrain after. Never the other way round.
```

None of the disposable types has a finalizer. A finalizer runs on the GC thread
at a time of the runtime's choosing, and freeing a world mid-step, or a mesh a
live shape still points at, corrupts rather than leaks. Forgetting to dispose
leaks until the process exits, which is a bug you can see; freeing early is one
you cannot.

## A frame

```mermaid
sequenceDiagram
    participant App as Your game
    participant World as PhysicsWorld
    participant Box3D as Box3D (C)

    App->>World: body.LinearVelocity = v
    Note over World: finite check, 0.11 ns
    World->>Box3D: b3Body_SetLinearVelocity

    App->>World: Step(1/60)
    World->>Box3D: b3World_Step
    Note over Box3D: collide · solve · integrate<br/>buffers events internally

    App->>World: Events.BodyMoves
    World->>Box3D: b3World_GetBodyEvents
    Box3D-->>World: pointer + count
    Note over World: ref struct view,<br/>no copy, no allocation
    World-->>App: only the bodies that moved

    App->>World: RaycastClosest(...)
    World->>Box3D: b3World_CastRayClosest
    Box3D-->>App: RaycastHit
```

Events are buffered by Box3D during the step and handed back afterwards rather
than raised as callbacks, because the solver is multithreaded and because
applications usually want to change the world in response — which is unsafe
mid-step. `WorldEvents` exposes them as `ref struct` views over engine memory,
so reading a frame's worth allocates nothing. They are valid only until the next
step.

## Why query callbacks are structs

```mermaid
flowchart LR
    q["world.Raycast&lt;TCallback&gt;"] --> ctx["context on the stack:<br/>pointer to your struct<br/>+ managed function pointer"]
    ctx --> thunk["static thunk<br/>[UnmanagedCallersOnly]"]
    thunk --> gen["InvokeRaycast&lt;TCallback&gt;<br/><i>generic, so it can be specialised</i>"]
    gen --> your["your OnHit — inlined"]

    style q fill:#512BD4,color:#fff
    style your fill:#16a34a,color:#fff
    style thunk fill:#b09ef5,color:#1a1a1a
```

The query is generic over the callback type, so the JIT specialises it and
devirtualises the call: your `OnHit` is inlined into the dispatcher with no
delegate allocation and nothing to keep alive across the transition.

The one indirection exists because `[UnmanagedCallersOnly]` cannot be applied to
a generic method. The context carries a *managed* function pointer to a generic
helper, which may be generic precisely because it is not the
`UnmanagedCallersOnly` one, and the non-generic native thunk calls through it.
That is one indirect call per hit, against an allocation and a GC handle per
query for the delegate design.

Measured: a ray cast with a struct callback runs at 163 ns against 167 ns for
the callback-free convenience method, and allocates nothing.

## The build

```mermaid
flowchart LR
    sub["external/box3d<br/><i>submodule, pinned, never modified</i>"]
    script["tools/build-native.ps1<br/>CMake · shared library"]
    runtimes["runtimes/&lt;rid&gt;/native/"]
    gen["tools/generate-bindings.ps1"]
    generated["Generated/*.g.cs<br/>543 declarations"]
    pkg["NuGet packages"]

    sub --> script --> runtimes --> pkg
    sub -->|"headers"| gen --> generated --> pkg

    style sub fill:#d6cdfa,color:#1a1a1a
    style pkg fill:#512BD4,color:#fff
```

Box3D is a submodule pinned to a commit and never modified. Both the binding and
the binary are derived from it, which is what makes an upgrade a matter of
moving the submodule, re-running two scripts and reading the diff. CI fails if
the checked-in generated sources differ from what the script produces.
