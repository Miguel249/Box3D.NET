# Threading and determinism

The short version: **one world, one thread.**

## What is safe

Each row is what the threading tests actually exercise, rather than what would
be convenient to promise.

| Operation | Safe concurrently |
| --- | :---: |
| `Step` on **different** worlds | yes |
| Reads and queries on **different** worlds | yes |
| `new PhysicsWorld(...)` and `Dispose` from several threads | yes, serialised by this library |
| Two threads reading one world, nothing stepping | yes |
| Passing a `Body`, `Shape` or `Joint` between threads | yes, the handle is a value |
| Anything on **one** world while that world is stepping | **no** |
| Two threads mutating one world | **no** |
| Reading `Events` while that world is stepping | **no** |

A `PhysicsWorld` is not internally synchronised, and deliberately so: a lock
around `Step` would cost every user in order to protect a pattern the engine
does not support anyway. Give each world an owner, and hand results to other
threads afterwards.

## Two things worth knowing

**World creation and destruction are guarded here, not by Box3D.** The engine
keeps its worlds in one global table and picks a slot by scanning for a free
entry, then marks it in use some thirty lines later; nothing synchronises the
two. Two threads creating a world at the same time can select the same slot, and
the corrupted world then spins forever inside `Step` rather than failing
outright. Box3D's own documentation says to hold a mutex around those calls, so
`PhysicsWorld` holds one. It covers only the two native calls, so `Step`,
queries and body edits never touch it. Code calling `b3CreateWorld` through
[the native layer](native-layer.md) directly is outside that guard and on its
own.

**A world may already be using several threads.** `WorkerCount` above one lets
Box3D start and own worker threads for the solver:

```csharp
using var world = new PhysicsWorld(WorldSettings.Default with { WorkerCount = 4 });
```

That is internal parallelism inside one `Step` on one calling thread. It does
not make the world safe to touch from your threads, and it is not additive with
running several worlds at once — the cores have to come from somewhere. Box3D
performs best on performance cores sharing one L2 cache; efficiency cores and
hyper-threading add little and can cost.

## Determinism

Box3D is written for cross-platform determinism, which is what makes lockstep
networking and replay possible. This binding's job is not to undermine it.
`DeterminismTests` hashes the exact bits of every body's position and velocity
after a fixed number of steps and requires equality, not closeness.

Verified on every CI platform:

- the same scene run twice, and run many times, gives identical bits;
- other worlds existing alongside it change nothing;
- interleaving two worlds step by step changes neither;
- a query between steps changes nothing;
- reading events changes nothing;
- a multithreaded world is reproducible against itself.

**Not** verified, and therefore not claimed: that two *different* platforms or
architectures produce identical bits for the same scene. That is a property of
Box3D and of the compiler it was built with, not of this binding, and proving it
would mean comparing hashes across the CI matrix rather than within each runner.
Until that exists, treat cross-platform determinism as Box3D's claim rather than
as this package's.

Determinism also depends on things you control: a fixed time step, a fixed
sub-step count, and the same sequence of operations. A variable time step makes
a simulation non-reproducible no matter what either layer does. See
[the simulation step](step.md).
