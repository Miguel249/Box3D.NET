# Benchmarks

What the idiomatic layer costs over calling the C API directly.

Reproduce with:

```sh
dotnet run -c Release --project src/Box3D.NET.Benchmarks
dotnet run -c Release --project src/Box3D.NET.Benchmarks -- --filter "*Overhead*"
```

Figures below were re-measured for 0.3.0 on Windows 11 x64, .NET 10.0.10,
BenchmarkDotNet 0.14.0, against a single-precision Release build of Box3D.
Absolute numbers depend on the machine; the ratios are the point.

Where 0.3.0's numbers differ from the ones this file carried for 0.2.0, the
difference is called out rather than quietly overwritten. Two of them did not
reproduce at all, which is the sort of thing a benchmark file exists to catch.

## Per-operation overhead

Each pair runs the same operation twice, once through each layer, so the
difference is the wrapper and nothing else.

| Operation | Native | Wrapper | Ratio | Allocated |
| --- | ---: | ---: | ---: | ---: |
| Read body position | 8.998 ns | 9.496 ns | **1.06** | 0 B |
| Write linear velocity | 6.879 ns | 7.796 ns | **1.13** | 0 B |
| Apply force to centre | 6.361 ns | 7.544 ns | **1.19** | 0 B |

Half a nanosecond to a nanosecond over the bare call. Since 0.3.0 that gap
includes the handle validity check, which is a second call into the library on
every one of these: `b3Body_IsValid` measures 2.07 ns against the 2.66 ns of the
`b3Body_GetPosition` it guards, timed directly over twenty million iterations
outside BenchmarkDotNet's harness. It buys the difference between an exception
and an access violation — see the handle section of the README.

## A frame, at three scales

The per-operation figures above measure a single call. This measures a whole
step, which is what a game actually pays, with both worlds built identically and
stepped the same way so that the only difference is which `Step` is called.

| Bodies | C API | Box3D.NET | Ratio | Allocated |
| ---: | ---: | ---: | ---: | ---: |
| 100 | 75.29 µs | 75.48 µs | **1.00** | 0 B |
| 1,000 | 746.82 µs | 745.40 µs | **1.00** | 0 B |
| 10,000 | 7,704.64 µs | 7,723.55 µs | **1.00** | 0 B |

The wrapper's overhead on a step is not measurable. Against a frame costing tens
of microseconds at the smallest size here, one P/Invoke does not register.

An earlier run of only this benchmark class reported 7,970 µs against 8,341 µs
at 10,000 bodies — a 4.6% gap. It did not reproduce in the full-suite run above,
and it could not have been real: the wrapper's `Step` does a disposed check,
three argument checks and one call, which cannot amount to 370 µs. It is
recorded here because a single run of a benchmark is a sample, not a
measurement, and this one would have been published as an overhead figure.

### Sleeping bodies measure nothing

Worth stating because it invalidated an earlier version of these benchmarks.
Box3D skips a body that has stopped moving, so a settled scene steps in roughly
constant time no matter how many bodies it holds:

| Bodies | Settled, sleep enabled | Awake |
| ---: | ---: | ---: |
| 100 | 214 ns | 75 µs |
| 1,000 | 221 ns | 747 µs |
| 10,000 | 204 ns | 7,705 µs |

The left column is the sleep check and nothing else — it does not respond to
body count at all, which is how the mistake is recognisable. Every step
benchmark here sets `EnableSleep = false` for that reason.

Since 0.3.0 that is no longer a convention to remember. Each benchmark states
what its scene should be doing and is held to it before anything is measured,
through `Workload.RequireAwake` and `Workload.RequireHits`. A scene that settles,
drifts apart or stops being hit now fails the benchmark instead of quietly
getting faster.

## What the input validation costs

The wrapper rejects NaN and infinity on every path that can reach the solver.
This is not free, so it was priced before it went in: the benchmark runs the
native call twice, once plain and once with the same finite check written out by
hand.

| | Time | Difference |
| --- | ---: | ---: |
| Write velocity, native | 6.879 ns | — |
| Write velocity, native + finite check | 6.705 ns | **−0.17 ns** |

The version with the check measured *faster* than the version without it, which
is impossible and is therefore the answer: three `float.IsFinite` tests on
values already in registers cost less than this harness can resolve. The 0.2.0
run put it at +0.11 ns, which is the same conclusion with the sign the other way
round. Either way it is below the noise floor, so the honest statement is that
the check is not measurable rather than that it costs some specific amount.

