// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Numerics;
using Box3D.Native;

namespace Box3D;

/*
 * Baked compounds
 * ---------------
 * There are two ways to put several shapes on one body, and they are not
 * alternatives:
 *
 *   Run time   Attach several shapes to a body with AddSphere, AddHull and the
 *              rest. Works on any body type, each shape is its own broad-phase
 *              proxy, and shapes can come and go while the world runs. This is
 *              what CompoundShapeSample in the samples does.
 *
 *   Baked      Build the children once into a b3CompoundData and attach the
 *              whole thing as a single shape. Static bodies only, nothing can be
 *              added or removed afterwards, and the children are indexed by a
 *              tree of their own. This is for the thousand-piece rock wall that
 *              never moves, where a thousand broad-phase proxies would be the
 *              cost rather than the collisions.
 *
 * Building one clones everything: the hulls, the meshes and the materials all
 * end up inside the compound, so the sources may be released as soon as Build
 * returns. What the compound itself is *not* is copied - attaching it borrows,
 * exactly like a mesh or a height field, so it must outlive every shape built
 * from it. The rules and the reasoning are in CollisionGeometry.cs.
 */

/// <summary>
/// Collects the children of a baked compound.
/// </summary>
/// <remarks>
/// <para>
/// A builder rather than a factory method, because a compound is heterogeneous:
/// Box3D wants its spheres, capsules, hulls and meshes in four separate arrays,
/// and asking a caller to assemble those is asking them to do the bookkeeping
/// this exists to do.
/// </para>
/// <para>
/// The builder holds the geometry it was given rather than a pointer into it, so
/// disposing a hull before <see cref="Build"/> raises
/// <see cref="ObjectDisposedException"/> instead of reading freed memory.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// using var rock = ConvexHull.Rock(0.4f);
///
/// var builder = new CompoundBuilder();
/// foreach (Vector3 position in wallPositions)
/// {
///     builder.AddHull(rock, position, Quaternion.Identity);
/// }
///
/// using CompoundGeometry wall = builder.Build();
/// // The hull may be disposed here; the compound holds its own copy.
///
/// using (var world = new PhysicsWorld())
/// {
///     Body body = world.CreateStaticBody();
///     body.AddCompound(wall);
///
///     Simulate(world);
/// }
/// // World first, compound second.
/// </code>
/// </example>
public sealed class CompoundBuilder
{
    private readonly List<(Sphere Sphere, PhysicsMaterial Material)> _spheres = new();
    private readonly List<(Capsule Capsule, PhysicsMaterial Material)> _capsules = new();
    private readonly List<(ConvexHull Hull, Vector3 Position, Quaternion Rotation, PhysicsMaterial Material)> _hulls = new();
    private readonly List<(CollisionMesh Mesh, Vector3 Position, Quaternion Rotation, Vector3 Scale, PhysicsMaterial Material)> _meshes = new();

    /// <summary>Gets how many children have been added so far.</summary>
    public int ChildCount => _spheres.Count + _capsules.Count + _hulls.Count + _meshes.Count;

    /// <summary>Adds a sphere.</summary>
    /// <param name="sphere">The sphere, in compound-local space.</param>
    /// <param name="material">The surface material, or null for the defaults.</param>
    /// <returns>This builder, so calls can be chained.</returns>
    public CompoundBuilder AddSphere(Sphere sphere, PhysicsMaterial? material = null)
    {
        _spheres.Add((sphere, material ?? PhysicsMaterial.Default));
        return this;
    }

    /// <summary>Adds a capsule.</summary>
    /// <param name="capsule">The capsule, in compound-local space.</param>
    /// <param name="material">The surface material, or null for the defaults.</param>
    /// <returns>This builder, so calls can be chained.</returns>
    public CompoundBuilder AddCapsule(Capsule capsule, PhysicsMaterial? material = null)
    {
        _capsules.Add((capsule, material ?? PhysicsMaterial.Default));
        return this;
    }

    /// <summary>Adds a convex hull at a transform.</summary>
    /// <param name="hull">The hull. It is copied into the compound by <see cref="Build"/>.</param>
    /// <param name="position">Where it goes, in compound-local space.</param>
    /// <param name="rotation">Its orientation, or null for none.</param>
    /// <param name="material">The surface material, or null for the defaults.</param>
    /// <returns>This builder, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="hull"/> is null.</exception>
    /// <remarks>
    /// The same hull may be added many times at different transforms; Box3D
    /// stores it once and the instances share it.
    /// </remarks>
    public CompoundBuilder AddHull(
        ConvexHull hull,
        Vector3 position,
        Quaternion? rotation = null,
        PhysicsMaterial? material = null)
    {
        ArgumentNullException.ThrowIfNull(hull);

        _hulls.Add((hull, position, rotation ?? Quaternion.Identity, material ?? PhysicsMaterial.Default));
        return this;
    }

