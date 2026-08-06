# Box3D.NET

An idiomatic, allocation-free C# binding for [Box3D](https://github.com/erincatto/box3d),
the 3D physics engine by Erin Catto.

> **Status: under construction.** The public API is not yet stable and no package
> has been published. See the [changelog](CHANGELOG.md) for what currently exists.

## Goals

- An API that reads like modern .NET, not like a C header.
- No allocations on the simulation hot path, no boxing, no reflection.
- .NET 8 or later, on Windows, Linux and macOS.
- Works under NativeAOT and trimming.
- No external dependencies beyond the base class library.

## Packages

| Package | What it is |
| --- | --- |
| `Box3D.NET` | The idiomatic surface. This is what you want. |
| `Box3D.NET.Native` | The raw P/Invoke layer, a one-to-one mirror of the C API. Use it if you need something the high-level API does not expose yet. |

## Design notes

**Vectors are `System.Numerics` types.** `b3Vec3` and `System.Numerics.Vector3`
have the same layout, as do `b3Quat` and `Quaternion`. Using the framework types
directly means no conversion at the boundary with your renderer or engine, and
the vector math gets the BCL's SIMD paths for free. A layout test guards the
assumption.

**Handles are value types, not `SafeHandle`.** Box3D identifiers are small
structs holding an index and a generation counter, not pointers. Wrapping each
one in a `SafeHandle` would put a finalizable heap object behind every body in
the simulation. The generation counter already detects stale handles, which is
the safety a `SafeHandle` would have provided.

**Single precision only.** Box3D's `BOX3D_DOUBLE_PRECISION` ("large world") mode
changes the ABI rather than being a runtime switch. This binding targets the
default single-precision build. Large-world support, if it lands, will be a
separate package.

## Building

Requires the .NET 8 SDK or later, CMake 3.22 or later, and a C compiler.

```sh
git clone --recursive https://github.com/box3d-net/Box3D.NET
cd Box3D.NET
dotnet build
```

Box3D itself lives in `external/box3d` as a submodule pinned to a specific
commit. It is never modified; this project only consumes it.

## License

MIT. See [LICENSE](LICENSE). Box3D is likewise MIT licensed and is redistributed
unmodified as a native binary.
