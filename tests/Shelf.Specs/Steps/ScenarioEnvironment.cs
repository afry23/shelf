using Shelf.Semantic;

namespace Shelf.Specs.Steps;

/// <summary>
/// Per-scenario environment variables shared by step classes. Never reads or writes the process environment.
/// </summary>
public sealed class ScenarioEnvironment
{
    public Dictionary<string, string> Variables { get; } = new();

    /// <summary>The model catalog and downloader of "the model catalog ... has the files"; null runs use the real ones.</summary>
    public FakeModels? Models { get; set; }
}

/// <summary>A catalog of tiny files with the right checksums, and a downloader that serves them (or breaks) without a network.</summary>
public sealed class FakeModels : IModelDownloader
{
    public FakeModels(string name, IReadOnlyDictionary<string, string> contents)
    {
        Contents = contents;
        Catalog = new ModelCatalog(
        [
            new ModelDefinition(
                name,
                [.. contents.Select(c => new ModelFile(
                    c.Key,
                    $"https://models.example/{name}/{c.Key}",
                    System.Text.Encoding.UTF8.GetByteCount(c.Value),
                    Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(c.Value)))))]),
        ]);
    }

    public ModelCatalog Catalog { get; }

    public IReadOnlyDictionary<string, string> Contents { get; }

    /// <summary>Content served instead of the catalog content, by file name.</summary>
    public Dictionary<string, string> Overrides { get; } = new();

    public string? FailWith { get; set; }

    public List<string> Requested { get; } = [];

    public Stream Open(string url)
    {
        Requested.Add(url);
        if (FailWith is not null)
        {
            throw new IOException(FailWith);
        }

        var file = url[(url.LastIndexOf('/') + 1)..];
        return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(Overrides.TryGetValue(file, out var content) ? content : Contents[file]));
    }
}