    /// <summary>Adds a triangle mesh at a transform.</summary>
    /// <param name="mesh">The mesh. It is copied into the compound by <see cref="Build"/>.</param>
    /// <param name="position">Where it goes, in compound-local space.</param>
    /// <param name="rotation">Its orientation, or null for none.</param>
    /// <param name="scale">The instance scale, or null for none.</param>
    /// <param name="material">
    /// The surface material, or null for the defaults. One material for the whole
    /// mesh: a compound stores at most
    /// <see cref="Native.Constants.B3_MAX_COMPOUND_MESH_MATERIALS"/> per mesh, and
    /// per-triangle materials are better served by a mesh shape outside a
    /// compound.
    /// </param>
    /// <returns>This builder, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="mesh"/> is null.</exception>
    public CompoundBuilder AddMesh(
        CollisionMesh mesh,
        Vector3 position,
        Quaternion? rotation = null,
        Vector3? scale = null,
        PhysicsMaterial? material = null)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        _meshes.Add((
            mesh,
            position,
            rotation ?? Quaternion.Identity,
            scale ?? Vector3.One,
            material ?? PhysicsMaterial.Default));

        return this;
    }

    /// <summary>Bakes the children into a compound.</summary>
    /// <returns>The compound.</returns>
    /// <exception cref="InvalidOperationException">
    /// No children were added, there are more than
    /// <see cref="Native.Constants.B3_MAX_CHILD_SHAPES"/> of them, or the engine
    /// refused to build the compound.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// A hull or mesh that was added has since been disposed.
    /// </exception>
    /// <remarks>
    /// The builder may be reused and built again; nothing here is consumed.
    /// </remarks>
    public unsafe CompoundGeometry Build()
    {
        if (ChildCount == 0)
        {
            throw new InvalidOperationException(
                "A compound needs at least one child shape. Add a sphere, capsule, hull or mesh first.");
        }

        if (ChildCount >= Constants.B3_MAX_CHILD_SHAPES)
        {
            throw new InvalidOperationException(
                $"A compound holds fewer than {Constants.B3_MAX_CHILD_SHAPES} children, and this one has {ChildCount}.");
        }

        var spheres = new b3CompoundSphereDef[_spheres.Count];
        for (int i = 0; i < spheres.Length; i++)
        {
            spheres[i] = new b3CompoundSphereDef
            {
                sphere = _spheres[i].Sphere.ToNative(),
                material = _spheres[i].Material.ToNative(),
            };
        }

        var capsules = new b3CompoundCapsuleDef[_capsules.Count];
        for (int i = 0; i < capsules.Length; i++)
        {
            capsules[i] = new b3CompoundCapsuleDef
            {
                capsule = _capsules[i].Capsule.ToNative(),
                material = _capsules[i].Material.ToNative(),
            };
        }

        var hulls = new b3CompoundHullDef[_hulls.Count];
        for (int i = 0; i < hulls.Length; i++)
        {
            hulls[i] = new b3CompoundHullDef
            {
                hull = _hulls[i].Hull.NativeHull,
                transform = new b3Transform { p = _hulls[i].Position, q = _hulls[i].Rotation },
                material = _hulls[i].Material.ToNative(),
            };
        }

        // One material per mesh instance, so the materials live in an array
        // parallel to the instances and each instance points at its own entry.
        var meshMaterials = new b3SurfaceMaterial[_meshes.Count];
        var meshes = new b3CompoundMeshDef[_meshes.Count];

        fixed (b3SurfaceMaterial* materialPtr = meshMaterials)
        {
            for (int i = 0; i < meshes.Length; i++)
            {
                meshMaterials[i] = _meshes[i].Material.ToNative();

                meshes[i] = new b3CompoundMeshDef
                {
                    meshData = _meshes[i].Mesh.NativeMesh,
                    transform = new b3Transform { p = _meshes[i].Position, q = _meshes[i].Rotation },
                    scale = _meshes[i].Scale,
                    materials = materialPtr + i,
                    materialCount = 1,
                };
            }

            fixed (b3CompoundSphereDef* spherePtr = spheres)
            fixed (b3CompoundCapsuleDef* capsulePtr = capsules)
            fixed (b3CompoundHullDef* hullPtr = hulls)
            fixed (b3CompoundMeshDef* meshPtr = meshes)
            {
                b3CompoundDef def = new()
                {
                    spheres = spherePtr,
                    sphereCount = spheres.Length,
                    capsules = capsulePtr,
                    capsuleCount = capsules.Length,
                    hulls = hullPtr,
                    hullCount = hulls.Length,
                    meshes = meshPtr,
                    meshCount = meshes.Length,
                };

                b3CompoundData* compound = B3.b3CreateCompound(&def);

                if (compound is null)
                {
                    throw new InvalidOperationException(
                        "Box3D could not bake this compound. The usual cause is a child whose geometry " +
                        "is degenerate or whose scale is below B3_MIN_SCALE.");
                }

                return new CompoundGeometry(compound);
            }
        }
    }
}

