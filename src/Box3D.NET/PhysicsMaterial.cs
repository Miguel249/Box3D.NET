// SPDX-License-Identifier: MIT

using System.Numerics;
using Box3D.Native;

namespace Box3D;

/// <summary>
/// The surface properties of a shape: how it rubs, how it bounces, and how it
/// drags things along.
/// </summary>
/// <remarks>
/// <para>
/// When two shapes touch, their materials are combined. By default friction
/// combines as the geometric mean and restitution as the maximum, so one bouncy
/// surface is enough to make a collision bounce. Both rules can be replaced per
/// world.
/// </para>
/// <para>
/// Meshes and height fields can carry a different material per triangle;
/// convex shapes have exactly one.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var ice = PhysicsMaterial.Default with { Friction = 0.02f };
/// var rubber = PhysicsMaterial.Default with { Restitution = 0.9f };
///
/// // A conveyor belt: the surface itself drags contacts along its local x axis.
/// var belt = PhysicsMaterial.Default with { SurfaceVelocity = new Vector3(2.0f, 0.0f, 0.0f) };
/// </code>
/// </example>
public readonly record struct PhysicsMaterial
{
    /// <summary>
    /// Gets the Coulomb friction coefficient, normally between zero and one.
    /// </summary>
    /// <remarks>Zero is frictionless. Values above one are allowed and are unusually grippy.</remarks>
    public float Friction { get; init; }

    /// <summary>
    /// Gets the bounciness, from zero for a dead stop to one for a perfectly
    /// elastic bounce.
    /// </summary>
    /// <remarks>
    /// Only applied above the world's restitution threshold, so slow contacts
    /// settle instead of jittering forever.
    /// </remarks>
    public float Restitution { get; init; }

    /// <summary>
    /// Gets the rolling resistance, normally between zero and one.
    /// </summary>
    /// <remarks>Only affects spheres and capsules. Use it to stop balls rolling forever.</remarks>
    public float RollingResistance { get; init; }

    /// <summary>
    /// Gets the surface velocity, in shape-local space, used for conveyor belts.
    /// </summary>
    /// <remarks>Projected onto the contact surface, so only the tangential part has an effect.</remarks>
    public Vector3 SurfaceVelocity { get; init; }

    /// <summary>
    /// Gets an application-defined identifier reported with query results and
    /// passed to the friction and restitution combining callbacks.
    /// </summary>
    /// <remarks>
    /// The physics engine never interprets this. It is the natural place to put
    /// a surface type such as "gravel" so that a ray cast can tell the caller
    /// what it hit without a lookup.
    /// </remarks>
    public ulong UserMaterialId { get; init; }

    /// <summary>Gets the default material: moderate friction and no bounce.</summary>
    /// <remarks>The values come from Box3D, so they track the engine's own defaults.</remarks>
    public static PhysicsMaterial Default => FromNative(NativeDefaults.SurfaceMaterial);

    internal b3SurfaceMaterial ToNative() => new()
    {
        friction = Friction,
        restitution = Restitution,
        rollingResistance = RollingResistance,
        tangentVelocity = SurfaceVelocity,
        userMaterialId = UserMaterialId,
        customColor = 0,
        padding = 0,
    };

    internal static PhysicsMaterial FromNative(in b3SurfaceMaterial material) => new()
    {
        Friction = material.friction,
        Restitution = material.restitution,
        RollingResistance = material.rollingResistance,
        SurfaceVelocity = material.tangentVelocity,
        UserMaterialId = material.userMaterialId,
    };
}
