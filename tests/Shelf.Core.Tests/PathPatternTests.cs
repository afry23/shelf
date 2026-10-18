using System.Text;
using Shelf.Core.Organization;
using Xunit;

namespace Shelf.Core.Tests;

public class PathPatternTests
{
    private static PatternInput Input(
        string title = "Patterns of Enterprise Application Architecture",
        string[]? authors = null,
        int? year = 2002,
        string? series = null,
        double? seriesIndex = null,
        string? language = "en",
        string? firstTag = null,
        string extension = "epub") =>
        new(title, authors ?? ["Martin Fowler"], year, series, seriesIndex, language, firstTag, extension);

    [Fact]
    public void Default_pattern_gives_author_then_title_and_year_then_file()
    {
        var path = PathPattern.Default.Render(Input());

        Assert.Equal(
            "Fowler, Martin/Patterns of Enterprise Application Architecture (2002)/Patterns of Enterprise Application Architecture.epub",
            path);
    }

    [Fact]
    public void A_missing_year_removes_the_empty_brackets()
    {
        Assert.Equal("Fowler, Martin/Refactoring/Refactoring.pdf", PathPattern.Default.Render(Input("Refactoring", year: null, extension: "pdf")));
    }

    [Theory]
    [InlineData(1, "Fowler, Martin")]
    [InlineData(3, "Fowler, Martin")]
    [InlineData(4, "Fowler, Martin et al")]
    [InlineData(6, "Fowler, Martin et al")]
    public void Only_the_first_author_names_the_folder_and_four_or_more_add_et_al_without_the_final_dot(int count, string folder)
    {
        var authors = new[] { "Martin Fowler", "A B", "C D", "E F", "G H", "I J" }[..count];

        Assert.Equal(folder, PathPattern.Default.Render(Input(authors: authors)).Split('/')[0]);
    }

    [Fact]
    public void Author_placeholder_is_the_display_name_with_et_al()
    {
        var pattern = PathPattern.Parse("{author}/{title}.{ext}");

        Assert.Equal("Martin Fowler et al/T.epub", pattern.Render(Input("T", ["Martin Fowler", "a", "b", "c"])));
    }

    [Fact]
    public void Without_an_author_the_folder_is_Unknown_Author()
    {
        Assert.StartsWith("Unknown Author/", PathPattern.Default.Render(Input(authors: [])), StringComparison.Ordinal);
    }

    [Fact]
    public void An_empty_folder_part_is_left_out()
    {
        var pattern = PathPattern.Parse("{series}/{title}.{ext}");

        Assert.Equal("T.epub", pattern.Render(Input("T")));
        Assert.Equal("Dune/T.epub", pattern.Render(Input("T", series: "Dune")));
    }

    [Fact]
    public void All_placeholders_render()
    {
        var pattern = PathPattern.Parse("{language}/{first_tag}/{series} {series_index}/{author} - {title} ({year}).{ext}");

        Assert.Equal(
            "en/devops/Dune 2.5/Frank Herbert - Messiah (1969).EPUB".Replace("EPUB", "epub"),
            pattern.Render(Input("Messiah", ["Frank Herbert"], 1969, "Dune", 2.5, "en", "devops", ".EPUB")));
    }

    [Theory]
    [InlineData(1.0, "Dune 1")]
    [InlineData(2.5, "Dune 2.5")]
    [InlineData(10.0, "Dune 10")]
    public void Series_index_has_no_trailing_zeros(double index, string folder)
    {
        var pattern = PathPattern.Parse("{series} {series_index}/{title}.{ext}");

        Assert.Equal(folder, pattern.Render(Input(series: "Dune", seriesIndex: index)).Split('/')[0]);
    }

    [Fact]
    public void Reserved_characters_are_replaced_per_part()
    {
        var path = PathPattern.Default.Render(Input("Flatland: A Romance / of *Many* Dimensions?", year: null));

        Assert.Equal("Fowler, Martin/Flatland - A Romance - of Many Dimensions/Flatland - A Romance - of Many Dimensions.epub", path);
    }

    [Fact]
    public void Names_are_normalized_to_NFC()
    {
        var decomposed = "Cafe\u0301";

        var path = PathPattern.Default.Render(Input(decomposed, year: null));

        Assert.Contains("Café", path, StringComparison.Ordinal);
        Assert.DoesNotContain('\u0301', path);
    }

    [Fact]
    public void A_long_title_is_shortened_before_the_extension_never_through_it()
    {
        var path = PathPattern.Default.Render(Input(new string('x', 300), year: null, extension: "epub"));
        var segments = path.Split('/');

        Assert.All(segments, s => Assert.True(Encoding.UTF8.GetByteCount(s) <= 120, s));
        Assert.EndsWith(".epub", segments[^1], StringComparison.Ordinal);
    }

    [Fact]
    public void Shortening_never_cuts_a_surrogate_pair()
    {
        var title = string.Concat(Enumerable.Repeat("\U0001F4DA", 60));

        var segment = PathPattern.Default.Render(Input(title, year: null)).Split('/')[1];

        Assert.True(Encoding.UTF8.GetByteCount(segment) <= 120);
        Assert.DoesNotContain('\uFFFD', Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(segment)));
    }

    [Fact]
    public void A_part_does_not_end_in_a_dot_or_space()
    {
        var path = PathPattern.Default.Render(Input("The End...", year: null));

        Assert.Equal("Fowler, Martin/The End/The End.epub", path);
    }

    [Fact]
    public void A_tag_hierarchy_becomes_one_folder_name()
    {
        var pattern = PathPattern.Parse("{first_tag}/{title}.{ext}");

        Assert.Equal("architektur-ddd/T.epub", pattern.Render(Input("T", firstTag: "architektur/ddd")));
    }

    [Theory]
    [InlineData("", "must not be empty")]
    [InlineData("   ", "must not be empty")]
    [InlineData("{title}", "the last part must end with '.{ext}'")]
    [InlineData("{title}.pdf", "the last part must end with '.{ext}'")]
    [InlineData("{title}/", "the last part must end with '.{ext}'")]
    [InlineData("{nope}/{title}.{ext}", "unknown placeholder '{nope}'")]
    [InlineData("{title.{ext}", "unbalanced braces")]
    [InlineData("{title}}.{ext}", "unbalanced braces")]
    [InlineData("{title}.{ext", "unbalanced braces")]
    [InlineData("/{title}.{ext}", "must be a relative path")]
    [InlineData("C:/{title}.{ext}", "must be a relative path")]
    [InlineData("a/../{title}.{ext}", "must not contain '..'")]
    public void Invalid_patterns_are_rejected_with_the_problem(string text, string message)
    {
        var e = Assert.Throws<PatternException>(() => PathPattern.Parse(text));

        Assert.Equal(message, e.Message);
    }

    [Fact]
    public void A_pattern_that_is_only_the_extension_gives_an_underscore_name()
    {
        Assert.Equal("_.epub", PathPattern.Parse(".{ext}").Render(Input()));
    }

    [Theory]
    [InlineData("", "_")]
    [InlineData("...", "_")]
    [InlineData("a:b", "a-b")]
    [InlineData("a: b", "a - b")]
    [InlineData("  a   b  ", "a b")]
    public void Sanitizer_cases(string name, string expected)
    {
        Assert.Equal(expected, PathSanitizer.Segment(name));
    }
}
