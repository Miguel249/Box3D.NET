// SPDX-License-Identifier: MIT

using Box3D.Native;

namespace Box3D;

/// <summary>
/// The kind of geometry backing a shape.
/// </summary>
public enum ShapeType
{
    /// <summary>A capsule, which is a sphere swept along a segment.</summary>
    Capsule = 0,

    /// <summary>A baked compound of many child shapes, allowed on static bodies only.</summary>
    Compound = 1,

    /// <summary>A height field, used for terrain.</summary>
    HeightField = 2,

    /// <summary>A convex hull.</summary>
    Hull = 3,

    /// <summary>A triangle mesh.</summary>
    Mesh = 4,

    /// <summary>A sphere.</summary>
    Sphere = 5,
}

/// <summary>
/// Everything needed to attach a shape to a body, other than the geometry itself.
/// </summary>
/// <remarks>
/// <para>
/// Start from <see cref="Default"/>; a zeroed definition has no density and no
/// collision mask, and the engine rejects it.
/// </para>
/// <para>
/// Attaching a shape moves the body's centre of mass, which changes its linear
/// velocity if it is already spinning. Build bodies before you start simulating
/// them where you can.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // A heavy, grippy shape that reports its collisions.
/// var def = ShapeDefinition.Default with
/// {
///     Density = 2000.0f,
///     Material = PhysicsMaterial.Default with { Friction = 0.9f },
///     EnableContactEvents = true,
/// };
///
/// body.AddSphere(new Sphere(Vector3.Zero, 0.5f), def);
/// </code>
/// A trigger volume, which reports overlaps but never pushes anything:
/// <code>
/// var trigger = ShapeDefinition.Default with
/// {
///     IsSensor = true,
///     EnableSensorEvents = true,
///     Density = 0.0f,
/// };
/// </code>
/// </example>
public readonly record struct ShapeDefinition
{
    /// <summary>Gets the density, usually in kilograms per cubic metre.</summary>
    /// <remarks>
    /// Mass is derived from density and volume, so this is what decides how
    /// heavy the body ends up. Water is about 1000.
    /// </remarks>
    public float Density { get; init; }

    /// <summary>Gets the surface properties.</summary>
    public PhysicsMaterial Material { get; init; }

    /// <summary>Gets the collision filtering rules.</summary>
    public CollisionFilter Filter { get; init; }

    /// <summary>Gets an optional name, used when debugging and drawing.</summary>
    public string? Name { get; init; }

    /// <summary>
    /// Gets a value indicating whether this shape reports overlaps instead of
    /// colliding.
    /// </summary>
    /// <remarks>
    /// A sensor generates no collision response but still contributes mass if
    /// its density is non-zero, so a trigger volume normally wants a density of
    /// zero. Sensors have no continuous collision; use a cast for a fast-moving
    /// trigger.
    /// </remarks>
    public bool IsSensor { get; init; }

    /// <summary>Gets a value indicating whether this shape reports sensor overlaps.</summary>
    /// <remarks>Off by default even for sensors, because collecting the events is not free.</remarks>
    public bool EnableSensorEvents { get; init; }

    /// <summary>Gets a value indicating whether this shape reports begin and end touch events.</summary>
    public bool EnableContactEvents { get; init; }

    /// <summary>
    /// Gets a value indicating whether this shape reports hit events for
    /// collisions above the world's hit threshold.
    /// </summary>
    /// <remarks>This is what you want for impact sounds and damage.</remarks>
    public bool EnableHitEvents { get; init; }

    /// <summary>
    /// Gets a value indicating whether the body recomputes its mass when this
    /// shape is attached.
    /// </summary>
    /// <remarks>
    /// Leave this on unless you are attaching many shapes at once, in which case
    /// turn it off and call <see cref="Body.RecomputeMass"/> once at the end.
    /// </remarks>
    public bool UpdateBodyMass { get; init; }

    /// <summary>
    /// Gets a value indicating whether the shape scans for contacts on the next
    /// step.
    /// </summary>
    /// <remarks>
    /// Ignored for dynamic and kinematic shapes. Turning it off makes bulk
    /// creation of static geometry substantially faster.
    /// </remarks>
    public bool InvokeContactCreation { get; init; }

    /// <summary>Gets the engine's default shape definition.</summary>
    public static ShapeDefinition Default => FromNative(NativeDefaults.Shape);

    internal unsafe b3ShapeDef ToNative(byte* name)
    {
        b3ShapeDef def = NativeDefaults.Shape;

        def.name = name;
        def.density = Density;
        def.baseMaterial = Material.ToNative();
        def.filter = Filter.ToNative();
        def.isSensor = IsSensor;
        def.enableSensorEvents = EnableSensorEvents;
        def.enableContactEvents = EnableContactEvents;
        def.enableHitEvents = EnableHitEvents;
        def.updateBodyMass = UpdateBodyMass;
        def.invokeContactCreation = InvokeContactCreation;

        return def;
    }

    internal static ShapeDefinition FromNative(in b3ShapeDef def) => new()
    {
        Density = def.density,
        Material = PhysicsMaterial.FromNative(def.baseMaterial),
        Filter = CollisionFilter.FromNative(def.filter),
        Name = null,
        IsSensor = def.isSensor,
        EnableSensorEvents = def.enableSensorEvents,
        EnableContactEvents = def.enableContactEvents,
        EnableHitEvents = def.enableHitEvents,
        UpdateBodyMass = def.updateBodyMass,
        InvokeContactCreation = def.invokeContactCreation,
    };
}
