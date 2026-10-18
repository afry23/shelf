using System.Text.Json;

namespace Shelf.Core.Model;

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

    public string? Description { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>1 to 5, or null if not rated.</summary>
    public int? Rating { get; init; }

    public ReadStatus ReadStatus { get; init; } = ReadStatus.Unread;

    /// <summary>When the work was added to the library (UTC, second precision).</summary>
    public DateTimeOffset? Added { get; init; }

    /// <summary>File name of the cover image inside the work folder.</summary>
    public string? Cover { get; init; }

    public IReadOnlyList<SidecarFile> Files { get; init; } = [];

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
