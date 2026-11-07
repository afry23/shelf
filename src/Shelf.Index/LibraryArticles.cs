using System.Globalization;
using System.Text;
using Shelf.Clip;
using Shelf.Core.Grouping;
using Shelf.Core.Model;
using Shelf.Extraction;
using Shelf.Organize;

namespace Shelf.Index;

/// <summary>What <see cref="LibraryArticles"/> did.</summary>
/// <param name="Existing">The article was in the library already (same canonical address); nothing was created.</param>
public sealed record ClipResult(Ulid Work, string Title, string File, string Folder, int Images, bool Existing, IReadOnlyList<string> Warnings);

/// <summary>
/// Web articles as Markdown with their images (F-ART-2, F-ART-3, F-ART-6): a work folder holds <c>article.md</c>,
/// <c>images/</c> and <c>book.json</c>. Nothing here goes online.
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

    /// <summary>
    /// Takes the Markdown files a web clipper saved (F-ART-2): each source is a file, or a folder whose <c>.md</c> files are
    /// searched recursively. One article per file; images come from below the folder of the file (F-ART-5). Nothing goes online.
    /// </summary>
    /// <exception cref="ImportException">A source does not exist.</exception>
    public static ImportResult Import(string root, IReadOnlyList<string> sources, bool dryRun)
    {
        ArgumentNullException.ThrowIfNull(sources);
        var (files, ignored) = CollectMarkdown(sources);
        var items = new List<ImportItem>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            items.Add(ImportFile(root, file, dryRun, seen));
        }

        return new ImportResult(items, ignored);
    }

    /// <summary>
    /// One article from one Markdown file, made by hand (F-GRP-10): the title, authors and year of the front matter are replaced by
    /// the values that are given. An article whose address is in the library already is not made again (F-ART-6).
    /// </summary>
    /// <exception cref="LibraryEditException"/>
    internal static CreateResult CreateByHand(string root, string file, string shownAs, WorkValues values)
    {
        ClippedArticle article;
        try
        {
            article = MarkdownArticle.Read(File.ReadAllText(file), Path.GetFileName(file), Path.GetDirectoryName(file)!);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or DecoderFallbackException)
        {
            throw new LibraryEditException($"{shownAs}: {e.Message}");
        }

        if (article.SourceUrl is not null && Existing(root, article.SourceUrl) is { } known)
        {
            throw new LibraryEditException($"{shownAs}: the article \"{known.Title}\" is already in the library (same source)");
        }

        if (!string.IsNullOrWhiteSpace(values.Title))
        {
            article = article with { Title = values.Title };
        }

        if (values.Authors is { Count: > 0 } authors)
        {
            article = article with { Authors = [.. authors] };
        }

        var created = Create(root, article, values.Year);
        return new CreateResult(created.Work, created.File, created.Folder);
    }

    private static (List<string> Files, int Ignored) CollectMarkdown(IReadOnlyList<string> sources)
    {
        var files = new List<string>();
        var ignored = 0;
        foreach (var source in sources)
        {
            var full = Path.GetFullPath(source);
            if (File.Exists(full))
            {
                files.Add(full); // a file that is not Markdown is reported as an error of that file
            }
            else if (Directory.Exists(full))
            {
                foreach (var path in Directory.EnumerateFiles(full, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
                {
                    if (BookExtractor.IsMarkdown(path))
                    {
                        files.Add(path);
                    }
                    else
                    {
                        ignored++;
                    }
                }
            }
            else
            {
                throw new ImportException($"{source}: does not exist");
            }
        }

        return ([.. files.Distinct(StringComparer.Ordinal)], ignored);
    }

    private static ImportItem ImportFile(string root, string file, bool dryRun, HashSet<string> seen)
    {
        if (!BookExtractor.IsMarkdown(file))
        {
            return Failed(file, "not a Markdown file");
        }

        ClippedArticle article;
        try
        {
            article = MarkdownArticle.Read(File.ReadAllText(file), Path.GetFileName(file), Path.GetDirectoryName(file)!);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or DecoderFallbackException)
        {
            return Failed(file, e.Message);
        }

        var warnings = article.Warnings.Select(w => $"{Path.GetFileName(file)}: {w}").ToList();
        if (article.SourceUrl is not null)
        {
            if (Existing(root, article.SourceUrl) is { } known)
            {
                return new ImportItem(file, ImportAction.Duplicate, null, known.Work, known.Title, 0, $"same source as \"{known.Title}\"", 0, warnings);
            }

            if (!seen.Add(article.SourceUrl))
            {
                return new ImportItem(file, ImportAction.Duplicate, null, null, article.Title, 0, "same source as an earlier file of this import", 0, warnings);
            }
        }

        if (dryRun)
        {
            var planned = RelativeFolder(Sidecar(article, DateTimeOffset.UtcNow));
            return new ImportItem(file, ImportAction.NewWork, planned + "/" + ArticleFile, null, article.Title, 1, $"{article.Images.Count} images", 0, warnings);
        }

        try
        {
            var created = Create(root, article);
            return new ImportItem(file, ImportAction.NewWork, created.Folder + "/" + ArticleFile, created.Work, created.Title, 1, created.Images == 0 ? null : $"{created.Images} images", 0, warnings);
        }
        catch (Exception e) when (e is LibraryEditException or SidecarException or JournalException or IndexException or IOException)
        {
            return Failed(file, e.Message);
        }
    }

    private static ImportItem Failed(string file, string detail) =>
        new(file, ImportAction.Error, null, null, null, 0, $"{file}: {detail}", 0);

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

    private static WorkSidecar Sidecar(ClippedArticle article, DateTimeOffset now)
    {
        var authors = article.Authors.Count > 0 ? article.Authors : [article.Site ?? AuthorNames.UnknownAuthor];
        return new WorkSidecar
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
    }

    private static ClipResult Create(string root, ClippedArticle article, int? year = null)
    {
        var now = new DateTimeOffset(DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond, TimeSpan.Zero);
        var sidecar = Sidecar(article, now);
        if (year is not null)
        {
            sidecar = sidecar with { Year = year };
        }

        var folder = FolderNames.FreeFolder(Path.Combine(root, RelativeFolder(sidecar).Replace('/', Path.DirectorySeparatorChar)));
        var sidecarPath = Path.Combine(folder, SidecarStore.FileName);
        SidecarFormat.Validate(sidecar, sidecarPath);
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var journal = FileJournal.Begin(root, "import", undoable: true);
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
