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
| Read body position | 7.897 ns | 8.104 ns | **1.03** | 0 B |
| Write linear velocity | 6.396 ns | 6.651 ns | **1.04** | 0 B |
| Apply force to centre | 6.273 ns | 6.776 ns | **1.08** | 0 B |

Reading a position through `body.Position` costs what calling
`b3Body_GetPosition` by hand costs. The property is a direct call with no
marshalling, so there is nothing left to remove.

## What the input validation costs

The wrapper rejects NaN and infinity on every path that can reach the solver.
This is not free, so it was priced before it went in: the benchmark runs the
native call twice, once plain and once with the same finite check written out by
hand.

| | Time | Difference |
| --- | ---: | ---: |
| Write velocity, native | 6.396 ns | — |
| Write velocity, native + finite check | 6.508 ns | **+0.11 ns** |

A tenth of a nanosecond, or under two percent of the call. What it buys:
Box3D validates its own inputs with assertions that release builds compile out,
so a single NaN is accepted in silence and then spreads. Measured directly —
setting one body's velocity to NaN and stepping thirty times left a second body,
twenty metres away and never touched, reading `(NaN, NaN, NaN)`. There is no way
to remove it from a world afterwards. `FuzzTests` pins both halves of this: that
the contamination is real, and that the guards stop it.

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
