using System.Text.Json.Serialization;

namespace Proto;

public sealed class GroundTruth
{
    public List<GtWork> Works { get; set; } = [];
}

public sealed class GtWork
{
    public string Key { get; set; } = "";
    public string Title { get; set; } = "";
    public List<string> Authors { get; set; } = [];
    public string Language { get; set; } = "en";
    public int Year { get; set; }
    public string? Isbn13 { get; set; }
    public GtSeries? Series { get; set; }
}

public sealed class GtSeries
{
    public string Name { get; set; } = "";
    public int Index { get; set; }
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(GroundTruth))]
public sealed partial class GtContext : JsonSerializerContext;
