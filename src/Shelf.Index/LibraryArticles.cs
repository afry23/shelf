using System.Globalization;
using System.Net;
using System.Text;
using Shelf.Clip;
using Shelf.Core.Grouping;
using Shelf.Core.Model;
using Shelf.Organize;

namespace Shelf.Index;

/// <summary>What <see cref="LibraryArticles"/> did.</summary>
/// <param name="Existing">The article was in the library already (same canonical address); nothing was created.</param>
public sealed record ClipResult(Ulid Work, string Title, string File, string Folder, int Images, bool Existing, IReadOnlyList<string> Warnings);

/// <summary>
/// Web articles as Markdown with their images (F-ART-2, F-ART-3, F-ART-6): a work folder holds <c>article.md</c>,
/// <c>images/</c> and <c>book.json</c>. Only <see cref="ClipUrl"/> goes online, and only on explicit command.
/// </summary>
public static class LibraryArticles
{
    public const string ArticleFile = "article.md";

    public const string ImagesFolder = "images";

    /// <summary>
    /// The folder of an article below the library root, as clipping places it and organize keeps it:
    /// <c>Site/Title (Year)</c>, or <c>Author/Title (Year)</c> if the page names no site.
    /// </summary>
    public static string RelativeFolder(WorkSidecar sidecar)
    {
        ArgumentNullException.ThrowIfNull(sidecar);
        var shelf = sidecar.Site ?? AuthorNames.Parse(sidecar.Authors[0]).SortKey;
        return FolderNames.Sanitize(shelf) + "/" + FolderNames.Sanitize(sidecar.Year is { } y ? $"{sidecar.Title} ({y})" : sidecar.Title);
    }

    /// <summary>The images of an article folder (F-ART-5), by full path; none if there is no <c>images/</c>.</summary>
    internal static IReadOnlyList<string> ImageFiles(string workDirectory)
    {
        var images = Path.Combine(workDirectory, ImagesFolder);
        return Directory.Exists(images) ? [.. Directory.EnumerateFiles(images).Order(StringComparer.Ordinal)] : [];
    }

    /// <summary>Removes <c>images/</c> of an article folder once the images have left it.</summary>
    internal static void RemoveImagesFolderIfEmpty(string workDirectory)
    {
        var images = Path.Combine(workDirectory, ImagesFolder);
        if (Directory.Exists(images) && !Directory.EnumerateFileSystemEntries(images).Any())
        {
            Directory.Delete(images);
        }
    }

    /// <summary>Loads a public page and its images and creates the article.</summary>
    /// <exception cref="LibraryEditException">The address is no web address, the page cannot be loaded, or it holds no article.</exception>
    public static ClipResult ClipUrl(string root, string url, IHttpGetter http, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(http);
        var address = SourceUrl.Normalize(url) ?? throw new LibraryEditException($"'{url}' is not a web address (http:// or https://)");
        if (Existing(root, address) is { } known)
        {
            return known;
        }

        var page = Load(http, address, cancellation) ?? throw new LibraryEditException($"{address} could not be loaded");
        ClippedArticle article;
        try
        {
            article = ArticleClipper.Clip(PageText.Decode(page.Body), address, image =>
            {
                try
                {
                    return Load(http, image, cancellation)?.Body;
                }
                catch (LibraryEditException)
                {
                    return null; // reported by the clipper as an image that was not saved
                }
            });
        }
        catch (NoArticleException e)
        {
            throw new LibraryEditException($"{e.Message} at {address}");
        }

        article = article with { SourceUrl = article.SourceUrl ?? address };
        return Existing(root, article.SourceUrl) ?? Create(root, article);
    }

