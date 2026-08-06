// SPDX-License-Identifier: MIT

using System.Numerics;
using Box3D.Native;

namespace Box3D;

/// <summary>
/// How a body participates in the simulation.
/// </summary>
public enum BodyType
{
    /// <summary>
    /// Never moves on its own and has infinite mass. Use it for level geometry.
    /// </summary>
    /// <remarks>Static bodies do not collide with each other.</remarks>
    Static = 0,

    /// <summary>
    /// Moves only where the application drives it, and pushes dynamic bodies out
    /// of the way without being pushed back.
    /// </summary>
    /// <remarks>Use it for moving platforms, lifts and doors.</remarks>
    Kinematic = 1,

    /// <summary>
    /// Fully simulated: it has mass and responds to forces, gravity and collisions.
    /// </summary>
    Dynamic = 2,
}

/// <summary>
/// Which axes a body is prevented from moving along or rotating about.
/// </summary>
/// <remarks>
/// Locking an axis is exact and costs nothing, unlike approximating it with a
/// very large inertia.
/// </remarks>
/// <example>
/// Keeping a character upright while letting it turn:
/// <code>
/// var def = BodyDefinition.Dynamic(spawnPoint) with
/// {
///     MotionLocks = MotionLocks.None with { AngularX = true, AngularZ = true },
/// };
/// </code>
/// </example>
public readonly record struct MotionLocks
{
    /// <summary>Gets a value indicating whether translation along the x axis is prevented.</summary>
    public bool LinearX { get; init; }

    /// <summary>Gets a value indicating whether translation along the y axis is prevented.</summary>
    public bool LinearY { get; init; }

    /// <summary>Gets a value indicating whether translation along the z axis is prevented.</summary>
    public bool LinearZ { get; init; }

    /// <summary>Gets a value indicating whether rotation about the x axis is prevented.</summary>
    public bool AngularX { get; init; }

    /// <summary>Gets a value indicating whether rotation about the y axis is prevented.</summary>
    public bool AngularY { get; init; }

    /// <summary>Gets a value indicating whether rotation about the z axis is prevented.</summary>
    public bool AngularZ { get; init; }

    /// <summary>Gets locks that restrict nothing.</summary>
    public static MotionLocks None => default;

    /// <summary>Gets locks that prevent all rotation, leaving translation free.</summary>
    public static MotionLocks NoRotation => new() { AngularX = true, AngularY = true, AngularZ = true };

    internal b3MotionLocks ToNative() => new()
    {
        linearX = LinearX,
        linearY = LinearY,
        linearZ = LinearZ,
        angularX = AngularX,
        angularY = AngularY,
        angularZ = AngularZ,
    };

    internal static MotionLocks FromNative(in b3MotionLocks locks) => new()
    {
        LinearX = locks.linearX,
        LinearY = locks.linearY,
        LinearZ = locks.linearZ,
        AngularX = locks.angularX,
        AngularY = locks.angularY,
        AngularZ = locks.angularZ,
    };
}

