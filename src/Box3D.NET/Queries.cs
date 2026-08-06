// SPDX-License-Identifier: MIT

using System.Numerics;
using Box3D.Native;

namespace Box3D;

/// <summary>
/// Where a ray met a shape.
/// </summary>
/// <example>
/// <code>
/// RaycastHit hit = world.CastRayClosest(camera.Position, camera.Forward * 100.0f);
/// if (hit.Hit)
/// {
///     PlaceDecal(hit.Point, hit.Normal);
///     float distance = hit.Fraction * 100.0f;
/// }
/// </code>
/// </example>
public readonly record struct RaycastHit
{
    /// <summary>Gets a value indicating whether the ray hit anything.</summary>
    /// <remarks>When this is false every other member is meaningless.</remarks>
    public bool Hit { get; init; }

    /// <summary>Gets the shape that was hit.</summary>
    public Shape Shape { get; init; }

    /// <summary>Gets the world-space point of intersection.</summary>
    public Vector3 Point { get; init; }

    /// <summary>Gets the outward surface normal at the point of intersection.</summary>
    public Vector3 Normal { get; init; }

    /// <summary>
    /// Gets how far along the ray the hit occurred, from zero at the origin to
    /// one at the end.
    /// </summary>
    /// <remarks>
    /// Multiply by the length of the translation passed to the cast to get a
    /// distance.
    /// </remarks>
    public float Fraction { get; init; }

    /// <summary>
    /// Gets the user material identifier at the hit point, which can vary per
    /// triangle on a mesh or height field.
    /// </summary>
    /// <remarks>See <see cref="PhysicsMaterial.UserMaterialId"/>.</remarks>
    public ulong UserMaterialId { get; init; }

    /// <summary>Gets the triangle index for a mesh or height field, or minus one otherwise.</summary>
    public int TriangleIndex { get; init; }

    /// <summary>Gets the child index for a compound shape, or minus one otherwise.</summary>
    public int ChildIndex { get; init; }

    internal static RaycastHit FromNative(in b3RayResult result) => new()
    {
        Hit = result.hit,
        Shape = new Shape(result.shapeId),
        Point = result.point,
        Normal = result.normal,
        Fraction = result.fraction,
        UserMaterialId = result.userMaterialId,
        TriangleIndex = result.triangleIndex,
        ChildIndex = result.childIndex,
    };
}

/// <summary>
/// Tells a ray cast what to do after each shape it finds.
/// </summary>
/// <remarks>
/// Returned from <see cref="IRaycastCallback.OnHit"/>. The static members cover
/// every common case; <see cref="ClipTo"/> is the one that yields a
/// closest-hit search.
/// </remarks>
public readonly record struct RaycastAction
{
    private RaycastAction(float value) => Value = value;

    /// <summary>Gets the raw value handed back to the engine.</summary>
    internal float Value { get; }

    /// <summary>Ignores this shape and carries on as if it were not there.</summary>
    public static RaycastAction Ignore => new(-1.0f);

    /// <summary>Stops the cast immediately, keeping the results gathered so far.</summary>
    public static RaycastAction Stop => new(0.0f);

    /// <summary>Records this hit and carries on looking for others, near and far.</summary>
    public static RaycastAction Continue => new(1.0f);

    /// <summary>
    /// Shortens the ray to end at this hit and carries on, so that only closer
    /// shapes are reported afterwards.
    /// </summary>
    /// <param name="fraction">The fraction reported for this hit.</param>
    /// <returns>The action.</returns>
    /// <remarks>Returning this from every hit gives a closest-hit search.</remarks>
    public static RaycastAction ClipTo(float fraction) => new(fraction);
}

/// <summary>
/// Receives the shapes a ray cast passes through.
/// </summary>
/// <remarks>
/// <para>
/// Implement this on a <see langword="struct"/>. The query is generic over the
/// implementing type, so the call is resolved statically and inlined: no
/// delegate is allocated, nothing is boxed, and nothing needs to be kept alive
/// across the call. This is the reason the API takes a callback type rather
/// than a <see cref="System.Func{T, TResult}"/>.
/// </para>
/// <para>
/// Shapes arrive in no particular order, so a callback looking for the nearest
/// hit must either compare fractions itself or return
/// <see cref="RaycastAction.ClipTo"/>.
/// </para>
/// <para>
/// The world is locked while the query runs. Creating or destroying bodies from
/// inside the callback is not allowed; collect what you need and act after the
/// query returns.
/// </para>
/// </remarks>
/// <example>
/// Collecting every shape a ray passes through, without allocating:
/// <code>
/// struct CollectAll : IRaycastCallback
/// {
///     public int Count;
///
///     public RaycastAction OnHit(in RaycastHit hit)
///     {
///         Count++;
///         return RaycastAction.Continue;
///     }
/// }
///
/// var callback = new CollectAll();
/// world.CastRay(origin, direction * 50.0f, ref callback);
/// Console.WriteLine($"passed through {callback.Count} shapes");
/// </code>
/// Ignoring the shooter's own body:
/// <code>
/// struct IgnoreBody : IRaycastCallback
/// {
///     public Body Ignored;
///     public RaycastHit Nearest;
///
///     public RaycastAction OnHit(in RaycastHit hit)
///     {
///         if (hit.Shape.Body == Ignored)
///         {
///             return RaycastAction.Ignore;
///         }
///
///         Nearest = hit;
///         return RaycastAction.ClipTo(hit.Fraction);
///     }
/// }
/// </code>
/// </example>
public interface IRaycastCallback
{
    /// <summary>Called once for each shape the ray reaches.</summary>
    /// <param name="hit">Where the ray met the shape.</param>
    /// <returns>What the cast should do next.</returns>
    RaycastAction OnHit(in RaycastHit hit);
}

/// <summary>
/// Receives the shapes found by an overlap query.
/// </summary>
/// <remarks>
/// Implement this on a <see langword="struct"/>, for the same reasons as
/// <see cref="IRaycastCallback"/>. The world is locked while the query runs, so
/// do not create or destroy anything from inside it.
/// </remarks>
/// <example>
/// <code>
/// struct CountBodies : IOverlapCallback
/// {
///     public int Count;
///
///     public bool OnOverlap(Shape shape)
///     {
///         Count++;
///         return true; // keep going
///     }
/// }
///
/// var callback = new CountBodies();
/// world.OverlapBox(explosionCentre, new Vector3(5.0f), ref callback);
/// </code>
/// </example>
public interface IOverlapCallback
{
    /// <summary>Called once for each shape found.</summary>
    /// <param name="shape">The shape that overlaps the query volume.</param>
    /// <returns><see langword="true"/> to keep searching, <see langword="false"/> to stop.</returns>
    bool OnOverlap(Shape shape);
}
