// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using Box3D.Native;

namespace Box3D;

/// <summary>
/// A terrain surface defined by a grid of heights.
/// </summary>
/// <remarks>
/// <para>
/// A height field is the right shape for terrain: it stores one number per grid
/// point instead of triangles, compresses those numbers, and queries faster than
/// the equivalent mesh. The cost is the restriction that it is a height map, so
/// it cannot describe caves or overhangs.
/// </para>
/// <para>
/// <b>Lifetime.</b> As with <see cref="CollisionMesh"/>, attaching a height
/// field does not copy it. It must outlive every shape built from it, and
/// disposing it early crashes inside the solver rather than raising. Dispose the
/// world first.
/// </para>
/// <para>
/// <b>Static bodies only.</b>
/// </para>
/// <para>
/// <b>Quantization.</b> Heights are stored compressed against the minimum and
/// maximum given at build time. Two height fields that must line up along an
/// edge have to share the same minimum and maximum, or their quantization steps
/// differ and a seam appears.
/// </para>
/// <para>
/// <b>Placement.</b> A height field starts at its body's origin and extends in
/// positive x and z, rather than being centred on it. A 64 by 64 grid of
/// two-metre cells therefore covers x and z from 0 to 128. Position the body to
/// place it, or read <see cref="Bounds"/> to find out where it ended up.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // A 256 by 256 terrain from your own height data.
/// float[] heights = LoadHeightmap();
///
/// using var terrain = HeightField.FromHeights(
///     heights,
///     columnCount: 256,
///     rowCount: 256,
///     scale: new Vector3(1.0f, 100.0f, 1.0f));   // 1m cells, 100m of relief
///
/// Body ground = world.CreateStaticBody();
/// ground.AddHeightField(terrain);
/// </code>
/// </example>
public sealed unsafe class HeightField : IDisposable
{
    /// <summary>
    /// The material index that marks a cell as a hole, which nothing collides with.
    /// </summary>
    /// <remarks>Use it to cut a doorway or a chasm into otherwise solid terrain.</remarks>
    public const byte HoleMaterial = Constants.B3_HEIGHT_FIELD_HOLE;

    private b3HeightFieldData* _field;

    private HeightField(b3HeightFieldData* field) => _field = field;

    /// <summary>Gets a value indicating whether this height field has been disposed.</summary>
    public bool IsDisposed => _field is null;

    /// <summary>Gets the number of grid columns along the x axis.</summary>
    /// <exception cref="ObjectDisposedException">The height field has been disposed.</exception>
    public int ColumnCount
    {
        get
        {
            ThrowIfDisposed();
            return _field->columnCount;
        }
    }

    /// <summary>Gets the number of grid rows along the z axis.</summary>
    /// <exception cref="ObjectDisposedException">The height field has been disposed.</exception>
    public int RowCount
    {
        get
        {
            ThrowIfDisposed();
            return _field->rowCount;
        }
    }

    /// <summary>Gets the local-space bounding box.</summary>
    /// <exception cref="ObjectDisposedException">The height field has been disposed.</exception>
    public BoundingBox Bounds
    {
        get
        {
            ThrowIfDisposed();
            return BoundingBox.FromNative(_field->aabb);
        }
    }

    /// <summary>Gets the memory the height field occupies, in bytes.</summary>
    /// <remarks>
    /// Compare against a mesh of the same resolution: heights are stored as
    /// sixteen-bit values, so a height field is far smaller than the equivalent
    /// triangle soup.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The height field has been disposed.</exception>
    public int ByteCount
    {
        get
        {
            ThrowIfDisposed();
            return _field->byteCount;
        }
    }

    internal b3HeightFieldData* NativeHeightField
    {
        get
        {
            ThrowIfDisposed();
            return _field;
        }
    }

    /// <summary>Builds a height field from a grid of heights.</summary>
    /// <param name="heights">
    /// The height at each grid point, row by row. Must hold exactly
    /// <paramref name="columnCount"/> times <paramref name="rowCount"/> values.
    /// </param>
    /// <param name="columnCount">The number of grid lines along the x axis.</param>
    /// <param name="rowCount">The number of grid lines along the z axis.</param>
    /// <param name="scale">
    /// The size of the field. The x and z components are the cell size; the y
    /// component multiplies the heights.
    /// </param>
    /// <param name="materialIndices">
    /// One material per cell, so <c>(columnCount - 1) * (rowCount - 1)</c> values.
    /// <see cref="HoleMaterial"/> marks a hole. Leave empty for solid terrain
    /// with a single material.
    /// </param>
    /// <param name="minimumHeight">
    /// The lowest height used for quantization, or null to take it from the data.
    /// Set it explicitly when several fields must line up.
    /// </param>
    /// <param name="maximumHeight">The highest height used for quantization, or null to take it from the data.</param>
    /// <param name="clockwiseWinding">Whether to flip the surface, so that it faces downwards.</param>
    /// <returns>The height field.</returns>
    /// <exception cref="ArgumentException">The counts do not agree, or the scale is not positive.</exception>
    /// <remarks>The inputs are copied, so the arrays may be released once this returns.</remarks>
    public static HeightField FromHeights(
        ReadOnlySpan<float> heights,
        int columnCount,
        int rowCount,
        Vector3 scale,
        ReadOnlySpan<byte> materialIndices = default,
        float? minimumHeight = null,
        float? maximumHeight = null,
        bool clockwiseWinding = false)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(columnCount, 2);
        ArgumentOutOfRangeException.ThrowIfLessThan(rowCount, 2);

