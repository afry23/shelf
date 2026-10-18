using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using ModelContextProtocol;

namespace Shelf.Mcp;

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
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Description,
    string[] Tags,
    string ReadStatus,
    BookFile[] Files,
    TocItem[] Toc);

public sealed record PassageText(string Id, string File, string Location, string Where, string Text, int Offset, int TotalChars, bool Truncated);

public sealed record TagNode(string Name, string Path, int Works, int Direct, TagNode[] Children);

public sealed record TagTree(int Tags, TagNode[] Roots);

public sealed record DuplicateEntry(string Id, string Title, string Path, long SizeBytes, [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? Pages);

public sealed record DuplicateSet(string Kind, string Format, DuplicateEntry[] Files, int Keep);

public sealed record DuplicatesResult(int Total, DuplicateSet[] Sets);

public sealed record PlannedMove(string Id, string From, string To);

public sealed record SkippedMove(string Id, string From, string To, string Reason);

public sealed record OrganizationProposal(string Pattern, int Files, int Works, int Unchanged, bool Truncated, PlannedMove[] Moves, SkippedMove[] Collisions);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SearchBooksResult))]
[JsonSerializable(typeof(BookDetails))]
[JsonSerializable(typeof(PassageText))]
[JsonSerializable(typeof(TagTree))]
[JsonSerializable(typeof(DuplicatesResult))]
[JsonSerializable(typeof(OrganizationProposal))]
[JsonSerializable(typeof(TocItem[]))]
[JsonSerializable(typeof(string[]))]
internal partial class McpJsonContext : JsonSerializerContext
{
    /// <summary>MCP default options plus the source-generated context; no reflection-based resolver is needed.</summary>
    /// <remarks>Built on first use: the generated <c>Default</c> is still null while this class initializes (ADR 0006, pitfall 3).</remarks>
    public static JsonSerializerOptions SharedOptions => s_shared ??= Create();

    private static JsonSerializerOptions? s_shared;

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(McpJsonUtilities.DefaultOptions)
        {
            TypeInfoResolver = JsonTypeInfoResolver.Combine(Default, McpJsonUtilities.DefaultOptions.TypeInfoResolver!),
        };
        options.MakeReadOnly();
        return options;
    }
}
