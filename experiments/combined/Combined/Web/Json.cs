using System.Text.Json;
using System.Text.Json.Serialization;

namespace Proto.Web;

public sealed record FormatInfo(string Kind, long SizeBytes, int? Pages);

public sealed record Book(string Id, string Title, string[] Authors, FormatInfo[] Formats, string[] Tags);

public sealed record TagsRequest(string[]? Tags);

public sealed record TagsResponse(string Id, string[] Tags);

public sealed record ErrorBody(string Title, int Status);

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(Book))]
[JsonSerializable(typeof(TagsRequest))]
[JsonSerializable(typeof(TagsResponse))]
[JsonSerializable(typeof(ErrorBody))]
internal partial class WebJsonContext : JsonSerializerContext;
