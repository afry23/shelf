using System.Text;
using Xunit;

namespace Shelf.Index.Tests;

public sealed class FolderNamesTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Theory]
    [InlineData("Flatland: A Romance of Many Dimensions", "Flatland - A Romance of Many Dimensions")]
    [InlineData("Re:Zero", "Re-Zero")]
    [InlineData("AC/DC \\ Live", "AC-DC - Live")]
    [InlineData("What is \"love\"? <really>*", "What is love really")]
    [InlineData("a|b", "a-b")]
    [InlineData("  ..hidden.. ", "hidden")]
    [InlineData("tab\there\u0001", "tabhere")]
    [InlineData("", "_")]
    [InlineData("???", "_")]
    [InlineData("Größe  der   Dinge", "Größe der Dinge")]
    public void Sanitize_makes_one_safe_path_component(string name, string expected) =>
        Assert.Equal(expected, FolderNames.Sanitize(name));

    [Fact]
    public void Sanitize_limits_the_length_in_bytes_without_cutting_a_character()
    {
        var sanitized = FolderNames.Sanitize(new string('ü', 200));

        Assert.True(Encoding.UTF8.GetByteCount(sanitized) <= 120);
        Assert.All(sanitized, c => Assert.Equal('ü', c));
    }

    [Fact]
    public void WorkFolder_is_author_then_title_with_the_year()
    {
        Assert.Equal(Path.Combine("/lib", "Ålander, Jördis", "Event Streams (2019)"), FolderNames.WorkFolder("/lib", "Ålander, Jördis", "Event Streams", 2019));
        Assert.Equal(Path.Combine("/lib", "Doe, Jane", "Quiet Systems"), FolderNames.WorkFolder("/lib", "Doe, Jane", "Quiet Systems", null));
    }

    [Fact]
    public void FreeFolder_skips_folders_that_already_hold_a_work_but_not_plain_folders()
    {
        var folder = _temp.Combine("Author", "Title");
        Directory.CreateDirectory(folder);

        Assert.Equal(folder, FolderNames.FreeFolder(folder));

        File.WriteAllText(Path.Combine(folder, "book.json"), "{}");
        Assert.Equal(folder + " (2)", FolderNames.FreeFolder(folder));

        Directory.CreateDirectory(folder + " (2)");
        File.WriteAllText(Path.Combine(folder + " (2)", "book.json"), "{}");
        Assert.Equal(folder + " (3)", FolderNames.FreeFolder(folder));
    }

    [Fact]
    public void UniqueFileName_numbers_before_the_extension()
    {
        File.WriteAllText(_temp.Combine("a.epub"), "x");
        File.WriteAllText(_temp.Combine("a (1).epub"), "x");

        Assert.Equal("b.epub", FolderNames.UniqueFileName(_temp.Path, "b.epub"));
        Assert.Equal("a (2).epub", FolderNames.UniqueFileName(_temp.Path, "a.epub"));
    }
}
