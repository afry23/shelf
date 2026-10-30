using System.Text;

namespace Shelf.Clip.Tests;

/// <summary>Bytes of pictures that <see cref="ImageProbe"/> reads; they need not be decodable.</summary>
internal static class Pictures
{
    public static byte[] Png(int width, int height, byte salt = 0)
    {
        var bytes = new byte[33];
        new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(bytes, 0);
        Be32(bytes, 16, width);
        Be32(bytes, 20, height);
        bytes[32] = salt;
        return bytes;
    }

    public static byte[] Gif(int width, int height) =>
        [.. Encoding.ASCII.GetBytes("GIF89a"), (byte)width, (byte)(width >> 8), (byte)height, (byte)(height >> 8), 0, 0, 0];

    public static byte[] Jpeg(int width, int height, byte salt = 0) =>
        [0xFF, 0xD8, 0xFF, 0xC0, 0x00, 0x0B, 0x08, (byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width, 0x01, 0x01, 0x11, salt, 0xFF, 0xD9];

    private static void Be32(byte[] bytes, int at, int value)
    {
        bytes[at] = (byte)(value >> 24);
        bytes[at + 1] = (byte)(value >> 16);
        bytes[at + 2] = (byte)(value >> 8);
        bytes[at + 3] = (byte)value;
    }
}
