using Shelf.Core.Model;
using Xunit;

namespace Shelf.Core.Tests.Model;

public sealed class SidecarFormatTests
{
    private static WorkSidecar Full() => new()
    {
        Id = Ulid.Parse("01ARZ3NDEK04HMASW9NF6YY093"),
        Title = "Der Mann ohne Eigenschaften",
        Subtitle = "Roman",
        Authors = ["Robert Musil", "Jörg Beispiel"],
        Language = "de",
        Year = 1930,
        Isbn10 = "320000001X",
        Isbn13 = "9783499134623",
        Publisher = "Rowohlt",
        Series = "Werke",
        SeriesIndex = 1.5,
        Edition = "2nd",
        CiteKey = "musil1930mann",
        Description = "Line one\nLine \"two\"",
        Tags = ["literatur/klassiker", "österreich"],
        Rating = 4,
        ReadStatus = ReadStatus.Reading,
        Added = new DateTimeOffset(2026, 10, 17, 9, 30, 15, TimeSpan.FromHours(2)),
        Cover = "cover.jpg",
        Files =
        [
            new SidecarFile("a.epub", new FileProgress("epubcfi(/6/2!)", new DateTimeOffset(2026, 10, 12, 19, 4, 0, TimeSpan.Zero))),
            new SidecarFile("a.pdf"),
        ],
        NotDuplicates = new NotDuplicates([Ulid.Parse("01J8XQ20000000000000000000")], [new FilePair("a.epub", "b.epub")]),
        Related = [Ulid.Parse("01J8XQ30000000000000000000")],
    };

    [Fact]
    public void Every_field_survives_serialize_and_parse()
    {
        var text = SidecarFormat.Serialize(Full());

        var parsed = SidecarFormat.Parse(text, "book.json");

        Assert.Equal(text, SidecarFormat.Serialize(parsed));
        Assert.Equal("Line one\nLine \"two\"", parsed.Description);
        Assert.Equal(1.5, parsed.SeriesIndex);
        Assert.Equal(ReadStatus.Reading, parsed.ReadStatus);
        Assert.Equal(new DateTimeOffset(2026, 10, 17, 7, 30, 15, TimeSpan.Zero), parsed.Added);
        Assert.Equal(new FilePair("a.epub", "b.epub"), Assert.Single(parsed.NotDuplicates.Files));
        Assert.Equal(Ulid.Parse("01J8XQ30000000000000000000"), Assert.Single(parsed.Related));
    }

    [Fact]
    public void Related_is_left_out_when_empty_and_is_not_an_unknown_field()
    {
        var text = SidecarFormat.Serialize(Full() with { Related = [] });

        var parsed = SidecarFormat.Parse(text, "book.json");

        Assert.DoesNotContain("related", text, StringComparison.Ordinal);
        Assert.Empty(parsed.Related);
        Assert.DoesNotContain("related", parsed.UnknownFields.Keys);
    }

    [Fact]
    public void A_work_cannot_be_related_to_itself()
    {
        var sidecar = Full();

        var error = Assert.Throws<SidecarException>(() => SidecarFormat.Validate(sidecar with { Related = [sidecar.Id] }, "x"));

        Assert.Equal("x: related[1]: must not be the work's own ID", error.Message);
    }

    [Fact]
    public void A_related_entry_that_is_not_an_ID_names_the_key()
    {
        var text = SidecarFormat.Serialize(Full()).Replace("01J8XQ30000000000000000000", "nope", StringComparison.Ordinal);

        var error = Assert.Throws<SidecarException>(() => SidecarFormat.Parse(text, "book.json"));

        Assert.Equal("book.json: related[1]: not a valid ULID", error.Message);
    }

    [Fact]
    public void Times_are_written_in_UTC_with_a_Z_suffix() =>
        Assert.Contains("\"added\": \"2026-10-17T07:30:15Z\"", SidecarFormat.Serialize(Full()), StringComparison.Ordinal);

    [Fact]
    public void Json_null_counts_as_absent()
    {
        var text = SidecarFormat.Serialize(Full()).Replace("\"cover\": \"cover.jpg\"", "\"cover\": null", StringComparison.Ordinal);

        Assert.Null(SidecarFormat.Parse(text, "book.json").Cover);
    }

    [Fact]
    public void A_number_where_a_string_is_expected_names_the_key()
    {
        var text = SidecarFormat.Serialize(Full()).Replace("\"title\": \"Der Mann ohne Eigenschaften\"", "\"title\": 5", StringComparison.Ordinal);

        var error = Assert.Throws<SidecarException>(() => SidecarFormat.Parse(text, "book.json"));

        Assert.Equal("book.json: title: expected string, found number", error.Message);
    }

    [Fact]
    public void Validate_rejects_a_sidecar_that_would_be_unreadable_later()
    {
        var error = Assert.Throws<SidecarException>(() => SidecarFormat.Validate(Full() with { Rating = 0 }, "x"));

        Assert.Equal("x: rating: must be between 1 and 5", error.Message);
    }

    [Fact]
    public void Write_refuses_an_unreadable_existing_file_instead_of_overwriting_it()
    {
        var directory = Directory.CreateTempSubdirectory("shelf-sidecar-").FullName;
        try
        {
            var path = Path.Combine(directory, SidecarStore.FileName);
            File.WriteAllText(path, "{ broken");

            Assert.Throws<SidecarException>(() => SidecarStore.Write(path, Full()));
            Assert.Equal("{ broken", File.ReadAllText(path));
            Assert.Single(Directory.GetFileSystemEntries(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Read_reports_a_missing_file_with_its_path()
    {
        var path = Path.Combine(Path.GetTempPath(), "shelf-missing-" + Guid.NewGuid().ToString("N"), "book.json");

        var error = Assert.Throws<SidecarException>(() => SidecarStore.Read(path));

        Assert.StartsWith(path + ": cannot read file", error.Message, StringComparison.Ordinal);
    }
}
