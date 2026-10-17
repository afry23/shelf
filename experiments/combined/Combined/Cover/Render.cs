using PDFtoImage;
using SkiaSharp;

namespace Proto.Cover;

public static class CoverRender
{
    public const int MaxEdge = 600;

    /// <summary>Page 1 rendered so that the longest edge is <paramref name="maxEdge"/> px (aspect ratio kept).</summary>
    public static SKBitmap RenderFirstPage(byte[] pdf, out (float Width, float Height) pageSizePoints, int maxEdge = MaxEdge)
    {
        var size = Conversion.GetPageSize(pdf, 0);
        pageSizePoints = (size.Width, size.Height);
        var landscapeOrSquare = size.Width >= size.Height;
        var options = new RenderOptions(
            Dpi: 300,
            Width: landscapeOrSquare ? maxEdge : null,
            Height: landscapeOrSquare ? null : maxEdge,
            WithAspectRatio: true);
        return Conversion.ToImage(pdf, 0, options: options);
    }

    public static byte[] Encode(SKBitmap bitmap, SKEncodedImageFormat format, int quality)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, quality);
        return data.ToArray();
    }
}
