namespace Shelf.Index.Tests;

internal static class TestFiles
{
    public static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "shelf.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("shelf.slnx not found above the test binary.");
    }

    public static string Fixture(string relative) => Path.Combine(RepositoryRoot(), "tests", "fixtures", relative);
}
