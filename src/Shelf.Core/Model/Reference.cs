using System.Text.Json;

namespace Shelf.Core.Model;

/// <summary>
/// One entry of the reference list of a paper (F-PAP-11), as the publisher gave it to Crossref. Most entries have a DOI; some have
/// only a title or only the text of the citation (<see cref="Unstructured"/>).
/// </summary>
public sealed record Reference(string? Doi, string? Title, int? Year, string? Author, string? Unstructured);

/// <summary>The content of a <c>references.json</c> next to a <c>book.json</c> (ADR 0019).</summary>
public sealed record ReferencesSidecar
{
    public const int CurrentSchema = 1;

    public int Schema { get; init; } = CurrentSchema;

    /// <summary>The catalog the list came from, e.g. <c>crossref</c>.</summary>
    public string? Source { get; init; }

    /// <summary>When the list was loaded (UTC, to the second).</summary>
    public DateTimeOffset? FetchedAt { get; init; }

    public IReadOnlyList<Reference> References { get; init; } = [];

    public IReadOnlyDictionary<string, JsonElement> UnknownFields { get; init; } = EmptyUnknownFields;

    private static readonly IReadOnlyDictionary<string, JsonElement> EmptyUnknownFields = new Dictionary<string, JsonElement>();
}
