using System.Text.Json;

namespace Shelf.Core.Model;

/// <summary>What a work is (F-PAP-1, F-ART-1). A sidecar without the field describes a book.</summary>
public enum WorkKind
{
    Book,
    Paper,
    Article,
}

/// <summary>Reading status of a work (PRD 6.1).</summary>
public enum ReadStatus
{
    Unread,
    Reading,
    Read,
}

/// <summary>The last known reading position in one file: an EPUB CFI or <c>page=N</c> (PRD 10.4).</summary>
public sealed record FileProgress(string Locator, DateTimeOffset UpdatedAt);

/// <summary>A file of a work, named by its file name inside the work folder (F-LIB-4).</summary>
public sealed record SidecarFile(string Name, FileProgress? Progress = null);

/// <summary>Two files of the same or different works that were confirmed as not being duplicates.</summary>
public sealed record FilePair(string A, string B);

/// <summary>"Not a duplicate" markings of a work (PRD 10.4).</summary>
public sealed record NotDuplicates(IReadOnlyList<Ulid> Works, IReadOnlyList<FilePair> Files)
{
    public static NotDuplicates None { get; } = new([], []);

    public bool IsEmpty => Works.Count == 0 && Files.Count == 0;
}

/// <summary>
/// The content of a <c>book.json</c>: the source of truth of one work (F-LIB-5, schema 1 of PRD 10.4).
/// A work has one or more files (F-LIB-4). Fields that this version does not know are kept in
/// <see cref="UnknownFields"/> and written back unchanged.
/// </summary>
public sealed record WorkSidecar
{
    public const int CurrentSchema = 1;

    public int Schema { get; init; } = CurrentSchema;

    public Ulid Id { get; init; }

    public string Title { get; init; } = string.Empty;

    public string? Subtitle { get; init; }

    public IReadOnlyList<string> Authors { get; init; } = [];

    public string? Language { get; init; }

    public int? Year { get; init; }

    public string? Isbn10 { get; init; }

    public string? Isbn13 { get; init; }

    public string? Publisher { get; init; }

    public string? Series { get; init; }

    /// <summary>The volume within <see cref="Series"/>; fractional for interim volumes such as 1.5.</summary>
    public double? SeriesIndex { get; init; }

    public string? Edition { get; init; }

    /// <summary>The key under which the work is cited (BibTeX and the like); see <see cref="CiteKeys"/>.</summary>
    public string? CiteKey { get; init; }

    public string? Description { get; init; }

    /// <summary>Book (default), paper or article; written only for papers and articles (F-PAP-1, F-ART-1).</summary>
    public WorkKind Kind { get; init; } = WorkKind.Book;

    /// <summary>DOI without prefix (<c>10.1145/3295500.3356159</c>); papers only in practice.</summary>
    public string? Doi { get; init; }

    /// <summary>arXiv ID without version (<c>1706.03762</c>).</summary>
    public string? ArxivId { get; init; }

    public string? Pmid { get; init; }

    /// <summary>Journal or conference the paper appeared in.</summary>
    public string? Venue { get; init; }

    public string? Volume { get; init; }

    public string? Issue { get; init; }

    public string? Pages { get; init; }

    public string? Abstract { get; init; }

    public IReadOnlyList<string> Keywords { get; init; } = [];

    /// <summary>The canonical address the article was clipped from, without fragment and tracking parameters (F-ART-1, F-ART-6).</summary>
    public string? SourceUrl { get; init; }

    /// <summary>The website or publication an article appeared on (F-ART-1).</summary>
    public string? Site { get; init; }

    /// <summary>When the article was published (UTC), if the page says so (F-ART-1).</summary>
    public DateTimeOffset? Published { get; init; }

    /// <summary>When the article was clipped (UTC, second precision) (F-ART-1).</summary>
    public DateTimeOffset? Captured { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>1 to 5, or null if not rated.</summary>
    public int? Rating { get; init; }

    public ReadStatus ReadStatus { get; init; } = ReadStatus.Unread;

    /// <summary>
    /// When the work was set to read (UTC, second precision), or null: not read, or read before shelf recorded the date (F-STA-1).
    /// <see cref="WithReadStatus"/> keeps it in line with <see cref="ReadStatus"/>.
    /// </summary>
    public DateTimeOffset? FinishedAt { get; init; }

    /// <summary>
    /// When the work was put on the reading list (UTC, second precision), or null: not on the list (F-RLS-1).
    /// <see cref="WithReadStatus"/> takes the work off the list when it becomes read (F-RLS-2).
    /// </summary>
    public DateTimeOffset? ReadingListAt { get; init; }

    /// <summary>When the work was added to the library (UTC, second precision).</summary>
    public DateTimeOffset? Added { get; init; }

    /// <summary>File name of the cover image inside the work folder.</summary>
    public string? Cover { get; init; }

    public IReadOnlyList<SidecarFile> Files { get; init; } = [];

    /// <summary>
    /// The work with this reading status. Becoming read dates it with <paramref name="now"/>; a work that is read already keeps its
    /// date; leaving read takes the date away. A work that becomes read leaves the reading list.
    /// </summary>
    public WorkSidecar WithReadStatus(ReadStatus status, DateTimeOffset now) => this with
    {
        ReadStatus = status,
        ReadingListAt = status == ReadStatus.Read ? null : ReadingListAt,
        FinishedAt = status != ReadStatus.Read
            ? null
            : ReadStatus == ReadStatus.Read ? FinishedAt : DateTimeOffset.FromUnixTimeSeconds(now.ToUnixTimeSeconds()),
    };

    public NotDuplicates NotDuplicates { get; init; } = NotDuplicates.None;

    /// <summary>
    /// Other works that are another edition of the same book (F-GRP-5). The mark is written on both sides; an ID of a
    /// work that no longer exists is ignored when shown.
    /// </summary>
    public IReadOnlyList<Ulid> Related { get; init; } = [];

    public IReadOnlyDictionary<string, JsonElement> UnknownFields { get; init; } = EmptyUnknownFields;

    private static readonly IReadOnlyDictionary<string, JsonElement> EmptyUnknownFields = new Dictionary<string, JsonElement>();
}

/// <summary>A <c>book.json</c> could not be read, is invalid, or must not be written.</summary>
public sealed class SidecarException(string message, string sourceName, Exception? innerException = null)
    : Exception(message, innerException)
{
    public string SourceName { get; } = sourceName;
}
