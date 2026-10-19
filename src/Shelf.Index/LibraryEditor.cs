using Shelf.Core.Grouping;
using Shelf.Core.Model;
using Shelf.Core.Review;
using Shelf.Extraction;
using Shelf.Organize;

namespace Shelf.Index;

/// <summary>What <see cref="LibraryEditor.Merge"/> did.</summary>
/// <param name="LeftoverFolder">The folder of the merged work, if it still holds files that shelf does not know.</param>
public sealed record MergeResult(Ulid Target, Ulid Source, int MovedFiles, string? LeftoverFolder);

/// <summary>What <see cref="LibraryEditor.Split"/> did.</summary>
public sealed record SplitResult(Ulid Source, Ulid NewWork, string File, string Folder);

/// <summary>
/// Manual corrections to a library (F-GRP-4 and editing). Each change is made on disk first (files, then sidecars,
/// which are the source of truth); the index is then brought in line by a scan, as for any other change of the folders.
/// </summary>
/// <remarks>
/// The order of steps leaves a state that a scan understands if the process stops in between: files are moved
/// before the sidecars name them, and a sidecar is removed last.
/// </remarks>
public static class LibraryEditor
{
    /// <summary>The sidecar of a work, read from disk.</summary>
    public static WorkSidecar Read(string root, LibraryWork work) => SidecarStore.Read(SidecarPath(root, work));

    /// <summary>The full path of a file of a work.</summary>
    public static string FilePath(string root, LibraryWork work, string fileName) => Path.Combine(root, work.Directory, fileName);

    /// <summary>Replaces the sidecar of a work by the changed one.</summary>
    /// <exception cref="SidecarException">The sidecar is invalid before or after the change.</exception>
    public static void Update(string root, LibraryWork work, Func<WorkSidecar, WorkSidecar> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        var path = SidecarPath(root, work);
        SidecarStore.Write(path, change(SidecarStore.Read(path)));
        Reconcile(root);
    }

    /// <summary>
    /// Stores the reading position of one file in the sidecar and in the index (F-RDR-4). A reader saves often, so this does
    /// not scan the library: the sidecar is written and the one row of the index is changed.
    /// </summary>
    /// <exception cref="SidecarException">The sidecar is invalid, or the file is not one of the work.</exception>
    /// <exception cref="IndexException"/>
    public static void SetProgress(string root, LibraryWork work, string fileName, FileProgress? progress)
    {
        var path = SidecarPath(root, work);
        var sidecar = SidecarStore.Read(path);
        if (!sidecar.Files.Any(f => string.Equals(f.Name, fileName, StringComparison.Ordinal)))
        {
            throw new SidecarException($"{path}: no file '{fileName}'", path);
        }

        SidecarStore.Write(path, sidecar with
        {
            Files = [.. sidecar.Files.Select(f => string.Equals(f.Name, fileName, StringComparison.Ordinal) ? f with { Progress = progress } : f)],
        });

        using var connection = IndexDatabase.OpenWrite(IndexDatabase.PathFor(root));
        using var writer = new IndexWriter(connection);
        using var transaction = writer.BeginTransaction();
        writer.SetProgressOfPath(IndexWriter.JoinRelative(work.Directory, fileName), progress);
        transaction.Commit();
    }

