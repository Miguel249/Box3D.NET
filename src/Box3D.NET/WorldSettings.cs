// SPDX-License-Identifier: MIT

using System.Numerics;
using Box3D.Native;

namespace Box3D;

/// <summary>
/// The tuning applied to a <see cref="PhysicsWorld"/> when it is created.
/// </summary>
/// <remarks>
/// Start from <see cref="Default"/>, whose values come from the engine, and
/// change only what you need. Most simulations only ever set
/// <see cref="Gravity"/> and <see cref="WorkerCount"/>.
/// </remarks>
/// <example>
/// <code>
/// var settings = WorldSettings.Default with
/// {
///     Gravity = new Vector3(0.0f, -9.81f, 0.0f),
///     WorkerCount = Environment.ProcessorCount / 2,
/// };
///
/// using var world = new PhysicsWorld(settings);
/// </code>
/// </example>
public readonly record struct WorldSettings
{
    /// <summary>Gets the gravity vector, usually in metres per second squared.</summary>
    /// <remarks>Box3D has no opinion about which way is up; this is where you decide.</remarks>
    public Vector3 Gravity { get; init; }

    /// <summary>
    /// Gets the approach speed above which collisions bounce, usually in metres
    /// per second.
    /// </summary>
    /// <remarks>
    /// Slower contacts have no restitution applied, which is what lets bodies
    /// settle instead of jittering. Setting this very low prevents sleeping.
    /// </remarks>
    public float RestitutionThreshold { get; init; }

    /// <summary>
    /// Gets the approach speed above which a collision can raise a hit event,
    /// usually in metres per second.
    /// </summary>
    public float HitEventThreshold { get; init; }

    /// <summary>Gets the contact stiffness, in cycles per second.</summary>
    /// <remarks>Higher recovers from overlap faster but can introduce jitter. Advanced.</remarks>
    public float ContactHertz { get; init; }

    /// <summary>Gets the contact damping ratio.</summary>
    /// <remarks>Advanced.</remarks>
    public float ContactDampingRatio { get; init; }

    /// <summary>Gets the cap on how fast overlap is pushed apart, usually in metres per second.</summary>
    public float ContactSpeed { get; init; }

    /// <summary>Gets the maximum linear speed any body may reach, usually in metres per second.</summary>
    public float MaximumLinearSpeed { get; init; }

    /// <summary>Gets a value indicating whether bodies may sleep when they stop moving.</summary>
    /// <remarks>
    /// A large performance win for scenes that come to rest. Turn it off only if
    /// something depends on every body being stepped every frame.
    /// </remarks>
    public bool EnableSleep { get; init; }

    /// <summary>
    /// Gets a value indicating whether continuous collision is enabled, which
    /// stops fast bodies tunnelling through thin geometry.
    /// </summary>
    /// <remarks>Leave this on; turning it off saves very little.</remarks>
    public bool EnableContinuous { get; init; }

    /// <summary>
    /// Gets the number of worker threads, clamped to one through
    /// <see cref="Constants.B3_MAX_WORKERS"/>.
    /// </summary>
    /// <remarks>
    /// Any value above one enables multithreading, and the engine starts and
    /// owns the threads. Box3D performs best on performance cores sharing one L2
    /// cache; efficiency cores and hyper-threading add little and can cost.
    /// </remarks>
    public int WorkerCount { get; init; }

    /// <summary>Gets the engine's default settings.</summary>
    public static WorldSettings Default => FromNative(B3.b3DefaultWorldDef());

    internal unsafe b3WorldDef ToNative()
    {
        b3WorldDef def = B3.b3DefaultWorldDef();

        def.gravity = Gravity;
        def.restitutionThreshold = RestitutionThreshold;
        def.hitEventThreshold = HitEventThreshold;
        def.contactHertz = ContactHertz;
        def.contactDampingRatio = ContactDampingRatio;
        def.contactSpeed = ContactSpeed;
        def.maximumLinearSpeed = MaximumLinearSpeed;
        def.enableSleep = EnableSleep;
        def.enableContinuous = EnableContinuous;
        def.workerCount = (uint)System.Math.Clamp(WorkerCount, 1, Constants.B3_MAX_WORKERS);

        return def;
    }

    internal static WorldSettings FromNative(in b3WorldDef def) => new()
    {
        Gravity = def.gravity,
        RestitutionThreshold = def.restitutionThreshold,
        HitEventThreshold = def.hitEventThreshold,
        ContactHertz = def.contactHertz,
        ContactDampingRatio = def.contactDampingRatio,
        ContactSpeed = def.contactSpeed,
        MaximumLinearSpeed = def.maximumLinearSpeed,
        EnableSleep = def.enableSleep,
        EnableContinuous = def.enableContinuous,
        WorkerCount = (int)def.workerCount,
    };
}
