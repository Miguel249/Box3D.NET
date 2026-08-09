# Debug draw

Box3D can draw what it is simulating — shapes, contacts, joints, bounds, islands
— into whatever renderer you already have. It knows nothing about OpenGL,
Vulkan, Unity or Godot: it hands over points, segments and boxes in world space
and you decide what a line looks like.

Two interfaces, and a drawn frame allocates nothing.

## The two halves

| | Called | Job |
| --- | --- | --- |
| [`IDebugShapeFactory`](../api/Box3D.IDebugShapeFactory.yml) | once per shape | Turn a shape into a drawable and return an opaque handle |
| [`IDebugDrawer`](../api/Box3D.IDebugDrawer.yml) | every frame | Receive the handles with their transforms, plus every other primitive |

The split is what makes debug draw usable rather than a slideshow: a real
renderer uploads a mesh once in the factory and issues a draw call per frame in
the drawer.

The factory has to be supplied when the world is built, because Box3D needs
those callbacks at construction time:

```csharp
var factory = new MyShapeFactory();
using var world = new PhysicsWorld(WorldSettings.Default, factory);
```

`DestroyShape` is called when a shape is modified or destroyed, and for
everything still alive when the world is disposed. Neither factory method may
touch the world.

## Drawing a frame

```csharp
var drawer = new MyDrawer(renderer);

world.Draw(ref drawer, DebugDrawOptions.Default with
{
    DrawShapes = true,
    DrawJoints = true,
});
```

Implement the drawer on a **struct** passed by `ref`. The calls arrive through
function pointers with no delegate, no closure and no boxing, so the JIT can
inline them and the frame allocates nothing — provided your implementation does
not allocate either.

Every method is called synchronously, on the thread that called `Draw`, and none
of them may touch the world being drawn. Reading or writing the simulation from
inside a draw callback is the same race as doing it mid-step.

A drawer that has no shape factory can leave `DrawShape` empty and still get
every segment, point and box.

## What can be drawn

`DebugDrawOptions.Default` has everything off. Turn on what you want to see:

| Option | Shows |
| --- | --- |
| `DrawShapes` | The geometry itself, through the factory |
| `DrawBounds` | Broad-phase bounding boxes |
| `DrawMass` | Centres of mass, with the mass as text |
| `DrawSleep` | Which bodies are asleep |
| `DrawJoints`, `DrawJointExtras`, `DrawAnchorA` | Joint frames, and how hard they are working |
| `DrawContacts`, `DrawContactNormals`, `DrawContactFeatures`, `DrawContactForces` | What the solver is working with |
| `DrawIslands`, `DrawGraphColors` | How the solver has partitioned the scene |
| `DrawBodyNames` | The `Name` from each body's definition |

`ForceScale` and `JointScale` set how long the force and joint markers are;
`Bounds` clips drawing to a region, which is how you draw only what the camera
can see.

## Drawing part of a world

`CategoryMask` restricts a call to some [collision
categories](collision-filtering.md), and the options apply to the whole call, so
seeing annotations on some bodies and not others means two calls:

```csharp
// Everything, plainly.
world.Draw(ref drawer, DebugDrawOptions.Default with { DrawShapes = true });

// Contacts and bounds, but only for the dynamic bodies.
world.Draw(ref drawer, DebugDrawOptions.Default with
{
    DrawContacts = true,
    DrawContactNormals = true,
    DrawBounds = true,
    CategoryMask = (ulong)Layers.Dynamic,
});
```

Without the mask, the floor's bounding box — eighty metres across — is the only
thing in the picture.

## Text and colour

Box3D emits labels through `DrawString`: the mass over a body, the separation at
a contact point. It uses the ninety-five printable ASCII characters and nothing
else, so a bitmap font is enough.

[`DebugColor`](../api/Box3D.DebugColor.yml) is the suggested colour, packed as
`0xMMRRGGBB` — the top byte is a
[`DebugMaterial`](../api/Box3D.DebugMaterial.yml) hint, and `ToUnitRgb` gives the
three floats a shader wants. Suggested is the operative word: a drawer is free
to ignore it.

## A worked implementation

`src/Box3D.NET.Visualizer` is a complete drawer: a software rasterizer with its
own PNG and GIF writers, no dependencies beyond the base class library, using
`Box3D.NET` through these two interfaces and no privileged access. Every picture
in the [gallery](../gallery.md) comes out of it.

The smaller `debug-draw` [sample](../examples.md) is the shorter read if all you
want is the shape of the code.