    /// <summary>Moves the files of <paramref name="source"/> into <paramref name="target"/> and removes the source work.</summary>
    /// <exception cref="LibraryEditException"/>
    public static MergeResult Merge(string root, LibraryWork target, LibraryWork source)
    {
        if (target.Id == source.Id)
        {
            throw new LibraryEditException("cannot merge a work into itself");
        }

        var targetPath = SidecarPath(root, target);
        var sourcePath = SidecarPath(root, source);
        var t = SidecarStore.Read(targetPath);
        var s = SidecarStore.Read(sourcePath);
        var targetDirectory = Path.Combine(root, target.Directory);
        var sourceDirectory = Path.Combine(root, source.Directory);

        var files = new List<SidecarFile>(t.Files);
        var renames = new Dictionary<string, string>(StringComparer.Ordinal);
        var movedPaths = new Dictionary<string, string>(StringComparer.Ordinal);
        var journal = FileJournal.Begin(root, "merge", undoable: false);
        foreach (var file in s.Files)
        {
            var from = Path.Combine(sourceDirectory, file.Name);
            if (!File.Exists(from))
            {
                continue; // named but gone: there is nothing to carry over
            }

            var name = FolderNames.UniqueFileName(targetDirectory, file.Name);
            Move(journal, from, Path.Combine(targetDirectory, name));
            renames[file.Name] = name;
            movedPaths[IndexWriter.JoinRelative(source.Directory, file.Name)] = IndexWriter.JoinRelative(target.Directory, name);
            files.Add(new SidecarFile(name, file.Progress));
        }

        // The cover of the work that goes away: the target keeps its own; a target without one takes it over. Left in the
        // folder, it would keep the folder from being removed.
        string? cover = null;
        if (s.Cover is { } sourceCover && File.Exists(Path.Combine(sourceDirectory, sourceCover)))
        {
            var from = Path.Combine(sourceDirectory, sourceCover);
            if (t.Cover is { } targetCover && File.Exists(Path.Combine(targetDirectory, targetCover)))
            {
                journal.Trash(from);
            }
            else
            {
                cover = FolderNames.UniqueFileName(targetDirectory, sourceCover);
                Move(journal, from, Path.Combine(targetDirectory, cover));
            }
        }

        journal.Finish();

        string Renamed(string name) => renames.GetValueOrDefault(name, name);
        var pairs = t.NotDuplicates.Files
            .Concat(s.NotDuplicates.Files.Select(p => new FilePair(Renamed(p.A), Renamed(p.B))))
            .Distinct()
            .ToList();
        var works = t.NotDuplicates.Works.Concat(s.NotDuplicates.Works)
            .Where(id => id != target.Id && id != source.Id)
            .Distinct()
            .ToList();
        var added = new[] { t.Added, s.Added }.Where(a => a.HasValue).Select(a => a!.Value).DefaultIfEmpty().Min();
        var merged = t with
        {
            Subtitle = t.Subtitle ?? s.Subtitle,
            Language = t.Language ?? s.Language,
            Year = t.Year ?? s.Year,
            Isbn10 = t.Isbn10 ?? s.Isbn10,
            Isbn13 = t.Isbn13 ?? s.Isbn13,
            Publisher = t.Publisher ?? s.Publisher,
            Series = t.Series ?? s.Series,
            SeriesIndex = t.Series is null ? s.SeriesIndex : t.SeriesIndex,
            Edition = t.Edition ?? s.Edition,
            Description = t.Description ?? s.Description,
            Rating = t.Rating ?? s.Rating,
            Tags = [.. t.Tags.Concat(s.Tags).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)],
            ReadStatus = (ReadStatus)Math.Max((int)t.ReadStatus, (int)s.ReadStatus),
            Added = added == default ? null : added,
            Cover = cover ?? t.Cover,
            Files = files,
            NotDuplicates = new NotDuplicates(works, pairs),
            Related = [.. t.Related.Concat(s.Related).Where(id => id != target.Id && id != source.Id).Distinct()],
        };
        SidecarStore.Write(targetPath, merged);

        File.Delete(sourcePath);
        var leftover = RemoveFolderIfEmpty(root, sourceDirectory) ? null : source.Directory;

