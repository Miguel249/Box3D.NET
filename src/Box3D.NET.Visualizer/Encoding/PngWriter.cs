// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.IO.Compression;
using Box3D.Visualizer.Rendering;

namespace Box3D.Visualizer.Encoding;

/// <summary>
/// Writes a truecolour PNG.
/// </summary>
/// <remarks>
/// The whole format, for this one case, is a signature, three chunks and a
/// deflate stream. <see cref="ZLibStream"/> supplies the compression and the
/// checksum the format wants, which leaves the CRC over each chunk and the
/// per-scanline filter as the only things worth writing down.
/// </remarks>
internal static class PngWriter
{
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];

    private static readonly uint[] CrcTable = BuildCrcTable();

    /// <summary>Writes an image to a file.</summary>
    /// <param name="image">The image.</param>
    /// <param name="path">Where to write it.</param>
    public static void Write(Image image, string path)
    {
        ArgumentNullException.ThrowIfNull(image);

        using FileStream file = File.Create(path);
        Write(image, file);
    }

    /// <summary>Writes an image to a stream.</summary>
    /// <param name="image">The image.</param>
    /// <param name="output">Where to write it.</param>
    public static void Write(Image image, Stream output)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(output);

        output.Write(Signature);

        byte[] header = new byte[13];
        WriteBigEndian(header, 0, (uint)image.Width);
        WriteBigEndian(header, 4, (uint)image.Height);
        header[8] = 8;      // bits per channel
        header[9] = 2;      // truecolour, no alpha
        header[10] = 0;     // deflate
        header[11] = 0;     // adaptive filtering
        header[12] = 0;     // no interlacing

        WriteChunk(output, "IHDR"u8, header);
        WriteChunk(output, "IDAT"u8, Compress(image));
        WriteChunk(output, "IEND"u8, []);
    }

    private static byte[] Compress(Image image)
    {
        int stride = image.Width * 3;
        byte[] scanline = new byte[stride + 1];
        byte[] previous = new byte[stride];

        using var buffer = new MemoryStream();

        using (var deflate = new ZLibStream(buffer, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            for (int y = 0; y < image.Height; y++)
            {
                int start = y * stride;

                // Paeth, on every row. Choosing a filter per row by the usual
                // heuristic would save a little more, but these images are
                // smooth gradients and Paeth is the right answer for all of them.
                scanline[0] = 4;

                for (int x = 0; x < stride; x++)
                {
                    byte raw = image.Pixels[start + x];
                    byte left = x >= 3 ? image.Pixels[start + x - 3] : (byte)0;
                    byte above = previous[x];
                    byte upperLeft = x >= 3 ? previous[x - 3] : (byte)0;

                    scanline[x + 1] = (byte)(raw - Paeth(left, above, upperLeft));
                }

                deflate.Write(scanline, 0, scanline.Length);
                Array.Copy(image.Pixels, start, previous, 0, stride);
            }
        }

        return buffer.ToArray();
    }

    private static byte Paeth(byte left, byte above, byte upperLeft)
    {
        int estimate = left + above - upperLeft;
        int dLeft = Math.Abs(estimate - left);
        int dAbove = Math.Abs(estimate - above);
        int dUpperLeft = Math.Abs(estimate - upperLeft);

        if (dLeft <= dAbove && dLeft <= dUpperLeft)
        {
            return left;
        }

        return dAbove <= dUpperLeft ? above : upperLeft;
    }

    private static void WriteChunk(Stream output, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        WriteBigEndian(length, 0, (uint)data.Length);
        output.Write(length);

        output.Write(type);
        output.Write(data);

        uint crc = Crc(0xFFFFFFFFu, type);
        crc = Crc(crc, data) ^ 0xFFFFFFFFu;

        Span<byte> checksum = stackalloc byte[4];
        WriteBigEndian(checksum, 0, crc);
        output.Write(checksum);
    }

    private static uint Crc(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (byte value in data)
        {
            crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        return crc;
    }

    private static uint[] BuildCrcTable()
    {
        uint[] table = new uint[256];

        for (uint i = 0; i < 256; i++)
        {
            uint value = i;

            for (int bit = 0; bit < 8; bit++)
            {
                value = (value & 1) != 0 ? 0xEDB88320u ^ (value >> 1) : value >> 1;
            }

            table[i] = value;
        }

        return table;
    }

    private static void WriteBigEndian(Span<byte> destination, int offset, uint value)
    {
        destination[offset] = (byte)(value >> 24);
        destination[offset + 1] = (byte)(value >> 16);
        destination[offset + 2] = (byte)(value >> 8);
        destination[offset + 3] = (byte)value;
    }
}
