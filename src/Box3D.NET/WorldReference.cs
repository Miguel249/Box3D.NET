// SPDX-License-Identifier: MIT

using Box3D.Native;

namespace Box3D;

/// <summary>
/// Identifies the world a body, shape or joint belongs to.
/// </summary>
/// <remarks>
/// <para>
/// This is not a <see cref="PhysicsWorld"/>. Box3D can name the world that owns
/// an object, but it cannot hand back the managed instance that wraps it, and
/// inventing one would produce a second object claiming ownership of the same
/// world and a second thing to dispose.
/// </para>
/// <para>
/// So this answers the question the engine can actually answer: which world is
/// it. Compare it against another reference, or against
/// <see cref="PhysicsWorld.Reference"/>, to find out whether two objects belong
/// to the same simulation.
/// </para>
/// </remarks>
/// <example>
/// Rejecting a body that came from somewhere else:
/// <code>
/// if (body.World != world.Reference)
/// {
///     throw new ArgumentException("That body belongs to a different world.");
/// }
/// </code>
/// </example>
public readonly record struct WorldReference
{
    internal WorldReference(b3WorldId id) => NativeId = id;

    /// <summary>Gets the native identifier this reference wraps.</summary>
    /// <remarks>
    /// Internal on purpose, so that no Box3D.Native type appears in the public
    /// surface. Reach it through <c>Box3D.Interop</c> if you need it.
    /// </remarks>
    internal b3WorldId NativeId { get; }

    /// <summary>Gets a value indicating whether the world is still alive.</summary>
    public bool IsValid => !NativeId.IsNull && B3.b3World_IsValid(NativeId);
}
