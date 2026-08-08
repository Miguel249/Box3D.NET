// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using Box3D.Visualizer.Rendering;

namespace Box3D.Visualizer.Encoding;

/// <summary>
/// Reduces a set of frames to one shared palette of at most 255 colours.
/// </summary>
/// <remarks>
/// <para>
/// A GIF gets 256 palette entries for the whole animation, and one of those is
/// spent on transparency so that unchanged pixels can be left out of later
/// frames. Median cut is the classic answer: repeatedly split the colour cloud
/// across its widest axis at the median, which spends entries where the colours
/// actually are rather than on a fixed cube.
/// </para>
/// <para>
/// The palette is built once from every frame together. Per-frame palettes
/// would each be better and the animation would shimmer as they disagreed.
/// </para>
/// </remarks>
internal sealed class Quantizer
{
    // Six bits per channel. Coarse enough to fit in memory, fine enough that
    // the dither pattern below is not quantized away before it is used.
    private const int CacheBits = 6;
    private const int CacheSize = 1 << (CacheBits * 3);

    private static readonly int[] Bayer =
    [
        0, 32, 8, 40, 2, 34, 10, 42,
        48, 16, 56, 24, 50, 18, 58, 26,
        12, 44, 4, 36, 14, 46, 6, 38,
        60, 28, 52, 20, 62, 30, 54, 22,
        3, 35, 11, 43, 1, 33, 9, 41,
        51, 19, 59, 27, 49, 17, 57, 25,
        15, 47, 7, 39, 13, 45, 5, 37,
        63, 31, 55, 23, 61, 29, 53, 21,
    ];

    private readonly byte[] _palette;
    private readonly int _colorCount;
    private readonly int[] _cache;

    private Quantizer(byte[] palette, int colorCount)
    {
        _palette = palette;
        _colorCount = colorCount;
        _cache = new int[CacheSize];

        Array.Fill(_cache, -1);
    }

    /// <summary>Gets the palette, three bytes per entry, always 256 entries long.</summary>
    /// <remarks>The last entry is the transparent one and is never chosen by <see cref="Map"/>.</remarks>
    public byte[] Palette => _palette;

    /// <summary>Gets the index reserved for transparency.</summary>
    public static byte TransparentIndex => 255;

    /// <summary>Builds a palette covering every frame.</summary>
    /// <param name="frames">The frames the palette has to serve.</param>
    /// <param name="colorCount">How many entries to spend, at most 255.</param>
    /// <returns>The quantizer.</returns>
    public static Quantizer Build(IReadOnlyList<Image> frames, int colorCount = 255)
    {
        ArgumentNullException.ThrowIfNull(frames);
        ArgumentOutOfRangeException.ThrowIfLessThan(colorCount, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(colorCount, 255);

        int[] samples = Sample(frames);
        List<Bucket> boxes = MedianCut(samples, colorCount);

        byte[] palette = new byte[256 * 3];

        for (int i = 0; i < boxes.Count; i++)
        {
            (byte r, byte g, byte b) = Average(samples, boxes[i]);

            palette[(i * 3) + 0] = r;
            palette[(i * 3) + 1] = g;
            palette[(i * 3) + 2] = b;
        }

        return new Quantizer(palette, boxes.Count);
    }

    /// <summary>Maps a frame onto the palette, with an ordered dither.</summary>
    /// <param name="image">The frame.</param>
    /// <returns>One palette index per pixel.</returns>
    /// <remarks>
    /// The dither is ordered rather than error diffused on purpose. Error
    /// diffusion looks better on a still, but the pattern it produces is
    /// exquisitely sensitive to the input, so a body moving in one corner
    /// changes the noise everywhere - which both flickers and defeats the
    /// frame differencing that keeps the file small.
    /// </remarks>
    public byte[] Map(Image image)
    {
        ArgumentNullException.ThrowIfNull(image);

        byte[] indices = new byte[image.Width * image.Height];

        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                int pixel = ((y * image.Width) + x) * 3;
                float bias = (Bayer[((y & 7) * 8) + (x & 7)] * (1.0f / 64.0f)) - 0.5f;

                int r = Clamp(image.Pixels[pixel] + (bias * 6.0f));
                int g = Clamp(image.Pixels[pixel + 1] + (bias * 6.0f));
                int b = Clamp(image.Pixels[pixel + 2] + (bias * 6.0f));

                indices[(y * image.Width) + x] = (byte)Nearest(r, g, b);
            }
        }

