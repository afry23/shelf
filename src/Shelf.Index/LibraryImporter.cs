using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Shelf.Core.Configuration;
using Shelf.Core.Grouping;
using Shelf.Core.Model;
using Shelf.Core.Review;
using Shelf.Extraction;

namespace Shelf.Index;

public sealed record ImportOptions(bool Move = false, bool DryRun = false);

public enum ImportAction
{
    /// <summary>The file became a work of its own.</summary>
    NewWork,

    /// <summary>The file joined an existing work.</summary>
    AddedToWork,

    /// <summary>A file with the same content is already in the library; nothing was done.</summary>
    Duplicate,

    /// <summary>The file could not be read or placed; nothing was done.</summary>
    Error,
}

/// <summary>What happened to one source file.</summary>
/// <param name="Destination">Library-relative path of the copy (planned, in a dry run); null if nothing was placed.</param>
/// <param name="Confidence">How sure the grouping was; 0 for a work created without any candidate.</param>
/// <param name="Message">Reason or detail: the evidence of a merge, the candidates of a review, the error.</param>
/// <param name="ReviewEntries">How many entries this file added to the review queue.</param>
public sealed record ImportItem(
    string Source,
    ImportAction Action,
    string? Destination,
    Ulid? WorkId,
    string? Title,
    double Confidence,
    string? Message,
    int ReviewEntries);

public sealed record ImportResult(IReadOnlyList<ImportItem> Items, int Ignored);

/// <summary>A source of an import does not exist, or the review queue cannot be used.</summary>
public sealed class ImportException(string message, Exception? innerException = null) : Exception(message, innerException);

