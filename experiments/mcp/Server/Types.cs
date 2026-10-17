using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using ModelContextProtocol;

namespace Proto;

public sealed record FormatInfo(string Kind, long SizeBytes, int? Pages);

public sealed record Book(string Id, string Title, string[] Authors, FormatInfo[] Formats, string[] Tags);

public sealed record TagNode(string Name, int Count, TagNode[] Children);

public sealed record AddTagsResult(string Id, string[] Tags, int Added);

public sealed record TocEntry(string Title, int Page);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Book))]
[JsonSerializable(typeof(TagNode))]
[JsonSerializable(typeof(AddTagsResult))]
[JsonSerializable(typeof(TocEntry[]))]
[JsonSerializable(typeof(string[]))]
internal partial class ProtoJsonContext : JsonSerializerContext
{
    /// <summary>MCP default options plus the source-generated context; no reflection-based resolver is needed.</summary>
    /// <remarks>Built on first use: the generated <c>Default</c> is a static property initializer in another part of this class and is still null during this class's own static initialization.</remarks>
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
