using Xunit;

namespace Shelf.Library.Tests;

public sealed class FileIdsTests
{
    [Theory]
    [InlineData("01J9Z3K0000000000000000001", "kp.epub")]
    [InlineData("01J9Z3K0000000000000000001", "ddd (1).epub")]
    [InlineData("01J9Z3K0000000000000000001", "Größe & Maß.pdf")]
    [InlineData("01J9Z3K0000000000000000001", "a~b.epub")]
    [InlineData("01J9Z3K0000000000000000001", "~leading.epub")]
    public void An_ID_is_split_back_into_the_work_and_the_name(string work, string name)
    {
        var id = FileIds.Make(work, name);

        Assert.True(FileIds.TryParse(id, out var parsedWork, out var parsedName));
        Assert.Equal(work, parsedWork);
        Assert.Equal(name, parsedName);
    }

    [Fact]
    public void A_tilde_in_the_name_stays_in_the_name_because_the_first_tilde_splits()
    {
        Assert.True(FileIds.TryParse("01J9Z3K0000000000000000001~a~b.epub", out var work, out var name));
        Assert.Equal("01J9Z3K0000000000000000001", work);
        Assert.Equal("a~b.epub", name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("kp.epub")]
    [InlineData("01J9Z3K0000000000000000001")]
    [InlineData("01J9Z3K0000000000000000001~")]
    [InlineData("~kp.epub")]
    [InlineData("~")]
    public void Text_without_a_work_and_a_name_is_no_ID(string? id) => Assert.False(FileIds.TryParse(id, out _, out _));
}
