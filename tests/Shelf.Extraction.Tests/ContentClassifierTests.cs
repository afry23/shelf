using Xunit;

namespace Shelf.Extraction.Tests;

public sealed class ContentClassifierTests
{
    private const string Prose =
        "The sidecar container extends the main container with logging and proxying so that the application itself stays small and focused on its own task.";

    [Theory]
    [InlineData("toc", ContentKind.Toc)]
    [InlineData("frontmatter preface", ContentKind.FrontMatter)]
    [InlineData("chapter preface", ContentKind.FrontMatter)]
    [InlineData("INDEX", ContentKind.Index)]
    [InlineData("bibliography", ContentKind.BackMatter)]
    [InlineData("chapter", ContentKind.Body)]
    [InlineData("bodymatter", ContentKind.Body)]
    [InlineData("", ContentKind.Body)]
    [InlineData(null, ContentKind.Body)]
    public void The_epub_type_decides_the_kind(string? value, ContentKind expected) =>
        Assert.Equal(expected, ContentClassifier.FromEpubType(value));

    [Theory]
    [InlineData("Table of Contents", ContentKind.Toc)]
    [InlineData("Inhaltsverzeichnis", ContentKind.Toc)]
    [InlineData("  Vorwort:  ", ContentKind.FrontMatter)]
    [InlineData("Preface", ContentKind.FrontMatter)]
    [InlineData("Index", ContentKind.Index)]
    [InlineData("Stichwortverzeichnis", ContentKind.Index)]
    [InlineData("Literaturverzeichnis", ContentKind.BackMatter)]
    [InlineData("12 Bibliography", ContentKind.BackMatter)]
    [InlineData("Index of Refraction", ContentKind.Body)]
    [InlineData("Contents of a Pod", ContentKind.Body)]
    [InlineData("Introduction", ContentKind.Body)]
    [InlineData(null, ContentKind.Body)]
    public void A_well_known_title_decides_the_kind(string? title, ContentKind expected) =>
        Assert.Equal(expected, ContentClassifier.FromTitle(title));

    [Fact]
    public void Prose_is_body() => Assert.Equal(ContentKind.Body, ContentClassifier.Classify(ContentKind.Body, Prose));

    [Fact]
    public void A_segment_kind_is_kept_for_prose() =>
        Assert.Equal(ContentKind.FrontMatter, ContentClassifier.Classify(ContentKind.FrontMatter, Prose));

    [Fact]
    public void Dot_leaders_with_page_numbers_are_a_table_of_contents() =>
        Assert.Equal(ContentKind.Toc, ContentClassifier.Classify(ContentKind.Body, "A ........ 1 B ........ 4 C ........ 9 D . . . . . 12"));

    [Fact]
    public void Page_references_are_an_index() =>
        Assert.Equal(
            ContentKind.Index,
            ContentClassifier.Classify(ContentKind.Body, "adapter, 12, 45-47 ambassador, 27, 88 batch job, 33, 90-92 cluster, 4, 19, 56 daemon, 58, 61, 103 pod, 7, 14, 22"));

    [Fact]
    public void Prose_with_numbers_and_sentences_is_not_an_index() =>
        Assert.Equal(
            ContentKind.Body,
            ContentClassifier.Classify(ContentKind.Body, "In 2017 the team shipped 12 releases. Each of them had 3 or 4 changes. Nobody noticed the 5 small bugs that stayed in 2 of the 12 builds. That was fine."));

    [Fact]
    public void Source_code_is_code() =>
        Assert.Equal(
            ContentKind.Code,
            ContentClassifier.Classify(ContentKind.Body, "static int Sum(int[] xs) { var total = 0; for (var i = 0; i < xs.Length; i++) { total += xs[i]; } return total; }"));

    [Fact]
    public void Prose_that_names_a_function_is_not_code() =>
        Assert.Equal(ContentKind.Body, ContentClassifier.Classify(ContentKind.Body, "Call the Sum() function with a list of numbers and it returns the total of all numbers that are in the list you gave it."));

    [Fact]
    public void A_short_text_is_never_judged_by_its_look() =>
        Assert.Equal(ContentKind.Body, ContentClassifier.Classify(ContentKind.Body, "x = f(y); }"));

    [Fact]
    public void Pdf_pages_follow_the_outline_up_to_the_next_entry_of_the_same_level()
    {
        TocEntry[] outline =
        [
            new("Preface", 1, "page=3"),
            new("Why this book", 2, "page=3"),
            new("Chapter 1", 1, "page=6"),
            new("Index", 1, "page=20"),
        ];

        var kinds = ContentClassifier.PdfPageKinds(outline, 22);

        Assert.Equal([3, 4, 5, 20, 21, 22], kinds.Keys.Order());
        Assert.All([3, 4, 5], page => Assert.Equal(ContentKind.FrontMatter, kinds[page]));
        Assert.All([20, 21, 22], page => Assert.Equal(ContentKind.Index, kinds[page]));
    }

    [Fact]
    public void An_outline_entry_without_a_page_marks_nothing() =>
        Assert.Empty(ContentClassifier.PdfPageKinds([new TocEntry("Index", 1, null)], 10));
}
