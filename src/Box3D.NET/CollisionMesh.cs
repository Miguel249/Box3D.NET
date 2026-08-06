// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using Box3D.Native;

namespace Box3D;

/// <summary>
/// How a mesh is built from its triangles.
/// </summary>
/// <remarks>
/// The defaults suit level geometry exported from a modelling tool. The two
/// switches worth understanding are <see cref="WeldVertices"/>, which repairs
/// meshes whose triangles do not share vertices, and <see cref="IdentifyEdges"/>,
/// which is what stops characters catching on the seams between triangles.
/// </remarks>
public readonly record struct MeshOptions
{
    /// <summary>
    /// Gets a value indicating whether nearby vertices are merged before building.
    /// </summary>
    /// <remarks>
    /// Meshes exported as triangle soups often have three separate copies of
    /// every shared corner. Welding them is what lets adjacency be computed at
    /// all, so this is usually wanted alongside <see cref="IdentifyEdges"/>.
    /// </remarks>
    public bool WeldVertices { get; init; }

    /// <summary>Gets the distance within which two vertices are considered the same.</summary>
    public float WeldTolerance { get; init; }

    /// <summary>
    /// Gets a value indicating whether triangle adjacency is computed.
    /// </summary>
    /// <remarks>
    /// This is what suppresses ghost collisions: without it, a body sliding
    /// across a flat floor made of triangles can catch on the internal edges
    /// between them. Worth the build cost for anything a character walks on.
    /// </remarks>
    public bool IdentifyEdges { get; init; }

    /// <summary>
    /// Gets a value indicating whether a median split is used instead of a
    /// surface area heuristic when building the hierarchy.
    /// </summary>
    /// <remarks>
    /// Faster to build and slightly slower to query. A good trade for meshes
    /// laid out on a grid, such as terrain tiles.
    /// </remarks>
    public bool UseMedianSplit { get; init; }

    /// <summary>Gets options suited to level geometry a character walks on.</summary>
    public static MeshOptions Default => new()
    {
        WeldVertices = true,
        WeldTolerance = 1e-4f,
        IdentifyEdges = true,
        UseMedianSplit = false,
    };

    /// <summary>Gets options that build as fast as possible, skipping the repair passes.</summary>
    /// <remarks>Appropriate for meshes you already know are clean and welded.</remarks>
    public static MeshOptions Fast => new()
    {
        WeldVertices = false,
        WeldTolerance = 0.0f,
        IdentifyEdges = false,
        UseMedianSplit = true,
    };
}

/// <summary>
/// A triangle mesh, used for static level geometry.
/// </summary>
/// <remarks>
/// <para>
/// A mesh is built once and shared by as many shapes as needed, at different
/// scales if you like. Building one is expensive; attaching it is not.
/// </para>
/// <para>
/// <b>Lifetime.</b> Unlike every other shape, attaching a mesh does not copy it.
/// The shape points at this object's memory, so it must outlive every shape
/// built from it. Disposing it while a shape still refers to it will crash
/// inside the solver rather than raise an exception. The safe order is: dispose
/// the world, then dispose the meshes.
/// </para>
/// <para>
/// <b>Static bodies only.</b> Box3D generates mesh contacts against static
/// bodies. Attaching one to a dynamic or kinematic body is rejected.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Build once, from your level data.
/// using var terrain = CollisionMesh.FromTriangles(vertices, indices);
///
/// using (var world = new PhysicsWorld())
/// {
///     Body level = world.CreateStaticBody();
///     level.AddMesh(terrain);
///
///     // The same mesh again, at half scale, somewhere else.
///     Body scaled = world.CreateStaticBody(new Vector3(100.0f, 0.0f, 0.0f));
///     scaled.AddMesh(terrain, scale: new Vector3(0.5f));
///
///     Simulate(world);
/// }
/// // World first, mesh second.
/// </code>
/// </example>
public sealed unsafe class CollisionMesh : IDisposable
{
    private b3MeshData* _mesh;

    private CollisionMesh(b3MeshData* mesh) => _mesh = mesh;

    /// <summary>Gets a value indicating whether this mesh has been disposed.</summary>
    public bool IsDisposed => _mesh is null;

    /// <summary>Gets the number of vertices.</summary>
    /// <exception cref="ObjectDisposedException">The mesh has been disposed.</exception>
    public int VertexCount
    {
        get
        {
            ThrowIfDisposed();
            return _mesh->vertexCount;
        }
    }

