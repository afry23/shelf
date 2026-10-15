using Shelf.TestData.Content;
using Shelf.TestData.Formats;

namespace Shelf.TestData.Model;

internal enum PdfMetadataMode
{
    None = 0,
    Info = 1,
    InfoAndXmp = 2,
}

internal sealed class FileSpec
{
    public required string Format { get; init; }

    /// <summary>2 or 3 for EPUB files, 0 for PDF files.</summary>
    public int EpubVersion { get; init; }

    public bool Cover { get; init; }

    public string? EmbeddedTitle { get; init; }

    public IReadOnlyList<string> EmbeddedAuthors { get; init; } = [];

    public IReadOnlyList<EpubAuthor> EpubAuthors { get; init; } = [];

    public string? EmbeddedLanguage { get; init; }

    public string? EmbeddedIsbn { get; init; }

    public bool IsbnInText { get; init; }

    public bool IsbnInInfo { get; init; }

    public bool SeriesEmbedded { get; init; }

    public PdfMetadataMode PdfMode { get; init; }

    public IReadOnlyList<string> Traits { get; init; } = [];

    public FileNamePattern NamePattern { get; init; }

    public string Folder { get; init; } = "downloads";

    /// <summary>The original of a duplicate (byte copy for kind "exact", a second EPUB of the same text for "same-format").</summary>
    public FileSpec? DuplicateOf { get; init; }

    public string? DuplicateKind { get; init; }

    // Filled in by the naming pass and the render pass.
    public string Path { get; set; } = string.Empty;

    public long Size { get; set; }

    public string Sha256 { get; set; } = string.Empty;

    public int? PageCount { get; set; }
}

internal sealed class WorkSpec(int index)
{
    public int Index { get; } = index;

    public string Key { get; } = "w" + (index + 1).ToString("D5", System.Globalization.CultureInfo.InvariantCulture);

    public string Title { get; set; } = string.Empty;

    public List<AuthorName> Authors { get; set; } = [];

    public string Language { get; set; } = Languages.English;

    public int Year { get; set; }

    public string? Isbn13 { get; set; }

    public string? SeriesName { get; set; }

    public int SeriesIndex { get; set; }

    public ulong TextSeed { get; set; }

    public int Words { get; set; }

    /// <summary>Index of the work whose text this edition derives from, or -1.</summary>
    public int EditionOf { get; set; } = -1;

    public ulong EditionSeed { get; set; }

    public int ShapeFiles { get; set; } = 1;

    /// <summary>For three-file works: the duplicate is an exact byte copy.</summary>
    public bool ShapeExact { get; set; }

    /// <summary>For single-file works: the file is a PDF.</summary>
    public bool ShapePdfOnly { get; set; }

    public List<FileSpec> Files { get; } = [];
}
