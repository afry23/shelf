using Xunit;

namespace Shelf.TestData.Tests;

public sealed class GuardTests
{
    [Fact]
    public void NoSourceProjectReferencesTheTestData()
    {
        var source = Path.Combine(RepositoryPaths.Root(), "src");
        var projects = Directory.EnumerateFiles(source, "*.csproj", SearchOption.AllDirectories).ToList();
        Assert.NotEmpty(projects);
        foreach (var project in projects)
        {
            Assert.DoesNotContain("Shelf.TestData", File.ReadAllText(project), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void NoSourceFileMentionsTheTestData()
    {
        var source = Path.Combine(RepositoryPaths.Root(), "src");
        foreach (var file in Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories)
                     .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
        {
            Assert.DoesNotContain("Shelf.TestData", File.ReadAllText(file), StringComparison.Ordinal);
        }
    }
}
