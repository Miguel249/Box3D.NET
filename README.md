<!--
  The images are absolute URLs on purpose. This file is the package description
  on nuget.org, which serves it from its own domain, so a relative path resolves
  against nuget.org and renders as a broken image. tools/set-repository.ps1
  rewrites them along with everything else that names the repository.
-->
<img src="https://raw.githubusercontent.com/Miguel249/Box3D.NET/main/assets/icon.png" width="96" align="right" alt="" />

# Box3D.NET

An idiomatic C# binding for [Box3D](https://github.com/erincatto/box3d), the 3D
physics engine by Erin Catto, with no managed allocations on the simulation hot
path.

[![NuGet](https://img.shields.io/nuget/v/Box3D.NET.svg?logo=nuget&label=Box3D.NET)](https://www.nuget.org/packages/Box3D.NET/)
[![CI](https://github.com/Miguel249/Box3D.NET/actions/workflows/ci.yml/badge.svg)](https://github.com/Miguel249/Box3D.NET/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-8.0%2B-512BD4)](https://dotnet.microsoft.com/)
[![Platforms](https://img.shields.io/badge/platforms-win%20%7C%20linux%20%7C%20macOS-lightgrey)](#platforms)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

**[Getting started](docs/getting-started.md)** ·
**[Gallery](docs/gallery.md)** ·
**[Architecture](docs/architecture.md)** ·
**[Benchmarks](docs/benchmarks.md)** ·
**[API reference](https://Miguel249.github.io/Box3D.NET/)**

![Fifteen crates and the ball that ends them](https://raw.githubusercontent.com/Miguel249/Box3D.NET/main/assets/renders/stack.gif)

> **Status: 0.x.** Published and usable. The binding is complete and verified
> against the C ABI, and the idiomatic layer covers worlds, bodies, shapes,
> queries, events, all nine joint types, meshes, height fields, baked compounds,
> the character mover and debug draw. The API may still change between minor versions; every
> break is recorded in the [changelog](CHANGELOG.md), and packages are validated
> against the previous release so none happens by accident.

```sh
dotnet add package Box3D.NET
```

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

Console.WriteLine(ball.Position);   // resting on the ground
```

That is the whole API for a first simulation: a world, some bodies, shapes on
them, and a step. Everything else is opt-in.

## The next five minutes

```csharp
// Tune the world when the defaults are not what you want.
using var world = new PhysicsWorld(WorldSettings.Default with
{
    Gravity = new Vector3(0.0f, -9.81f, 0.0f),
    WorkerCount = 4,
});

// Link physics objects to your own game state with an entity id or an index.
ball.UserData = entityId;

// Push transforms into your game from one contiguous array of what moved.
world.Step(1.0f / 60.0f);

foreach (BodyMoveEvent moved in world.Events.BodyMoves)
{
    ref Transform t = ref transforms[moved.Body.UserData];
    t.Position = moved.Position;
    t.Rotation = moved.Rotation;
}

// Shoot something.
RaycastHit hit = world.RaycastClosest(muzzle, aim * 100.0f);
if (hit.Hit)
{
    Damage(hit.Shape.Body.UserData, hit.Point, hit.Normal);
}

// Hang a door on a hinge that stops at ninety degrees.
world.CreateRevoluteJoint(
    RevoluteJointDefinition.Hinge(frame, door, hingePoint, Vector3.UnitY) with
    {
        LimitsEnabled = true,
        LowerAngle = 0.0f,
        UpperAngle = MathF.PI * 0.5f,
    });
```

## Goals

- An API that reads like modern .NET, not like a C header.
- No allocations on the simulation hot path, no boxing, no reflection.
- .NET 8 or later, on Windows, Linux and macOS, x64 and arm64.
- Works under NativeAOT and trimming.
- No dependencies beyond the base class library.

## Packages

| Package | What it is |
| --- | --- |
| `Box3D.NET` | The idiomatic surface. This is what you want. |
| `Box3D.NET.Native` | The raw P/Invoke layer, a one-to-one mirror of the C API. Reach for it when you need something the high-level API does not expose yet. |

Both are MIT licensed and ship the native Box3D binary for every supported
platform, so `dotnet add package Box3D.NET` is all that is required.

## Design notes

The decisions below are the ones that shaped the API. Each is also recorded in
the commit that introduced it.

### Vectors are `System.Numerics` types

`b3Vec3` and `System.Numerics.Vector3` have the same layout, as do `b3Quat` and
`Quaternion` — both are `x, y, z` followed by a scalar. Using the framework
types directly means no conversion at the boundary with a renderer or engine,
and the vector math gets the BCL's SIMD paths for free. `LayoutTests` asserts
the assumption rather than trusting it.

### Only the world is disposable

`PhysicsWorld` owns native memory. `Body`, `Shape` and the definition types are
handles and values that die with their world, so making them `IDisposable` would
imply an ownership they do not have.

`PhysicsWorld` has no finalizer, which is a deliberate departure from the usual
guidance. A finalizer runs on the GC thread at a time of the runtime's choosing,
and destroying a world while another thread is inside `Step` corrupts the
simulation rather than merely leaking. Forgetting to dispose leaks a world until
the process exits — visible, diagnosable, and much better than a use-after-free
that only shows up under load.

### Handles are value types, and stale ones are rejected

Box3D identifiers are small structs holding an index and a generation counter,
not pointers. Wrapping each one in a `SafeHandle` would put a finalizable heap
object behind every body in the simulation, which is not worth paying for
thousands of times.

What a `SafeHandle` would have bought is that a dead handle cannot be
dereferenced, and that has to be bought some other way. Box3D resolves an id by
indexing into the world's arrays and asserting on the way past that the id is
live — and those assertions are compiled out of the release binary this package
ships. Measured on `win-x64` against the shipped build, before 0.3.0:

```
default(Body).Position                 access violation, 0xC0000005
body.Position after body.Destroy()     access violation
body.Position after world.Dispose()    access violation
body.Destroy() twice                   access violation
handle whose index had been reused     returned the replacement body's
                                       position, in silence
```

The last one is the worst: no crash, no exception, just another body's state
reported as yours.

So every member of `Body`, `Shape`, `Joint` and the nine specific joint handles
asks `b3Body_IsValid`, `b3Shape_IsValid` or `b3Joint_IsValid` before it
dereferences, and throws `InvalidOperationException` if the answer is no. The
check costs 2.07 ns against the 2.66 ns of the `b3Body_GetPosition` it guards.
`IsValid`, handle conversions such as `hinge.AsJoint`, equality and `ToString`
never throw, so asking is always safe:

```csharp
foreach (ContactEndEvent touch in world.Events.ContactEnds)
{
    // An end-touch event is often raised *because* a shape was destroyed.
    if (touch.ShapeA.IsValid)
    {
        Handle(touch.ShapeA);
    }
}
```

One case remains and belongs to Box3D rather than to this binding. A `b3BodyId`
records which world *slot* it came from but not that world's generation, so a
handle held past `world.Dispose()` becomes indistinguishable from a handle into
whatever world next occupies the slot. Nothing in the id can separate them.
`HandleSafetyTests` pins that behaviour so it is a known boundary rather than a
surprise, and the rule that avoids it is simple: handles do not outlive their
world.

`Box3D.NET.Native` and `Box3D.Interop` validate nothing, by design. They are the
C API, and the C API's contract is that a handle is valid.

### Query callbacks are structs, not delegates

```csharp
struct NearestExcludingSelf : IRaycastCallback
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
        return RaycastAction.ClipTo(hit.Fraction);
    }
}

var callback = new NearestExcludingSelf { Self = player };
world.Raycast(muzzle, aim * 100.0f, ref callback);
```

The query is generic over the callback type, so the JIT specializes it and
devirtualizes the call: your code is inlined into the dispatcher with no
delegate allocation, no boxing, and nothing to keep alive across the native
transition. A delegate-based API would allocate a closure on every cast.

For the common case there is `world.RaycastClosest(...)`, which needs no
callback at all.

### Each joint type has its own handle

```csharp
RevoluteJoint hinge = world.CreateRevoluteJoint(
    RevoluteJointDefinition.Hinge(frame, door, hingePoint, Vector3.UnitY) with
    {
        LimitsEnabled = true,
        LowerAngle = 0.0f,
        UpperAngle = MathF.PI * 0.5f,
    });

hinge.MotorEnabled = true;
hinge.MotorSpeed = -1.0f;      // a door closer
hinge.MaxMotorTorque = 50.0f;
```

`CreateRevoluteJoint` returns a `RevoluteJoint`, not a generic `Joint`. A single
`CreateJoint` would hand back something that has to be narrowed before it is
useful, and would let a distance joint definition produce a handle whose
revolute members compile and then assert at run time. The shared members are
always one hop away through `hinge.AsJoint`.

The factory methods matter more than they look. A joint needs a *pair* of local
frames that describe the same world pose from each body's point of view; get
that wrong and the joint starts out violated and snaps on the first step.
`RevoluteJointDefinition.Hinge` and `Joint.FramesFromWorldAnchor` do that
calculation from a world-space anchor and axis.

### The layers are sealed, with one marked door

`Box3D.NET` never names a `Box3D.NET.Native` type in public API. If it did,
every consumer touching a handle would take a compile-time dependency on the C
ABI, and the two packages could no longer version independently.

Going down a level is still supported, because a thin wrapper should not be a
ceiling — Box3D exports around 580 functions and the idiomatic surface does not
cover all of them:

```csharp
using Box3D.Interop;

b3BodyId raw = body.ToNativeId();
B3.b3Body_SetName(raw, name);
```

Importing that namespace is the point: the coupling is visible in your source
rather than being the path of least resistance. `LayeringTests` enforces the
rule over the built assembly by reflection, because a rule like this decays
quietly — one convenient property and nothing fails.

### Ownership follows Box3D, and it is not uniform

Most geometry is a value: attaching a sphere or a box copies it and there is
nothing to manage. Three kinds are not, and the difference is load-bearing:

| | Copied on attach? | Disposable |
| --- | --- | --- |
| `Sphere`, `Capsule`, `Box` | Yes, by value | No |
| `ConvexHull` | Yes, interned in the world | Yes, freely |
| `CollisionMesh` | **No, borrowed** | **After the world** |
| `HeightField` | **No, borrowed** | **After the world** |
| `CompoundGeometry` | **No, borrowed** | **After the world** |

```csharp
using var terrain = HeightField.FromHeights(heights, 256, 256, scale);

using (var world = new PhysicsWorld())
{
    world.CreateStaticBody().AddHeightField(terrain);
    Simulate(world);
}
// World first, terrain second. A shape holds a borrowed pointer into it.
```

Like `PhysicsWorld`, none of these has a finalizer. Freeing a mesh that a live
shape still points at is a use-after-free inside the solver, and a finalizer
runs whenever the runtime chooses. Leaking until exit is a bug you can see.

### The character mover is primitives, not a controller

```csharp
var gather = new GatherPlanes { Planes = buffer };
world.CollideCapsule(capsule, position, ref gather);

Span<CollisionPlane> planes = buffer.AsSpan(0, gather.Count);

PlaneSolverResult result = CharacterMover.SolvePlanes(velocity * dt, planes);
position += result.Translation;
velocity = CharacterMover.ClipVelocity(velocity, planes);
```

That is the whole engine-side problem: find the planes, satisfy them, clip the
velocity. What counts as ground, how high a jump goes, whether a slope is
climbable — that is game design, and every game answers it differently. Wrapping
an opinion about it here would be inventing policy Box3D deliberately left to
the caller.

`CharacterControllerSample` builds a complete controller on these three calls in
about eighty lines, with gravity, jumping, ground detection, slope limits and
wall sliding. Copy it and change the parts that are yours.

### Bad numbers are rejected at the boundary

Box3D validates its inputs with assertions, and assertions are compiled out of
the release builds this package ships. So a NaN is accepted in silence — and it
does not stay where you put it.

Measured: setting one body's velocity to NaN and stepping thirty times left a
second body, twenty metres away and never touched, reading `(NaN, NaN, NaN)`.
The solver couples bodies through islands and the broad phase, so one bad number
reaches everything, and there is no way to remove it from a world afterwards.

So the library rejects non-finite values at the call that produced them:

```csharp
body.LinearVelocity = new Vector3(float.NaN, 0, 0);
// ArgumentException, and the world is untouched
```

The check costs **0.11 ns**, measured against the same native call without it.
That is under two percent of a property write, for the difference between an
exception with a stack trace and a simulation that silently becomes garbage.

### User data is an identifier, not a reference

`body.UserData` is a `ulong`. The alternative, pinning a managed object with a
`GCHandle`, reads better in object-oriented code and loses on every other axis:
the handle must be freed when the body is destroyed, a body can be destroyed by
destroying its world, so the world would have to track every handle it issued —
and a missed one is a leak the GC cannot see.

An integer costs nothing, cannot leak, and is what engines actually want back
out of a contact event: an entity id or an array index. Shapes carry their own,
separate from the body's, which is what lets a hit be attributed to the head
rather than merely to the character.

### Runtime marshalling is disabled

The native assembly is compiled with `DisableRuntimeMarshalling`. Every P/Invoke
is then a direct call with arguments passed as they already sit in memory, and
any accidentally non-blittable type becomes a compile error instead of a silent
field-by-field copy. Booleans cross the boundary as `NativeBool`, a one-byte
value type matching C's `_Bool`.

### Single precision only

Box3D's `BOX3D_DOUBLE_PRECISION` ("large world") mode changes the ABI rather
than being a runtime switch. This binding targets the default single-precision
build, and asserts at test time that the loaded library agrees. Large-world
support, if it lands, will be a separate package.

### The bindings are generated

`tools/generate-bindings.ps1` produces the 543 P/Invoke declarations from the
Box3D headers, converting the Doxygen comments into XML documentation along the
way. A mistyped parameter in a hand-written binding does not fail to compile; it
corrupts the stack at run time. Generating removes that class of bug and reduces
a Box3D upgrade to re-running the script and reading the diff. CI fails if the
checked-in output does not match what the script produces.

A C type the script has not been taught is a hard error rather than something
passed through, and `BindingSource.Commit` records which Box3D revision the
declarations came from, so an assembly can be traced back to its headers.

### The struct layouts are checked against the C compiler

The declarations are generated, but the structs they pass are hand-written
mirrors, and nothing about C# forces a mirror to match. A field of the wrong
width, or two fields swapped, compiles and runs: the call succeeds and reads the
wrong bytes, so a body ends up with its restitution in the friction slot. There
is no crash to investigate.

`tools/dump-abi.ps1` compiles a program against the real Box3D headers that
prints `sizeof`, `_Alignof` and `offsetof` for every field, and records the
answers in [`abi/native-layout.json`](abi/native-layout.json). The test suite
holds all 92 structs to that file — size, every field offset, blittability, and
whether a mirror exists at all — and CI regenerates it, so a submodule bump that
moves a field fails the build instead of shipping.

## Examples

Sixteen of them, each headless, self-checking and small enough to read in one
sitting. They assert on their own results rather than only printing, so CI runs
them — published with NativeAOT — and a regression fails the build instead of
producing plausible output nobody reads.

```sh
dotnet run --project src/Box3D.NET.Samples -- --list      # what there is
dotnet run --project src/Box3D.NET.Samples -- raycast     # run one
dotnet run --project src/Box3D.NET.Samples                # run all of them
```

| | |
| --- | --- |
| `basic-world` | A world, a body, a shape, a step. |
| `dynamic-body` | Gravity acting on a falling body. |
| `collision` | A falling box landing on static ground. |
| `raycast` | Closest-hit and callback ray casts. |
| `contact-events` | Reading contacts after a step. |
| `sensor` | A trigger volume that reports overlaps without colliding. |
| `compound` | One body carrying several shapes. |
| `continuous` | A fast body that would otherwise tunnel through a wall. |
| `height-field` | Terrain from a height map. |
| `mesh` | Collision against a triangle mesh. |
| `character` | A kinematic character walking, sliding and climbing. |
| `entities` | Associating game objects with bodies through user data. |
| `debug-draw` | Feeding the world's debug geometry to a renderer. |
| `hinged-door` | A revolute joint with limits. |
| `chain` | A hanging chain of revolute joints. |
| `vehicle` | A wheeled vehicle built from wheel joints. |

## Gallery

The library is renderer-agnostic, which makes "does the drawing interface
actually work" a fair question. `src/Box3D.NET.Visualizer` answers it: a console
application with a software rasterizer and its own PNG and GIF writers, no
dependencies beyond the base class library, consuming `Box3D.NET` through
`IDebugDrawer` and `IDebugShapeFactory` and nothing else.

```sh
dotnet run --project src/Box3D.NET.Visualizer -- --list      # what there is
dotnet run --project src/Box3D.NET.Visualizer                # render all of them
```

| | |
| --- | --- |
| [![Contact points, normals and broad-phase bounds](https://raw.githubusercontent.com/Miguel249/Box3D.NET/main/assets/renders/contacts.png)](docs/gallery.md#contacts)<br>**contacts** — what the solver is working with | [![A chain of revolute joints](https://raw.githubusercontent.com/Miguel249/Box3D.NET/main/assets/renders/chain.png)](docs/gallery.md#chain)<br>**chain** — nine hinges and a weight |
| [![A sweeping fan of ray casts](https://raw.githubusercontent.com/Miguel249/Box3D.NET/main/assets/renders/raycast.png)](docs/gallery.md#raycast)<br>**raycast** — closest hit and normal, per ray | [![A kinematic character climbing a ramp](https://raw.githubusercontent.com/Miguel249/Box3D.NET/main/assets/renders/character.png)](docs/gallery.md#character)<br>**character** — the mover primitives |
| [![A cart on wheel joints](https://raw.githubusercontent.com/Miguel249/Box3D.NET/main/assets/renders/vehicle.png)](docs/gallery.md#vehicle)<br>**vehicle** — suspension, a motor, a mesh ramp | [![Balls rolling into a height field bowl](https://raw.githubusercontent.com/Miguel249/Box3D.NET/main/assets/renders/terrain.png)](docs/gallery.md#terrain)<br>**terrain** — a height field, read back from the engine |
| [![Spheres, capsules and boxes poured into a pen](https://raw.githubusercontent.com/Miguel249/Box3D.NET/main/assets/renders/pour.png)](docs/gallery.md#pour)<br>**pour** — fifty-four bodies, three shapes | [![A colonnade baked into one shape](https://raw.githubusercontent.com/Miguel249/Box3D.NET/main/assets/renders/compound.png)](docs/gallery.md#compound)<br>**compound** — thirty-seven children, one proxy |

All nine, animated, are in the **[gallery](docs/gallery.md)**.

## Documentation

| | |
| --- | --- |
| [Getting started](docs/getting-started.md) | From nothing to a simulation, and the handful of things that will otherwise trip you up. |
| [Gallery](docs/gallery.md) | Nine scenes, animated, and how the renderer that drew them hangs off the public interface. |
| [Architecture](docs/architecture.md) | Layers, ownership and the frame loop, with diagrams. |
| [Benchmarks](docs/benchmarks.md) | What the wrapper costs, measured. |
| [API coverage](docs/api-coverage.md) | Every function Box3D exports, how it is bound, and whether the idiomatic layer reaches it. |
| [API reference](https://Miguel249.github.io/Box3D.NET/) | Every public type, generated from the XML documentation. |

The reference site is rebuilt from source on every push, so it cannot drift from
the code. Build it locally with:

```sh
dotnet tool restore
dotnet docfx docs/docfx.json --serve
```

## Building

Requires the .NET 8 SDK or later, CMake 3.22 or later, and a C compiler.

```sh
git clone --recursive https://github.com/Miguel249/Box3D.NET
cd Box3D.NET

# Build the native library for this machine.
pwsh tools/build-native.ps1

dotnet build
dotnet test
```

Box3D lives in `external/box3d` as a submodule pinned to a specific commit. It
is never modified; this project only consumes it.

Without the native library the project still builds, and the layout and math
tests still run. The tests that call into Box3D skip themselves rather than
fail, so `dotnet test` is useful immediately after cloning. CI always stages a
binary and then fails if anything was skipped.

## Testing

| Suite | What it protects |
| --- | --- |
| `AbiTests` | All 92 structs against what the C compiler reports for the same declarations: size, every field offset, blittability, and whether a mirror exists at all. |
| `LayoutTests` | A core set of sizes against values derived by hand from the C declarations. Narrower than `AbiTests` and kept because it needs neither a native binary nor a C toolchain. |
| `DebugDrawTests` | That debug draw reaches a managed drawer with usable values, that a shape factory is asked once per shape rather than once per frame, that disposal releases every drawable, and that a drawn frame allocates nothing. |
| `MathTests` | The math ported from the `B3_INLINE` functions, by algebraic identity and by agreement with `System.Numerics`. |
| `NativeInteropTests` | The binding against the real library: default definitions come back intact, bodies fall, rays hit, and `b3GetByteCount` returns to its starting value after worlds and hulls are destroyed. |
| `JointTests` | Joint behaviour, not round trips: limits actually hold, motors actually lift, filter joints actually let bodies through. |
| `LayeringTests` | That no `Box3D.NET.Native` type reaches the public surface, checked by reflection over the built assembly. |
| `UserDataTests` | Identifiers survive the round trip through the native `void*`, including the top bit, and come back from events and queries. |
| `GeometryTests` | Hull, mesh and height field behaviour, and the ownership rules for each. |
| `CharacterMoverTests` | Contact gathering, the plane solver, velocity clipping, and a character sliding along a wall. |
| `FuzzTests` | Non-finite input, extreme magnitudes, degenerate geometry, and seeded random operation sequences. |
| `DeterminismTests` | That the same scene run twice hashes identically, bit for bit, including alongside other worlds and interleaved queries. |
| `ThreadingTests` | That independent worlds step in parallel and reach exactly the state they would have reached alone. |
| `StressTests` | A thousand bodies, sixty-link chains, the world limit, and leak checks over every create-and-destroy cycle. |
| `HandleSafetyTests` | That a destroyed, default or orphaned handle throws instead of reading freed memory, in every way one can be produced, and that a disposed world's every member throws. |
| `GeometryOwnershipTests` | One mesh behind many shapes, every safe teardown order, and ten thousand build-and-release cycles per geometry kind with the native byte count required to return to zero. |
| `AllocationTests` | That the documented hot paths allocate exactly zero managed bytes: `Step`, body and shape access, body creation, both ray cast forms, overlap, every event list, joint access and the character mover. |

Tests that call into Box3D share a non-parallel xUnit collection. The library
keeps process-wide state — the allocated byte count, the live world count — so a
leak test running beside another class that creates worlds is measuring noise.

## Thread safety

The short version: **one world, one thread.** Everything below is the long
version, and each row is what the threading tests actually exercise rather than
what would be nice to promise.

| Operation | Safe concurrently? |
| --- | --- |
| `Step` on **different** worlds | Yes |
| Reads and queries on **different** worlds | Yes |
| `new PhysicsWorld(...)` and `Dispose` from several threads | Yes, serialised by this library |
| Anything on **one** world while that world is stepping | **No** |
| Two threads mutating one world | **No** |
| Two threads reading one world, nothing stepping | Yes |
| Passing a `Body`, `Shape` or `Joint` between threads | Yes, the handle is a value |
| Reading `Events` while that world is stepping | **No** |

A `PhysicsWorld` is not internally synchronised, and deliberately so: a lock
around `Step` would cost every user to protect a pattern the engine does not
support anyway. Give each world an owner, and hand results to other threads
afterwards.

Two things are worth knowing beyond the table.

**World creation and destruction are guarded here, not by Box3D.** The engine
keeps its worlds in one global table and picks a slot by scanning for a free
entry, then marks it in use some thirty lines later; nothing synchronises the
two. Two threads creating a world at the same time can select the same slot, and
the corrupted world then spins forever inside `Step` rather than failing. Box3D's
own documentation says to hold a mutex around those calls, so `PhysicsWorld`
holds one. It covers only the two native calls, so `Step`, queries and body edits
never touch it. Code calling `b3CreateWorld` through `Box3D.NET.Native` directly
is outside that guard and on its own.

**A world may already be using several threads.** `WorkerCount` above one lets
Box3D start and own worker threads for the solver. That is internal parallelism
inside one `Step` on one calling thread; it does not make the world safe to touch
from your own threads, and it is not additive with running several worlds at
once — the cores have to come from somewhere.

## Determinism

Box3D is written for cross-platform determinism, which is what makes lockstep
networking and replay possible. This binding's job is not to undermine it, and
`DeterminismTests` is what holds it to that: it hashes the exact bits of every
body's position and velocity after a fixed number of steps and requires equality,
not closeness.

What is verified, on every CI platform:

- the same scene run twice, and run many times, gives identical bits;
- other worlds existing alongside it change nothing;
- interleaving two worlds step by step changes neither;
- a query between steps changes nothing;
- reading events changes nothing;
- a multithreaded world is reproducible against itself.

What is **not** verified here, and so is not claimed: that two *different*
platforms or architectures produce identical bits for the same scene. That is a
property of Box3D and of the compiler it was built with, not of this binding,
and proving it would mean comparing hashes across the CI matrix rather than
within each runner. Until that exists, treat cross-platform determinism as
Box3D's claim rather than as this package's.

Determinism also depends on things the caller controls: a fixed time step, a
fixed sub-step count, and the same sequence of operations. A variable time step
makes a simulation non-reproducible no matter what either layer does.

## Performance

Measured, not asserted. See [docs/benchmarks.md](docs/benchmarks.md) for method
and conditions.

A whole frame, both worlds built identically so the only difference is which
`Step` is called:

| Bodies | C API | Box3D.NET | Ratio | Allocated |
| ---: | ---: | ---: | ---: | ---: |
| 100 | 75.29 µs | 75.48 µs | 1.00 | 0 B |
| 1,000 | 746.82 µs | 745.40 µs | 1.00 | 0 B |
| 10,000 | 7,704.64 µs | 7,723.55 µs | 1.00 | 0 B |

The wrapper's overhead on a step is not measurable.

Individual calls, where the wrapper is a larger share of a smaller number:

| | Native | Wrapper | Ratio | Allocated |
| --- | ---: | ---: | ---: | ---: |
| Read a body position | 8.998 ns | 9.496 ns | 1.06 | 0 B |
| Ray cast, closest hit over 200 shapes | 175.4 ns | 171.7 ns | 0.98 | 0 B |
| Ray cast with a struct callback, nearest | — | 168.9 ns | — | 0 B |
| Drain every event list after a step | — | 164.5 ns | — | 0 B |
| Create 1000 bodies with spheres | 725.2 µs | 1,066.4 µs | 1.48 | 0 B |

Everything allocates nothing, including the callback forms, the event
enumeration and a drawn debug frame — `AllocationTests` requires exactly zero
bytes on each of those paths rather than taking the benchmark's word for it.

Two figures are worth reading properly. Reading a body position now includes the
handle validity check, which is why it moved from parity to 1.06. And creating
bodies in bulk is the one place the wrapper genuinely costs something: it
rebuilds `b3BodyDef` and `b3ShapeDef` per body where the C loop hoists them.
That 1.48 replaces a 1.13 recorded for 0.2.0 that does not reproduce here — the
published 0.2.0 package measures 1.54 on this machine, so it is a correction to
the documentation rather than a regression. [docs/benchmarks.md](docs/benchmarks.md)
has the full working.

## Platforms

Built and verified in CI on every push. Only what is listed here is claimed.

The **package consumer** column is the one that matters, and it is not the same
question as "do the tests pass". The tests run against a project reference,
which resolves assemblies out of `bin/` and copies the native library through a
build target — it never touches the package's `runtimes/<rid>/native` layout or
the NuGet asset resolution that a real consumer depends on. So CI also installs
the packed `.nupkg` into a project that has never heard of this repository, and
runs a scene through it: create a world, create bodies and shapes, step 240
frames, check the ball actually fell and actually stopped, ray cast, read
events, dispose.

| Runtime identifier | Native build | Test suite | Package consumer | Trimmed | NativeAOT |
| --- | :---: | :---: | :---: | :---: | :---: |
| `win-x64` | yes | yes | yes | yes | yes |
| `win-arm64` | yes | — | yes | — | — |
| `linux-x64` | yes | yes | yes | yes | yes |
| `linux-arm64` | yes | — | yes | — | — |
| `osx-x64` | yes | — | under Rosetta | — | — |
| `osx-arm64` | yes | yes | yes | yes | yes |

A dash is "not verified", not "known broken". Trimming and NativeAOT are checked
on the three runners where the toolchain is native rather than cross-compiled; a
cross-compilation failure on the others would say more about the runner than
about this package, and claiming a platform on that evidence would be worse than
leaving the cell empty.

`osx-x64` carries its own caveat. GitHub's Intel macOS image is being retired,
and a job asking for one sat queued for over an hour without ever being picked
up, so the x64 package is installed and run on the arm64 image under Rosetta 2
instead. It is published self-contained there, because a framework-dependent x64
binary starts under Rosetta and then fails to load an x64 `libhostfxr.dylib`
that an arm64 runner does not have — a fact about the runner rather than about
the package.

That still exercises what matters: the x64 native asset is resolved out of the
package, the x64 library is loaded, and the simulation runs and is checked. What
it does not prove is behaviour on Intel silicon, which is why the cell says what
it says rather than "yes".

Requires .NET 8 or later.

## Contributing

Issues and pull requests are welcome. What CI will check, so there are no
surprises:

```sh
dotnet build  -c Release          # warnings are errors, documentation included
dotnet test   -c Release          # every test, on the platform you are on
dotnet format --verify-no-changes --severity warn
```

Four checks are easy to trip and worth knowing about in advance:

- **Public members need XML documentation.** It is a build gate, not a warning.
- **Regenerate after bumping the submodule.** `tools/generate-bindings.ps1` and
  `tools/dump-abi.ps1` both write files that CI compares against the headers, and
  a bump without a regenerate fails the build. That is the point of them.
- **Do not add a benchmark over a settled scene.** Box3D skips sleeping bodies,
  so it measures nothing. Set `EnableSleep = false`, and state what the scene
  should be doing with `Workload.RequireAwake` — a benchmark that stops doing
  real work now fails rather than quietly getting faster.
- **Do not allocate on a hot path.** `AllocationTests` measures the documented
  ones with `GC.GetAllocatedBytesForCurrentThread` and requires exactly zero
  bytes, so a captured closure or a boxed enumerator fails the build.

CI also installs the packed `.nupkg` into a fresh project on all six platforms
and runs a simulation through it, which is the only check that exercises the
package rather than the repository. Reproduce it locally with:

```sh
dotnet pack --configuration Release --output artifacts/packages
pwsh tools/verify-package.ps1
```

Changes to the public API are checked against the last published package
automatically. A break is allowed before 1.0, but it belongs in the changelog
rather than in someone's build log.

## Upgrading Box3D

The engine is a pinned, unmodified submodule.

```sh
git -C external/box3d checkout <commit>
pwsh tools/generate-bindings.ps1     # re-emit the P/Invokes and record the commit
pwsh tools/dump-abi.ps1              # re-record the struct layouts
dotnet test -c Release
```

Read both diffs. A changed offset in `abi/native-layout.json` means a struct
moved and its managed mirror has to move with it; the tests will say which.

## License

MIT. See [LICENSE](LICENSE). Box3D is likewise MIT licensed and is redistributed
unmodified as a native binary, with its copyright notice intact.
