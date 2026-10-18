using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shelf.Web;

/// <summary>An answer that is not a success: RFC 9457 fields, nothing else (no <c>type</c> URL, no trace ID).</summary>
public sealed record ApiProblem(int Status, string Title, string? Detail);

/// <summary>What <c>GET /api/v1/status</c> answers.</summary>
public sealed record StatusResponse(string Version, int Works, int Files);

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ApiProblem))]
[JsonSerializable(typeof(StatusResponse))]
internal partial class WebJsonContext : JsonSerializerContext;
