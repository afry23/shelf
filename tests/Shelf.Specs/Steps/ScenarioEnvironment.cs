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

    /// <summary>The embedder commands get instead of ONNX Runtime; null runs use the real one.</summary>
    public FakeEmbedder? Embedder { get; set; }

    /// <summary>Cancelled by the fake embedder to simulate Ctrl-C.</summary>
    public CancellationTokenSource Interrupt { get; set; } = new();
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

/// <summary>
/// An embedder without a model: every word becomes a direction, so texts that share words are near each other. It
/// records what it was asked to embed and can interrupt the run (as Ctrl-C would) after a number of calls.
/// </summary>
public sealed class FakeEmbedder(int dimensions, ScenarioEnvironment environment) : IEmbedder
{
    public int Dimensions => dimensions;

    public List<(string Kind, string Text)> Calls { get; } = [];

    /// <summary>Interrupts after this many calls since the last reset; null never interrupts.</summary>
    public int? StopAfter { get; set; }

    public float[] EmbedQuery(string text) => Embed("query", text);

    public float[] EmbedPassage(string text) => Embed("passage", text);

    private float[] Embed(string kind, string text)
    {
        Calls.Add((kind, text));
        var vector = new float[dimensions];
        foreach (var word in text.Split([' ', '\n', '\t', ',', '.', ':', ';', '!', '?', '(', ')', '"', '\''], StringSplitOptions.RemoveEmptyEntries))
        {
            var hash = 2166136261u;
            foreach (var c in word.ToLowerInvariant())
            {
                hash = (hash ^ c) * 16777619u;
            }

            vector[hash % dimensions] += 1f;
        }

        var norm = MathF.Sqrt(vector.Sum(v => v * v));
        if (norm == 0)
        {
            vector[0] = 1f;
            norm = 1f;
        }

        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] /= norm;
        }

        if (StopAfter is { } limit && Calls.Count >= limit)
        {
            environment.Interrupt.Cancel();
        }

        return vector;
    }
}