        if (scale.X <= 0.0f || scale.Y <= 0.0f || scale.Z <= 0.0f)
        {
            throw new ArgumentException(
                $"Every scale component must be positive, got {scale}.",
                nameof(scale));
        }

        int expectedHeights = columnCount * rowCount;
        if (heights.Length != expectedHeights)
        {
            throw new ArgumentException(
                $"Expected one height per grid point: {expectedHeights} for a {columnCount} by {rowCount} grid, " +
                $"got {heights.Length}.",
                nameof(heights));
        }

        // Materials are per cell, and a grid of N by M points has (N-1) by (M-1)
        // cells. Getting this off by one is the easiest mistake to make here, so
        // the message spells the arithmetic out.
        int expectedMaterials = (columnCount - 1) * (rowCount - 1);
        if (!materialIndices.IsEmpty && materialIndices.Length != expectedMaterials)
        {
            throw new ArgumentException(
                $"Expected one material per cell: {expectedMaterials} for a {columnCount} by {rowCount} grid " +
                $"({columnCount - 1} by {rowCount - 1} cells), got {materialIndices.Length}.",
                nameof(materialIndices));
        }

        float minimum = minimumHeight ?? Minimum(heights);
        float maximum = maximumHeight ?? Maximum(heights);

        if (maximum < minimum)
        {
            throw new ArgumentException(
                $"The maximum height {maximum} is below the minimum {minimum}.",
                nameof(maximumHeight));
        }

        // A perfectly flat field would give a zero quantization range, so it is
        // widened slightly rather than rejected.
        if (maximum - minimum < 1e-6f)
        {
            maximum = minimum + 1.0f;
        }

        fixed (float* heightPtr = heights)
        fixed (byte* materialPtr = materialIndices)
        {
            b3HeightFieldDef def = new()
            {
                heights = heightPtr,
                materialIndices = materialIndices.IsEmpty ? null : materialPtr,
                scale = scale,
                countX = columnCount,
                countZ = rowCount,
                globalMinimumHeight = minimum,
                globalMaximumHeight = maximum,
                clockwiseWinding = clockwiseWinding,
            };

            b3HeightFieldData* field = B3.b3CreateHeightField(&def);

            if (field is null)
            {
                throw new ArgumentException("Box3D could not build a height field from this data.", nameof(heights));
            }

            return new HeightField(field);
        }
    }

    /// <summary>Builds a flat grid, useful as a test floor.</summary>
    /// <param name="rowCount">The number of grid lines along the z axis.</param>
    /// <param name="columnCount">The number of grid lines along the x axis.</param>
    /// <param name="scale">The cell size and height scale.</param>
    /// <param name="withHoles">Whether to punch holes into some cells.</param>
    /// <returns>The height field.</returns>
    public static HeightField Grid(int rowCount, int columnCount, Vector3 scale, bool withHoles = false)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(rowCount, 2);
        ArgumentOutOfRangeException.ThrowIfLessThan(columnCount, 2);

        return new HeightField(B3.b3CreateGrid(rowCount, columnCount, scale, withHoles));
    }

    /// <summary>Builds a rolling wave surface, useful for testing uneven terrain.</summary>
    /// <param name="rowCount">The number of grid lines along the z axis.</param>
    /// <param name="columnCount">The number of grid lines along the x axis.</param>
    /// <param name="scale">The cell size and height scale.</param>
    /// <param name="rowFrequency">The wave frequency along one axis.</param>
    /// <param name="columnFrequency">The wave frequency along the other.</param>
    /// <param name="withHoles">Whether to punch holes into some cells.</param>
    /// <returns>The height field.</returns>
    public static HeightField Wave(
        int rowCount,
        int columnCount,
        Vector3 scale,
        float rowFrequency = 1.0f,
        float columnFrequency = 1.0f,
        bool withHoles = false)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(rowCount, 2);
        ArgumentOutOfRangeException.ThrowIfLessThan(columnCount, 2);

        return new HeightField(
            B3.b3CreateWave(rowCount, columnCount, scale, rowFrequency, columnFrequency, withHoles));
    }

    /// <summary>Releases the height field.</summary>
    /// <remarks>
    /// <b>Every shape built from it must already be gone.</b> Dispose the world
    /// first; a shape holds a borrowed pointer to this memory.
    /// </remarks>
    public void Dispose()
    {
        if (_field is not null)
        {
            B3.b3DestroyHeightField(_field);
            _field = null;
        }
    }

    private static float Minimum(ReadOnlySpan<float> values)
    {
        float min = float.MaxValue;
        foreach (float value in values)
        {
            if (value < min)
            {
                min = value;
            }
        }

        return min;
    }

    private static float Maximum(ReadOnlySpan<float> values)
    {
        float max = float.MinValue;
        foreach (float value in values)
        {
            if (value > max)
            {
                max = value;
            }
        }

        return max;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_field is null, this);
}
