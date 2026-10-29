using System.Text.Json;
using Shelf.Core.Model;
using Xunit;

namespace Shelf.Core.Tests.Model;

public sealed class AnnotationsFormatTests
{
    private static readonly DateTimeOffset Time = new(2026, 10, 25, 10, 0, 0, TimeSpan.Zero);

    private static Annotation Highlight(string id = "01J9Z3K00000000000000000A1") => new(
        Ulid.TryParse(id, out var ulid) ? ulid : throw new InvalidOperationException(),
        "kp.epub",
        AnnotationKind.Highlight,
        "epubcfi(/6/2!/4/2/1:0,/1:7)",
        new AnnotationQuote("The sidecar", "before ", " after"),
        "green",
        "# Idea\n\n* **bold** — ünïcode",
        Time,
        Time);

    [Fact]
    public void Serialize_then_Parse_gives_the_same_annotations()
    {
        var note = new Annotation(Ulid.New(), "kp.epub", AnnotationKind.Note, null, null, null, "A note", Time, Time);
        var sidecar = new AnnotationsSidecar { Annotations = [Highlight(), note] };

        var read = AnnotationsFormat.Parse(AnnotationsFormat.Serialize(sidecar), "x");

        Assert.Equal(sidecar.Annotations, read.Annotations);
    }

    [Fact]
    public void The_rects_of_a_highlight_survive_a_round_trip()
    {
        var rects = new[] { new AnnotationRect(0.1, 0.2, 0.5, 0.02), new AnnotationRect(0.1, 0.22, 0.3, 0.02) };
        var sidecar = new AnnotationsSidecar { Annotations = [Highlight() with { Locator = "page=1", Rects = rects }] };

        var read = AnnotationsFormat.Parse(AnnotationsFormat.Serialize(sidecar), "x");

        Assert.Equal(rects, read.Annotations[0].Rects);
    }

    [Fact]
    public void A_bookmark_with_a_label_survives_a_round_trip()
    {
        var bookmark = new Annotation(Ulid.New(), "es.pdf", AnnotationKind.Bookmark, "page=2", null, null, "Read model", Time, Time);
        var sidecar = new AnnotationsSidecar { Annotations = [bookmark] };

        var text = AnnotationsFormat.Serialize(sidecar);
        var read = AnnotationsFormat.Parse(text, "x");

        Assert.Contains("\"kind\": \"bookmark\"", text, StringComparison.Ordinal);
        Assert.Equal(sidecar.Annotations, read.Annotations);
    }

    [Theory]
    [InlineData(null, null, null)]
    [InlineData("page=2", "yellow", null)]
    [InlineData("page=2", null, "x")]
    public void A_bookmark_needs_a_locator_and_has_no_color_or_quote(string? locator, string? color, string? quote)
    {
        var bookmark = new Annotation(Ulid.New(), "es.pdf", AnnotationKind.Bookmark, locator, quote is null ? null : new AnnotationQuote(quote), color, null, Time, Time);
        var sidecar = new AnnotationsSidecar { Annotations = [bookmark] };

        Assert.Throws<SidecarException>(() => AnnotationsFormat.Validate(sidecar, "x"));
    }

    [Theory]
    [InlineData(0.9, 0.2, 0.5, 0.1)]
    [InlineData(-0.1, 0.2, 0.5, 0.1)]
    [InlineData(0.1, 0.2, 1.5, 0.1)]
    public void Rects_outside_the_page_are_refused(double x, double y, double width, double height)
    {
        var sidecar = new AnnotationsSidecar { Annotations = [Highlight() with { Rects = [new AnnotationRect(x, y, width, height)] }] };

        var e = Assert.Throws<SidecarException>(() => AnnotationsFormat.Validate(sidecar, "annotations.json"));

        Assert.Contains("rects", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Serialize_keeps_non_ASCII_text_readable_and_ends_with_a_newline()
    {
        var text = AnnotationsFormat.Serialize(new AnnotationsSidecar { Annotations = [Highlight()] });

        Assert.Contains("ünïcode", text, StringComparison.Ordinal);
        Assert.EndsWith("}\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\r", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_top_level_fields_are_written_back()
    {
        var read = AnnotationsFormat.Parse("""{ "schema": 1, "annotations": [], "future": { "a": 1 } }""", "x");

        Assert.Contains("\"future\"", AnnotationsFormat.Serialize(read), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{ "schema": 2, "annotations": [] }""", "newer")]
    [InlineData("""{ "annotations": {} }""", "annotations: must be an array")]
    [InlineData("""{ "annotations": [{ "id": "nope" }] }""", "annotations[1].id")]
    [InlineData("""{ "annotations": [{ "id": "01J9Z3K00000000000000000A1", "file": "a.pdf", "kind": "mark", "createdAt": "2026-10-25T10:00:00Z", "updatedAt": "2026-10-25T10:00:00Z" }] }""", "kind")]
    [InlineData("""{ "annotations": [{ "id": "01J9Z3K00000000000000000A1", "file": "a.pdf", "kind": "note", "createdAt": "yesterday", "updatedAt": "2026-10-25T10:00:00Z" }] }""", "createdAt")]
    [InlineData("""{ "annotations": [{ "id": "01J9Z3K00000000000000000A1", "file": "a/b.pdf", "kind": "note", "note": "x", "createdAt": "2026-10-25T10:00:00Z", "updatedAt": "2026-10-25T10:00:00Z" }] }""", "file name")]
    [InlineData("""{ "annotations": [{ "id": "01J9Z3K00000000000000000A1", "file": "a.pdf", "kind": "highlight", "locator": "page=1", "createdAt": "2026-10-25T10:00:00Z", "updatedAt": "2026-10-25T10:00:00Z" }] }""", "quote")]
    public void Parse_refuses_invalid_files_and_names_the_key(string json, string expected)
    {
        var e = Assert.Throws<SidecarException>(() => AnnotationsFormat.Parse(json, "annotations.json"));

        Assert.Contains(expected, e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_refuses_a_duplicate_ID()
    {
        var text = AnnotationsFormat.Serialize(new AnnotationsSidecar { Annotations = [Highlight(), Highlight()] });
        var e = Assert.Throws<SidecarException>(() => AnnotationsFormat.Parse(text, "x"));

        Assert.Contains("duplicate", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Store_removes_the_file_when_the_last_annotation_goes()
    {
        var path = Path.Combine(Path.GetTempPath(), $"annotations-{Guid.NewGuid():N}.json");
        try
        {
            AnnotationsStore.Write(path, new AnnotationsSidecar { Annotations = [Highlight()] });
            Assert.True(File.Exists(path));
            Assert.Single(AnnotationsStore.Read(path).Annotations);

            AnnotationsStore.Write(path, new AnnotationsSidecar());

            Assert.False(File.Exists(path));
            Assert.Empty(AnnotationsStore.Read(path).Annotations);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
