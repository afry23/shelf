using Shelf.Core.Grouping;
using Xunit;

namespace Shelf.Core.Tests.Grouping;

public sealed class NamesAndTitlesTests
{
    [Theory]
    [InlineData("Dune (3rd Edition) ", "dune")]
    [InlineData("Dune [Second Edition]", "dune")]
    [InlineData("Dune, 2nd ed", "dune")]
    [InlineData("Dune - Revised Edition", "dune")]
    [InlineData("Dune Special Edition", "dune special edition")]
    [InlineData("Handbuch, erste Auflage", "handbuch")]
    [InlineData("Handbuch (3. Aufl.)", "handbuch")]
    public void Edition_notes_are_removed_in_their_known_forms(string title, string expected) =>
        Assert.Equal(expected, TitleNormalizer.Normalize(title));

    [Fact]
    public void Normalization_is_idempotent()
    {
        foreach (var title in new[] { "The Hobbit (2nd Edition)", "Größenordnungen!", "A", "!!!", "Der Prozess" })
        {
            var once = TitleNormalizer.Normalize(title);
            Assert.Equal(once, TitleNormalizer.Normalize(once));
        }
    }

    [Fact]
    public void Surname_keys_are_distinct()
    {
        Assert.Equal(["abbott", "weiss"], AuthorNames.SurnameKeys(["Edwin Abbott", "Abbott, Edwin", "Lukas Weiß"]));
    }

    [Fact]
    public void A_capitalized_particle_is_a_given_name()
    {
        var name = AuthorNames.Parse("Van Morrison");

        Assert.Equal("morrison", name.SurnameKey);
        Assert.Equal("Morrison, Van", name.SortKey);
    }

    [Fact]
    public void Apostrophes_and_hyphens_do_not_split_a_surname_key()
    {
        Assert.Equal("oreilly", AuthorNames.Parse("Tim O'Reilly").SurnameKey);
        Assert.Equal("smithjones", AuthorNames.Parse("Ann Smith-Jones").SurnameKey);
    }
}