What it buys:
Box3D validates its own inputs with assertions that release builds compile out,
so a single NaN is accepted in silence and then spreads. Measured directly —
setting one body's velocity to NaN and stepping thirty times left a second body,
twenty metres away and never touched, reading `(NaN, NaN, NaN)`. There is no way
to remove it from a world afterwards. `FuzzTests` pins both halves of this: that
the contamination is real, and that the guards stop it.

## Spatial queries

The same ray, once through each layer, plus the callback forms.

| Query | Time | Ratio | Allocated |
| --- | ---: | ---: | ---: |
| `b3World_CastRayClosest` over 200 shapes | 175.4 ns | 1.00 | **0 B** |
| `RaycastClosest` over 200 shapes | 171.7 ns | **0.98** | **0 B** |
| `Raycast` with a struct callback, nearest hit | 168.9 ns | 0.96 | **0 B** |
| `Raycast` with a struct callback, all 200 hits | 17.4 µs | 99.3 | **0 B** |
| `OverlapBox` over 200 shapes | 1.75 µs | 10.0 | **0 B** |

A query through the wrapper costs what the C API costs; the ratio below 1.00 is
the noise floor rather than an achievement. The callback form is no slower than
the callback-free convenience method either: the query is generic over the
callback type, so the JIT specializes it and inlines the user's `OnHit` into the
dispatcher. A delegate-based API would have allocated a closure on every one of
these calls.

## Events

| | Time | Allocated |
| --- | ---: | ---: |
| Step 200 bodies, then drain every event list | 145.8 µs | **0 B** |
| Drain every event list, no step | 164.5 ns | **0 B** |

Draining is measured on its own as well because the step swamps it: reading all
six lists is a thousandth of the frame it belongs to. It is legitimate to read
them without stepping — the buffers belong to the world and stay valid until the
next `Step`, which is the documented lifetime.

## Bulk creation

Creating 1000 dynamic bodies, each with a sphere attached. This is where the
wrapper does the most work per call and the only place it costs something worth
naming.

| | Time | Ratio | Allocated |
| --- | ---: | ---: | ---: |
| Native | 725.2 µs | 1.00 | 0 B |
| Wrapper, definitions hoisted out of the loop | 1,066.4 µs | **1.48** | 0 B |
| Wrapper, definitions written inline | 1,192.7 µs | **1.65** | 0 B |

**This table replaces figures that did not reproduce.** For 0.2.0 it recorded
1.13 and 1.12, and said the two wrapper rows were identical. Neither holds on
this machine. Re-measured two ways — under BenchmarkDotNet as above, and under a
separate alternating timing harness — the hoisted row lands at 1.48 and 1.53 and
the inline row at 1.65 and 1.79.

That is not a 0.3.0 regression, and it was checked rather than assumed: the same
loop run against the **published 0.2.0 package** measures 1.54 here. The
validity check 0.3.0 adds accounts for 1 to 3% of it, measured by adding one
`b3Body_IsValid` per body to the native loop, which moved it from 1.00 to 1.01.

What the remaining half is: the native loop hoists `b3BodyDef` and `b3ShapeDef`
out of the loop and mutates one field of each per body. The wrapper rebuilds
both from `NativeDefaults` on every call, because a `BodyDefinition` is a value
that cannot know it is being used in a loop. Two large struct constructions per
body is the price of definitions being records rather than mutable buffers, and
the inline row is that price paid twice more, for the `BodyDefinition.Dynamic`
and `ShapeDefinition.Default` that the hoisted row builds once.

If creating tens of thousands of bodies in one frame is the workload, hoist the
definitions, or reach through `Box3D.Interop` and call `b3CreateBody` directly.
For anything else this is 340 nanoseconds per body against 725, on a code path
that runs when a level loads.

## What is measured, and what is not

`StepBenchmarks` measures `World.Step` over piles of 100 and 1000 boxes. It is
included for scale rather than as a comparison: the step is Box3D's work, the
wrapper contributes one P/Invoke to it, and at hundreds of microseconds per step
that call is unmeasurable. It is the reason the overhead above matters so
little in a real frame.

Not yet measured: joints under load, contact event throughput at scale, and
multi-threaded stepping. Those belong with the stress and threading suites.
