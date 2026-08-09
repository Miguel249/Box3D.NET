# API reference

Three namespaces, and you almost certainly want the first.

| Namespace | What it is |
| --- | --- |
| `Box3D` | The idiomatic surface. Start at `PhysicsWorld`, then `Body` and `Shape`. |
| `Box3D.Native` | A literal mirror of the Box3D C API: the same names, the same signatures, no abstraction. |
| `Box3D.Interop` | The one sanctioned bridge between the two, as extension methods. |

Everything in `Box3D` validates its input, manages nothing you did not ask it to
manage, and allocates nothing on the simulation path.

Nothing in `Box3D.Native` validates anything or manages a lifetime. Passing an
invalid identifier crashes the process rather than raising an exception. Reach
for it when you need one of the roughly 580 exported functions the idiomatic
surface does not cover yet — [the native layer](../concepts/native-layer.md)
explains the boundary, and [API coverage](../api-coverage.md) lists what is on
each side of it.

Importing `Box3D.Interop` is what makes reaching for the C layer visible in your
own source.

## Where to start

| Looking for | Type |
| --- | --- |
| Creating and stepping a world | [`PhysicsWorld`](Box3D.PhysicsWorld.yml) |
| A physical object | [`Body`](Box3D.Body.yml), [`BodyDefinition`](Box3D.BodyDefinition.yml) |
| Collision geometry | [`Shape`](Box3D.Shape.yml), [`ShapeDefinition`](Box3D.ShapeDefinition.yml) |
| Ray casts and overlaps | [`RaycastHit`](Box3D.RaycastHit.yml), [`IRaycastCallback`](Box3D.IRaycastCallback.yml) |
| What happened last step | [`WorldEvents`](Box3D.WorldEvents.yml) |
| Constraints | [`Joint`](Box3D.Joint.yml) and the nine specific handles |
| Terrain | [`HeightField`](Box3D.HeightField.yml), [`CollisionMesh`](Box3D.CollisionMesh.yml) |
| Character movement | [`CharacterMover`](Box3D.CharacterMover.yml) |
| Drawing the simulation | [`IDebugDrawer`](Box3D.IDebugDrawer.yml) |
