# Handle validity

`Body`, `Shape`, `Joint` and the nine specific joint handles are small value
types holding an index and a generation counter — not pointers, not
`SafeHandle`s. Copy them, store them, compare them, put them in a dictionary.

The question this page answers is what happens when you use one after the thing
it refers to is gone.

## What the library does

Every member that dereferences a handle asks the engine whether it is still live
first, and throws `InvalidOperationException` if it is not:

```csharp
Body body = world.CreateDynamicBody(spawn);
body.Destroy();

Vector3 position = body.Position;   // InvalidOperationException
```

Without that check the same line reads freed memory. Measured on `win-x64`
against the shipped release build, before the checks went in:

```
default(Body).Position                 access violation, 0xC0000005
body.Position after body.Destroy()     access violation
body.Position after world.Dispose()    access violation
body.Destroy() twice                   access violation
handle whose index had been reused     returned the replacement body's
                                       position, in silence
```

The last one is the reason a generation counter is not enough on its own: no
crash, no exception, just another body's state reported as yours.

Box3D validates handles with assertions, and assertions are compiled out of the
release binary this package ships — which is why the check has to live here.

## Asking is always safe

`IsValid`, handle conversions such as `hinge.AsJoint`, equality and `ToString`
never throw. That makes them usable in exactly the place you need them: reading
events, where a handle may refer to something that was destroyed during the
step.

```csharp
foreach (ContactEndEvent touch in world.Events.ContactEnds)
{
    // An end-touch event is often raised *because* a shape was destroyed.
    if (touch.ShapeA.IsValid)
    {
        Handle(touch.ShapeA);
    }
}
```

## What it costs

`b3Body_IsValid` measures 2.07 ns against the 2.66 ns of the
`b3Body_GetPosition` it guards, timed over twenty million iterations. Reading a
body position through the wrapper costs 9.50 ns against the C API's 9.00 ns, and
that check is most of the difference.

That is the price of an exception with a stack trace instead of an access
violation. If you are reading thousands of properties per frame and have already
proved the handles are live, [the native layer](native-layer.md) does no
checking at all, by design.

## The one case that remains

A `b3BodyId` records which world *slot* it came from, but not that world's
generation. A handle held past `world.Dispose()` is therefore indistinguishable
from a handle into whatever world next occupies the slot, and nothing in the id
can separate them.

That belongs to Box3D rather than to this binding. `HandleSafetyTests` pins the
behaviour so it is a known boundary rather than a surprise, and the rule that
avoids it is simple:

> Handles do not outlive their world.

## Related

- [Memory and ownership](ownership.md) — who owns what, and the disposal order.
- [Events](../guides/events.md#begin-and-end-touch) — where invalid handles
  actually turn up.
