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

/// <summary>Where a person stopped reading a file (F-RDR-4): <c>page=N</c> for a PDF, an <c>epubcfi(...)</c> for an EPUB.</summary>
/// <param name="UpdatedAt">UTC, as <c>yyyy-MM-ddTHH:mm:ssZ</c>.</param>
public sealed record ReadingPosition(string Locator, string UpdatedAt);

public sealed record BookFile(
    string Name,
    string Format,
    long SizeBytes,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? Pages,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] ReadingPosition? Progress = null);

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

/// <summary>The works most like the work <paramref name="Id"/> (F-SEM-6), best first, without the work itself.</summary>
public sealed record SimilarBooksResult(
    string Id,
    string Title,
    int Total,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Note,
    SemanticItem[] Results);

/// <summary>The tags of a work after they were set, and how many were added and removed.</summary>
public sealed record TagSetResult(string Id, string[] Tags, int Added, int Removed);

/// <summary>What a duplicate action did: files moved to the trash, and works that were left without a file and went along.</summary>
public sealed record DuplicateOutcome(int Trashed, string[] WorksRemoved);

/// <summary>A file that was split off its work: the work it came from, the work it became, its name and the folder of the new work (relative to the library).</summary>
public sealed record SplitOutcome(string Work, string NewWork, string File, string Folder);

/// <summary>A merge of two works: the work that stays, the work that went into it, the files that moved and the folder of the merged work when it still holds files that shelf does not know.</summary>
public sealed record MergeOutcome(string Id, string Merged, int MovedFiles, string? LeftoverFolder);

public sealed record TagChange(string Id, string Title, string[] Tags, int Changed);

public sealed record TagNode(string Name, string Path, int Works, int Direct, TagNode[] Children);

public sealed record TagTree(int Tags, TagNode[] Roots);

public sealed record DuplicateEntry(string Id, string Title, string Path, long SizeBytes, [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? Pages);

public sealed record DuplicateSet(string Kind, string Format, DuplicateEntry[] Files, int Keep);

public sealed record DuplicatesResult(int Total, DuplicateSet[] Sets);

/// <param name="Folder">The folder of the work, library-relative.</param>
public sealed record ProbableEntry(string Id, string Title, string[] Authors, string Folder);

/// <param name="Hamming">Distance of the text fingerprints; null if the texts are not that close (or a work has no text).</param>
/// <param name="Cosine">Similarity of the vectors of the works; null if it is below the threshold or the library has no embeddings.</param>
public sealed record ProbablePair(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? Hamming,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] double? Cosine,
    ProbableEntry A,
    ProbableEntry B);

/// <param name="Note">What is missing for the comparison of the vectors (no embeddings yet), or null.</param>
public sealed record ProbablePairsResult(int Total, [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Note, ProbablePair[] Pairs);

public sealed record PlannedMove(string Id, string From, string To);

public sealed record SkippedMove(string Id, string From, string To, string Reason);

public sealed record OrganizationProposal(string Pattern, int Files, int Works, int Unchanged, bool Truncated, PlannedMove[] Moves, SkippedMove[] Collisions);

/// <summary>A file of the library, found by its ID: where it is and what a server needs to send it.</summary>
/// <param name="Path">The full path.</param>
public sealed record FileLocation(Ulid Work, string Title, string Name, string Format, string Path, long Size, DateTime LastWriteUtc);

/// <summary>What renaming or merging a tag did: the names, and the number of works whose tags changed (F-TAG-4).</summary>
public sealed record TagMoveResult(string From, string To, int Changed);

/// <summary>A work that an uncertain file may belong to; <paramref name="Title"/> is null when the index does not know the work.</summary>
public sealed record ReviewCandidateItem(
    string Work,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Title,
    double Confidence,
    string[] Signals);

/// <summary>One thing that needs a decision (F-IMP-7): <paramref name="Kind"/> is grouping, extraction-error or weak-metadata.</summary>
public sealed record ReviewItem(
    string Id,
    string Kind,
    string CreatedAt,
    string Message,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? File,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Work,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? WorkTitle,
    ReviewCandidateItem[] Candidates);

public sealed record ReviewList(int Total, ReviewItem[] Entries);

/// <summary>What deciding an entry did; <paramref name="Into"/> is the work that took the files, null when the entry was dismissed.</summary>
public sealed record ReviewOutcome(
    string Entry,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Into,
    int MovedFiles);

/// <summary>The dry run of <c>organize</c>; <paramref name="NextUndo"/> is the label of the run that <c>undo</c> would revert now.</summary>
public sealed record OrganizePreview(
    string Pattern,
    int Files,
    int Works,
    int Unchanged,
    bool Truncated,
    PlannedMove[] Moves,
    SkippedMove[] Collisions,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? NextUndo);

public sealed record OrganizeOutcome(string Pattern, int Files, int Works, int Unchanged, SkippedMove[] Collisions);

public sealed record UndoSkipped(string Path, string Reason);

public sealed record UndoOutcome(string Label, int Reverted, UndoSkipped[] Skipped);

/// <summary>A field that an online source would write into a work: <c>action</c> is <c>fill</c> or <c>overwrite</c>.</summary>
public sealed record EnrichChange(string Field, string Action, string? Before, string After);

/// <summary>A field the work already has with another value, and which therefore stays.</summary>
public sealed record EnrichKept(string Field, string? Current, string Online);

/// <summary>A book that matches the title and author of a work; <c>number</c> is what <c>pick</c> takes.</summary>
public sealed record EnrichCandidate(int Number, string Title, string[] Authors, string? Publisher, int? Year, string? Isbn13);

/// <summary>
/// What an online source knows about a work. When several books match, <c>source</c> and <c>match</c> are absent, <c>candidates</c>
/// lists them and nothing is proposed. <c>warnings</c> name the sources that failed before one answered.
/// </summary>
public sealed record EnrichPreviewResult(
    string Id,
    string? Source,
    string? Match,
    EnrichChange[] Changes,
    EnrichKept[] Kept,
    EnrichCandidate[] Candidates,
    string[] Warnings);

/// <summary>What <c>enrich/apply</c> wrote: the fields, the name of the cover file when one was saved, and the warnings.</summary>
public sealed record EnrichApplyResult(string Id, string Source, string[] Fields, string? Cover, string[] Warnings);
