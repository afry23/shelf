using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using ModelContextProtocol;
using Shelf.Library;

namespace Shelf.Mcp;

// The records themselves live in Shelf.Library; every member is always written, null included (see LibraryTypes.cs).

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SearchBooksResult))]
[JsonSerializable(typeof(BookDetails))]
[JsonSerializable(typeof(PassageText))]
[JsonSerializable(typeof(SemanticSearchResult))]
[JsonSerializable(typeof(SimilarBooksResult))]
[JsonSerializable(typeof(TagChange))]
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
