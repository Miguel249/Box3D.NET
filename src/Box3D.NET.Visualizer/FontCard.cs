// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Numerics;
using Box3D.Visualizer.Encoding;
using Box3D.Visualizer.Rendering;

namespace Box3D.Visualizer;

/// <summary>
/// A proof sheet for the bitmap font.
/// </summary>
/// <remarks>
/// <para>
/// The font in <see cref="GlyphFont"/> is a table of bytes typed out by hand,
/// and a single wrong byte is a character that is subtly wrong in exactly the
/// places nobody looks - a <c>5</c> that reads as an <c>S</c> in a mass label.
/// The only way to check a table like that is to look at all of it.
/// </para>
/// <para>
/// It goes through the renderer rather than writing pixels directly, so what is
/// on the sheet is what a scene would get: the same projection, the same
/// supersampling, the same shadow. Half the rows sit over a pale band, because
/// the thing worth proving is that a label is readable whatever it lands on.
/// </para>
/// </remarks>
internal static class FontCard
{
    private const int Columns = 16;
    private const int Width = 1000;
    private const int Height = 560;

    /// <summary>Renders the sheet.</summary>
    /// <param name="path">Where the image goes.</param>
    public static void Write(string path)
    {
        var renderer = new Renderer(Width, Height, 2);

        renderer.Begin(Camera.Orbit(Vector3.Zero, 10.0f, 0.0f, 0.0f));

        // A pale band under the bottom half. White text on a light surface is
        // the case the one-pixel shadow exists for.
        renderer.DrawLine(
            new Vector3(-6.0f, -0.75f, -0.5f),
            new Vector3(6.0f, -0.75f, -0.5f),
            Rgb.FromHex(0xD9CFC1),
            thickness: 150.0f);

        renderer.DrawText(
            new Vector3(-3.05f, 2.45f, 0.0f),
            $"5x7, {GlyphFont.GlyphCount} glyphs",
            Rgb.FromHex(0xF5D76E),
            scale: 3.0f);

        int rows = ((GlyphFont.GlyphCount + Columns) - 1) / Columns;

        for (int row = 0; row < rows; row++)
        {
            int start = row * Columns;
            int count = Math.Min(Columns, GlyphFont.GlyphCount - start);

            char[] characters = new char[count];
            for (int i = 0; i < count; i++)
            {
                characters[i] = (char)(GlyphFont.First + start + i);
            }

            renderer.DrawText(
                new Vector3(-3.05f, 1.55f - (row * 0.74f), 0.0f),
                new string(characters),
                Rgb.FromHex(0xFFFFFF),
                scale: 5.0f);
        }

        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        PngWriter.Write(renderer.Resolve(), path);
    }
}
