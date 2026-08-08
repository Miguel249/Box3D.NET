// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using Box3D.Visualizer.Rendering;

namespace Box3D.Visualizer.Encoding;

/// <summary>
/// Writes an animated GIF.
/// </summary>
/// <remarks>
/// <para>
/// GIF is here because it is the one animated format a README on any host will
/// play inline, without a video element, a codec or a click. The cost is 256
/// colours and LZW, both of which are handled below: <see cref="Quantizer"/>
/// picks the palette, and each frame stores only the rectangle that changed,
/// with unchanged pixels left transparent so the previous frame shows through.
/// On these scenes - a static backdrop with a few bodies moving over it - that
/// is most of the file.
/// </para>
/// </remarks>
internal static class GifWriter
{
    /// <summary>Writes an animation to a file.</summary>
    /// <param name="frames">The frames, all the same size.</param>
    /// <param name="delayHundredths">How long each frame is shown, in hundredths of a second.</param>
    /// <param name="path">Where to write it.</param>
    public static void Write(IReadOnlyList<Image> frames, int delayHundredths, string path)
    {
        ArgumentNullException.ThrowIfNull(frames);
        ArgumentOutOfRangeException.ThrowIfZero(frames.Count);

        using FileStream file = File.Create(path);
        Write(frames, delayHundredths, file);
    }

    /// <summary>Writes an animation to a stream.</summary>
    /// <param name="frames">The frames, all the same size.</param>
    /// <param name="delayHundredths">How long each frame is shown, in hundredths of a second.</param>
    /// <param name="output">Where to write it.</param>
    public static void Write(IReadOnlyList<Image> frames, int delayHundredths, Stream output)
    {
        ArgumentNullException.ThrowIfNull(frames);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentOutOfRangeException.ThrowIfZero(frames.Count);

        int width = frames[0].Width;
        int height = frames[0].Height;

        Quantizer quantizer = Quantizer.Build(frames);

        var indexed = new List<byte[]>(frames.Count);
        foreach (Image frame in frames)
        {
            if (frame.Width != width || frame.Height != height)
            {
                throw new ArgumentException("Every frame must be the same size.", nameof(frames));
            }

            indexed.Add(quantizer.Map(frame));
        }

        WriteHeader(output, width, height, quantizer.Palette);

        for (int i = 0; i < indexed.Count; i++)
        {
            byte[] current = indexed[i];
            byte[]? previous = i == 0 ? null : indexed[i - 1];

            WriteFrame(output, current, previous, width, height, delayHundredths);
        }

        output.WriteByte(0x3B);
    }

    private static void WriteHeader(Stream output, int width, int height, byte[] palette)
    {
        output.Write("GIF89a"u8);

        WriteShort(output, width);
        WriteShort(output, height);

        // Global colour table present, eight bits of colour resolution, 256 entries.
        output.WriteByte(0xF7);
        output.WriteByte(0);    // background colour index
        output.WriteByte(0);    // no pixel aspect ratio

        output.Write(palette, 0, 768);

        // The Netscape application extension, which is how a GIF says "loop
        // forever". Without it every viewer plays the animation once.
        output.Write([0x21, 0xFF, 0x0B]);
        output.Write("NETSCAPE2.0"u8);
        output.Write([0x03, 0x01, 0x00, 0x00, 0x00]);
    }

    private static void WriteFrame(
        Stream output,
        byte[] current,
        byte[]? previous,
        int width,
        int height,
        int delayHundredths)
    {
        int left = 0;
        int top = 0;
        int frameWidth = width;
        int frameHeight = height;
        byte[] payload = current;

        if (previous is not null)
        {
            DirtyRectangle(current, previous, width, height, out left, out top, out frameWidth, out frameHeight);

            payload = new byte[frameWidth * frameHeight];

            for (int y = 0; y < frameHeight; y++)
            {
                int source = ((top + y) * width) + left;
                int destination = y * frameWidth;

                for (int x = 0; x < frameWidth; x++)
                {
                    byte value = current[source + x];
                    payload[destination + x] = value == previous[source + x]
                        ? Quantizer.TransparentIndex
                        : value;
                }
            }
        }

        // Graphic control extension: leave the frame in place when the next one
        // arrives, which is what makes a partial frame composite correctly.
        output.Write([(byte)0x21, (byte)0xF9, (byte)0x04, (byte)((1 << 2) | 1)]);
        WriteShort(output, delayHundredths);
        output.WriteByte(Quantizer.TransparentIndex);
        output.WriteByte(0);

        output.WriteByte(0x2C);
        WriteShort(output, left);
        WriteShort(output, top);
        WriteShort(output, frameWidth);
        WriteShort(output, frameHeight);
        output.WriteByte(0);    // no local colour table, not interlaced

        Compress(output, payload);
    }

