namespace Shelf.Index.Tests;

internal sealed class TempDirectory : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("shelf-index-").FullName;

    public string Combine(params string[] parts) => System.IO.Path.Combine([Path, .. parts]);

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
