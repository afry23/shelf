using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Shelf.Library;

namespace Shelf.Web;

// Members that can be missing are written as null, never left out, so that a client can rely on the shape.

/// <summary>A work in the list of the library view.</summary>
public sealed record WorkItem(
    string Id,
    string Title,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Subtitle,
    string[] Authors,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? Year,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Language,
    string[] Formats,
    string[] Tags,
    string ReadStatus,
    bool HasCover);

public sealed record FacetValue(string Value, int Count);

public sealed record Facets(
    FacetValue[] Tags,
    FacetValue[] Authors,
    FacetValue[] Publishers,
    FacetValue[] Languages,
    FacetValue[] Years,
    FacetValue[] Formats,
    FacetValue[] Statuses);

/// <summary>One page of the library, and the counts of every facet over all the works that pass the filter.</summary>
public sealed record WorkList(int Total, int Offset, int Limit, WorkItem[] Items, Facets Facets);

/// <summary>A place in a file where a search found something, with the ID of the file, so that a hit can link to the reader.</summary>
/// <param name="FileId">See <see cref="FileIds"/>.</param>
/// <param name="File">Library-relative path.</param>
/// <param name="Location"><c>page=N</c> or an EPUB location.</param>
/// <param name="Where">The location for people: "page 12".</param>
public sealed record Passage(string FileId, string File, string Format, string Location, string Where, string Snippet);

/// <summary>A work found by a search, with the places in its text (none for a search by meaning at the level of works).</summary>
public sealed record SearchItem(
    string Id,
    string Title,
    string[] Authors,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? Year,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Language,
    string[] Formats,
    double Score,
    Passage[] Passages);

/// <param name="Mode"><c>fulltext</c>, <c>semantic</c> or <c>hybrid</c>.</param>
/// <param name="Level"><c>work</c>, or <c>chunk</c> for passages (semantic and hybrid only).</param>
/// <param name="Note">Says when the embeddings are missing or incomplete; null otherwise.</param>
public sealed record SearchResponse(
    string Query,
    string Mode,
    string Level,
    int Total,
    int PendingFiles,
    int WorksWithoutEmbedding,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Note,
    SearchItem[] Results);

/// <summary>The works most like the work <paramref name="Id"/> (<c>GET /works/{id}/similar</c>), best first; <paramref name="Note"/> says when the embeddings are missing.</summary>
public sealed record SimilarResponse(
    string Id,
    string Title,
    int Total,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Note,
    SearchItem[] Results);

/// <summary>The tags of the works most like the work <paramref name="Id"/> (<c>GET /works/{id}/tag-suggestions</c>), best first; <paramref name="Note"/> says when the embeddings are missing.</summary>
public sealed record TagSuggestionsResponse(
    string Id,
    string Title,
    int Total,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Note,
    TagSuggestionItem[] Suggestions);

/// <summary>The body of <c>POST /files/{id}/quote</c>: the passage and, optionally, its place in the file. Any JSON is read so that a wrong type is answered with a reason.</summary>
public sealed record QuoteRequest(JsonNode? Quote, JsonNode? Locator);

/// <summary>The body of <c>PUT /works/{id}/tags</c>. Any JSON is read so that a wrong type is answered with a reason, not a bare 400.</summary>
public sealed record TagsRequest(JsonNode? Tags);

/// <summary>A file of a work. <paramref name="Id"/> is what <c>/files/{id}</c> takes (see <see cref="FileIds"/>).</summary>
public sealed record WorkFile(
    string Id,
    string Name,
    string Format,
    long SizeBytes,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? Pages,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] ReadingPosition? Progress);

/// <summary>The body of <c>PUT /files/{id}/progress</c>. Any JSON is read so that a wrong type is answered with a reason, not a bare 400.</summary>
public sealed record ProgressRequest(JsonNode? Locator);

/// <summary>The body of <c>POST /files/{id}/annotations</c>. Any JSON is read so that a wrong type is answered with a reason.</summary>
public sealed record AnnotationRequest(JsonNode? Kind, JsonNode? Locator, JsonNode? Quote, JsonNode? Color, JsonNode? Note, JsonNode? Rects = null);

/// <summary>A work as the work view shows it: the metadata, the tags, the files with their IDs and the table of contents.</summary>
public sealed record WorkDetail(
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
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? CiteKey,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Description,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? Rating,
    string[] Tags,
    string ReadStatus,
    bool HasCover,
    WorkFile[] Files,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? ReadFile,
    TocItem[] Toc);

/// <param name="Completeness">How many of the 8 metadata fields the file has; only for different files of one work (null for identical files).</param>
public sealed record DuplicateFileItem(
    string Id,
    string WorkId,
    string Title,
    string Name,
    string Path,
    long SizeBytes,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? Pages,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? Completeness);

/// <param name="Kind"><c>identical</c> (same content) or <c>same-format</c> (different files of one work in one format).</param>
/// <param name="Keep">Index in <paramref name="Files"/> of the file that is suggested to stay.</param>
public sealed record DuplicateGroupItem(string Kind, string Format, int Keep, DuplicateFileItem[] Files);

public sealed record DuplicateList(int Total, DuplicateGroupItem[] Groups);

/// <summary>The body of a duplicate action. Any JSON is read so that a wrong type is answered with a reason.</summary>
public sealed record FileRequest(JsonNode? FileId);

/// <summary>The body of the action on a probable pair: the IDs of the two works. Any JSON is read so that a wrong type is answered with a reason.</summary>
public sealed record PairRequest(JsonNode? A, JsonNode? B);

/// <summary>The body of <c>POST /works/{id}/merge</c>: the ID of the work that goes into the one in the path. Any JSON is read so that a wrong type is answered with a reason.</summary>
public sealed record MergeRequest(JsonNode? From);

/// <summary>The body of <c>POST /works/fetch</c>: <c>paper</c> is a DOI or an arXiv ID. Any JSON is read so that a wrong type is answered with a reason.</summary>
public sealed record FetchRequest(JsonNode? Paper);

/// <summary>The body of <c>POST /works/{id}/enrich</c> and <c>/enrich/apply</c>; every field is optional. Any JSON is read so that a wrong type is answered with a reason.</summary>
public sealed record EnrichRequest(JsonNode? Source, JsonNode? Pick, JsonNode? Overwrite, JsonNode? Cover);

/// <summary>The body of <c>POST /works/{id}/references/fetch</c>: <c>apply</c> stores the list, else nothing is written. Any JSON is read so that a wrong type is answered with a reason.</summary>
public sealed record ReferencesFetchRequest(JsonNode? Apply);

/// <summary>The body of <c>POST /files/{id}/move</c>: <c>to</c> (the ID of the work that receives the file) or <c>new</c> (true: a work of its own, described by the optional <c>title</c>, <c>authors</c> and <c>year</c>). Any JSON is read so that a wrong type is answered with a reason.</summary>
public sealed record MoveRequest(JsonNode? To, JsonNode? New, JsonNode? Title, JsonNode? Authors, JsonNode? Year);

/// <summary>The body of <c>POST /tags/rename</c> and <c>/tags/merge</c>: the tag, and the name it gets (<c>to</c>) or the tag it goes into (<c>into</c>).</summary>
public sealed record TagMoveRequest(JsonNode? From, JsonNode? To, JsonNode? Into);

/// <summary>The body of <c>POST /review/{id}/accept</c>: the work to merge into; without it the best candidate of the entry.</summary>
public sealed record AcceptRequest(JsonNode? Into);

/// <summary>The body of <c>POST /organize</c>: the pattern that was previewed; without it the configured one.</summary>
public sealed record OrganizeRequest(JsonNode? Pattern);
