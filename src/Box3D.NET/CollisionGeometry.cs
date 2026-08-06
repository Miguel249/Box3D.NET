// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using Box3D.Native;

namespace Box3D;

/*
 * Heavy collision geometry
 * ------------------------
 * Spheres, capsules and boxes are values: attaching one copies it and there is
 * nothing to manage. Meshes, height fields and baked compounds are not. They are
 * built once, cost real memory, and are meant to be shared by many shapes.
 *
 * Box3D's ownership rules for them are not uniform, and the difference matters:
 *
 *   Hull        b3CreateHullShape interns a copy in the world's hull database,
 *               so the source may be released as soon as the shape exists.
 *   Mesh        shape->mesh.data = geometry. Borrowed. Must outlive the shape.
 *   HeightField Borrowed. Must outlive the shape.
 *   Compound    shape->compound = geometry. Borrowed. Must outlive the shape.
 *
 * The three borrowed cases are the only things in this library besides
 * PhysicsWorld that own an unmanaged resource, and so the only other types that
 * are IDisposable. ConvexHull is disposable too, but only because building one
 * allocates; releasing it early is safe.
 *
 * None of them has a finalizer, for the same reason PhysicsWorld does not: a
 * finalizer runs at a time of the runtime's choosing, and freeing a mesh that a
 * live shape is still pointing at is a use-after-free inside the solver.
 * Forgetting to dispose leaks until the process exits, which is a bug you can
 * see. Freeing early is a crash you cannot.
 */

/// <summary>
/// A convex hull built from a point cloud.
/// </summary>
/// <remarks>
/// <para>
/// Use this for shapes a <see cref="Box"/> cannot express. Attaching it copies
/// the hull into the world, so it may be disposed as soon as every shape that
/// needs it has been created.
/// </para>
/// <para>
/// The world deduplicates identical hulls, so attaching the same hull to a
/// thousand bodies stores it once.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// ReadOnlySpan&lt;Vector3&gt; points = /* your vertices */;
///
/// using var hull = ConvexHull.FromPoints(points);
///
/// foreach (Vector3 position in spawnPoints)
/// {
///     Body body = world.CreateDynamicBody(position);
///     body.AddHull(hull);
/// }
/// // The hull may be disposed here; the shapes hold their own copy.
/// </code>
/// </example>
public sealed unsafe class ConvexHull : IDisposable
{
    private b3HullData* _hull;

    private ConvexHull(b3HullData* hull) => _hull = hull;

    /// <summary>Gets a value indicating whether this hull has been disposed.</summary>
    public bool IsDisposed => _hull is null;

    /// <summary>Gets the number of vertices in the hull.</summary>
    /// <exception cref="ObjectDisposedException">The hull has been disposed.</exception>
    public int VertexCount
    {
        get
        {
            ThrowIfDisposed();
            return _hull->vertexCount;
        }
    }

    /// <summary>Gets the number of faces in the hull.</summary>
    /// <exception cref="ObjectDisposedException">The hull has been disposed.</exception>
    public int FaceCount
    {
        get
        {
            ThrowIfDisposed();
            return _hull->faceCount;
        }
    }

    /// <summary>Gets the enclosed volume, usually in cubic metres.</summary>
    /// <exception cref="ObjectDisposedException">The hull has been disposed.</exception>
    public float Volume
    {
        get
        {
            ThrowIfDisposed();
            return _hull->volume;
        }
    }

    /// <summary>Gets the local-space bounding box.</summary>
    /// <exception cref="ObjectDisposedException">The hull has been disposed.</exception>
    public BoundingBox Bounds
    {
        get
        {
            ThrowIfDisposed();
            return BoundingBox.FromNative(_hull->aabb);
        }
    }

    internal b3HullData* NativeHull
    {
        get
        {
            ThrowIfDisposed();
            return _hull;
        }
    }