/// <summary>
/// Copies (or moves) files into the library (F-IMP-1): reads each file, decides which work it belongs to
/// (F-GRP-1), places it in the folder of that work or in a new folder Author / Title (Year), writes the sidecar,
/// updates the index and queues what is unsure (F-IMP-7). A file is never overwritten; a name that is taken gets
/// a number. With <see cref="ImportOptions.DryRun"/> everything is decided and nothing is written.
/// </summary>
/// <remarks>
/// Order of effects per file: copy (verified), sidecar, index, then (move only) delete the source. A crash in
/// between leaves a copy that the next scan finds; the source is never lost. The import is not journaled: its source
/// lies outside the library, and the journal of PRD 9.3 (<c>Shelf.Organize</c>) only covers paths inside the root.
/// </remarks>
public static class LibraryImporter
{
    /// <exception cref="ImportException">A source does not exist, or <c>review.json</c> is damaged.</exception>
    /// <exception cref="IndexException">There is no usable index.</exception>
    public static ImportResult Import(string root, IReadOnlyList<string> sources, ImportOptions options, GroupingSettings settings)
    {
        ArgumentException.ThrowIfNullOrEmpty(root);
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(settings);
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));

        var (files, ignored) = Collect(sources);
        using var connection = IndexDatabase.OpenWrite(IndexDatabase.PathFor(root));
        using var writer = new IndexWriter(connection);

        ReviewQueue queue;
        try
        {
            queue = ReviewQueue.Load(ReviewQueue.PathFor(root));
        }
        catch (ReviewQueueException e)
        {
            throw new ImportException(e.Message, e);
        }

        var run = new Run(root, options, connection, writer, queue, new WorkMatcher(settings, WorkCandidateStore.Load(connection)));
        var items = new List<ImportItem>();
        try
        {
            FileAnalyzer.ForEach(
                [.. files.Select(f => (f, (string?)null))],
                (index, analysis) => items.Add(run.Apply(files[index], analysis)));
        }
        finally
        {
            // Whatever was queued so far is kept, even if the run ends with an exception.
            run.SaveQueue();
        }

        if (!options.DryRun)
        {
            using (var transaction = writer.BeginTransaction())
            {
                writer.ReplaceReviewMirror(queue);
                transaction.Commit();
            }

            IndexDatabase.Checkpoint(connection);
        }

        return new ImportResult(items, ignored);
    }

    private static (List<string> Files, int Ignored) Collect(IReadOnlyList<string> sources)
    {
        var files = new List<string>();
        var ignored = 0;
        foreach (var source in sources)
        {
            var full = Path.GetFullPath(source);
            if (File.Exists(full))
            {
                files.Add(full);
            }
            else if (Directory.Exists(full))
            {
                foreach (var path in Directory.EnumerateFiles(full, "*", LibraryReindexer.Recursive).Order(StringComparer.Ordinal))
                {
                    if (BookExtractor.IsSupported(path))
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

        // The same file named twice (or reached through two folders) is imported once.
        return ([.. files.Distinct(StringComparer.Ordinal)], ignored);
    }

    /// <summary>The state of one import run; files are applied one at a time, in order.</summary>
    private sealed class Run(
        string root,
        ImportOptions options,
        SqliteConnection connection,
        IndexWriter writer,
        ReviewQueue queue,
        WorkMatcher matcher)
    {
        // The queue file is rewritten as a whole: every few entries, not after each one (1000 entries would be 500,000 lines written).
        private const int SaveEvery = 25;
        private int _unsaved;
        private readonly HashSet<string> _shasThisRun = new(StringComparer.Ordinal);
        private readonly Dictionary<Ulid, string> _titlesThisRun = [];
        private readonly Dictionary<Ulid, string> _directoriesThisRun = [];

        public ImportItem Apply(string source, FileAnalysis analysis)
        {
            if (analysis.Sha256 is null || analysis.Error is not null)
            {
                return Failed(source, analysis.Error!);
            }

            // A real run finds earlier files of this import in the index; a dry run writes nothing and must remember them.
            var existing = FindBySha(analysis.Sha256);
            if (existing is null && options.DryRun && !_shasThisRun.Add(analysis.Sha256))
            {
                existing = "an earlier file of this import";
            }

            if (existing is not null)
            {
                return new ImportItem(source, ImportAction.Duplicate, null, null, null, 0, $"same content as {existing}", 0);
            }

            var metadata = analysis.Book!.Metadata;
            var signature = WorkSignature.Create(metadata.Title, metadata.Authors, metadata.Year, metadata.Isbn13);
            var name = Path.GetFileName(source);
            var decision = matcher.Decide(new IncomingFile(name, signature, analysis.Fingerprint));

            try
            {
                return decision.Action == GroupingAction.MergeIntoExisting
                    ? AddToWork(source, analysis, decision)
                    : CreateWork(source, analysis, signature, decision);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or SidecarException or ReviewQueueException or SqliteException)
            {
                return new ImportItem(source, ImportAction.Error, null, null, null, 0, $"{source}: io: {e.Message}", 0);
            }
        }

        private ImportItem Failed(string source, ExtractionException error)
        {
            // The same broken file imported again must not fill the queue with the same entry.
            var known = queue.Entries.Any(e => e.Kind == ReviewKind.ExtractionError && e.File == source);
            if (!options.DryRun && !known)
            {
                Enqueue(new ReviewEntry(Ulid.New(), ReviewKind.ExtractionError, DateTimeOffset.UtcNow, $"{error.KindName}: {error.Message}", source));
            }

            return new ImportItem(source, ImportAction.Error, null, null, null, 0, error.Message, known ? 0 : 1);
        }

        private ImportItem AddToWork(string source, FileAnalysis analysis, GroupingDecision decision)
        {
            var workId = decision.WorkId!.Value;
            var match = decision.Matches[0];
            var dir = DirectoryOf(workId) ?? throw new IOException($"work {workId} is not in the index");
            var title = TitleOf(workId);
            var name = Path.GetFileName(source);
            var directory = Path.Combine(root, dir);
            var finalName = FolderNames.UniqueFileName(directory, name);
            var destination = IndexWriter.JoinRelative(dir, finalName);
            var message = $"{match.Confidence.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}: {string.Join("; ", match.Signals)}";

            if (!options.DryRun)
            {
                var placed = Place(source, analysis, Path.Combine(directory, finalName));
                var sidecarPath = Path.Combine(directory, SidecarStore.FileName);
                try
                {
                    var sidecar = SidecarStore.Read(sidecarPath);
                    SidecarStore.Write(sidecarPath, sidecar with { Files = [.. sidecar.Files, new SidecarFile(finalName)] });
                }
                catch
                {
                    File.Delete(placed.FullName);
                    throw;
                }

                using var transaction = writer.BeginTransaction();
                var fileId = writer.InsertFile(workId.ToString(), finalName, destination, placed.Length, placed.Mtime, progress: null);
                writer.SetContent(fileId, placed.Length, placed.Mtime, analysis.Sha256, analysis.Book!.PageCount, analysis.Fingerprint, analysis.TextStatus, analysis.Chunks);
                writer.SetWorkModified(workId.ToString(), File.GetLastWriteTimeUtc(sidecarPath));
                transaction.Commit();
                RemoveSourceIfMoving(source);
            }

            if (analysis.Fingerprint is { } fingerprint)
            {
                matcher.AddFingerprint(workId, fingerprint);
            }

            return new ImportItem(source, ImportAction.AddedToWork, destination, workId, title, match.Confidence, message, 0);
        }

        private ImportItem CreateWork(string source, FileAnalysis analysis, WorkSignature signature, GroupingDecision decision)
        {
            var metadata = analysis.Book!.Metadata;
            var name = Path.GetFileName(source);
            var authors = metadata.Authors.Count > 0 ? metadata.Authors : [AuthorNames.UnknownAuthor];
            var id = Ulid.New();
            var sidecar = new WorkSidecar
            {
                Id = id,
                Title = metadata.Title!,
                Authors = authors,
                Language = metadata.Language,
                Year = metadata.Year,
                Isbn13 = metadata.Isbn13,
                Publisher = metadata.Publisher,
                Series = metadata.Series,
                SeriesIndex = metadata.SeriesIndex,
                Description = metadata.Description,
                Added = new DateTimeOffset(DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond, TimeSpan.Zero),
                Files = [new SidecarFile(name)],
            };

            var folder = FolderNames.FreeFolder(FolderNames.WorkFolder(root, AuthorNames.Parse(authors[0]).SortKey, metadata.Title!, metadata.Year));
            var dir = LibraryReindexer.RelativePath(root, folder);
            var finalName = Directory.Exists(folder) ? FolderNames.UniqueFileName(folder, name) : name;
            if (finalName != name)
            {
                sidecar = sidecar with { Files = [new SidecarFile(finalName)] };
            }

            var destination = IndexWriter.JoinRelative(dir, finalName);
            var entries = new List<ReviewEntry>();
            var matches = decision.Matches;
            string? message = null;
            if (matches.Count > 0)
            {
                message = "may belong to " + string.Join(
                    "; ",
                    matches.Select(m => $"\"{TitleOf(m.WorkId)}\" ({m.Confidence.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}: {string.Join(", ", m.Signals)})"));
                entries.Add(new ReviewEntry(
                    Ulid.New(),
                    ReviewKind.Grouping,
                    DateTimeOffset.UtcNow,
                    message,
                    destination,
                    id,
                    [.. matches.Select(m => new ReviewCandidate(m.WorkId, m.Confidence, m.Signals))]));
            }

            if (WeakMetadataMessage(metadata) is { } weak)
            {
                entries.Add(new ReviewEntry(Ulid.New(), ReviewKind.WeakMetadata, DateTimeOffset.UtcNow, weak, destination, id));
            }

            if (!options.DryRun)
            {
                Directory.CreateDirectory(folder);
                var placed = Place(source, analysis, Path.Combine(folder, finalName));
                var sidecarPath = Path.Combine(folder, SidecarStore.FileName);
                try
                {
                    SidecarStore.Write(sidecarPath, sidecar);
                }
                catch
                {
                    File.Delete(placed.FullName);
                    throw;
                }

                using (var transaction = writer.BeginTransaction())
                {
                    writer.InsertWork(sidecar, dir, File.GetLastWriteTimeUtc(sidecarPath));
                    var fileId = writer.InsertFile(id.ToString(), finalName, destination, placed.Length, placed.Mtime, progress: null);
                    writer.SetContent(fileId, placed.Length, placed.Mtime, analysis.Sha256, analysis.Book!.PageCount, analysis.Fingerprint, analysis.TextStatus, analysis.Chunks);
                    transaction.Commit();
                }

                foreach (var entry in entries)
                {
                    Enqueue(entry);
                }

                RemoveSourceIfMoving(source);
            }

            matcher.Add(new WorkCandidate(id, signature, analysis.Fingerprint is { } f ? [f] : []));
            _titlesThisRun[id] = metadata.Title!;
            _directoriesThisRun[id] = dir;
            return new ImportItem(source, ImportAction.NewWork, destination, id, metadata.Title, decision.Confidence, message, entries.Count);
        }

        private void Enqueue(ReviewEntry entry)
        {
            queue.Add(entry);
            if (++_unsaved >= SaveEvery)
            {
                SaveQueue();
            }
        }

        /// <summary>Writes the queue file if there is anything not yet written.</summary>
        public void SaveQueue()
        {
            if (_unsaved > 0 && !options.DryRun)
            {
                queue.Save(ReviewQueue.PathFor(root));
                _unsaved = 0;
            }
        }

        /// <summary>Copies the file (temporary name, then rename) and checks the copy; with --move the content is hashed again.</summary>
        private Placed Place(string source, FileAnalysis analysis, string destination)
        {
            var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.Copy(source, temporary);
                File.Move(temporary, destination);
            }
            catch
            {
                TryDelete(temporary);
                throw;
            }

            var info = new FileInfo(destination);
            var verified = info.Length == analysis.Size;
            if (verified && options.Move)
            {
                using var stream = File.OpenRead(destination);
                verified = Convert.ToHexStringLower(SHA256.HashData(stream)) == analysis.Sha256;
            }

            if (!verified)
            {
                TryDelete(destination);
                throw new IOException($"the copy of {source} does not match the original");
            }

            return new Placed(info.FullName, info.Length, IndexWriter.Mtime(info));
        }

        private void RemoveSourceIfMoving(string source)
        {
            if (options.Move)
            {
                File.Delete(source);
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Best effort; the original error is the one to report.
            }
        }

        private static string? WeakMetadataMessage(BookMetadata metadata)
        {
            var fromName = new List<string>();
            if (metadata.TitleOrigin == MetadataOrigin.FileName)
            {
                fromName.Add("title");
            }

            if (metadata.AuthorsOrigin == MetadataOrigin.FileName)
            {
                fromName.Add("authors");
            }

            var parts = new List<string>();
            if (fromName.Count > 0)
            {
                parts.Add($"{string.Join(" and ", fromName)} {(fromName.Count == 1 && fromName[0] == "title" ? "comes" : "come")} from the file name");
            }

            if (metadata.AuthorsOrigin == MetadataOrigin.None)
            {
                parts.Add($"no author found, stored as '{AuthorNames.UnknownAuthor}'");
            }

            return parts.Count == 0 ? null : string.Join("; ", parts);
        }

        private string? FindBySha(string sha)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT rel_path FROM files WHERE sha256 = $sha LIMIT 1";
            command.Parameters.AddWithValue("$sha", sha);
            return command.ExecuteScalar() as string;
        }

        private string? DirectoryOf(Ulid workId)
        {
            if (_directoriesThisRun.TryGetValue(workId, out var planned))
            {
                return planned;
            }

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT dir_path FROM works WHERE id = $id";
            command.Parameters.AddWithValue("$id", workId.ToString());
            return command.ExecuteScalar() as string;
        }

        private string TitleOf(Ulid workId)
        {
            if (_titlesThisRun.TryGetValue(workId, out var title))
            {
                return title;
            }

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT title FROM works WHERE id = $id";
            command.Parameters.AddWithValue("$id", workId.ToString());
            return command.ExecuteScalar() as string ?? workId.ToString();
        }

        private sealed record Placed(string FullName, long Length, long Mtime);
    }
}
