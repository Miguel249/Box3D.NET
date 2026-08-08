// SPDX-License-Identifier: MIT

using Box3D.Native;

namespace Box3D.Interop;

/// <summary>
/// Conversions between the types of <see cref="Box3D"/> and the raw identifiers
/// of <see cref="Box3D.Native"/>.
/// </summary>
/// <remarks>
/// <para>
/// These live in their own namespace so that reaching for the C API is a
/// deliberate act. The main surface never mentions a native type, which keeps
/// the two layers independent: <c>Box3D.NET</c> can change how it represents a
/// body without that being a breaking change for anyone who never imported this
/// namespace, and nothing about the C ABI leaks into ordinary application code.
/// </para>
/// <para>
/// The escape hatch exists because a thin wrapper should never be a ceiling.
/// Box3D exports around 580 functions and the idiomatic surface deliberately
/// does not cover all of them; when you need one that is missing, convert the
/// handle and call it, rather than waiting for this library to catch up.
/// </para>
/// <para>
/// Nothing here validates anything. Converting a stale or foreign handle
/// produces a value the C API will reject or, worse, silently misuse.
/// </para>
/// </remarks>
/// <example>
/// Setting a debug name, which the high-level API does not expose:
/// <code>
/// using Box3D;
/// using Box3D.Interop;
/// using Box3D.Native;
///
/// Body body = world.CreateDynamicBody(position);
///
/// unsafe
/// {
///     fixed (byte* name = "player\0"u8)
///     {
///         B3.b3Body_SetName(body.ToNativeId(), name);
///     }
/// }
/// </code>
/// Going the other way, when a native callback hands back a raw identifier:
/// <code>
/// b3ShapeId raw = /* from a native query */;
/// Shape shape = raw.ToShape();
/// </code>
/// </example>
public static class NativeInterop
{
    /// <summary>Gets the native identifier of a world.</summary>
    /// <param name="world">The world.</param>
    /// <returns>The identifier the C API expects.</returns>
    /// <exception cref="System.ArgumentNullException"><paramref name="world"/> is null.</exception>
    public static b3WorldId ToNativeId(this PhysicsWorld world)
    {
        System.ArgumentNullException.ThrowIfNull(world);

        return world.NativeId;
    }

    /// <summary>Gets the native identifier of a body.</summary>
    /// <param name="body">The body.</param>
    /// <returns>The identifier the C API expects.</returns>
    public static b3BodyId ToNativeId(this Body body) => body.NativeId;

    /// <summary>Gets the native identifier of a shape.</summary>
    /// <param name="shape">The shape.</param>
    /// <returns>The identifier the C API expects.</returns>
    public static b3ShapeId ToNativeId(this Shape shape) => shape.NativeId;

    /// <summary>Gets the native identifier of a joint.</summary>
    /// <param name="joint">The joint.</param>
    /// <returns>The identifier the C API expects.</returns>
    public static b3JointId ToNativeId(this Joint joint) => joint.NativeId;

    /// <summary>Gets the native pointer of a baked compound.</summary>
    /// <param name="compound">The compound.</param>
    /// <returns>The pointer the C API expects, owned by <paramref name="compound"/>.</returns>
    /// <exception cref="System.ArgumentNullException"><paramref name="compound"/> is null.</exception>
    /// <exception cref="System.ObjectDisposedException"><paramref name="compound"/> has been disposed.</exception>
    /// <remarks>
    /// <para>
    /// The one piece of geometry that cannot be reached from the shape that
    /// carries it. Hulls, meshes and height fields all have a
    /// <c>b3Shape_Get...</c> accessor; compounds have none, so anything that
    /// needs to read a compound's children - a debug renderer tessellating them,
    /// a tool serializing them with <c>b3ConvertCompoundToBytes</c> - has to be
    /// handed the pointer by whoever baked it.
    /// </para>
    /// <para>
    /// Valid only while the compound is alive, and freed by disposing it.
    /// </para>
    /// </remarks>
    public static unsafe b3CompoundData* ToNativePointer(this CompoundGeometry compound)
    {
        System.ArgumentNullException.ThrowIfNull(compound);

        return compound.NativeCompound;
    }

    /// <summary>Wraps a native body identifier.</summary>
    /// <param name="id">The identifier, which is not validated.</param>
    /// <returns>The body handle.</returns>
    public static Body ToBody(this b3BodyId id) => new(id);

    /// <summary>Wraps a native shape identifier.</summary>
    /// <param name="id">The identifier, which is not validated.</param>
    /// <returns>The shape handle.</returns>
    public static Shape ToShape(this b3ShapeId id) => new(id);

    /// <summary>Wraps a native joint identifier.</summary>
    /// <param name="id">The identifier, which is not validated.</param>
    /// <returns>The joint handle.</returns>
    public static Joint ToJoint(this b3JointId id) => new(id);
}