    /// <summary>Gets the number of triangles.</summary>
    /// <exception cref="ObjectDisposedException">The mesh has been disposed.</exception>
    public int TriangleCount
    {
        get
        {
            ThrowIfDisposed();
            return _mesh->triangleCount;
        }
    }

    /// <summary>Gets the local-space bounding box.</summary>
    /// <exception cref="ObjectDisposedException">The mesh has been disposed.</exception>
    public BoundingBox Bounds
    {
        get
        {
            ThrowIfDisposed();
            return BoundingBox.FromNative(_mesh->bounds);
        }
    }

    /// <summary>Gets the memory the mesh occupies, in bytes.</summary>
    /// <exception cref="ObjectDisposedException">The mesh has been disposed.</exception>
    public int ByteCount
    {
        get
        {
            ThrowIfDisposed();
            return _mesh->byteCount;
        }
    }

    /// <summary>
    /// Gets the number of degenerate triangles found while building, which were
    /// discarded.
    /// </summary>
    /// <remarks>
    /// A non-zero count usually means the source geometry has zero-area
    /// triangles. They are skipped rather than rejected, so this is worth
    /// checking when a mesh does not collide where you expect.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The mesh has been disposed.</exception>
    public int DegenerateTriangleCount
    {
        get
        {
            ThrowIfDisposed();
            return _mesh->degenerateCount;
        }
    }

    internal b3MeshData* NativeMesh
    {
        get
        {
            ThrowIfDisposed();
            return _mesh;
        }
    }

    /// <summary>Builds a mesh from vertices and triangle indices.</summary>
    /// <param name="vertices">The vertex positions, in mesh-local space.</param>
    /// <param name="indices">
    /// Three indices per triangle, wound counter-clockwise when seen from the
    /// side the surface faces.
    /// </param>
    /// <param name="options">The build options, or null for <see cref="MeshOptions.Default"/>.</param>
    /// <param name="materialIndices">
    /// One material index per triangle, selecting into the per-triangle materials
    /// of the shape definition. Leave empty for a single material.
    /// </param>
    /// <returns>The mesh.</returns>
    /// <exception cref="ArgumentException">
    /// The inputs are too small, the index count is not a multiple of three, an
    /// index is out of range, or the material index count does not match the
    /// triangle count.
    /// </exception>
    /// <remarks>
    /// The inputs are copied, so the arrays may be reused or released as soon as
    /// this returns. Winding decides which side of a triangle is solid, so a
    /// mesh built with the wrong winding lets bodies fall through from above.
    /// </remarks>
    public static CollisionMesh FromTriangles(
        ReadOnlySpan<Vector3> vertices,
        ReadOnlySpan<int> indices,
        MeshOptions? options = null,
        ReadOnlySpan<byte> materialIndices = default)
    {
        if (vertices.Length < 3)
        {
            throw new ArgumentException(
                $"A mesh needs at least three vertices, got {vertices.Length}.",
                nameof(vertices));
        }

        if (indices.Length < 3 || indices.Length % 3 != 0)
        {
            throw new ArgumentException(
                $"The index count must be a positive multiple of three, got {indices.Length}.",
                nameof(indices));
        }

        int triangleCount = indices.Length / 3;

        // Box3D asserts on an out-of-range index, and asserts are compiled out of
        // a release build, so an index past the end would read arbitrary memory
        // while building the hierarchy. Checking here is cheap next to the build.
        for (int i = 0; i < indices.Length; i++)
        {
            if ((uint)indices[i] >= (uint)vertices.Length)
            {
                throw new ArgumentException(
                    $"Index {i} refers to vertex {indices[i]}, but there are only {vertices.Length} vertices.",
                    nameof(indices));
            }
        }

        if (!materialIndices.IsEmpty && materialIndices.Length != triangleCount)
        {
            throw new ArgumentException(
                $"Expected one material index per triangle: {triangleCount} expected, {materialIndices.Length} given.",
                nameof(materialIndices));
        }

        MeshOptions settings = options ?? MeshOptions.Default;

        fixed (Vector3* vertexPtr = vertices)
        fixed (int* indexPtr = indices)
        fixed (byte* materialPtr = materialIndices)
        {
            b3MeshDef def = new()
            {
                vertices = vertexPtr,
                indices = indexPtr,
                materialIndices = materialIndices.IsEmpty ? null : materialPtr,
                vertexCount = vertices.Length,
                triangleCount = triangleCount,
                weldVertices = settings.WeldVertices,
                weldTolerance = settings.WeldTolerance,
                identifyEdges = settings.IdentifyEdges,
                useMedianSplit = settings.UseMedianSplit,
            };

            b3MeshData* mesh = B3.b3CreateMesh(&def, null, 0);

            if (mesh is null)
            {
                throw new ArgumentException(
                    "Box3D could not build a mesh from this geometry.",
                    nameof(vertices));
            }

            return new CollisionMesh(mesh);
        }
    }

