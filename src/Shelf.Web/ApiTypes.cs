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
    string ReadStatus);

public sealed record FacetValue(string Value, int Count);

public sealed record Facets(
    FacetValue[] Tags,
    FacetValue[] Authors,
    FacetValue[] Languages,
    FacetValue[] Years,
    FacetValue[] Formats,
    FacetValue[] Statuses);

/// <summary>One page of the library, and the counts of every facet over all the works that pass the filter.</summary>
public sealed record WorkList(int Total, int Offset, int Limit, WorkItem[] Items, Facets Facets);

/// <summary>A work found by a search, with the places in its text (none for a search by meaning at the level of works).</summary>
public sealed record SearchItem(
    string Id,
    string Title,
    string[] Authors,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? Year,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Language,
    string[] Formats,
    double Score,
    PassageRef[] Passages);

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

/// <summary>The body of <c>PUT /works/{id}/tags</c>. Any JSON is read so that a wrong type is answered with a reason, not a bare 400.</summary>
public sealed record TagsRequest(JsonNode? Tags);