/// <summary>
/// Everything needed to create a body.
/// </summary>
/// <remarks>
/// <para>
/// This is a plain value that carries creation parameters. It holds no
/// resources, is safe to reuse for many bodies, and can be discarded as soon as
/// the body exists.
/// </para>
/// <para>
/// Start from <see cref="Default"/> or one of the factory methods rather than
/// from <c>default</c>: a zeroed definition has no rotation quaternion and no
/// gravity scale, and the engine rejects it.
/// </para>
/// <para>
/// Create bodies where they belong. Creating one at the origin and moving it
/// afterwards costs close to twice as much, and more once shapes are attached.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // A crate falling from ten metres up.
/// var crate = world.CreateBody(BodyDefinition.Dynamic(new Vector3(0, 10, 0)));
///
/// // A moving platform the application drives itself.
/// var platform = world.CreateBody(BodyDefinition.Kinematic(start));
///
/// // Level geometry.
/// var ground = world.CreateBody(BodyDefinition.Static());
/// </code>
/// </example>
public readonly record struct BodyDefinition
{
    /// <summary>Gets how the body participates in the simulation.</summary>
    public BodyType Type { get; init; }

    /// <summary>Gets the initial world position of the body origin.</summary>
    public Vector3 Position { get; init; }

    /// <summary>Gets the initial world rotation.</summary>
    public Quaternion Rotation { get; init; }

    /// <summary>Gets the initial linear velocity, usually in metres per second.</summary>
    public Vector3 LinearVelocity { get; init; }

    /// <summary>Gets the initial angular velocity, in radians per second.</summary>
    public Vector3 AngularVelocity { get; init; }

    /// <summary>
    /// Gets the linear damping, which bleeds off linear velocity over time.
    /// </summary>
    /// <remarks>
    /// Generally undesirable: it makes objects look like they are moving through
    /// treacle. Prefer friction where you can.
    /// </remarks>
    public float LinearDamping { get; init; }

    /// <summary>Gets the angular damping, which slows down spinning bodies.</summary>
    public float AngularDamping { get; init; }

    /// <summary>Gets the multiplier applied to gravity for this body.</summary>
    /// <remarks>Zero makes the body float; a negative value makes it fall upwards.</remarks>
    public float GravityScale { get; init; }

    /// <summary>Gets the speed below which the body is considered still enough to sleep.</summary>
    public float SleepThreshold { get; init; }

    /// <summary>Gets an optional name, used when debugging and drawing.</summary>
    /// <remarks>Copied by the engine, so the string need not be kept alive.</remarks>
    public string? Name { get; init; }

    /// <summary>Gets the axes the body may not move along or rotate about.</summary>
    public MotionLocks MotionLocks { get; init; }

    /// <summary>Gets a value indicating whether the body may fall asleep when it stops moving.</summary>
    /// <remarks>Sleeping is a large performance win; turn it off only when something depends on the body being stepped every frame.</remarks>
    public bool CanSleep { get; init; }

    /// <summary>Gets a value indicating whether the body starts awake.</summary>
    public bool StartAwake { get; init; }

    /// <summary>
    /// Gets a value indicating whether the body is treated as a bullet, using
    /// continuous collision against dynamic and kinematic bodies.
    /// </summary>
    /// <remarks>
    /// Use sparingly. Bullets do not guarantee correct collision when both
    /// bodies move fast, because they are swept after everything else has moved.
    /// For a projectile that must not miss, cast a ray or a shape along its path
    /// instead.
    /// </remarks>
    public bool IsBullet { get; init; }

    /// <summary>Gets a value indicating whether the body takes part in the simulation at all.</summary>
    /// <remarks>A disabled body neither moves nor collides, and costs almost nothing.</remarks>
    public bool IsEnabled { get; init; }

    /// <summary>
    /// Gets a value indicating whether the body may spin faster than the usual
    /// rotation limit.
    /// </summary>
    /// <remarks>Only sound for bodies that are symmetric about the spin axis, such as wheels.</remarks>
    public bool AllowFastRotation { get; init; }

    /// <summary>
    /// Gets a value indicating whether contact recycling is enabled.
    /// </summary>
    /// <remarks>
    /// Recycling reuses contact manifolds across small movements and is a
    /// performance win, but it can produce ghost collisions. Turn it off for
    /// characters, where a snagged step is more noticeable than the cost.
    /// </remarks>
    public bool EnableContactRecycling { get; init; }

    /// <summary>
    /// Gets the engine's default definition, which describes a static body at
    /// the origin.
    /// </summary>
    public static BodyDefinition Default => FromNative(NativeDefaults.Body);

    /// <summary>Creates a definition for a static body.</summary>
    /// <param name="position">The world position.</param>
    /// <param name="rotation">The world rotation, or null for no rotation.</param>
    /// <returns>The definition.</returns>
    public static BodyDefinition Static(Vector3 position = default, Quaternion? rotation = null) =>
        Default with
        {
            Type = BodyType.Static,
            Position = position,
            Rotation = rotation ?? Quaternion.Identity,
        };

    /// <summary>Creates a definition for a dynamic body.</summary>
    /// <param name="position">The world position.</param>
    /// <param name="rotation">The world rotation, or null for no rotation.</param>
    /// <returns>The definition.</returns>
    public static BodyDefinition Dynamic(Vector3 position = default, Quaternion? rotation = null) =>
        Default with
        {
            Type = BodyType.Dynamic,
            Position = position,
            Rotation = rotation ?? Quaternion.Identity,
        };

    /// <summary>Creates a definition for a kinematic body.</summary>
    /// <param name="position">The world position.</param>
    /// <param name="rotation">The world rotation, or null for no rotation.</param>
    /// <returns>The definition.</returns>
    public static BodyDefinition Kinematic(Vector3 position = default, Quaternion? rotation = null) =>
        Default with
        {
            Type = BodyType.Kinematic,
            Position = position,
            Rotation = rotation ?? Quaternion.Identity,
        };

    /// <summary>
    /// Converts to the native definition. The name is not copied here; the
    /// caller supplies the UTF-8 pointer, because its lifetime is bound to the
    /// call rather than to this value.
    /// </summary>
    internal unsafe b3BodyDef ToNative(byte* name)
    {
        b3BodyDef def = NativeDefaults.Body;

        def.type = (b3BodyType)Type;
        def.position = Position;
        def.rotation = Rotation;
        def.linearVelocity = LinearVelocity;
        def.angularVelocity = AngularVelocity;
        def.linearDamping = LinearDamping;
        def.angularDamping = AngularDamping;
        def.gravityScale = GravityScale;
        def.sleepThreshold = SleepThreshold;
        def.name = name;
        def.motionLocks = MotionLocks.ToNative();
        def.enableSleep = CanSleep;
        def.isAwake = StartAwake;
        def.isBullet = IsBullet;
        def.isEnabled = IsEnabled;
        def.allowFastRotation = AllowFastRotation;
        def.enableContactRecycling = EnableContactRecycling;

        return def;
    }

    internal static unsafe BodyDefinition FromNative(in b3BodyDef def) => new()
    {
        Type = (BodyType)def.type,
        Position = def.position,
        Rotation = def.rotation,
        LinearVelocity = def.linearVelocity,
        AngularVelocity = def.angularVelocity,
        LinearDamping = def.linearDamping,
        AngularDamping = def.angularDamping,
        GravityScale = def.gravityScale,
        SleepThreshold = def.sleepThreshold,
        Name = null,
        MotionLocks = MotionLocks.FromNative(def.motionLocks),
        CanSleep = def.enableSleep,
        StartAwake = def.isAwake,
        IsBullet = def.isBullet,
        IsEnabled = def.isEnabled,
        AllowFastRotation = def.allowFastRotation,
        EnableContactRecycling = def.enableContactRecycling,
    };
}
