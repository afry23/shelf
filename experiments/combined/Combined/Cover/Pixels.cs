using System.Security.Cryptography;
using SkiaSharp;

namespace Proto.Cover;

public static class Pixels
{
    /// <summary>SHA-256 of the raw pixel bytes plus the dimensions and the color type.</summary>
    public static string Hash(SKBitmap bitmap)
    {
        var header = System.Text.Encoding.ASCII.GetBytes($"{bitmap.Width}x{bitmap.Height}:{bitmap.ColorType}:{bitmap.AlphaType};");
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        sha.AppendData(header);
        sha.AppendData(bitmap.GetPixelSpan());
        return Convert.ToHexString(sha.GetHashAndReset());
    }

    /// <summary>Standard deviation of the luminance (0-255 scale) over all pixels.</summary>
    public static double LuminanceStdDev(SKBitmap bitmap)
    {
        double sum = 0, sumSq = 0;
        long n = (long)bitmap.Width * bitmap.Height;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var c = bitmap.GetPixel(x, y);
                var l = 0.2126 * c.Red + 0.7152 * c.Green + 0.0722 * c.Blue;
                sum += l;
                sumSq += l * l;
            }
        }

        var mean = sum / n;
        return Math.Sqrt(Math.Max(0, sumSq / n - mean * mean));
    }
}
