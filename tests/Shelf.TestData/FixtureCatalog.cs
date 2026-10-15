using System.Text.Json;
using Shelf.TestData.Model;

namespace Shelf.TestData;

public sealed class FixtureSource
{
    public string? Url { get; init; }

    public string? Retrieved { get; init; }

    public string? Publisher { get; init; }
}

public sealed class FixturePublicDomain
{
    public string? Us { get; init; }

    public string? LifePlus70 { get; init; }
}

public sealed class FixtureEntry
{
    public string Path { get; init; } = string.Empty;

    public string Origin { get; init; } = string.Empty;

    public string Work { get; init; } = string.Empty;

    public string Format { get; init; } = string.Empty;

    public string? EpubVersion { get; init; }

    public string? Title { get; init; }

    public List<string> Authors { get; init; } = [];

    public string? Language { get; init; }

    public List<string> Traits { get; init; } = [];

    public FixtureSource? Source { get; init; }

    public string License { get; init; } = string.Empty;

    public FixturePublicDomain? PublicDomain { get; init; }

    public List<string> Modifications { get; init; } = [];

    public string? GeneratorCommand { get; init; }

    public long Size { get; init; }

    public string Sha256 { get; init; } = string.Empty;
}

public sealed class FixtureBudget
{
    public long TotalBytes { get; init; }

    public long FileBytes { get; init; }
}

/// <summary>Reader of <c>tests/fixtures/manifest.json</c> for specs and later slices.</summary>
public sealed class FixtureCatalog
{
    public const string Upstream = "upstream";
    public const string Derived = "derived";
    public const string ManifestRelativePath = "tests/fixtures/manifest.json";

    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public int Schema { get; init; }

    public FixtureBudget Budget { get; init; } = new();

    public List<FixtureEntry> Fixtures { get; init; } = [];

    /// <summary>Absolute path of <c>tests/fixtures</c>; not part of the manifest.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string Directory { get; private set; } = string.Empty;

    public static FixtureCatalog Load(string repositoryRoot)
    {
        var manifestPath = System.IO.Path.Combine(repositoryRoot, ManifestRelativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
        var catalog = JsonSerializer.Deserialize<FixtureCatalog>(File.ReadAllBytes(manifestPath), Options)
            ?? throw new InvalidDataException($"'{manifestPath}' is not a fixture manifest.");
        catalog.Directory = System.IO.Path.GetDirectoryName(manifestPath)!;
        catalog.Validate(manifestPath);
        return catalog;
    }

    public string FullPath(FixtureEntry entry) =>
        System.IO.Path.Combine(Directory, entry.Path.Replace('/', System.IO.Path.DirectorySeparatorChar));

    private void Validate(string manifestPath)
    {
        if (Schema != 1)
        {
            throw new InvalidDataException($"{manifestPath}: unsupported schema {Schema}.");
        }

        foreach (var entry in Fixtures)
        {
            var where = $"{manifestPath}: fixture '{entry.Path}'";
            if (entry.Path.Length == 0 || entry.Path.Contains('\\', StringComparison.Ordinal))
            {
                throw new InvalidDataException($"{where}: path must be non-empty and use '/' separators.");
            }

            if (entry.Origin is not (Upstream or Derived))
            {
                throw new InvalidDataException($"{where}: unknown origin '{entry.Origin}'.");
            }

            foreach (var trait in entry.Traits.Where(t => !FixtureTraits.All.Contains(t)))
            {
                throw new InvalidDataException($"{where}: unknown trait '{trait}'.");
            }

            var missing = new[]
            {
                (entry.Work, "work"), (entry.Format, "format"), (entry.License, "license"), (entry.Sha256, "sha256"),
            }.Where(x => string.IsNullOrWhiteSpace(x.Item1)).Select(x => x.Item2).ToList();
            if (missing.Count > 0)
            {
                throw new InvalidDataException($"{where}: missing field(s) {string.Join(", ", missing)}.");
            }

            if (entry.Size <= 0)
            {
                throw new InvalidDataException($"{where}: missing field size.");
            }
        }
    }
}
