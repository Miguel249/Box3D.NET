# Terrain and meshes

Three kinds of geometry exist for large static scenery: height fields, triangle
meshes and baked compounds. All three are **static only** — Box3D generates
their contacts against static bodies — and all three are **borrowed** rather
than copied.

> [!IMPORTANT]
> A shape holds a pointer into the geometry it was built from. Dispose the world
> first, then the geometry. Doing it the other way round is a use-after-free
> inside the solver, not an exception. See
> [Memory and ownership](../concepts/ownership.md).

```csharp
using var terrain = HeightField.FromHeights(heights, 256, 256, scale);

using (var world = new PhysicsWorld())
{
    world.CreateStaticBody().AddHeightField(terrain);
    Simulate(world);
}
// World disposed here, terrain after. Never the other way round.
```

## Choosing between them

| | Describes | Cost | Use for |
| --- | --- | --- | --- |
| [`HeightField`](../api/Box3D.HeightField.yml) | one height per grid point | smallest, queries fastest | Outdoor terrain |
| [`CollisionMesh`](../api/Box3D.CollisionMesh.yml) | arbitrary triangles | one entry per triangle | Buildings, ramps, anything with an underside |
| [`CompoundGeometry`](../api/Box3D.CompoundGeometry.yml) | many primitives baked into one | one broad-phase proxy for the lot | Rock fields, colonnades, clutter |

A height field cannot describe a cave or an overhang. That is the whole
trade-off: it is a height map, and in exchange it stores one number per grid
point instead of triangles.

## Height fields

```csharp
using var terrain = HeightField.FromHeights(
    heights,
    columnCount: 256,      // grid lines along x
    rowCount: 256,         // grid lines along z
    scale: new Vector3(1.0f, 100.0f, 1.0f));   // 1 m cells, 100 m of relief

Body ground = world.CreateStaticBody();
ground.AddHeightField(terrain);
```

Three things about height fields surprise people:

**They start at the body's origin** and extend into positive x and z rather than
being centred. A 64 by 64 grid of two-metre cells covers 0 to 128 on both axes.
Position the body to place it, or read `Bounds` to find out where it ended up.

**Heights are quantized** against a minimum and maximum. Two fields that must
line up along an edge have to be built with the same `minimumHeight` and
`maximumHeight`, or their steps differ and a seam appears.

**Holes are a material.** Passing `HeightField.HoleMaterial` for a cell removes
it, which is how you cut a cave mouth or a shaft into otherwise solid terrain.

`HeightField.Grid` and `HeightField.Wave` build test terrain without a height
map, which is what most of the [samples](../examples.md) use.

## Triangle meshes

```csharp
using var level = CollisionMesh.FromTriangles(vertices, indices);

Body geometry = world.CreateStaticBody();
geometry.AddMesh(level);
geometry.AddMesh(level, scale: new Vector3(-1.0f, 1.0f, 1.0f));   // mirrored
```

Indices are three per triangle, wound counter-clockwise seen from the side the
surface faces. **Winding decides which side is solid**, so a mesh built the wrong
way round lets bodies fall through from above while stopping them from below.
The inputs are copied, so the arrays can be released as soon as the call
returns.

[`MeshOptions`](../api/Box3D.MeshOptions.yml) controls how the mesh is prepared:

| Option | Effect |
| --- | --- |
| `WeldVertices`, `WeldTolerance` | Merge vertices that coincide, so shared edges are recognised |
| `IdentifyEdges` | Mark internal edges, which stops bodies catching on triangle boundaries |
| `UseMedianSplit` | A faster build with a slightly worse tree |

`MeshOptions.Fast` is the preset for content built at run time, where build time
matters more than query time. `DegenerateTriangleCount` reports how many
triangles the build had to discard — a non-zero count on content you exported
means the exporter did something you did not intend.

The `Grid`, `Wave`, `HollowBox` and `BoxMesh` factories build meshes directly,
which saves writing vertex arrays by hand in tests and samples.

## Baked compounds

A compound bakes many primitives into a single shape with one broad-phase proxy:

```csharp
using CompoundGeometry colonnade = new CompoundBuilder()
    .AddMesh(plinth, Vector3.Zero)
    .AddHull(column, new Vector3(0.0f, 0.5f, 0.0f))
    .AddCapsule(lintel)
    .AddSphere(capital)
    .Build();

Body scenery = world.CreateStaticBody();
scenery.AddCompound(colonnade);
```

The whole compound arrives as one shape: one filter, one set of events, one
user data, one colour when drawn. Per-child materials are fixed when it is
baked, and the children cannot be told apart from outside.

That is the trade. For geometry that has to be told apart, or on a body that
moves, [attach shapes one at a time](shapes.md#several-shapes-on-one-body)
instead.

## Measuring what they cost

All three expose `ByteCount`, which reports what the engine actually allocated:

```csharp
Console.WriteLine($"{terrain.ByteCount / 1024} KB");
```

That is the honest way to decide between a height field and the equivalent mesh
for your own content, rather than taking a rule of thumb on trust.
