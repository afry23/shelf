using System.Text.Json;
using System.Text.Json.Nodes;
using System.IO.Compression;
using Reqnroll;
using Shelf.Index;
using Shelf.TestData;
using SkiaSharp;
using Xunit;

namespace Shelf.Specs.Steps;

/// <summary>Steps for cover images: where a work's folder is, and what the cover file is.</summary>
[Binding]
public sealed class CoverSteps(ConfigSteps workspace)
{
    private string? _folder;

    /// <summary>The folder of the work whose title contains the text; the sidecars are read from the disk, not from the index.</summary>
    public string FindWorkFolder(string title)
    {
        var library = Path.Combine(workspace.WorkingDirectory, "library");
        foreach (var sidecar in Directory.EnumerateFiles(library, "book.json", SearchOption.AllDirectories))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(sidecar));
            if (document.RootElement.TryGetProperty("title", out var value)
                && value.GetString()!.Contains(title, StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetDirectoryName(sidecar)!;
            }
        }

        throw new InvalidOperationException($"No work with '{title}' in its title.");
    }

    public static string FixturePath(string relative) => Path.Combine(RepositoryPaths.Root(), "tests", "fixtures", relative);

    private JsonObject Sidecar(string folder) => JsonNode.Parse(File.ReadAllText(Path.Combine(folder, "book.json")))!.AsObject();

    private static string? CoverName(JsonObject sidecar) => sidecar["cover"]?.GetValue<string>();

    // ---- the folder and its cover ----

    [Then("the work folder of {string} has the cover {string}")]
    public void ThenTheWorkFolderHasTheCover(string title, string name)
    {
        _folder = FindWorkFolder(title);
        Assert.Equal(name, CoverName(Sidecar(_folder)));
        Assert.True(File.Exists(Path.Combine(_folder, name)), $"{name} is not in {_folder}");
    }

    [Then("the work folder of {string} has no cover")]
    public void ThenTheWorkFolderHasNoCover(string title)
    {
        _folder = FindWorkFolder(title);
        Assert.Null(CoverName(Sidecar(_folder)));
        Assert.Empty(Directory.EnumerateFiles(_folder, "cover.*"));
    }

    [Then("the work folder of {string} has no file {string}")]
    public void ThenTheWorkFolderHasNoFile(string title, string name) =>
        Assert.False(File.Exists(Path.Combine(FindWorkFolder(title), name)), $"{name} exists");

    [Then("the work {string} has a cover")]
    public void ThenTheWorkHasACover(string title)
    {
        var folder = FindWorkFolder(title);
        var name = CoverName(Sidecar(folder));
        Assert.NotNull(name);
        Assert.True(File.Exists(Path.Combine(folder, name)));
    }

    [Then("the work {string} has no cover")]
    public void ThenTheWorkHasNoCover(string title) => Assert.Null(CoverName(Sidecar(FindWorkFolder(title))));

    [Then("the index lists a cover for {string}")]
    public void ThenTheIndexListsACover(string title)
    {
        var folder = FindWorkFolder(title);
        var id = Sidecar(folder)["id"]!.GetValue<string>();
        using var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(Path.Combine(workspace.WorkingDirectory, "library")));
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT cover_path FROM works WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        Assert.IsType<string>(command.ExecuteScalar());
    }

    // ---- the cover file ----

    [Then("the cover {string} of that work is byte for byte the image {string} of the EPUB {string}")]
    public void ThenTheCoverIsTheImageOfTheEpub(string name, string entry, string epub)
    {
        using var archive = ZipFile.OpenRead(FixturePath(epub));
        using var stream = archive.GetEntry(entry)!.Open();
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        Assert.Equal(copy.ToArray(), File.ReadAllBytes(Path.Combine(_folder!, name)));
    }

    [Then("the cover {string} of that work is an image of {int} x {int} pixels")]
    public void ThenTheCoverIsAnImageOf(string name, int width, int height)
    {
        using var bitmap = SKBitmap.Decode(File.ReadAllBytes(Path.Combine(_folder!, name)));
        Assert.NotNull(bitmap);
        Assert.Equal((width, height), (bitmap.Width, bitmap.Height));
    }

    [Then("the cover {string} of that work is a JPEG")]
    public void ThenTheCoverIsAJpeg(string name) =>
        Assert.Equal([0xFF, 0xD8, 0xFF], File.ReadAllBytes(Path.Combine(_folder!, name))[..3]);

    [Then("the cover {string} of that work is not blank")]
    public void ThenTheCoverIsNotBlank(string name)
    {
        using var bitmap = SKBitmap.Decode(File.ReadAllBytes(Path.Combine(_folder!, name)));
        double sum = 0, squares = 0;
        var count = bitmap.Width * bitmap.Height;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var c = bitmap.GetPixel(x, y);
                var luminance = (0.299 * c.Red) + (0.587 * c.Green) + (0.114 * c.Blue);
                sum += luminance;
                squares += luminance * luminance;
            }
        }

        var deviation = Math.Sqrt((squares / count) - Math.Pow(sum / count, 2));
        Assert.True(deviation >= 5, $"The standard deviation of the luminance is {deviation:F1}; the cover looks blank.");
    }

    // ---- changing the folder ----

    [Given("the sidecar of {string} has no cover entry")]
    public void GivenTheSidecarHasNoCoverEntry(string title)
    {
        var folder = FindWorkFolder(title);
        var sidecar = Sidecar(folder);
        sidecar.Remove("cover");
        File.WriteAllText(Path.Combine(folder, "book.json"), sidecar.ToJsonString());
    }

    [Given("the cover of {string} is removed")]
    public void GivenTheCoverIsRemoved(string title)
    {
        var folder = FindWorkFolder(title);
        foreach (var file in Directory.EnumerateFiles(folder, "cover.*"))
        {
            File.Delete(file);
        }

        GivenTheSidecarHasNoCoverEntry(title);
    }

    [Given("the cover of {string} is deleted from the disk")]
    public void GivenTheCoverIsDeletedFromTheDisk(string title)
    {
        var folder = FindWorkFolder(title);
        foreach (var file in Directory.EnumerateFiles(folder, "cover.*"))
        {
            File.Delete(file);
        }
    }

    [Given("the fixture {string} is added to the work folder of {string}")]
    public void GivenTheFixtureIsAdded(string fixture, string title) =>
        File.Copy(FixturePath(fixture), Path.Combine(FindWorkFolder(title), Path.GetFileName(fixture)));
}
