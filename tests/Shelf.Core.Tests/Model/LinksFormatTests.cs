using Shelf.Core.Model;
using Xunit;

namespace Shelf.Core.Tests.Model;

public sealed class LinksFormatTests
{
    private static readonly Ulid Other = Ulid.New();

    private static WorkSidecar Work(params WorkLink[] links) => new()
    {
        Id = Ulid.New(),
        Title = "T",
        Authors = ["A"],
        Files = [new SidecarFile("a.pdf")],
        Links = links,
    };

    [Fact]
    public void Links_survive_a_round_trip_and_an_empty_note_is_left_out()
    {
        var text = SidecarFormat.Serialize(Work(new WorkLink(Other, "sequel"), new WorkLink(Ulid.New())));

        var parsed = SidecarFormat.Parse(text, "book.json");

        Assert.Equal(2, parsed.Links.Count);
        Assert.Equal("sequel", parsed.Links[0].Note);
        Assert.Null(parsed.Links[1].Note);
    }

    [Fact]
    public void A_work_without_links_has_no_field()
    {
        Assert.DoesNotContain("links", SidecarFormat.Serialize(Work()), StringComparison.Ordinal);
    }

    [Fact]
    public void A_link_to_the_work_itself_is_refused()
    {
        var work = Work();
        work = work with { Links = [new WorkLink(work.Id)] };

        Assert.Throws<SidecarException>(() => SidecarFormat.Validate(work, "book.json"));
    }

    [Fact]
    public void The_same_work_twice_is_refused()
    {
        Assert.Throws<SidecarException>(() => SidecarFormat.Validate(Work(new WorkLink(Other), new WorkLink(Other, "x")), "book.json"));
    }

    [Theory]
    [InlineData("two\nlines")]
    [InlineData("two\rlines")]
    public void A_note_has_one_line(string note)
    {
        Assert.Throws<SidecarException>(() => SidecarFormat.Validate(Work(new WorkLink(Other, note)), "book.json"));
    }

    [Fact]
    public void A_note_has_at_most_200_characters()
    {
        SidecarFormat.Validate(Work(new WorkLink(Other, new string('x', 200))), "book.json");
        Assert.Throws<SidecarException>(() => SidecarFormat.Validate(Work(new WorkLink(Other, new string('x', 201))), "book.json"));
    }

    [Theory]
    [InlineData("""{ "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "T", "authors": ["A"], "files": [{ "name": "a.pdf" }], "links": [{ "work": "nope" }] }""")]
    [InlineData("""{ "schema": 1, "id": "01J9Z3K0000000000000000001", "title": "T", "authors": ["A"], "files": [{ "name": "a.pdf" }], "links": ["01J9Z3K0000000000000000002"] }""")]
    public void A_link_that_is_not_an_object_with_a_work_is_refused(string text)
    {
        Assert.Throws<SidecarException>(() => SidecarFormat.Parse(text, "book.json"));
    }
}