        var queue = LoadQueue(root);
        SaveQueue(root, new ReviewQueue(queue.Entries.Select(e => MergedEntry(e, source.Id, target.Id, movedPaths)).OfType<ReviewEntry>()));
        Reconcile(root);
        return new MergeResult(target.Id, source.Id, renames.Count, leftover);
    }

    /// <summary>Moves one file of a work into a work of its own, described by the file's own metadata.</summary>
    /// <param name="file">A file name of the work, or the path of the file.</param>
    /// <exception cref="LibraryEditException"/>
    public static SplitResult Split(string root, LibraryWork work, string file, string workingDirectory)
    {
        var path = SidecarPath(root, work);
        var sidecar = SidecarStore.Read(path);
        var entry = FindFile(root, work, sidecar, file, workingDirectory)
            ?? throw new LibraryEditException($"{work.Id} has no file '{file}'");
        if (sidecar.Files.Count < 2)
        {
            throw new LibraryEditException($"{work.Id} has only one file; there is nothing to split off");
        }

        var directory = Path.Combine(root, work.Directory);
        var from = Path.Combine(directory, entry.Name);
        if (!File.Exists(from))
        {
            throw new LibraryEditException($"{IndexWriter.JoinRelative(work.Directory, entry.Name)} does not exist");
        }

        BookMetadata metadata;
        try
        {
            metadata = BookExtractor.ReadMetadata(from);
        }
        catch (ExtractionException e)
        {
            throw new LibraryEditException($"{IndexWriter.JoinRelative(work.Directory, entry.Name)}: {e.Message}");
        }

        var authors = metadata.Authors.Count > 0 ? metadata.Authors : [AuthorNames.UnknownAuthor];
        var title = string.IsNullOrWhiteSpace(metadata.Title) ? Path.GetFileNameWithoutExtension(entry.Name) : metadata.Title;
        var id = Ulid.New();
        var folder = FolderNames.FreeFolder(FolderNames.WorkFolder(root, AuthorNames.Parse(authors[0]).SortKey, title, metadata.Year));
        var relativeFolder = LibraryReindexer.RelativePath(root, folder);
        var name = FolderNames.UniqueFileName(folder, entry.Name);
        var journal = FileJournal.Begin(root, "split", undoable: false);
        Move(journal, from, Path.Combine(folder, name));
        journal.Finish();

        var created = new WorkSidecar
        {
            Id = id,
            Title = title,
            Authors = authors,
            Language = metadata.Language,
            Year = metadata.Year,
            Isbn13 = metadata.Isbn13,
            Publisher = metadata.Publisher,
            Series = metadata.Series,
            SeriesIndex = metadata.SeriesIndex,
            Description = metadata.Description,
            Added = new DateTimeOffset(DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond, TimeSpan.Zero),
            Files = [new SidecarFile(name, entry.Progress)],
            NotDuplicates = new NotDuplicates([work.Id], []),
        };
        SidecarStore.Write(Path.Combine(folder, SidecarStore.FileName), created);

        SidecarStore.Write(path, sidecar with
        {
            Files = [.. sidecar.Files.Where(f => f.Name != entry.Name)],
            NotDuplicates = new NotDuplicates([.. sidecar.NotDuplicates.Works.Append(id).Distinct()], [.. sidecar.NotDuplicates.Files.Where(p => p.A != entry.Name && p.B != entry.Name)]),
        });

        var oldPath = IndexWriter.JoinRelative(work.Directory, entry.Name);
        var newPath = IndexWriter.JoinRelative(relativeFolder, name);
        var queue = LoadQueue(root);
        SaveQueue(root, new ReviewQueue(queue.Entries.Select(e => e.File == oldPath ? e with { File = newPath, Work = e.Work == work.Id ? id : e.Work } : e)));
        Reconcile(root);
        return new SplitResult(work.Id, id, name, relativeFolder);
    }

    /// <summary>Records in both sidecars that the two works are not the same book.</summary>
    public static void MarkNotDuplicates(string root, LibraryWork a, LibraryWork b)
    {
        if (a.Id == b.Id)
        {
            return;
        }

        foreach (var (work, other) in new[] { (a, b), (b, a) })
        {
            var path = SidecarPath(root, work);
            var sidecar = SidecarStore.Read(path);
            if (!sidecar.NotDuplicates.Works.Contains(other.Id))
            {
                SidecarStore.Write(path, sidecar with { NotDuplicates = sidecar.NotDuplicates with { Works = [.. sidecar.NotDuplicates.Works, other.Id] } });
            }
        }

        Reconcile(root);
    }

    /// <summary>Records in both sidecars that the two works are editions of one book (F-GRP-5).</summary>
    /// <exception cref="LibraryEditException">The works are the same.</exception>
    public static void MarkRelated(string root, LibraryWork a, LibraryWork b) => ChangeRelated(root, a, b, related: true);

    /// <summary>Takes the mark away from both sidecars; nothing happens if the works were not related.</summary>
    /// <exception cref="LibraryEditException">The works are the same.</exception>
    public static void UnmarkRelated(string root, LibraryWork a, LibraryWork b) => ChangeRelated(root, a, b, related: false);

    private static void ChangeRelated(string root, LibraryWork a, LibraryWork b, bool related)
    {
        if (a.Id == b.Id)
        {
            throw new LibraryEditException("a work cannot be related to itself");
        }

        foreach (var (work, other) in new[] { (a, b), (b, a) })
        {
            var path = SidecarPath(root, work);
            var sidecar = SidecarStore.Read(path);
            if (sidecar.Related.Contains(other.Id) == related)
            {
                continue;
            }

            SidecarStore.Write(path, sidecar with { Related = related ? [.. sidecar.Related, other.Id] : [.. sidecar.Related.Where(id => id != other.Id)] });
        }

        Reconcile(root);
    }

    /// <summary>Removes entries from the review queue file and its copy in the index.</summary>
    public static void RemoveReviewEntry(string root, Ulid id)
    {
        var queue = LoadQueue(root);
        queue.Remove(id);
        SaveQueue(root, queue);
        RefreshMirror(root, queue);
    }

    // ---- helpers ----

    /// <summary>Moves a file through the journal; a target that is taken is an error, never overwritten (F-ORG-7).</summary>
    internal static void Move(FileJournal journal, string from, string to)
    {
        switch (journal.Move(from, to))
        {
            case MoveStatus.Collision:
                throw new LibraryEditException($"{Path.GetFileName(to)} already exists");
            case MoveStatus.Missing:
                throw new LibraryEditException($"{Path.GetFileName(from)} does not exist");
        }
    }

    private static string SidecarPath(string root, LibraryWork work) => Path.Combine(root, work.Directory, SidecarStore.FileName);

    private static SidecarFile? FindFile(string root, LibraryWork work, WorkSidecar sidecar, string file, string workingDirectory)
    {
        var byName = sidecar.Files.FirstOrDefault(f => f.Name == file);
        if (byName is not null || (!file.Contains('/', StringComparison.Ordinal) && !file.Contains('\\', StringComparison.Ordinal)))
        {
            return byName;
        }

        foreach (var baseDirectory in new[] { workingDirectory, root })
        {
            var full = Path.GetFullPath(file, baseDirectory);
            if (string.Equals(Path.GetDirectoryName(full), Path.Combine(root, work.Directory), StringComparison.Ordinal))
            {
                return sidecar.Files.FirstOrDefault(f => f.Name == Path.GetFileName(full));
            }
        }

        return null;
    }

    /// <summary>Deletes the folder, and empty parents up to the root, if nothing is left in it.</summary>
    internal static bool RemoveFolderIfEmpty(string root, string directory)
    {
        if (!Directory.Exists(directory) || Directory.EnumerateFileSystemEntries(directory).Any())
        {
            return !Directory.Exists(directory);
        }

        Directory.Delete(directory);
        var parent = Path.GetDirectoryName(directory);
        while (parent is not null && !string.Equals(Path.TrimEndingDirectorySeparator(parent), root, StringComparison.Ordinal)
            && Directory.Exists(parent) && !Directory.EnumerateFileSystemEntries(parent).Any())
        {
            Directory.Delete(parent);
            parent = Path.GetDirectoryName(parent);
        }

        return true;
    }

    /// <summary>The entry after the source work became the target: ids and file paths follow; an entry about the pair itself is settled.</summary>
    private static ReviewEntry? MergedEntry(ReviewEntry entry, Ulid source, Ulid target, Dictionary<string, string> movedPaths)
    {
        var work = entry.Work == source ? target : entry.Work;
        var file = entry.File is { } f && movedPaths.TryGetValue(f, out var moved) ? moved : entry.File;
        if (entry.Candidates is null)
        {
            return entry with { Work = work, File = file };
        }

        var candidates = entry.Candidates
            .Select(c => c.Work == source ? c with { Work = target } : c)
            .GroupBy(c => c.Work)
            .Select(g => g.OrderByDescending(c => c.Confidence).First())
            .Where(c => c.Work != work)
            .ToList();
        return entry.Kind == ReviewKind.Grouping && candidates.Count == 0
            ? null
            : entry with { Work = work, File = file, Candidates = candidates };
    }

    private static ReviewQueue LoadQueue(string root) => ReviewQueue.Load(ReviewQueue.PathFor(root));

    private static void SaveQueue(string root, ReviewQueue queue) => queue.Save(ReviewQueue.PathFor(root));

    /// <summary>Brings the index in line with the folders and the review queue file.</summary>
    internal static void Reconcile(string root)
    {
        LibraryScanner.Scan(root);
        RefreshMirror(root, LoadQueue(root));
    }

    private static void RefreshMirror(string root, ReviewQueue queue)
    {
        using var connection = IndexDatabase.OpenWrite(IndexDatabase.PathFor(root));
        using (var writer = new IndexWriter(connection))
        {
            using var transaction = writer.BeginTransaction();
            writer.ReplaceReviewMirror(queue);
            transaction.Commit();
        }

        IndexDatabase.Checkpoint(connection);
    }
}