        return indices;
    }

    private int Nearest(int r, int g, int b)
    {
        int key = ((r >> (8 - CacheBits)) << (CacheBits * 2))
            | ((g >> (8 - CacheBits)) << CacheBits)
            | (b >> (8 - CacheBits));

        int cached = _cache[key];
        if (cached >= 0)
        {
            return cached;
        }

        int best = 0;
        int bestDistance = int.MaxValue;

        for (int i = 0; i < _colorCount; i++)
        {
            int dr = r - _palette[(i * 3) + 0];
            int dg = g - _palette[(i * 3) + 1];
            int db = b - _palette[(i * 3) + 2];

            // Weighted towards green, which is where the eye keeps most of its
            // luminance sensitivity.
            int distance = (dr * dr * 3) + (dg * dg * 6) + (db * db * 1);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }

        _cache[key] = best;
        return best;
    }

    private static int[] Sample(IReadOnlyList<Image> frames)
    {
        const int Target = 200_000;

        int total = 0;
        foreach (Image frame in frames)
        {
            total += frame.Width * frame.Height;
        }

        int step = Math.Max(1, total / Target);
        var samples = new List<int>(Math.Min(total, Target) + 16);

        foreach (Image frame in frames)
        {
            int count = frame.Width * frame.Height;

            for (int i = 0; i < count; i += step)
            {
                int pixel = i * 3;
                samples.Add((frame.Pixels[pixel] << 16) | (frame.Pixels[pixel + 1] << 8) | frame.Pixels[pixel + 2]);
            }
        }

        return samples.ToArray();
    }

    private static List<Bucket> MedianCut(int[] samples, int colorCount)
    {
        var boxes = new List<Bucket> { new(0, samples.Length) };

        while (boxes.Count < colorCount)
        {
            int chosen = -1;
            int widest = 0;
            int axis = 0;

            for (int i = 0; i < boxes.Count; i++)
            {
                Bucket box = boxes[i];
                if (box.Count < 2)
                {
                    continue;
                }

                Extent(samples, box, out int spread, out int channel);

                if (spread > widest)
                {
                    widest = spread;
                    chosen = i;
                    axis = channel;
                }
            }

            // Every remaining box holds a single colour: there is nothing left
            // to split, and asking for more entries would not buy any.
            if (chosen < 0)
            {
                break;
            }

            Bucket target = boxes[chosen];
            int shift = (2 - axis) * 8;

            Array.Sort(samples, target.Start, target.Count, new ChannelComparer(shift));

            int half = target.Count / 2;
            boxes[chosen] = new Bucket(target.Start, half);
            boxes.Add(new Bucket(target.Start + half, target.Count - half));
        }

        return boxes;
    }

    private static void Extent(int[] samples, Bucket box, out int spread, out int axis)
    {
        Span<int> low = [255, 255, 255];
        Span<int> high = [0, 0, 0];

        for (int i = box.Start; i < box.Start + box.Count; i++)
        {
            int color = samples[i];

            for (int channel = 0; channel < 3; channel++)
            {
                int value = (color >> ((2 - channel) * 8)) & 0xFF;
                low[channel] = Math.Min(low[channel], value);
                high[channel] = Math.Max(high[channel], value);
            }
        }

        spread = 0;
        axis = 0;

        for (int channel = 0; channel < 3; channel++)
        {
            int range = high[channel] - low[channel];
            if (range > spread)
            {
                spread = range;
                axis = channel;
            }
        }
    }

    private static (byte R, byte G, byte B) Average(int[] samples, Bucket box)
    {
        long r = 0;
        long g = 0;
        long b = 0;

        for (int i = box.Start; i < box.Start + box.Count; i++)
        {
            int color = samples[i];
            r += (color >> 16) & 0xFF;
            g += (color >> 8) & 0xFF;
            b += color & 0xFF;
        }

        int count = Math.Max(1, box.Count);
        return ((byte)(r / count), (byte)(g / count), (byte)(b / count));
    }

    private static int Clamp(float value) => (int)Math.Clamp(MathF.Round(value), 0.0f, 255.0f);

    private readonly record struct Bucket(int Start, int Count);

    private sealed class ChannelComparer(int shift) : IComparer<int>
    {
        public int Compare(int x, int y) => ((x >> shift) & 0xFF).CompareTo((y >> shift) & 0xFF);
    }
}