    private static void DirtyRectangle(
        byte[] current,
        byte[] previous,
        int width,
        int height,
        out int left,
        out int top,
        out int rectangleWidth,
        out int rectangleHeight)
    {
        int minX = width;
        int minY = height;
        int maxX = -1;
        int maxY = -1;

        for (int y = 0; y < height; y++)
        {
            int row = y * width;

            for (int x = 0; x < width; x++)
            {
                if (current[row + x] == previous[row + x])
                {
                    continue;
                }

                minX = Math.Min(minX, x);
                maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y);
                maxY = Math.Max(maxY, y);
            }
        }

        // Nothing moved. The format has no way to say that, so the frame
        // becomes a single transparent pixel and costs a dozen bytes.
        if (maxX < 0)
        {
            left = 0;
            top = 0;
            rectangleWidth = 1;
            rectangleHeight = 1;
            return;
        }

        left = minX;
        top = minY;
        rectangleWidth = maxX - minX + 1;
        rectangleHeight = maxY - minY + 1;
    }

    /*
     * LZW, as GIF specifies it: codes are emitted least significant bit first,
     * the width grows from nine bits as the table fills, and the whole table is
     * cleared once it reaches 4096 entries.
     *
     * The one place this is easy to get wrong is *when* the width grows, and it
     * is worth spelling out because the failure is spectacular and silent. The
     * encoder adds a table entry every time it emits a code; the decoder adds
     * one every time it reads a code, but it cannot add the first one until it
     * has read a second code. The encoder therefore runs exactly one entry
     * ahead for the whole stream.
     *
     * So the encoder must go from nine bits to ten when its next free code
     * reaches 513, not 512: at 512 the decoder is still at 511 and would read
     * the next code nine bits wide. One bit of disagreement and everything from
     * that point on is noise - which is exactly what the first version of this
     * produced.
     */
    private static void Compress(Stream output, byte[] indices)
    {
        const int MinimumCodeSize = 8;
        const int ClearCode = 1 << MinimumCodeSize;
        const int EndCode = ClearCode + 1;
        const int MaximumCodeSize = 12;
        const int LastCode = (1 << MaximumCodeSize) - 1;

        output.WriteByte(MinimumCodeSize);

        var blocks = new BlockWriter(output);
        var table = new Dictionary<int, int>();

        int codeSize = MinimumCodeSize + 1;
        int next = EndCode + 1;

        blocks.WriteCode(ClearCode, codeSize);

        int prefix = -1;

        foreach (byte value in indices)
        {
            if (prefix < 0)
            {
                prefix = value;
                continue;
            }

            int key = (prefix << 8) | value;

            if (table.TryGetValue(key, out int existing))
            {
                prefix = existing;
                continue;
            }

            blocks.WriteCode(prefix, codeSize);
            table[key] = next++;

            if (codeSize < MaximumCodeSize)
            {
                if (next > 1 << codeSize)
                {
                    codeSize++;
                }
            }
            else if (next > LastCode)
            {
                blocks.WriteCode(ClearCode, codeSize);
                table.Clear();

                codeSize = MinimumCodeSize + 1;
                next = EndCode + 1;
            }

            prefix = value;
        }

        if (prefix >= 0)
        {
            blocks.WriteCode(prefix, codeSize);
        }

        blocks.WriteCode(EndCode, codeSize);
        blocks.Finish();
    }

    private static void WriteShort(Stream output, int value)
    {
        output.WriteByte((byte)(value & 0xFF));
        output.WriteByte((byte)((value >> 8) & 0xFF));
    }

    /// <summary>Packs codes into bits, and bits into the format's 255-byte sub-blocks.</summary>
    private sealed class BlockWriter(Stream output)
    {
        private readonly byte[] _block = new byte[255];
        private int _count;
        private int _bits;
        private int _bitCount;

        public void WriteCode(int code, int size)
        {
            _bits |= code << _bitCount;
            _bitCount += size;

            while (_bitCount >= 8)
            {
                Emit((byte)(_bits & 0xFF));
                _bits >>= 8;
                _bitCount -= 8;
            }
        }

        public void Finish()
        {
            if (_bitCount > 0)
            {
                Emit((byte)(_bits & 0xFF));
                _bits = 0;
                _bitCount = 0;
            }

            FlushBlock();
            output.WriteByte(0);
        }

        private void Emit(byte value)
        {
            _block[_count++] = value;

            if (_count == _block.Length)
            {
                FlushBlock();
            }
        }

        private void FlushBlock()
        {
            if (_count == 0)
            {
                return;
            }

            output.WriteByte((byte)_count);
            output.Write(_block, 0, _count);
            _count = 0;
        }
    }
}
