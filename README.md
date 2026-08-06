# Box3D.NET

An idiomatic, allocation-free C# binding for [Box3D](https://github.com/erincatto/box3d),
the 3D physics engine by Erin Catto.

> **Status: pre-release.** The native binding is complete, and the high-level API
> covers worlds, bodies, shapes, queries, events and all nine joint types. The
> character mover, debug draw, meshes and height fields are still only reachable
> through the low-level layer. No package has been published yet. See the
> [changelog](CHANGELOG.md).

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

### Handles are value types, not `SafeHandle`

Box3D identifiers are small structs holding an index and a generation counter,
not pointers. Wrapping each one in a `SafeHandle` would put a finalizable heap
object behind every body in the simulation. The generation counter already
detects stale handles, which is the safety a `SafeHandle` would have bought.

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
world.CastRay(muzzle, aim * 100.0f, ref callback);
```

The query is generic over the callback type, so the JIT specializes it and
devirtualizes the call: your code is inlined into the dispatcher with no
delegate allocation, no boxing, and nothing to keep alive across the native
transition. A delegate-based API would allocate a closure on every cast.

For the common case there is `world.CastRayClosest(...)`, which needs no
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

## Building

Requires the .NET 8 SDK or later, CMake 3.22 or later, and a C compiler.

```sh
git clone --recursive https://github.com/box3d-net/Box3D.NET
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
| `LayoutTests` | The size and layout of every public struct, against values derived from the C declarations. Runs without a native binary. |
| `MathTests` | The math ported from the `B3_INLINE` functions, by algebraic identity and by agreement with `System.Numerics`. |
| `NativeInteropTests` | The binding against the real library: default definitions come back intact, bodies fall, rays hit, and `b3GetByteCount` returns to its starting value after worlds and hulls are destroyed. |
| `JointTests` | Joint behaviour, not round trips: limits actually hold, motors actually lift, filter joints actually let bodies through. |
| `LayeringTests` | That no `Box3D.NET.Native` type reaches the public surface, checked by reflection over the built assembly. |
| `UserDataTests` | Identifiers survive the round trip through the native `void*`, including the top bit, and come back from events and queries. |

Tests that call into Box3D share a non-parallel xUnit collection. The library
keeps process-wide state — the allocated byte count, the live world count — so a
leak test running beside another class that creates worlds is measuring noise.

## Performance

Measured, not asserted. See [docs/benchmarks.md](docs/benchmarks.md).

| | Native | Wrapper | Allocated |
| --- | ---: | ---: | ---: |
| Read a body position | 7.892 ns | 7.886 ns | 0 B |
| Ray cast, closest hit over 200 shapes | — | 166.6 ns | 0 B |
| Ray cast with a struct callback | — | 163.2 ns | 0 B |
| Create 1000 bodies with spheres | 710.6 µs | 799.3 µs | 0 B |

Reading a position through the wrapper costs what calling the C function costs.
Queries allocate nothing, including the callback forms.

## License

MIT. See [LICENSE](LICENSE). Box3D is likewise MIT licensed and is redistributed
unmodified as a native binary.
