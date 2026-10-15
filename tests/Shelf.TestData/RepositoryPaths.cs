namespace Shelf.TestData;

public static class RepositoryPaths
{
    /// <summary>Walks up from the application base directory to the directory that contains <c>shelf.slnx</c>.</summary>
    public static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "shelf.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("The repository root (a directory containing shelf.slnx) was not found above " + AppContext.BaseDirectory);
    }
}
