using System.IO.Compression;
using System.Xml;

namespace Shelf.Extraction;

/// <summary>
/// Reads an EPUB, a PDF or a Markdown article: metadata (F-IMP-2), file name fallback (F-IMP-3), ISBN (F-IMP-4) and text by
/// page or chapter with locators (F-SRC-5). Stateless and thread-safe; at most 4 files should be read at the
/// same time (PdfPig does not scale beyond that, ADR 0005). Every failure is an <see cref="ExtractionException"/>
/// about this one file.
/// </summary>
public static class BookExtractor
{
    /// <summary>True for a file that can be a book. A TXT file is read only as a notes file (F-NOTEF-3), so it is not supported here.</summary>
    public static bool IsSupported(string path) => FormatOf(path) is { } format && format != Format.Text;

    /// <summary>True for a file that can be attached to a work as a notes file: a PDF or a TXT file (F-NOTEF-1).</summary>
    public static bool IsNotesFile(string path) => FormatOf(path) is Format.Pdf or Format.Text;

    /// <summary>True for a Markdown file. Unlike an EPUB or a PDF it is no book by itself: it counts only inside a work (F-ART-9).</summary>
    public static bool IsMarkdown(string path) => FormatOf(path) == Format.Markdown;

    /// <summary>Metadata only, without reading the text; the ISBN then comes from the metadata alone.</summary>
    /// <exception cref="ExtractionException"/>
    public static BookMetadata ReadMetadata(string path, ExtractionOptions? options = null) => Run(path, withText: false, options).Metadata;

    /// <summary>Metadata and text.</summary>
    /// <exception cref="ExtractionException"/>
    public static ExtractedBook Extract(string path, ExtractionOptions? options = null) => Run(path, withText: true, options);

    private static ExtractedBook Run(string path, bool withText, ExtractionOptions? options)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        options ??= ExtractionOptions.Default;
        var format = FormatOf(path)
            ?? throw new ExtractionException($"{path}: unsupported: only EPUB, PDF, Markdown and TXT files are read", ExtractionErrorKind.Unsupported, path);

        // PdfPig reports a missing file as InvalidOperationException; say what is wrong instead.
        if (!File.Exists(path))
        {
            throw new ExtractionException($"{path}: io: the file does not exist", ExtractionErrorKind.Io, path);
        }

        ExtractedBook book;
        try
        {
            book = format switch
            {
                Format.Epub => EpubExtractor.Read(path, withText, options),
                Format.Markdown => MarkdownExtractor.Read(path, withText),
                Format.Text => TextFileExtractor.Read(path, withText),
                _ => PdfExtractor.Read(path, withText),
            };
        }
        catch (ExtractionException)
        {
            throw;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // Both libraries throw all kinds of exceptions for damaged input; none of them may end the program.
            var kind = Classify(e);
            throw new ExtractionException($"{path}: {KindNames.Of(kind)}: {e.Message}", kind, path, e);
        }

        return options.FileNameFallback ? book with { Metadata = WithFileName(book.Metadata, path) } : book;
    }

    internal static ExtractionErrorKind Classify(Exception e)
    {
        var type = e.GetType().FullName ?? e.GetType().Name;
        return e switch
        {
            InvalidDataException => ExtractionErrorKind.NotAContainer,
            FileNotFoundException or DirectoryNotFoundException or IOException or UnauthorizedAccessException => ExtractionErrorKind.Io,
            NotSupportedException => ExtractionErrorKind.Unsupported,
            XmlException => ExtractionErrorKind.BadEpub,
            _ when type.StartsWith("VersOne.Epub.", StringComparison.Ordinal) => ExtractionErrorKind.BadEpub,
            _ when type.EndsWith("PdfDocumentEncryptedException", StringComparison.Ordinal) => ExtractionErrorKind.Encrypted,
            _ when type.StartsWith("UglyToad.PdfPig.", StringComparison.Ordinal) => ExtractionErrorKind.BadPdf,
            _ => ExtractionErrorKind.Other,
        };
    }

    private static BookMetadata WithFileName(BookMetadata metadata, string path)
    {
        if (metadata.Title is not null && metadata.Authors.Count > 0 && metadata.Year is not null)
        {
            return metadata;
        }

        var guess = FileNameMetadata.Parse(path);
        var result = metadata;
        if (result.Title is null && guess.Title is not null)
        {
            result = result with { Title = guess.Title, TitleOrigin = MetadataOrigin.FileName };
        }

        if (result.Authors.Count == 0 && guess.Authors.Count > 0)
        {
            result = result with { Authors = guess.Authors, AuthorsOrigin = MetadataOrigin.FileName };
        }

        if (result.Year is null && guess.Year is not null)
        {
            result = result with { Year = guess.Year };
        }

        return result;
    }

    private enum Format
    {
        Epub,
        Pdf,
        Markdown,
        Text,
    }

    private static Format? FormatOf(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".epub" => Format.Epub,
        ".pdf" => Format.Pdf,
        ".md" => Format.Markdown,
        ".txt" => Format.Text,
        _ => null,
    };
}
