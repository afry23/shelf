using Shelf.Core.Model;
using Shelf.Index;
using Shelf.Organize;

namespace Shelf.Library;

public sealed partial class LibraryService
{
    private static readonly HashSet<string> ImageExtensions = ["jpg", "jpeg", "png", "gif", "webp"];

    /// <summary>
    /// An image in the <c>images/</c> folder next to a file of an article. Only a plain image name counts: the reader of an
    /// article may not reach any other file of the library through this.
    /// </summary>
    /// <exception cref="LibraryException">NotFound: no such file or image.</exception>
    public FileLocation ResolveAsset(string fileId, string name)
    {
        var file = ResolveFile(fileId);
        var extension = Path.GetExtension(name).TrimStart('.').ToLowerInvariant();
        if (name.Length == 0 || name != Path.GetFileName(name) || !ImageExtensions.Contains(extension))
        {
            throw new LibraryException($"'{name}' is not an image of an article", LibraryErrorKind.NotFound);
        }

        var full = Path.Combine(Path.GetDirectoryName(file.Path)!, LibraryArticles.ImagesFolder, name);
        var info = new FileInfo(full);
        return info.Exists
            ? new FileLocation(file.Work, file.Title, name, extension, full, info.Length, info.LastWriteTimeUtc)
            : throw new LibraryException($"no image '{name}' in '{file.Title}'", LibraryErrorKind.NotFound);
    }
}
