# Benchmarks

What the idiomatic layer costs over calling the C API directly.

Reproduce with:

```sh
dotnet run -c Release --project src/Box3D.NET.Benchmarks
dotnet run -c Release --project src/Box3D.NET.Benchmarks -- --filter "*Overhead*"
```

Figures below are from Windows 11 x64, .NET 10, Box3D built with GCC 14.2 at
`-O2`. Absolute numbers depend on the machine; the ratios are the point.

## Per-operation overhead

Each pair runs the same operation twice, once through each layer, so the
difference is the wrapper and nothing else.

| Operation | Native | Wrapper | Ratio | Allocated |
| --- | ---: | ---: | ---: | ---: |
| Read body position | 7.892 ns | 7.886 ns | **1.00** | 0 B |
| Write linear velocity | 6.044 ns | 6.155 ns | **1.02** | 0 B |
| Apply force to centre | 6.192 ns | 6.691 ns | **1.08** | 0 B |

Reading a position through `body.Position` costs the same as calling
`b3Body_GetPosition` by hand. The property is a direct call with no marshalling,
so there is nothing left to remove.

## Spatial queries

| Query | Time | Allocated |
| --- | ---: | ---: |
| `RaycastClosest` over 200 shapes | 166.6 ns | **0 B** |
| `Raycast` with a struct callback, nearest hit | 163.2 ns | **0 B** |
| `Raycast` with a struct callback, all 200 hits | 17.0 µs | **0 B** |
| `OverlapBox` over 200 shapes | 1.72 µs | **0 B** |

The callback form is not slower than the callback-free convenience method: the
query is generic over the callback type, so the JIT specializes it and inlines
the user's `OnHit` into the dispatcher. A delegate-based API would have
allocated a closure on every one of these calls.

## Bulk creation

Creating 1000 dynamic bodies, each with a sphere attached.

| | Time | Ratio | Allocated |
| --- | ---: | ---: | ---: |
| Native | 710.6 µs | 1.00 | 0 B |
| Wrapper, definitions hoisted out of the loop | 799.3 µs | **1.13** | 0 B |
| Wrapper, definitions written inline | 796.3 µs | **1.12** | 0 B |

The two wrapper rows being identical is the interesting part. They were not
always: definitions used to fetch the engine defaults on construction *and*
again on conversion, so creating a body was three P/Invokes rather than one, and
hoisting the definition out of the loop was worth real time. The defaults are
now read once into `NativeDefaults` and copied thereafter, so writing the
natural code costs the same as the careful version.

The remaining 13% is the per-body work the wrapper genuinely does: copying the
definition struct and mapping its fields. It buys the API that makes the
difference between the two columns invisible.

## What is measured, and what is not

`StepBenchmarks` measures `World.Step` over piles of 100 and 1000 boxes. It is
included for scale rather than as a comparison: the step is Box3D's work, the
wrapper contributes one P/Invoke to it, and at hundreds of microseconds per step
that call is unmeasurable. It is the reason the overhead above matters so
little in a real frame.

Not yet measured: joints under load, contact event throughput at scale, and
multi-threaded stepping. Those belong with the stress and threading suites.
