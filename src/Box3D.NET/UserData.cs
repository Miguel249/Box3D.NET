// SPDX-License-Identifier: MIT

using Box3D.Native;

namespace Box3D;

/*
 * User data
 * ---------
 * Box3D stores a void* on each world, body, shape and joint that it never
 * interprets. This library exposes it as a ulong rather than as an object.
 *
 * The alternative would be to pin a managed object with a GCHandle and store
 * that pointer. It reads better in object-oriented code, and it is the wrong
 * trade here:
 *
 *   - Lifetime. The handle has to be freed when the body is destroyed, and a
 *     body can be destroyed by destroying its world, which means the world has
 *     to track every handle it ever handed out. Miss one and it is a leak the
 *     GC cannot see.
 *   - Cost. Every association becomes a pinned heap object.
 *   - Fit. Engines written this decade keep component data in parallel arrays
 *     or an ECS and refer to entities by index. An index is what they want back
 *     out of a contact event, not a reference.
 *
 * A ulong covers the index case exactly, costs nothing, cannot leak, and stays
 * valid across a world reload in a way a pointer does not. Anyone who genuinely
 * needs an object reference can keep a Dictionary<Body, T> - Body is a proper
 * value type with equality and a hash code - or index into their own array.
 *
 * The one thing this must not become is a place to stash a raw pointer. That is
 * why it is typed ulong and documented as an identifier: a pointer stored here
 * outlives nothing and is not tracked by the GC.
 */

/// <summary>
/// Application data attached to physics objects.
/// </summary>
/// <remarks>
/// <para>
/// Every world, body, shape and joint carries one <see cref="ulong"/> that
/// Box3D stores and never interprets. It is the intended way to get from a
/// physics object back to whatever your game calls it.
/// </para>
/// <para>
/// Treat it as an identifier: an entity id, an index into your own array, a
/// handle from your own table. It is deliberately not an object reference, so
/// there is nothing to keep alive and nothing to free.
/// </para>
/// </remarks>
/// <example>
/// The pattern this is designed for. Physics reports what moved, and the
/// identifier indexes straight into the application's own storage:
/// <code>
/// Body body = world.CreateDynamicBody(spawnPoint);
/// body.UserData = entityId;
///
/// world.Step(1.0f / 60.0f);
///
/// foreach (BodyMoveEvent moved in world.Events.BodyMoves)
/// {
///     ref Transform transform = ref transforms[moved.Body.UserData];
///     transform.Position = moved.Position;
///     transform.Rotation = moved.Rotation;
/// }
/// </code>
/// Working out which entity a ray hit:
/// <code>
/// RaycastHit hit = world.RaycastClosest(muzzle, aim * range);
/// if (hit.Hit)
/// {
///     ulong entity = hit.Shape.Body.UserData;
///     Damage(entity, amount: 25);
/// }
/// </code>
/// </example>
public static class UserData
{
    // A ulong and a void* are both 64 bits on every platform this library
    // supports, so the value round-trips exactly. The casts are the whole
    // implementation; they live here so the reasoning above has one home.
    internal static unsafe ulong FromPointer(void* pointer) => (ulong)pointer;

    internal static unsafe void* ToPointer(ulong value) => (void*)value;
}
