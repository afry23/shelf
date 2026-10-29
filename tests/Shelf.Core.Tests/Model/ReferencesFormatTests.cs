using Shelf.Core.Model;
using Xunit;

namespace Shelf.Core.Tests.Model;

public sealed class ReferencesFormatTests
{
    private static readonly DateTimeOffset Time = new(2026, 10, 29, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Serialize_then_Parse_gives_the_same_references()
    {
        var sidecar = new ReferencesSidecar
        {
            Source = "crossref",
            FetchedAt = Time,
            References =
            [
                new Reference("10.5555/attention", "Attention Is All You Need", 2017, "Vaswani", null),
                new Reference(null, null, null, null, "Peters et al., ünïcode, 2018."),
            ],
        };

        var read = ReferencesFormat.Parse(ReferencesFormat.Serialize(sidecar), "x");

        Assert.Equal(sidecar.References, read.References);
        Assert.Equal(("crossref", Time), (read.Source, read.FetchedAt));
    }

    [Fact]
    public void Unknown_fields_are_kept()
    {
        var read = ReferencesFormat.Parse("""{ "schema": 1, "references": [], "mine": { "a": 1 } }""", "x");

        Assert.Contains("\"mine\"", ReferencesFormat.Serialize(read), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{ "references": [ {} ] }""", "references[1]")]
    [InlineData("""{ "references": [ { "doi": " 10.1/x " } ] }""", "references[1].doi")]
    [InlineData("""{ "references": [ { "title": "T", "year": "2017" } ] }""", "references[1].year")]
    [InlineData("""{ "references": {} }""", "references")]
    [InlineData("""{ "schema": 2, "references": [] }""", "schema 2")]
    [InlineData("""{ "fetchedAt": "yesterday" }""", "fetchedAt")]
    public void A_list_that_cannot_be_used_is_refused_with_the_key(string json, string key)
    {
        var error = Assert.Throws<SidecarException>(() => ReferencesFormat.Parse(json, "references.json"));

        Assert.Contains(key, error.Message, StringComparison.Ordinal);
    }
}
