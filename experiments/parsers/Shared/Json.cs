using System.Text.Json;
using System.Text.Json.Serialization;

namespace Proto;

public sealed class ManifestDoc
{
    public List<ManifestEntry> Fixtures { get; set; } = [];
}

public sealed class ManifestEntry
{
    public string Path { get; set; } = "";

    public string Format { get; set; } = "";

    public string? EpubVersion { get; set; }

    public string? Title { get; set; }

    public List<string> Authors { get; set; } = [];

    public string? Language { get; set; }

    public List<string> Traits { get; set; } = [];
}

public sealed class GroundTruthDoc
{
    public List<GtWork> Works { get; set; } = [];

    public List<GtFile> Files { get; set; } = [];
}

public sealed class GtWork
{
    public string Key { get; set; } = "";

    public string Title { get; set; } = "";

    public List<string> Authors { get; set; } = [];

    public string? Language { get; set; }

    public string? Isbn13 { get; set; }
}

public sealed class GtFile
{
    public string Path { get; set; } = "";

    public string Work { get; set; } = "";

    public string Format { get; set; } = "";

    public int? PageCount { get; set; }

    public long Size { get; set; }

    public List<string> Traits { get; set; } = [];

    public GtEmbedded Embedded { get; set; } = new();
}

public sealed class GtEmbedded
{
    public string? Title { get; set; }

    public List<string> Authors { get; set; } = [];

    public string? Isbn13 { get; set; }

    public string? Language { get; set; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ManifestDoc))]
[JsonSerializable(typeof(GroundTruthDoc))]
public sealed partial class SpikeJsonContext : JsonSerializerContext;

public static class JsonFiles
{
    public static ManifestDoc Manifest(string path) =>
        JsonSerializer.Deserialize(File.ReadAllText(path), SpikeJsonContext.Default.ManifestDoc) ?? throw new InvalidDataException(path);

    public static GroundTruthDoc GroundTruth(string path) =>
        JsonSerializer.Deserialize(File.ReadAllText(path), SpikeJsonContext.Default.GroundTruthDoc) ?? throw new InvalidDataException(path);
}
