using System.Text.Json.Serialization;
using Shelf.Core.Model;

namespace Shelf.Library;

// Every member is always written, null included, so the output schema that the SDK derives (all members required, nullable
// ones typed with "null") is true for what is sent (ADR 0006, pitfall 2).

/// <summary>A place in a file where something was found, as <c>read_passage</c> wants it as <c>location</c>.</summary>
public sealed record PassageRef(string File, string Format, string Location, string Where, string Snippet);

public sealed record BookSummary(
    string Id,
    string Title,
    string[] Authors,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? Year,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Language,
    string[] Formats,
    double Score,
    PassageRef[] Passages);

public sealed record SearchBooksResult(string Query, int Total, BookSummary[] Results);

public sealed record BookFile(
    string Name,
    string Format,
    long SizeBytes,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? Pages);

public sealed record TocItem(
    string Title,
    int Level,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Location);

public sealed record BookDetails(
    string Id,
    string Title,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Subtitle,
    string[] Authors,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Language,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? Year,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Isbn13,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Publisher,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Series,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] double? SeriesIndex,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Edition,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Description,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? Rating,
    string[] Tags,
    string ReadStatus,
    BookFile[] Files,
    TocItem[] Toc);

public sealed record PassageText(string Id, string File, string Location, string Where, string Text, int Offset, int TotalChars, bool Truncated);

/// <summary>A work, or a passage of a work (<paramref name="Passage"/>), found by meaning. <paramref name="Score"/>: 1 is the same, higher is nearer.</summary>
public sealed record SemanticItem(
    string Id,
    string Title,
    string[] Authors,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? Year,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Language,
    string[] Formats,
    double Score,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] PassageRef? Passage);

public sealed record SemanticSearchResult(
    string Query,
    string Level,
    string Mode,
    int Total,
    int PendingFiles,
    int WorksWithoutEmbedding,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Note,
    SemanticItem[] Results);

/// <summary>The tags of a work after they were set, and how many were added and removed.</summary>
public sealed record TagSetResult(string Id, string[] Tags, int Added, int Removed);

/// <summary>What a duplicate action did: files moved to the trash, and works that were left without a file and went along.</summary>
public sealed record DuplicateOutcome(int Trashed, string[] WorksRemoved);

public sealed record TagChange(string Id, string Title, string[] Tags, int Changed);

public sealed record TagNode(string Name, string Path, int Works, int Direct, TagNode[] Children);

public sealed record TagTree(int Tags, TagNode[] Roots);

public sealed record DuplicateEntry(string Id, string Title, string Path, long SizeBytes, [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? Pages);

public sealed record DuplicateSet(string Kind, string Format, DuplicateEntry[] Files, int Keep);

public sealed record DuplicatesResult(int Total, DuplicateSet[] Sets);

public sealed record PlannedMove(string Id, string From, string To);

public sealed record SkippedMove(string Id, string From, string To, string Reason);

public sealed record OrganizationProposal(string Pattern, int Files, int Works, int Unchanged, bool Truncated, PlannedMove[] Moves, SkippedMove[] Collisions);

/// <summary>A file of the library, found by its ID: where it is and what a server needs to send it.</summary>
/// <param name="Path">The full path.</param>
public sealed record FileLocation(Ulid Work, string Title, string Name, string Format, string Path, long Size, DateTime LastWriteUtc);