    /// <summary>Builds the convex hull enclosing a set of points.</summary>
    /// <param name="points">The points to enclose.</param>
    /// <param name="maxVertexCount">
    /// The vertex budget for the result, or zero for the engine limit. Points
    /// beyond it are merged, which simplifies the hull.
    /// </param>
    /// <returns>The hull.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="points"/> holds fewer than four points, or the engine could
    /// not build a hull from them.
    /// </exception>
    /// <remarks>
    /// Interior points are discarded, so passing a whole mesh is wasteful but
    /// harmless. A degenerate cloud, such as coplanar points, has no hull and is
    /// rejected.
    /// </remarks>
    public static ConvexHull FromPoints(ReadOnlySpan<Vector3> points, int maxVertexCount = 0)
    {
        if (points.Length < 4)
        {
            throw new ArgumentException(
                $"A convex hull needs at least four points, got {points.Length}.",
                nameof(points));
        }

        int budget = maxVertexCount > 0 ? maxVertexCount : Constants.B3_MAX_HULL_VERTICES;

        fixed (Vector3* p = points)
        {
            b3HullData* hull = B3.b3CreateHull(p, points.Length, budget);

            if (hull is null)
            {
                throw new ArgumentException(
                    "Box3D could not build a hull from these points. They are most likely degenerate: " +
                    "coplanar, collinear, or all within the merge tolerance of each other.",
                    nameof(points));
            }

            return new ConvexHull(hull);
        }
    }

    /// <summary>Builds a tessellated cylinder about the y axis.</summary>
    /// <param name="height">The total height.</param>
    /// <param name="radius">The radius.</param>
    /// <param name="baseHeight">
    /// Where the flat base sits along the y axis. The cylinder then extends up to
    /// <c>baseHeight + height</c>.
    /// </param>
    /// <param name="sides">The number of sides around the circumference, from 3 to 32.</param>
    /// <returns>The hull.</returns>
    /// <remarks>
    /// <b>The cylinder stands on the origin rather than being centred on it.</b>
    /// A cylinder built with the default <paramref name="baseHeight"/> occupies
    /// y from 0 to <paramref name="height"/>, so a body carrying one rests with
    /// its origin on the ground. Pass <c>-height / 2</c> to centre it.
    /// </remarks>
    /// <example>
    /// <code>
    /// // A barrel standing on the body origin.
    /// using var barrel = ConvexHull.Cylinder(height: 1.2f, radius: 0.4f);
    ///
    /// // The same barrel centred on the origin instead.
    /// using var centred = ConvexHull.Cylinder(1.2f, 0.4f, baseHeight: -0.6f);
    /// </code>
    /// </example>
    public static ConvexHull Cylinder(float height, float radius, float baseHeight = 0.0f, int sides = 16)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);
        ArgumentOutOfRangeException.ThrowIfLessThan(sides, 3);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(sides, 32);

        return new ConvexHull(B3.b3CreateCylinder(height, radius, baseHeight, sides));
    }

    /// <summary>Builds a tessellated cone or truncated cone about the y axis.</summary>
    /// <param name="height">The total height.</param>
    /// <param name="lowerRadius">The radius at the base.</param>
    /// <param name="upperRadius">The radius at the top. Zero gives a true cone.</param>
    /// <param name="slices">The number of slices around the circumference.</param>
    /// <returns>The hull.</returns>
    /// <remarks>As with <see cref="Cylinder"/>, the base sits at the origin rather than the centre.</remarks>
    public static ConvexHull Cone(float height, float lowerRadius, float upperRadius = 0.0f, int slices = 16)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lowerRadius);
        ArgumentOutOfRangeException.ThrowIfNegative(upperRadius);
        ArgumentOutOfRangeException.ThrowIfLessThan(slices, 3);

        return new ConvexHull(B3.b3CreateCone(height, lowerRadius, upperRadius, slices));
    }

    /// <summary>Builds an irregular rock-like hull, useful for scattering debris.</summary>
    /// <param name="radius">The approximate radius.</param>
    /// <returns>The hull.</returns>
    public static ConvexHull Rock(float radius)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);

        return new ConvexHull(B3.b3CreateRock(radius));
    }

    /// <summary>Releases the hull.</summary>
    /// <remarks>
    /// Safe once every shape built from it exists, because attaching a hull
    /// copies it into the world.
    /// </remarks>
    public void Dispose()
    {
        if (_hull is not null)
        {
            B3.b3DestroyHull(_hull);
            _hull = null;
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_hull is null, this);
}
