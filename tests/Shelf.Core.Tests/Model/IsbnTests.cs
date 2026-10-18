using Shelf.Core.Model;
using Xunit;

namespace Shelf.Core.Tests.Model;

public sealed class IsbnTests
{
    [Theory]
    [InlineData("9780306406157", true)]
    [InlineData("9780306406158", false)]
    [InlineData("978030640615", false)]
    [InlineData("97803064061X7", false)]
    public void IsValid13_checks_length_digits_and_check_digit(string digits, bool expected) =>
        Assert.Equal(expected, Isbn.IsValid13(digits));

    [Theory]
    [InlineData("0306406152", true)]
    [InlineData("080442957X", true)]
    [InlineData("080442957x", true)]
    [InlineData("0306406153", false)]
    [InlineData("03064061", false)]
    public void IsValid10_checks_the_check_digit_including_X(string digits, bool expected) =>
        Assert.Equal(expected, Isbn.IsValid10(digits));

    [Fact]
    public void ToIsbn13_adds_the_prefix_and_a_new_check_digit()
    {
        Assert.Equal("9780306406157", Isbn.ToIsbn13("0306406152"));
        Assert.Equal("9780804429573", Isbn.ToIsbn13("080442957X"));
    }

    [Fact]
    public void Full_width_digits_and_dash_variants_are_normalized()
    {
        Assert.Equal("9780306406157", Isbn.Find("ISBN ９７８－０－３０６－４０６１５－７"));
        Assert.Equal("9780306406157", Isbn.Find("ISBN 978–0–306–40615–7"));
    }

    [Fact]
    public void FindAll_lists_each_ISBN_once_in_order()
    {
        var found = Isbn.FindAll("ISBN 978-0-00-000002-6, again 9780000000026 and ISBN 978-0-00-000001-9");

        Assert.Equal(["9780000000026", "9780000000019"], found);
    }

    [Fact]
    public void An_ISBN_10_needs_the_word_ISBN()
    {
        Assert.Null(Isbn.Find("0306406152"));
        Assert.Equal("9780306406157", Isbn.Find("ISBN-10: 0306406152"));
    }

    [Fact]
    public void A_number_inside_longer_digit_runs_is_not_an_ISBN()
    {
        Assert.Null(Isbn.Find("19780306406157"));
        Assert.Null(Isbn.Find("97803064061570"));
    }

    [Fact]
    public void A_UUID_is_never_taken_for_an_ISBN()
    {
        Assert.Null(Isbn.FromIdentifier("urn:uuid:9b6ffe1d-f194-4cc9-bf3f-fdd005afba9d"));
        Assert.Null(Isbn.FromIdentifier("9b6ffe1d-f194-4cc9-bf3f-fdd005afba9d"));
    }
}
