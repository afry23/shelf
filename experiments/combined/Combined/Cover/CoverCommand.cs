using System.Globalization;
using PDFtoImage.Exceptions;
using SkiaSharp;

namespace Proto.Cover;

/// <summary>`proto-shelf cover <pdf> <out-dir>`: the render line of experiments/pdf-cover, errors with exit code 1.</summary>
internal static class CoverCommand
{
    public static int Run(string pdfPath, string outDir, TextWriter stdout, TextWriter stderr)
    {
        try
        {
            var pdf = File.ReadAllBytes(pdfPath);
            using var bitmap = CoverRender.RenderFirstPage(pdf, out var page);
            var png = CoverRender.Encode(bitmap, SKEncodedImageFormat.Png, 100);
            var jpg = CoverRender.Encode(bitmap, SKEncodedImageFormat.Jpeg, 85);
            var webp = CoverRender.Encode(bitmap, SKEncodedImageFormat.Webp, 80);
            Directory.CreateDirectory(outDir);
            File.WriteAllBytes(Path.Combine(outDir, "cover.png"), png);
            File.WriteAllBytes(Path.Combine(outDir, "cover.jpg"), jpg);
            File.WriteAllBytes(Path.Combine(outDir, "cover.webp"), webp);
            stdout.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"page={page.Width:0.##}x{page.Height:0.##}pt size={bitmap.Width}x{bitmap.Height} png={png.Length} jpg={jpg.Length} webp={webp.Length} pixelhash={Pixels.Hash(bitmap)} stddev={Pixels.LuminanceStdDev(bitmap):0.00}"));
            return 0;
        }
        catch (PdfException e)
        {
            stderr.WriteLine($"error kind=pdf type={e.GetType().Name} message={e.Message}");
            return 1;
        }
        catch (Exception e) when (e is IOException or ArgumentException)
        {
            stderr.WriteLine($"error kind=io type={e.GetType().Name} message={e.Message}");
            return 1;
        }
    }
}
