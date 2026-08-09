# Events

Box3D buffers what happened during a step and hands it back afterwards.
[`world.Events`](../api/Box3D.WorldEvents.yml) exposes six lists as views over engine
memory, so reading a frame's worth allocates nothing.

```csharp
world.Step(FixedStep);

foreach (ContactHitEvent hit in world.Events.ContactHits)
{
    PlayImpactSound(hit.Point, volume: hit.ApproachSpeed / 20.0f);
}
```

| List | Raised when | Needs |
| --- | --- | --- |
| `ContactBegins` | two shapes start touching | `EnableContactEvents` |
| `ContactEnds` | two shapes stop touching | `EnableContactEvents` |
| `ContactHits` | an impact above the world's hit threshold | `EnableHitEvents` |
| `SensorBegins` | a shape enters a sensor | `EnableSensorEvents` on both |
| `SensorEnds` | a shape leaves a sensor | `EnableSensorEvents` on both |
| `BodyMoves` | a body moved during the step | nothing |

## Events are opt-in

Collecting them is not free, so every list except `BodyMoves` is off until a
shape asks for it:

```csharp
var reporting = ShapeDefinition.Default with
{
    EnableContactEvents = true,
    EnableHitEvents = true,
};

body.AddBox(Box.Cube(0.5f), reporting);
```

Turn on what you read, and nothing else. A world where every shape reports every
contact spends real time filling lists nobody walks.

## What moved

`BodyMoves` is the list to drive rendering from. It contains only the bodies
that actually moved, so a settled scene produces nothing:

```csharp
foreach (BodyMoveEvent moved in world.Events.BodyMoves)
{
    ref Transform transform = ref transforms[moved.Body.UserData];
    transform.Position = moved.Position;
    transform.Rotation = moved.Rotation;

    if (moved.FellAsleep)
    {
        StopAnimating(moved.Body.UserData);
    }
}
```

`FellAsleep` is the last report you get about that body until something wakes
it, which makes it the natural place to release whatever you were spending on
it.

## Impacts

A hit event is what you want for impact sounds and damage. It carries the point,
the normal and the closing speed, plus each surface's
[`UserMaterialId`](shapes.md#density-and-material):

```csharp
foreach (ContactHitEvent hit in world.Events.ContactHits)
{
    Material surface = Materials[hit.UserMaterialIdA];
    PlayImpactSound(surface, hit.Point, hit.ApproachSpeed);
}
```

Only impacts above `WorldSettings.HitEventThreshold` are reported, which is what
keeps a settling stack from firing a hundred of them.

## Begin and end touch

`ContactBegins` and `ContactEnds` are for state: a foot on the ground, a card in
a slot, a fuse burning while two things touch.

An end-touch event is often raised *because* a shape was destroyed, so check
before using the handle:

```csharp
foreach (ContactEndEvent touch in world.Events.ContactEnds)
{
    if (touch.ShapeA.IsValid && touch.ShapeB.IsValid)
    {
        Separate(touch.ShapeA, touch.ShapeB);
    }
}
```

`IsValid` never throws, so asking is always safe. See
[Handle validity](../concepts/handles.md).

## Sensors

A sensor reports overlaps without pushing anything. Both the sensor and the
visitor need `EnableSensorEvents`:

```csharp
Body trigger = world.CreateStaticBody(doorway);
trigger.AddBox(volume, ShapeDefinition.Default with
{
    IsSensor = true,
    EnableSensorEvents = true,
    Density = 0.0f,
});

foreach (SensorBeginEvent entered in world.Events.SensorBegins)
{
    OpenDoor(entered.Sensor.UserData, by: entered.Visitor.Body.UserData);
}
```

Sensors have no continuous collision, so something fast enough to cross the
volume within one step passes through unreported. Use a
[shape cast](queries.md) for a trigger that has to catch a bullet.

## Lifetime

Events are valid only until the next `Step`. Read what you need before stepping
again; copy anything you want to keep.

It is safe to create and destroy bodies while walking the lists — that is the
whole reason Box3D buffers events instead of calling back mid-step. Be aware
that doing so can invalidate handles carried by events you have not read yet.
