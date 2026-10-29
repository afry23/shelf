namespace Shelf.Extraction;

/// <summary>Why a file could not be read; mapped to review entries by the importer (ADR 0005 hand-off).</summary>
public enum ExtractionErrorKind
{
    /// <summary>An EPUB that is not a ZIP archive.</summary>
    NotAContainer,

    /// <summary>A ZIP archive that is not a usable EPUB (missing or broken package, content, navigation).</summary>
    BadEpub,

    /// <summary>Not a readable PDF, or a PDF without pages.</summary>
    BadPdf,

    /// <summary>A PDF that needs a password.</summary>
    Encrypted,

    /// <summary>A format shelf does not read (MOBI, ...).</summary>
    Unsupported,

    /// <summary>The file could not be read from disk.</summary>
    Io,

    /// <summary>An archive entry or the whole archive is larger than the limits allow; it is not read.</summary>
    TooLarge,

    /// <summary>Any other exception from a parser; counts as an error of this one file.</summary>
    Other,
}

/// <summary>A file could not be extracted. Always the problem of that one file.</summary>
public sealed class ExtractionException(string message, ExtractionErrorKind kind, string path, Exception? innerException = null)
    : Exception(message, innerException)
{
    public ExtractionErrorKind Kind { get; } = kind;

    public string Path { get; } = path;

    /// <summary>The kind as written in messages and JSON: <c>not-a-container</c>, <c>bad-epub</c>, ...</summary>
    public string KindName => KindNames.Of(Kind);
}

public static class KindNames
{
    public static string Of(ExtractionErrorKind kind) => kind switch
    {
        ExtractionErrorKind.NotAContainer => "not-a-container",
        ExtractionErrorKind.BadEpub => "bad-epub",
        ExtractionErrorKind.BadPdf => "bad-pdf",
        ExtractionErrorKind.Encrypted => "encrypted",
        ExtractionErrorKind.Unsupported => "unsupported",
        ExtractionErrorKind.Io => "io",
        ExtractionErrorKind.TooLarge => "too-large",
        _ => "other",
    };
}

/// <summary>Where a piece of metadata came from; weaker origins make a grouping less certain (F-GRP-3).</summary>
public enum MetadataOrigin
{
    None,
    FileContent,
    FileName,
}

/// <summary>What the file says about the book. Missing values are null or empty, never guessed (F-IMP-2, F-IMP-3).</summary>
public sealed record BookMetadata
{
    public string? Title { get; init; }

    public MetadataOrigin TitleOrigin { get; init; }

    public IReadOnlyList<string> Authors { get; init; } = [];

    public MetadataOrigin AuthorsOrigin { get; init; }

    /// <summary>The language code of the file (<c>dc:language</c>), as written; never detected.</summary>
    public string? Language { get; init; }

    public int? Year { get; init; }

    public string? Publisher { get; init; }

    public string? Description { get; init; }

    /// <summary>ISBN-13: from the metadata first, from the first and last pages second.</summary>
    public string? Isbn13 { get; init; }

    /// <summary>DOI of a paper: from the metadata first, from the first pages second (F-PAP-2).</summary>
    public string? Doi { get; init; }

    /// <summary>arXiv ID without version: from the file name first, from the metadata and the first pages second (F-PAP-2).</summary>
    public string? ArxivId { get; init; }

    public string? Series { get; init; }

    public double? SeriesIndex { get; init; }
}

/// <summary>A piece of text with its location: <c>page=N</c> or a chapter CFI (<see cref="Shelf.Core.Model.Locators"/>), and what kind of text it is.</summary>
public sealed record TextSegment(string Locator, string Text, ContentKind Kind = ContentKind.Body);

/// <summary>The result of reading one file.</summary>
/// <param name="Metadata">What the file says about the book.</param>
/// <param name="PageCount">Pages of a PDF; null for an EPUB.</param>
/// <param name="Segments">Text by page or chapter in reading order, empty for a PDF without a text layer.</param>
public sealed record ExtractedBook(BookMetadata Metadata, int? PageCount, IReadOnlyList<TextSegment> Segments);

public sealed record ExtractionOptions
{
    public static ExtractionOptions Default { get; } = new();

    /// <summary>Fill a missing title, author or year from the file name (F-IMP-3).</summary>
    public bool FileNameFallback { get; init; } = true;

    /// <summary>Largest unpacked size of one EPUB entry (ADR 0005: 2.8 GiB of memory for a 256 KB crafted entry).</summary>
    public long MaxEntryBytes { get; init; } = 64L * 1024 * 1024;

    /// <summary>Largest total unpacked size of an EPUB.</summary>
    public long MaxTotalBytes { get; init; } = 1024L * 1024 * 1024;
}
