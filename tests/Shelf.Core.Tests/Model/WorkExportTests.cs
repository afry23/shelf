using Shelf.Core.Model;
using Xunit;

namespace Shelf.Core.Tests.Model;

public sealed class WorkExportTests
{
    private static WorkSidecar Work(string? citeKey = "alander2021event") => new()
    {
        Id = Ulid.TryParse("01J9Z3K0000000000000000002", out var id) ? id : default,
        Title = "Event \"Sourcing\": in Practice",
        Authors = ["Jördis Ålander"],
        Year = 2021,
        CiteKey = citeKey,
    };

    private static Annotation Highlight(string file, string quote, string? note = null) =>
        new(Ulid.New(), file, AnnotationKind.Highlight, "page=2", new AnnotationQuote(quote), "green", note, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);

    [Theory]
    [InlineData("alander2021event", "alander2021event.md")]
    [InlineData("a/b:c", "a-b-c.md")]
    [InlineData("smith-2020_x+y", "smith-2020_x+y.md")]
    public void FileName_is_the_cite_key_made_safe(string key, string expected) =>
        Assert.Equal(expected, WorkExport.FileName(Work(key)));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void A_work_without_cite_key_is_not_exported(string? key)
    {
        Assert.Throws<InvalidOperationException>(() => WorkExport.FileName(Work(key)));
        Assert.Throws<InvalidOperationException>(() => WorkExport.Render(Work(key), []));
    }

    [Fact]
    public void Front_matter_quotes_what_yaml_would_misread()
    {
        var text = WorkExport.Render(Work(), []);
        Assert.StartsWith("---\ncitekey: alander2021event\ntitle: \"Event \\\"Sourcing\\\": in Practice\"\n", text);
        Assert.Contains("authors:\n  - Jördis Ålander\n", text);
        Assert.Contains("year: 2021\n", text);
        Assert.Contains("read_status: unread\n", text);
        Assert.DoesNotContain("subtitle", text);
        Assert.Contains("No highlights or notes.", text);
    }

    [Theory]
    [InlineData("a/b:c", "citekey: \"a/b:c\"")]
    [InlineData("yes", "citekey: \"yes\"")]
    [InlineData("1999book", "citekey: 1999book")]
    public void A_cite_key_is_quoted_only_when_needed(string key, string expected) =>
        Assert.Contains(expected + "\n", WorkExport.Render(Work(key), []));

    [Fact]
    public void Highlights_come_before_notes_grouped_by_file_in_source_order()
    {
        var note = new Annotation(Ulid.New(), "a.pdf", AnnotationKind.Note, null, null, null, "A note.", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
        var text = WorkExport.Render(Work(), [note, Highlight("b.epub", "second file"), Highlight("a.pdf", "first\nline two", "Mine.")]);

        Assert.True(text.IndexOf("## Highlights", StringComparison.Ordinal) < text.IndexOf("## Notes", StringComparison.Ordinal));
        Assert.True(text.IndexOf("### b.epub", StringComparison.Ordinal) < text.IndexOf("### a.pdf", StringComparison.Ordinal));
        Assert.Contains("> first\n> line two\n\n*page 2, green*\n\nMine.\n", text);
        Assert.EndsWith("A note.\n", text);
        Assert.DoesNotContain('\r', text);
    }
}
