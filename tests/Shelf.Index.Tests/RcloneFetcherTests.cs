using Shelf.Index;
using Xunit;

namespace Shelf.Index.Tests;

public sealed class RcloneFetcherTests
{
    [Fact]
    public void Arguments_ArePassedSeparately_SoNothingIsParsedByAShell()
    {
        var arguments = RcloneFetcher.Arguments("gdrive:My Books; rm -rf ~", "/tmp/shelf import");

        Assert.Equal(
            ["copy", "gdrive:My Books; rm -rf ~", "/tmp/shelf import", "--include", "*.epub", "--include", "*.pdf"],
            arguments);
    }
}
