# Examples

Sixteen runnable samples in `src/Box3D.NET.Samples`, each headless,
self-checking and small enough to read in one sitting. They assert on their own
results rather than only printing, so CI runs them — published with NativeAOT —
and a regression fails the build instead of producing plausible output nobody
reads.

```sh
dotnet run --project src/Box3D.NET.Samples -- --list      # what there is
dotnet run --project src/Box3D.NET.Samples -- raycast     # run one
dotnet run --project src/Box3D.NET.Samples                # run all of them
```

## Start here

| Sample | Teaches | Guide |
| --- | --- | --- |
| `basic-world` | A world, a body, a shape, a step | [Getting started](getting-started.md) |
| `dynamic-body` | Gravity acting on a falling body | [Bodies](guides/bodies.md) |
| `collision` | A falling box landing on static ground | [Bodies](guides/bodies.md) |

## Reacting to the simulation

| Sample | Teaches | Guide |
| --- | --- | --- |
| `raycast` | Closest-hit and callback ray casts | [Queries](guides/queries.md) |
| `contact-events` | Reading contacts after a step | [Events](guides/events.md) |
| `sensor` | A trigger volume that reports overlaps without colliding | [Events](guides/events.md#sensors) |
| `entities` | Associating game objects with bodies through user data | [Getting started](getting-started.md#get-the-results-back) |

## Geometry

| Sample | Teaches | Guide |
| --- | --- | --- |
| `compound` | Several shapes on one body, and many baked into one | [Shapes](guides/shapes.md) |
| `mesh` | Collision against a triangle mesh | [Terrain and meshes](guides/terrain.md) |
| `height-field` | Terrain from a height map | [Terrain and meshes](guides/terrain.md) |
| `continuous` | A fast body that would otherwise tunnel through a wall | [The simulation step](concepts/step.md#continuous-collision) |

## Joints

| Sample | Teaches | Guide |
| --- | --- | --- |
| `hinged-door` | A revolute joint with limits | [Joints](guides/joints.md) |
| `chain` | A hanging chain of revolute joints | [Joints](guides/joints.md) |
| `vehicle` | A wheeled vehicle built from wheel joints | [Joints](guides/joints.md) |

## The rest

| Sample | Teaches | Guide |
| --- | --- | --- |
| `character` | A kinematic character walking, sliding and climbing | [Characters](guides/characters.md) |
| `debug-draw` | Feeding the world's debug geometry to a renderer | [Debug draw](guides/debug-draw.md) |

`character` is the one to copy. It builds a complete controller — gravity,
jumping, ground detection, slope limits, wall sliding — on the three mover
primitives in about eighty lines.

## Seeing them run

The [gallery](gallery.md) renders nine scenes to animated GIFs through the
public debug draw interface. Same library, same API, a renderer with no
privileged access.
