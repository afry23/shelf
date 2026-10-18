using System.Text.Json.Nodes;
using Shelf.Core.Model;
using Xunit;

namespace Shelf.Library.Tests;

public sealed class MetadataPatchTests
{
    private static readonly WorkSidecar Start = new()
    {
        Title = "Old",
        Subtitle = "Sub",
        Authors = ["A"],
        Language = "en",
        Year = 2000,
        Isbn13 = "9780306406157",
        Publisher = "P",
        Series = "S",
        SeriesIndex = 1,
        Edition = "1",
        Description = "D",
        Rating = 3,
        ReadStatus = ReadStatus.Unread,
    };

    private static WorkSidecar Apply(string field, string json) => MetadataPatch.Edit(field, JsonNode.Parse(json))(Start);

    private static string Refusal(string field, string json) =>
        Assert.Throws<LibraryException>(() => MetadataPatch.Edit(field, JsonNode.Parse(json))).Message;

    [Fact]
    public void Each_field_sets_its_value()
    {
        Assert.Equal("New", Apply("title", "\"  New \"").Title);
        Assert.Equal("Two", Apply("subtitle", "\"Two\"").Subtitle);
        Assert.Equal(["B", "C"], Apply("authors", "[\"B\", \" C \"]").Authors);
        Assert.Equal("de", Apply("language", "\"de\"").Language);
        Assert.Equal(2024, Apply("year", "2024").Year);
        Assert.Equal("Q", Apply("publisher", "\"Q\"").Publisher);
        Assert.Equal("T", Apply("series", "\"T\"").Series);
        Assert.Equal(2.5, Apply("seriesIndex", "2.5").SeriesIndex);
        Assert.Equal("2", Apply("edition", "\"2\"").Edition);
        Assert.Equal("E", Apply("description", "\"E\"").Description);
        Assert.Equal(5, Apply("rating", "5").Rating);
        Assert.Equal(ReadStatus.Reading, Apply("readStatus", "\"Reading\"").ReadStatus);
    }

    [Theory]
    [InlineData("subtitle")]
    [InlineData("language")]
    [InlineData("publisher")]
    [InlineData("series")]
    [InlineData("edition")]
    [InlineData("description")]
    public void Null_and_blank_text_remove_an_optional_field(string field)
    {
        var fromNull = Apply(field, "null");
        var fromBlank = Apply(field, "\"   \"");

        Assert.Equal(fromNull, fromBlank);
        Assert.NotEqual(Start, fromNull);
    }

    [Fact]
    public void Null_removes_the_numbers_and_the_ISBN()
    {
        Assert.Null(Apply("year", "null").Year);
        Assert.Null(Apply("rating", "null").Rating);
        Assert.Null(Apply("seriesIndex", "null").SeriesIndex);
        Assert.Null(Apply("isbn", "null").Isbn13);
    }

    [Fact]
    public void An_ISBN_10_gives_the_ISBN_13_too()
    {
        var edited = Apply("isbn", "\"0-306-40615-2\"");

        Assert.Equal("0306406152", edited.Isbn10);
        Assert.Equal("9780306406157", edited.Isbn13);
    }

    [Theory]
    [InlineData("title", "\"\"", "title: must not be empty")]
    [InlineData("title", "null", "title: must not be empty")]
    [InlineData("title", "3", "title: must be text")]
    [InlineData("subtitle", "[]", "subtitle: must be text")]
    [InlineData("authors", "null", "authors: give at least one author")]
    [InlineData("authors", "[]", "authors: give at least one author")]
    [InlineData("authors", "\"A\"", "authors: give at least one author")]
    [InlineData("authors", "[\"A\", 3]", "authors: must not be empty")]
    [InlineData("authors", "[\"  \"]", "authors: must not be empty")]
    [InlineData("year", "0", "year: must be between 1 and 9999")]
    [InlineData("year", "10000", "year: must be between 1 and 9999")]
    [InlineData("year", "2000.5", "year: must be a whole number")]
    [InlineData("year", "\"2000\"", "year: must be a whole number")]
    [InlineData("rating", "0", "rating: must be between 1 and 5")]
    [InlineData("rating", "6", "rating: must be between 1 and 5")]
    [InlineData("seriesIndex", "\"1\"", "seriesIndex: must be a number")]
    [InlineData("isbn", "\"123\"", "isbn: '123' is not a valid ISBN")]
    [InlineData("isbn", "\"9780306406158\"", "isbn: '9780306406158' is not a valid ISBN")]
    [InlineData("isbn", "5", "isbn: must be text")]
    [InlineData("readStatus", "\"done\"", "readStatus: must be unread, reading or read")]
    [InlineData("readStatus", "null", "readStatus: must be unread, reading or read")]
    [InlineData("tags", "[\"a\"]", "tags: unknown field")]
    [InlineData("colour", "\"red\"", "colour: unknown field")]
    public void A_value_that_is_not_valid_is_refused_with_the_field_and_the_reason(string field, string json, string message) =>
        Assert.Equal(message, Refusal(field, json));
}