/// <summary>
/// Many child shapes baked into one, used for static geometry.
/// </summary>
/// <remarks>
/// <para>
/// Built by a <see cref="CompoundBuilder"/> and attached with
/// <see cref="Body.AddCompound"/>. The whole compound is one shape as far as the
/// broad phase is concerned, which is the point: it is how a piece of static
/// scenery made of a thousand rocks costs one proxy instead of a thousand.
/// </para>
/// <para>
/// <b>Lifetime.</b> Attaching a compound does not copy it. The shape points at
/// this object's memory, so it must outlive every shape built from it. The safe
/// order is: dispose the world, then dispose the compound.
/// </para>
/// <para>
/// <b>Static bodies only.</b> Box3D restricts baked compounds to static bodies.
/// For a moving object made of several shapes, attach the shapes to the body
/// one at a time instead - that is what a run-time compound is, and it works on
/// any body type.
/// </para>
/// </remarks>
public sealed unsafe class CompoundGeometry : IDisposable
{
    private b3CompoundData* _compound;

    internal CompoundGeometry(b3CompoundData* compound) => _compound = compound;

    /// <summary>Gets a value indicating whether this compound has been disposed.</summary>
    public bool IsDisposed => _compound is null;

    /// <summary>Gets the number of sphere children.</summary>
    /// <exception cref="ObjectDisposedException">The compound has been disposed.</exception>
    public int SphereCount
    {
        get
        {
            ThrowIfDisposed();
            return _compound->sphereCount;
        }
    }

    /// <summary>Gets the number of capsule children.</summary>
    /// <exception cref="ObjectDisposedException">The compound has been disposed.</exception>
    public int CapsuleCount
    {
        get
        {
            ThrowIfDisposed();
            return _compound->capsuleCount;
        }
    }

    /// <summary>Gets the number of hull children.</summary>
    /// <exception cref="ObjectDisposedException">The compound has been disposed.</exception>
    public int HullCount
    {
        get
        {
            ThrowIfDisposed();
            return _compound->hullCount;
        }
    }

    /// <summary>Gets the number of mesh children.</summary>
    /// <exception cref="ObjectDisposedException">The compound has been disposed.</exception>
    public int MeshCount
    {
        get
        {
            ThrowIfDisposed();
            return _compound->meshCount;
        }
    }

    /// <summary>Gets the total number of children.</summary>
    /// <exception cref="ObjectDisposedException">The compound has been disposed.</exception>
    public int ChildCount => SphereCount + CapsuleCount + HullCount + MeshCount;

    /// <summary>Gets the memory the compound occupies, in bytes.</summary>
    /// <exception cref="ObjectDisposedException">The compound has been disposed.</exception>
    public int ByteCount
    {
        get
        {
            ThrowIfDisposed();
            return _compound->byteCount;
        }
    }

    /// <summary>Gets the bounding box enclosing every child, in compound-local space.</summary>
    /// <exception cref="ObjectDisposedException">The compound has been disposed.</exception>
    public BoundingBox Bounds
    {
        get
        {
            ThrowIfDisposed();
            return BoundingBox.FromNative(B3.b3ComputeCompoundAABB(_compound, b3Transform.Identity));
        }
    }

    internal b3CompoundData* NativeCompound
    {
        get
        {
            ThrowIfDisposed();
            return _compound;
        }
    }

    /// <summary>Releases the compound.</summary>
    /// <remarks>
    /// <b>Every shape built from this compound must already be gone.</b> Shapes
    /// hold a borrowed pointer to this memory, so disposing while one is alive is
    /// a use-after-free inside the solver, not an exception. Dispose the world
    /// first.
    /// </remarks>
    public void Dispose()
    {
        if (_compound is not null)
        {
            B3.b3DestroyCompound(_compound);
            _compound = null;
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_compound is null, this);
}
