using System.Buffers.Binary;

namespace Shelf.TestData.Formats;

/// <summary>
/// Minimal deterministic PNG writer: 8-bit RGB, filter 0, IDAT as a zlib stream of stored deflate blocks
/// (no compression, so the bytes do not depend on the zlib implementation of the platform).
/// </summary>
internal static class PngWriter
{
    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>A <paramref name="width"/> x <paramref name="height"/> image with a vertical gradient around the given color.</summary>
    public static byte[] Write(int width, int height, byte red, byte green, byte blue)
    {
        var raw = new byte[height * (1 + (width * 3))];
        var position = 0;
        for (var y = 0; y < height; y++)
        {
            raw[position++] = 0;
            var shade = (byte)(255 - (y * 96 / height));
            for (var x = 0; x < width; x++)
            {
                var edge = x < 2 || x >= width - 2 || y < 2 || y >= height - 2;
                raw[position++] = edge ? (byte)0 : (byte)(red * shade / 255);
                raw[position++] = edge ? (byte)0 : (byte)(green * shade / 255);
                raw[position++] = edge ? (byte)0 : (byte)(blue * shade / 255);
            }
        }

        var header = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(0, 4), (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4, 4), (uint)height);
        header[8] = 8; // bit depth
        header[9] = 2; // color type: RGB
        using var output = new MemoryStream();
        output.Write(Signature);
        WriteChunk(output, "IHDR"u8, header);
        WriteChunk(output, "IDAT"u8, ZlibStored(raw));
        WriteChunk(output, "IEND"u8, []);
        return output.ToArray();
    }

    internal static byte[] ZlibStored(ReadOnlySpan<byte> data)
    {
        using var stream = new MemoryStream();
        stream.WriteByte(0x78);
        stream.WriteByte(0x01);
        var offset = 0;
        Span<byte> lengths = stackalloc byte[4];
        do
        {
            var length = Math.Min(65535, data.Length - offset);
            var final = offset + length >= data.Length;
            stream.WriteByte(final ? (byte)1 : (byte)0);
            BinaryPrimitives.WriteUInt16LittleEndian(lengths[..2], (ushort)length);
            BinaryPrimitives.WriteUInt16LittleEndian(lengths[2..], (ushort)~length);
            stream.Write(lengths);
            stream.Write(data.Slice(offset, length));
            offset += length;
        }
        while (offset < data.Length);

        Span<byte> adler = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(adler, Adler32.Compute(data));
        stream.Write(adler);
        return stream.ToArray();
    }

    private static void WriteChunk(Stream output, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> number = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(number, (uint)data.Length);
        output.Write(number);
        output.Write(type);
        output.Write(data);
        var crc = Crc32.Update(Crc32.Update(0, type), data);
        BinaryPrimitives.WriteUInt32BigEndian(number, crc);
        output.Write(number);
    }
}
