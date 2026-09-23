# Characters

Box3D.NET gives you the three engine-side primitives a kinematic character
controller needs, and stops there:

```csharp
// 1. What is the capsule touching?
var gather = new GatherPlanes { Planes = buffer };
world.CollideCapsule(capsule, position, ref gather);

Span<CollisionPlane> planes = buffer.AsSpan(0, gather.Count);

// 2. Where can it actually go?
PlaneSolverResult result = CharacterMover.SolvePlanes(velocity * dt, planes);
position += result.Translation;

// 3. Do not accumulate speed into a wall.
velocity = CharacterMover.ClipVelocity(velocity, planes);
```

Walk speed, jump height, what counts as ground, whether a slope is climbable —
that is game design, and every game answers it differently. Wrapping an opinion
about it here would be inventing policy Box3D deliberately left to the caller.

`CharacterControllerSample` builds a complete controller on these three calls in
about eighty lines, with gravity, jumping, ground detection, slope limits and
wall sliding. Copy it and change the parts that are yours.

## Gathering contacts

[`CollideCapsule`](../api/Box3D.PhysicsWorld.yml) reports every surface the capsule is
touching, through a struct callback so that gathering them every frame allocates
nothing:

```csharp
struct GatherPlanes : ICharacterCollisionCallback
{
    public CollisionPlane[] Planes;
    public int Count;

    public bool OnContact(in CharacterContact contact)
    {
        if (Count < Planes.Length)
        {
            Planes[Count++] = CollisionPlane.From(contact);
        }

        return true;   // false stops gathering
    }
}
```

The capsule is given relative to `origin`, which is the character's world
position. A [`CharacterContact`](../api/Box3D.CharacterContact.yml) carries the shape,
the plane normal, the offset and the contact point — **relative to the query
origin, not in world space**, which is the detail that makes a debug marker
appear in the wrong place the first time.

It also says what was touched. `TriangleIndex` names the mesh or height field
triangle, `ChildIndex` the child of a compound, and `MaterialIndex` the material
of the shape at that point — the way to tell ice from stone on a compound built
from both. All three are zero for shapes they do not apply to, rather than minus
one as on a ray hit. Mesh and height field triangles are one-sided here: a
capsule touching one from behind reports nothing.

## Solving

[`SolvePlanes`](../api/Box3D.CharacterMover.yml) finds the translation closest to the
one requested that satisfies every plane. That is what makes a character slide
along a wall instead of stopping dead against it, and what keeps it out of the
corner where two walls meet.

The solver writes back into the planes it was given: each one's `Push` reports
how far it had to move along that plane. `ClipVelocity` then projects the
velocity onto the planes that resisted, skipping the ones with no push. Without
that step a character walking into a wall keeps building velocity into it and
shoots sideways the moment the wall ends.

## Tuning what a plane means

[`CollisionPlane`](../api/Box3D.CollisionPlane.yml) is where policy goes:

```csharp
CollisionPlane plane = CollisionPlane.From(contact);

if (plane.Normal.Y < SlopeLimit)
{
    plane = plane with { ClipsVelocity = false };   // steep: do not slide down it
}

plane = plane with { PushLimit = maxStepHeight };   // do not be shoved further than this
```

`PushLimit` bounds how far the solver may move the character along that plane,
and `ClipsVelocity` decides whether the plane takes part in velocity clipping at
all. Between them they cover step height, slope limits and one-way surfaces.

## Casting instead of colliding

```csharp
float fraction = world.CastCapsule(capsule, position, move);
```

A specialised shape cast that slides along surfaces instead of catching on them.
Good for a quick "can the character get there", poor as a source of information
about what it is touching — use `CollideCapsule` for that.

## Being struck

A kinematic character is not simulated, so nothing tells it when a falling crate
or a swinging door hits it. [`TimeOfImpact`](../api/Box3D.CharacterMover.yml) sweeps a
body between two poses against the capsule's own movement over the same
interval:

```csharp
Vector3 crateStart = crate.Position;
Quaternion crateStartRotation = crate.Rotation;

world.Step(dt);

CharacterImpact impact = CharacterMover.TimeOfImpact(
    crate, capsule, position, velocity * dt,
    crateStart, crateStartRotation, crate.Position, crate.Rotation);
```

Sweep before solving the character's own movement for the frame. Once the
character has been pushed clear of the body the two only touch, and a touch is
not an impact. Only the sphere, capsule and hull shapes on the body take part.

## Choosing a body

A character is usually a **kinematic** body: you own its position, and it pushes
dynamic bodies without being pushed back. Move it with `MoveTowards` so that its
contacts carry what stands on it, never with `SetTransform`.

If the character is a dynamic body instead, lock its rotation so it cannot tip
over:

```csharp
character.MotionLocks = MotionLocks.NoRotation;
```

Either way, turn off contact recycling on the character's body:

```csharp
var def = BodyDefinition.Kinematic(spawn) with { EnableContactRecycling = false };
```

Recycling reuses contact manifolds across small movements and is a performance
win, but it can produce ghost collisions. On a character a snagged step is more
noticeable than the cost.