    /// <summary>Builds a flat grid in the xz plane, useful as a test floor.</summary>
    /// <param name="xCount">The number of cells along the x axis.</param>
    /// <param name="zCount">The number of cells along the z axis.</param>
    /// <param name="cellWidth">The size of each cell.</param>
    /// <param name="materialCount">The number of distinct materials to distribute over the cells.</param>
    /// <param name="identifyEdges">Whether to compute adjacency, which suppresses ghost collisions.</param>
    /// <returns>The mesh.</returns>
    public static CollisionMesh Grid(
        int xCount,
        int zCount,
        float cellWidth,
        int materialCount = 1,
        bool identifyEdges = true)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(xCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(zCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cellWidth);

        return new CollisionMesh(B3.b3CreateGridMesh(xCount, zCount, cellWidth, materialCount, identifyEdges));
    }

    /// <summary>Builds a rolling wave surface, useful for testing uneven ground.</summary>
    /// <param name="xCount">The number of cells along the x axis.</param>
    /// <param name="zCount">The number of cells along the z axis.</param>
    /// <param name="cellWidth">The size of each cell.</param>
    /// <param name="amplitude">The height of the waves.</param>
    /// <param name="rowFrequency">The wave frequency along one axis.</param>
    /// <param name="columnFrequency">The wave frequency along the other.</param>
    /// <returns>The mesh.</returns>
    /// <remarks>
    /// <para>
    /// <b>Watch the frequency against the cell width.</b> The surface is sampled
    /// at <c>sin(2 * pi * frequency * cellWidth * i)</c> for each grid index
    /// <c>i</c>, so whenever <c>frequency * cellWidth</c> lands on a whole
    /// number every sample falls on a zero crossing and the result is perfectly
    /// flat. The default frequency with a cell width of one does exactly that.
    /// </para>
    /// <para>
    /// Keep <c>frequency * cellWidth</c> well below one — a quarter gives four
    /// cells per wavelength, which is about the coarsest that still looks like a
    /// wave.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Sixteen cells of one metre, one wave every four cells.
    /// using var waves = CollisionMesh.Wave(16, 16, cellWidth: 1.0f, amplitude: 2.0f,
    ///     rowFrequency: 0.25f, columnFrequency: 0.25f);
    /// </code>
    /// </example>
    public static CollisionMesh Wave(
        int xCount,
        int zCount,
        float cellWidth,
        float amplitude,
        float rowFrequency = 0.25f,
        float columnFrequency = 0.25f)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(xCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(zCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cellWidth);

        return new CollisionMesh(
            B3.b3CreateWaveMesh(xCount, zCount, cellWidth, amplitude, rowFrequency, columnFrequency));
    }

    /// <summary>Builds a hollow box, useful as a container that things fall into.</summary>
    /// <param name="center">The centre of the box.</param>
    /// <param name="extents">The half-widths of the box.</param>
    /// <returns>The mesh.</returns>
    public static CollisionMesh HollowBox(Vector3 center, Vector3 extents) =>
        new(B3.b3CreateHollowBoxMesh(center, extents));

    /// <summary>Builds a closed box as a triangle mesh.</summary>
    /// <param name="center">The centre of the box.</param>
    /// <param name="extents">The half-widths of the box.</param>
    /// <param name="identifyEdges">Whether to compute adjacency.</param>
    /// <returns>The mesh.</returns>
    /// <remarks>
    /// A <see cref="Box"/> is cheaper for a solid box. This exists for cases that
    /// specifically need mesh behaviour, such as per-triangle materials.
    /// </remarks>
    public static CollisionMesh BoxMesh(Vector3 center, Vector3 extents, bool identifyEdges = true) =>
        new(B3.b3CreateBoxMesh(center, extents, identifyEdges));

    /// <summary>Releases the mesh.</summary>
    /// <remarks>
    /// <b>Every shape built from this mesh must already be gone.</b> Shapes hold
    /// a borrowed pointer to this memory, so disposing while one is alive is a
    /// use-after-free inside the solver, not an exception. Dispose the world
    /// first.
    /// </remarks>
    public void Dispose()
    {
        if (_mesh is not null)
        {
            B3.b3DestroyMesh(_mesh);
            _mesh = null;
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_mesh is null, this);
}
