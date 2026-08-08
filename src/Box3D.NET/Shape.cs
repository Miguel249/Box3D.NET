// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using Box3D.Native;

namespace Box3D;

/// <summary>
/// A piece of collision geometry attached to a <see cref="Body"/>.
/// </summary>
/// <remarks>
/// <para>
/// A shape is a handle, not an object: it is a small value identifying geometry
/// the world owns. Copying one is free and copies refer to the same shape.
/// Because it owns nothing it is not <see cref="IDisposable"/>; call
/// <see cref="Destroy"/> to remove it, and note that destroying the body or the
/// world removes its shapes too.
/// </para>
/// <para>
/// Handles carry a generation counter, so a handle to a destroyed shape is
/// detected rather than silently addressing whatever took its place. Use
/// <see cref="IsValid"/> after anything that might have destroyed it, in
/// particular when reading shapes out of end-touch events.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// Shape ground = body.AddBox(new Box(new Vector3(50.0f, 0.5f, 50.0f)));
/// ground.Friction = 0.8f;
///
/// // A shape from an event may already be gone.
/// foreach (ref readonly var touch in world.Events.ContactEnd)
/// {
///     if (touch.ShapeA.IsValid)
///     {
///         Handle(touch.ShapeA);
///     }
/// }
/// </code>
/// </example>
public readonly record struct Shape
{
    internal Shape(b3ShapeId id) => NativeId = id;

    /// <summary>Gets the native identifier this handle wraps.</summary>
    /// <remarks>
    /// Internal on purpose: exposing it would put a Box3D.Native type in the
    /// public surface and weld the C ABI to this API. Reach it through
    /// <c>Box3D.Interop.NativeInterop.ToNativeId</c> when you genuinely need the
    /// C function this layer does not wrap.
    /// </remarks>
    internal b3ShapeId NativeId { get; }

    /// <summary>Gets a value indicating whether this handle refers to a live shape.</summary>
    /// <remarks>
    /// False for a default-constructed handle and for one whose shape has been
    /// destroyed.
    /// </remarks>
    public bool IsValid => !NativeId.IsNull && B3.b3Shape_IsValid(NativeId);

    /*
     * Every member below reaches the shape through this rather than through
     * NativeId, so that a stale handle is rejected instead of being indexed
     * into a freed slot. See the note in Validate.
     */
    private b3ShapeId Id => Validate.Handle(NativeId);

    /// <summary>Gets the kind of geometry backing this shape.</summary>
    public ShapeType Type => (ShapeType)B3.b3Shape_GetType(Id);

    /// <summary>Gets the body this shape is attached to.</summary>
    public Body Body => new(B3.b3Shape_GetBody(Id));

    /// <summary>Gets the world this shape belongs to.</summary>
    public WorldReference World => new(B3.b3Shape_GetWorld(Id));

    /// <summary>
    /// Gets or sets the application identifier attached to this shape.
    /// </summary>
    /// <remarks>
    /// Separate from the body's identifier, which is what makes it useful:
    /// a body can carry the entity id while each of its shapes carries which
    /// part of that entity it is, so a hit can be attributed to the head rather
    /// than merely to the character. See <see cref="Box3D.UserData"/>.
    /// </remarks>
    /// <example>
    /// <code>
    /// head.UserData = (ulong)HitZone.Head;
    /// torso.UserData = (ulong)HitZone.Torso;
    ///
    /// RaycastHit hit = world.RaycastClosest(muzzle, aim * range);
    /// float damage = (HitZone)hit.Shape.UserData == HitZone.Head ? 100 : 25;
    /// </code>
    /// </example>
    public unsafe ulong UserData
    {
        get => Box3D.UserData.FromPointer(B3.b3Shape_GetUserData(Id));
        set => B3.b3Shape_SetUserData(Id, Box3D.UserData.ToPointer(value));
    }

    /// <summary>Gets a value indicating whether this shape reports overlaps instead of colliding.</summary>
    public bool IsSensor => B3.b3Shape_IsSensor(Id);

    /// <summary>Gets or sets the friction coefficient.</summary>
    public float Friction
    {
        get => B3.b3Shape_GetFriction(Id);
        set => B3.b3Shape_SetFriction(Id, value);
    }

    /// <summary>Gets or sets the bounciness.</summary>
    public float Restitution
    {
        get => B3.b3Shape_GetRestitution(Id);
        set => B3.b3Shape_SetRestitution(Id, value);
    }

    /// <summary>Gets the density, usually in kilograms per cubic metre.</summary>
    /// <remarks>Use <see cref="SetDensity"/> to change it, which lets you defer the body mass update.</remarks>
    public float Density => B3.b3Shape_GetDensity(Id);

    /// <summary>Gets or sets the surface properties.</summary>
    public PhysicsMaterial Material
    {
        get => PhysicsMaterial.FromNative(B3.b3Shape_GetSurfaceMaterial(Id));
        set => B3.b3Shape_SetSurfaceMaterial(Id, value.ToNative());
    }

    /// <summary>Gets the collision filtering rules.</summary>
    /// <remarks>Use <see cref="SetFilter"/> to change them; it is nearly as expensive as recreating the shape.</remarks>
    public CollisionFilter Filter => CollisionFilter.FromNative(B3.b3Shape_GetFilter(Id));

    /// <summary>Gets or sets a value indicating whether this shape reports begin and end touch events.</summary>
    public bool ContactEventsEnabled
    {
        get => B3.b3Shape_AreContactEventsEnabled(Id);
        set => B3.b3Shape_EnableContactEvents(Id, value);
    }

    /// <summary>Gets or sets a value indicating whether this shape reports hit events.</summary>
    public bool HitEventsEnabled
    {
        get => B3.b3Shape_AreHitEventsEnabled(Id);
        set => B3.b3Shape_EnableHitEvents(Id, value);
    }

    /// <summary>Gets or sets a value indicating whether this shape reports sensor overlaps.</summary>
    public bool SensorEventsEnabled
    {
        get => B3.b3Shape_AreSensorEventsEnabled(Id);
        set => B3.b3Shape_EnableSensorEvents(Id, value);
    }

    /// <summary>Gets the world-space bounding box of this shape.</summary>
    public BoundingBox Bounds => BoundingBox.FromNative(B3.b3Shape_GetAABB(Id));

    /// <summary>Sets the density, optionally deferring the body mass update.</summary>
    /// <param name="density">The new density, usually in kilograms per cubic metre.</param>
    /// <param name="updateBodyMass">
    /// Whether to recompute the body's mass now. Pass false when changing several
    /// shapes at once and call <see cref="Body.RecomputeMass"/> afterwards.
    /// </param>
    public void SetDensity(float density, bool updateBodyMass = true) =>
        B3.b3Shape_SetDensity(Id, density, updateBodyMass);

    /// <summary>Sets the collision filtering rules.</summary>
    /// <param name="filter">The new rules.</param>
    /// <param name="recomputeContacts">
    /// Whether every contact involving this shape is recomputed on the next step.
    /// This is expensive but is what you want if the change should take effect
    /// against shapes already touching it.
    /// </param>
    public void SetFilter(CollisionFilter filter, bool recomputeContacts = true) =>
        B3.b3Shape_SetFilter(Id, filter.ToNative(), recomputeContacts);

    /// <summary>Gets the closest point on this shape to a world-space target.</summary>
    /// <param name="target">The target point, in world space.</param>
    /// <returns>The closest point on the shape surface, in world space.</returns>
    public Vector3 ClosestPointTo(Vector3 target) => B3.b3Shape_GetClosestPoint(Id, target);

    /// <summary>Casts a ray directly against this shape, skipping the broad phase.</summary>
    /// <param name="origin">The start of the ray, in world space.</param>
    /// <param name="translation">The ray vector; its length is the range.</param>
    /// <returns>Where the ray met the shape.</returns>
    /// <remarks>Faster than a world cast when you already know which shape you care about.</remarks>
    public RaycastHit Raycast(Vector3 origin, Vector3 translation)
    {
        b3CastOutput output = B3.b3Shape_RayCast(Id, origin, translation);

        return new RaycastHit
        {
            Hit = output.hit,
            Shape = this,
            Point = output.point,
            Normal = output.normal,
            Fraction = output.fraction,
            TriangleIndex = output.triangleIndex,
            ChildIndex = output.childIndex,
        };
    }

    /// <summary>
    /// Applies an aerodynamic force to the body, based on this shape's area
    /// facing the wind.
    /// </summary>
    /// <param name="wind">The wind velocity, in world space.</param>
    /// <param name="drag">The drag coefficient, opposing the relative velocity.</param>
    /// <param name="lift">The lift coefficient, perpendicular to the relative velocity.</param>
    /// <param name="maxSpeed">
    /// The cap on relative speed. Needed for stability; ten metres per second or
    /// less is typical.
    /// </param>
    /// <param name="wake">Whether to wake the body.</param>
    public void ApplyWind(Vector3 wind, float drag, float lift, float maxSpeed, bool wake = true) =>
        B3.b3Shape_ApplyWind(Id, wind, drag, lift, maxSpeed, wake);

    /// <summary>Removes this shape from its body.</summary>
    /// <param name="updateBodyMass">
    /// Whether to recompute the body's mass now. Pass false when removing several
    /// shapes at once and call <see cref="Body.RecomputeMass"/> afterwards.
    /// </param>
    /// <remarks>
    /// Every copy of this handle becomes invalid. Destroying the body or the
    /// world destroys its shapes, so this is only needed to remove one shape
    /// while keeping the rest.
    /// </remarks>
    public void Destroy(bool updateBodyMass = true) => B3.b3DestroyShape(Id, updateBodyMass);
}