    /// <summary>Creates an article from a page that was saved in a browser (HTML or MHTML); nothing goes online.</summary>
    /// <param name="url">The address the page came from, if the file does not say.</param>
    /// <exception cref="LibraryEditException">The file cannot be read or holds no article.</exception>
    public static ClipResult ClipFile(string root, string path, string? url, string workingDirectory)
    {
        var full = Path.GetFullPath(path, workingDirectory);
        if (!File.Exists(full))
        {
            throw new LibraryEditException($"{path} does not exist");
        }

        if (url is not null && SourceUrl.Normalize(url) is null)
        {
            throw new LibraryEditException($"'{url}' is not a web address (http:// or https://)");
        }

        var bytes = File.ReadAllBytes(full);
        var directory = Path.GetDirectoryName(full)!;
        string html;
        Func<string, byte[]?> load;
        string? origin = url;
        var head = Encoding.Latin1.GetString(bytes, 0, Math.Min(bytes.Length, 256));
        if (Path.GetExtension(full) is ".mhtml" or ".mht" || Mhtml.Looks(head))
        {
            Mhtml saved;
            try
            {
                saved = Mhtml.Parse(bytes);
            }
            catch (FormatException e)
            {
                throw new LibraryEditException($"{path}: {e.Message}");
            }

            html = saved.Html;
            origin ??= saved.Location;
            load = address => saved.Find(address) ?? LocalFile(directory, address);
        }
        else
        {
            html = PageText.Decode(bytes);
            load = address => LocalFile(directory, address);
        }

        ClippedArticle article;
        try
        {
            article = ArticleClipper.Clip(html, null, load);
        }
        catch (NoArticleException)
        {
            throw new LibraryEditException($"no article found in {Path.GetFileName(path)}");
        }

        article = article with { SourceUrl = SourceUrl.Normalize(origin) ?? article.SourceUrl };
        return (article.SourceUrl is null ? null : Existing(root, article.SourceUrl)) ?? Create(root, article);
    }

    private static byte[]? LocalFile(string directory, string address)
    {
        if (address.Contains("://", StringComparison.Ordinal) || address.StartsWith("//", StringComparison.Ordinal))
        {
            return null; // a saved page does not go online
        }

        // Only below the folder of the page ("post_files/a.jpg"); a saved page may not reach into the rest of the disk.
        var file = Path.GetFullPath(Uri.UnescapeDataString(address.Split('?', '#')[0]), directory);
        return file.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.Ordinal) && File.Exists(file)
            ? File.ReadAllBytes(file)
            : null;
    }

    private static HttpAnswer? Load(IHttpGetter http, string address, CancellationToken cancellation)
    {
        HttpAnswer answer;
        try
        {
            answer = http.Get(address, cancellation);
        }
        catch (MetadataSourceException e)
        {
            throw new LibraryEditException($"{address}: {e.Message}");
        }

        return answer.Status is >= HttpStatusCode.OK and < HttpStatusCode.Ambiguous
            ? answer
            : throw new LibraryEditException($"{address}: the server answered {(int)answer.Status} ({answer.Status})");
    }

    private static ClipResult? Existing(string root, string address)
    {
        using var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(root));
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, title, dir_path FROM works WHERE source_url = $url LIMIT 1";
        command.Parameters.AddWithValue("$url", address);
        using var reader = command.ExecuteReader();
        return reader.Read() && Ulid.TryParse(reader.GetString(0), out var id)
            ? new ClipResult(id, reader.GetString(1), ArticleFile, reader.GetString(2), 0, Existing: true, [])
            : null;
    }

    private static ClipResult Create(string root, ClippedArticle article)
    {
        var now = new DateTimeOffset(DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond, TimeSpan.Zero);
        var authors = article.Authors.Count > 0 ? article.Authors : [article.Site ?? AuthorNames.UnknownAuthor];
        var sidecar = new WorkSidecar
        {
            Id = Ulid.New(),
            Title = article.Title,
            Authors = authors,
            Language = article.Language,
            Year = article.Published?.Year,
            Kind = WorkKind.Article,
            SourceUrl = article.SourceUrl,
            Site = article.Site,
            Published = article.Published,
            Captured = now,
            Added = now,
            Files = [new SidecarFile(ArticleFile)],
        };

        var folder = FolderNames.FreeFolder(Path.Combine(root, RelativeFolder(sidecar).Replace('/', Path.DirectorySeparatorChar)));
        var sidecarPath = Path.Combine(folder, SidecarStore.FileName);
        SidecarFormat.Validate(sidecar, sidecarPath);
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var journal = FileJournal.Begin(root, "clip", undoable: true);
        try
        {
            Write(journal, Path.Combine(folder, ArticleFile), utf8.GetBytes(article.Markdown));
            foreach (var image in article.Images)
            {
                Write(journal, Path.Combine(folder, ImagesFolder, image.FileName), image.Bytes);
            }

            Write(journal, sidecarPath, utf8.GetBytes(SidecarFormat.Serialize(sidecar)));
        }
        finally
        {
            journal.Finish();
        }

        LibraryEditor.Reconcile(root);
        return new ClipResult(sidecar.Id, article.Title, ArticleFile, LibraryReindexer.RelativePath(root, folder), article.Images.Count, Existing: false, article.Warnings);
    }

    private static void Write(FileJournal journal, string path, byte[] content)
    {
        if (journal.Create(path, content) != MoveStatus.Moved)
        {
            throw new LibraryEditException($"{Path.GetFileName(path)} already exists in {Path.GetDirectoryName(path)}");
        }
    }
}
