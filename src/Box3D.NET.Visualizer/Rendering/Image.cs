// SPDX-License-Identifier: MIT

using System;

namespace Box3D.Visualizer.Rendering;

/// <summary>
/// An 8-bit RGB image, which is what both encoders in this project consume.
/// </summary>
internal sealed class Image
{
    /// <summary>Creates a black image.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    public Image(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        Width = width;
        Height = height;
        Pixels = new byte[width * height * 3];
    }

    /// <summary>Gets the width in pixels.</summary>
    public int Width { get; }

    /// <summary>Gets the height in pixels.</summary>
    public int Height { get; }

    /// <summary>Gets the pixels, three bytes each, row by row from the top.</summary>
    public byte[] Pixels { get; }
}
