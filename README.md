# Box3D.NET

An idiomatic, allocation-free C# binding for [Box3D](https://github.com/erincatto/box3d),
the 3D physics engine by Erin Catto.

> **Status: pre-release.** The native binding is complete and the high-level API
> covers worlds, bodies, shapes, queries and events. Joints, the character mover
> and debug draw are still only reachable through the low-level layer. No package
> has been published yet. See the [changelog](CHANGELOG.md).

```csharp
using var world = new PhysicsWorld(WorldSettings.Default with
{
    Gravity = new Vector3(0.0f, -9.81f, 0.0f),
});

Body ground = world.CreateBody(BodyDefinition.Static(new Vector3(0.0f, -0.5f, 0.0f)));
ground.AddBox(new Box(new Vector3(50.0f, 0.5f, 50.0f)));

Body ball = world.CreateBody(BodyDefinition.Dynamic(new Vector3(0.0f, 10.0f, 0.0f)));
ball.AddSphere(new Sphere(0.5f));

for (int frame = 0; frame < 120; frame++)
{
    world.Step(1.0f / 60.0f);
}

Console.WriteLine(ball.Position);
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

## License

MIT. See [LICENSE](LICENSE). Box3D is likewise MIT licensed and is redistributed
unmodified as a native binary.
