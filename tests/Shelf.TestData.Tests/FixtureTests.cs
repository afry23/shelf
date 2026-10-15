using System.Text.Json;
using UglyToad.PdfPig;
using VersOne.Epub;
using VersOne.Epub.Schema;
using Xunit;

namespace Shelf.TestData.Tests;

public sealed class FixtureTests
{
    private static readonly string Root = RepositoryPaths.Root();

    [Fact]
    public async Task UpstreamEpubsOpenAndMatchTheManifest()
    {
        var catalog = FixtureCatalog.Load(Root);
        var epubs = catalog.Fixtures.Where(f => f.Origin == FixtureCatalog.Upstream && f.Format == "epub").ToList();
        Assert.NotEmpty(epubs);
        foreach (var entry in epubs)
        {
            var book = await EpubReader.ReadBookAsync(catalog.FullPath(entry));
            Assert.Equal(entry.Title, book.Title);
            Assert.StartsWith(entry.Language, book.Schema.Package.Metadata.Languages.First().Language, StringComparison.Ordinal);
            Assert.Equal(entry.EpubVersion, book.Schema.Package.EpubVersion switch
            {
                EpubVersion.EPUB_2 => "2.0",
                EpubVersion.EPUB_3 or EpubVersion.EPUB_3_1 => "3.0",
                _ => "?",
            });
            Assert.True(entry.Traits.Contains("cover") == (book.CoverImage is not null), entry.Path);
        }
    }

    [Fact]
    public void UpstreamPdfHasTextOnPageOne()
    {
        var catalog = FixtureCatalog.Load(Root);
        var pdf = catalog.Fixtures.Single(f => f.Origin == FixtureCatalog.Upstream && f.Format == "pdf");
        using var document = PdfDocument.Open(catalog.FullPath(pdf));
        Assert.Contains("Hilbert", document.GetPage(1).Text, StringComparison.Ordinal);
        Assert.True(document.TryGetXmpMetadata(out _));
    }

    [Fact]
    public async Task DerivedEpubsParseAsIntended()
    {
        var directory = Path.Combine(Root, "tests", "fixtures", "derived");

        var series = await EpubReader.ReadBookAsync(Path.Combine(directory, "epub3-isbn-series.epub"));
        Assert.Equal("Event Streams in Practice", series.Title);
        Assert.Equal(["Jördis Ålander"], series.AuthorList);
        Assert.Contains(series.Schema.Package.Metadata.Identifiers, i => i.Identifier == "urn:isbn:9780000000019");
        Assert.Null(series.CoverImage);

        var german = await EpubReader.ReadBookAsync(Path.Combine(directory, "epub2-isbn-cover.epub"));
        Assert.Equal("Größenordnungen verteilter Systeme", german.Title);
        Assert.Equal(["Lukas Weiß", "Ana Núñez"], german.AuthorList);
        Assert.NotNull(german.CoverImage);
        Assert.Contains(german.Schema.Package.Metadata.Identifiers, i => i.Identifier == "9780000000026" && i.Scheme == "ISBN");

        var empty = await EpubReader.ReadBookAsync(Path.Combine(directory, "Jane Doe - Quiet Systems (2004).epub"));
        Assert.True(string.IsNullOrEmpty(empty.Title));
        Assert.Empty(empty.AuthorList);
    }

    [Fact]
    public async Task BrokenDerivedFixturesFailToParse()
    {
        var directory = Path.Combine(Root, "tests", "fixtures", "derived");
        await Assert.ThrowsAnyAsync<Exception>(() => EpubReader.ReadBookAsync(Path.Combine(directory, "malformed-opf.epub")));
        await Assert.ThrowsAnyAsync<Exception>(() => EpubReader.ReadBookAsync(Path.Combine(directory, "not-a-zip.epub")));
        Assert.Equal(64, new FileInfo(Path.Combine(directory, "not-a-zip.epub")).Length);
    }

    [Fact]
    public void DerivedPdfsParseAsIntended()
    {
        var directory = Path.Combine(Root, "tests", "fixtures", "derived");
        using (var document = PdfDocument.Open(Path.Combine(directory, "Event Streams in Practice.pdf")))
        {
            Assert.Equal("Event Streams in Practice", document.Information.Title);
            Assert.Equal("Jördis Ålander", document.Information.Author);
            Assert.Contains("ISBN 978-0-00-000001-9", document.GetPage(1).Text, StringComparison.Ordinal);
            Assert.True(document.TryGetXmpMetadata(out _));
        }

        using var plain = PdfDocument.Open(Path.Combine(directory, "Jane Doe - Quiet Systems (2004).pdf"));
        Assert.True(string.IsNullOrEmpty(plain.Information.Title));
        Assert.False(plain.TryGetXmpMetadata(out _));
        Assert.StartsWith("Quiet Systems", plain.GetPage(1).Text, StringComparison.Ordinal);
    }

    [Fact]
    public void DerivedFixturesMatchTheDefinitions()
    {
        foreach (var name in DerivedFixtures.FileNames)
        {
            var committed = File.ReadAllBytes(Path.Combine(Root, "tests", "fixtures", "derived", name));
            Assert.True(committed.AsSpan().SequenceEqual(DerivedFixtures.Create(name)), name);
        }
    }

    [Fact]
    public void CatalogRejectsUnknownTraitsOriginsAndBackslashes()
    {
        Assert.Contains("unknown trait 'bogus'", LoadMutated(e => e.Traits.Add("bogus")), StringComparison.Ordinal);
        Assert.Contains("unknown origin 'elsewhere'", LoadMutated(e => e.Origin = "elsewhere"), StringComparison.Ordinal);
        Assert.Contains("separators", LoadMutated(e => e.Path = @"derived\x.epub"), StringComparison.Ordinal);
        Assert.Contains("missing field(s) sha256", LoadMutated(e => e.Sha256 = string.Empty), StringComparison.Ordinal);
    }

    private static string LoadMutated(Action<MutableEntry> mutate)
    {
        var document = JsonSerializer.Deserialize<MutableManifest>(File.ReadAllText(Path.Combine(Root, FixtureCatalog.ManifestRelativePath)), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!;
        mutate(document.Fixtures[0]);
        var temporary = Path.Combine(Path.GetTempPath(), "shelf-testdata-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var manifest = Path.Combine(temporary, "tests", "fixtures");
            Directory.CreateDirectory(manifest);
            File.WriteAllText(Path.Combine(manifest, "manifest.json"), JsonSerializer.Serialize(document, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
            return Assert.Throws<InvalidDataException>(() => FixtureCatalog.Load(temporary)).Message;
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    private sealed class MutableManifest
    {
        public int Schema { get; set; }

        public JsonElement Budget { get; set; }

        public List<MutableEntry> Fixtures { get; set; } = [];
    }

    private sealed class MutableEntry
    {
        public string Path { get; set; } = string.Empty;

        public string Origin { get; set; } = string.Empty;

        public string Work { get; set; } = string.Empty;

        public string Format { get; set; } = string.Empty;

        public string? EpubVersion { get; set; }

        public string? Title { get; set; }

        public List<string> Authors { get; set; } = [];

        public string? Language { get; set; }

        public List<string> Traits { get; set; } = [];

        public JsonElement Source { get; set; }

        public string License { get; set; } = string.Empty;

        public JsonElement PublicDomain { get; set; }

        public List<string> Modifications { get; set; } = [];

        public string? GeneratorCommand { get; set; }

        public long Size { get; set; }

        public string Sha256 { get; set; } = string.Empty;
    }
}
