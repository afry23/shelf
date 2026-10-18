using PDFtoImage;
using PDFtoImage.Exceptions;
using SkiaSharp;
using VersOne.Epub;

namespace Shelf.Extraction;

/// <summary>A cover image and the extension it is stored under (<c>jpg</c>, <c>png</c>, <c>gif</c> or <c>webp</c>).</summary>
public sealed record CoverImage(byte[] Content, string Extension);

/// <summary>
/// The cover of an EPUB or PDF (F-IMP-5). An EPUB gives the image it embeds, as it is (no transcoding). A PDF gives a rendering
/// of its first page as JPEG, 600 px on the longest edge, quality 85 (ADR 0010).
/// </summary>
public static class CoverExtractor
{
    /// <summary>The longest edge of the cover of a PDF, in pixels.</summary>
    public const int PdfEdge = 600;

    public const int JpegQuality = 85;

    /// <summary>A bigger embedded image is not taken as a cover.</summary>
    public const long MaxEpubCoverBytes = 20 * 1024 * 1024;

    /// <summary>A bigger PDF is not rendered: the file is read into memory first.</summary>
    public const long MaxPdfBytes = 256L * 1024 * 1024;

    // PDFium is not thread-safe (ADR 0010, R3). PDFtoImage locks around its calls, too; this lock keeps the page size and the
    // rendering of one file together and makes the rule ours.
    private static readonly Lock RenderGate = new();

    /// <summary>The cover of the file, or null when it has none (an EPUB without a cover image, a file of another kind).</summary>
    /// <exception cref="ExtractionException">The file cannot be read as an EPUB or PDF.</exception>
    public static CoverImage? Read(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (!File.Exists(path))
        {
            throw new ExtractionException($"{path}: io: the file does not exist", ExtractionErrorKind.Io, path);
        }

        try
        {
            return Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".epub" => ReadEpub(path),
                ".pdf" => ReadPdf(path),
                _ => null,
            };
        }
        catch (ExtractionException)
        {
            throw;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // The libraries throw all kinds of exceptions for damaged input; none of them may end the program.
            var kind = Classify(e);
            throw new ExtractionException($"{path}: {KindNames.Of(kind)}: {e.Message}", kind, path, e);
        }
    }

    /// <summary>The kind of image by its first bytes, or null: only JPEG, PNG, GIF and WebP are covers.</summary>
    public static string? ExtensionOf(ReadOnlySpan<byte> image)
    {
        if (image.Length >= 3 && image[0] == 0xFF && image[1] == 0xD8 && image[2] == 0xFF)
        {
            return "jpg";
        }

        if (image.Length >= 8 && image[..8].SequenceEqual((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return "png";
        }

        if (image.Length >= 6 && image[0] == 'G' && image[1] == 'I' && image[2] == 'F' && image[3] == '8')
        {
            return "gif";
        }

        if (image.Length >= 12 && image[..4].SequenceEqual("RIFF"u8) && image[8..12].SequenceEqual("WEBP"u8))
        {
            return "webp";
        }

        return null;
    }

    private static CoverImage? ReadEpub(string path)
    {
        EpubLimits.Check(path, ExtractionOptions.Default);
        return ReadEpubAsync(path).GetAwaiter().GetResult();
    }

    private static async Task<CoverImage?> ReadEpubAsync(string path)
    {
        using var book = await EpubReader.OpenBookAsync(path).ConfigureAwait(false);
        if (book.Content.Cover is not { } cover)
        {
            return null;
        }

        var bytes = await cover.ReadContentAsBytesAsync().ConfigureAwait(false);
        if (bytes.LongLength > MaxEpubCoverBytes || ExtensionOf(bytes) is not { } extension)
        {
            return null;
        }

        return new CoverImage(bytes, extension);
    }

    private static CoverImage ReadPdf(string path)
    {
        var length = new FileInfo(path).Length;
        if (length > MaxPdfBytes)
        {
            throw new ExtractionException($"{path}: too-large: {length} bytes, the limit for rendering a cover is {MaxPdfBytes}", ExtractionErrorKind.TooLarge, path);
        }

        var pdf = File.ReadAllBytes(path);
        try
        {
            lock (RenderGate)
            {
                // CA1416: PDFtoImage lists the platforms it supports and shelf is built for exactly two of them (osx-arm64,
                // linux-x64; PRD 9.2). A class-level SupportedOSPlatform would only move the warning to every caller (ADR 0010).
#pragma warning disable CA1416
                if (Conversion.GetPageCount(pdf) < 1)
                {
                    throw new ExtractionException($"{path}: bad-pdf: the PDF has no pages", ExtractionErrorKind.BadPdf, path);
                }

                var size = Conversion.GetPageSize(pdf, 0);
                var wide = size.Width >= size.Height;
                var options = new RenderOptions(
                    Dpi: 300,
                    Width: wide ? PdfEdge : null,
                    Height: wide ? null : PdfEdge,
                    WithAspectRatio: true);
                using var bitmap = Conversion.ToImage(pdf, 0, options: options);
#pragma warning restore CA1416
                using var image = SKImage.FromBitmap(bitmap);
                using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);
                return new CoverImage(encoded.ToArray(), "jpg");
            }
        }
        catch (PdfPasswordProtectedException e)
        {
            throw new ExtractionException($"{path}: encrypted: {e.Message}", ExtractionErrorKind.Encrypted, path, e);
        }
        catch (Exception e) when (e is PdfException or ArgumentOutOfRangeException)
        {
            // A PDF with zero pages raises ArgumentOutOfRangeException, not a PdfException (ADR 0010, R6).
            throw new ExtractionException($"{path}: bad-pdf: {e.Message}", ExtractionErrorKind.BadPdf, path, e);
        }
    }

    private static ExtractionErrorKind Classify(Exception e) => BookExtractor.Classify(e);
}
