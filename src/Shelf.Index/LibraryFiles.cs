using System.Security.Cryptography;
using System.Text;
using Shelf.Core.Grouping;
using Shelf.Core.Model;
using Shelf.Core.Review;
using Shelf.Extraction;
using Shelf.Organize;

namespace Shelf.Index;

/// <param name="Move">The source file is deleted after it was copied into the library.</param>
/// <param name="Replace">A file of the work (name or path) that the new file replaces; it goes to the trash.</param>
/// <param name="CarryNotes">Reading position and notes of the replaced file go to the new file.</param>
/// <param name="Notes">The file is a notes file (F-NOTEF-1): a PDF or TXT file with the role <see cref="FileRole.Notes"/>.</param>
public sealed record AddOptions(bool Move = false, string? Replace = null, bool CarryNotes = false, bool Notes = false);

/// <summary>What <see cref="LibraryFiles.AddFile"/> did.</summary>
public sealed record AddResult(Ulid Work, string File, string? Replaced);

/// <summary>Values that replace the metadata read from the file; null leaves the file's own.</summary>
public sealed record WorkValues(string? Title = null, IReadOnlyList<string>? Authors = null, int? Year = null);

/// <summary>What <see cref="LibraryFiles.CreateWork"/> did.</summary>
public sealed record CreateResult(Ulid Work, string File, string Folder);

/// <summary>What <see cref="LibraryFiles.MoveFile"/> did, or would do.</summary>
/// <param name="Target">The work that receives the file; empty in a plan that makes a new work.</param>
/// <param name="SourceRemoved">The file was the last of its work; the work goes to the trash with it.</param>
public sealed record FileMoveResult(Ulid Source, Ulid Target, string File, bool SourceRemoved);

/// <summary>
/// Files and works by hand (F-GRP-7 to F-GRP-9): add a file to a work, create a work from a file, move a file to another
/// work. Files first, sidecar next, the index last (a scan); every run is one journal that <c>shelf undo</c> can reverse.
/// </summary>
public static class LibraryFiles
{
    /// <summary>Adds a file to a work, optionally replacing one of its files (the old one goes to the trash).</summary>
    /// <param name="source">The file to add, as the person gave it; relative paths are taken from <paramref name="workingDirectory"/>.</param>
    /// <exception cref="LibraryEditException"/>
    public static AddResult AddFile(string root, LibraryWork work, string source, AddOptions options, string workingDirectory)
    {
        var sidecarPath = LibraryEditor.SidecarPath(root, work);
        var sidecar = LibraryEditor.Read(root, work);
        SidecarFile? replaced = null;
        if (options.Replace is { } replace)
        {
            replaced = LibraryEditor.FindFile(root, work, sidecar, replace, workingDirectory)
                ?? throw new LibraryEditException($"{work.Id} has no file '{replace}'");
        }

        var fullSource = CheckSource(root, source, workingDirectory, options.Notes);
        var directory = Path.Combine(root, work.Directory);
        var name = FolderNames.UniqueFileName(directory, Path.GetFileName(fullSource));
        var journal = FileJournal.Begin(root, "add", undoable: true);
        try
        {
            Place(journal, Path.Combine(directory, name), fullSource);
            var files = sidecar.Files.Where(f => f.Name != replaced?.Name).ToList();
            files.Add(new SidecarFile(name, options.CarryNotes ? replaced?.Progress : null, options.Notes ? FileRole.Notes : FileRole.Book));
            var updated = sidecar with
            {
                Files = files,
                NotDuplicates = sidecar.NotDuplicates with
                {
                    Files = [.. sidecar.NotDuplicates.Files.Where(p => p.A != replaced?.Name && p.B != replaced?.Name)],
                },
            };
            journal.Replace(sidecarPath, SidecarBytes(updated, sidecarPath));
            if (replaced is not null)
            {
                journal.Trash(Path.Combine(directory, replaced.Name));
                if (options.CarryNotes)
                {
                    CarryNotes(root, journal, work, work, new Dictionary<string, string> { [replaced.Name] = name });
                }
                else
                {
                    LibraryDeleter.DropNotes(root, work, journal, replaced.Name);
                }
            }
        }
        finally
        {
            journal.Finish();
        }

        if (replaced is not null)
        {
            LibraryDeleter.DropFromQueue(root, work.Id, IndexWriter.JoinRelative(work.Directory, replaced.Name));
        }

        if (options.Move)
        {
            File.Delete(fullSource);
        }

        LibraryEditor.Reconcile(root);
        return new AddResult(work.Id, name, replaced?.Name);
    }

    /// <summary>Creates a work from a file without grouping it; given values replace what the file says.</summary>
    /// <exception cref="LibraryEditException"/>
    public static CreateResult CreateWork(string root, string source, WorkValues values, bool move, string workingDirectory)
    {
        var fullSource = CheckSource(root, source, workingDirectory);
        var metadata = ReadMetadata(fullSource, source);
        var id = Ulid.New();
        var name = Path.GetFileName(fullSource);
        var sidecar = NewSidecar(id, metadata, values, name, progress: null);
        var folder = FolderNames.FreeFolder(FolderNames.WorkFolder(root, AuthorNames.Parse(sidecar.Authors[0]).SortKey, sidecar.Title, sidecar.Year));
        using (var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(root)))
        {
            if (LibraryCiteKeys.ForNew(connection, sidecar) is { } citeKey)
            {
                sidecar = sidecar with { CiteKey = citeKey };
            }
        }

        var journal = FileJournal.Begin(root, "create", undoable: true);
        try
        {
            Place(journal, Path.Combine(folder, name), fullSource);
            var sidecarPath = Path.Combine(folder, SidecarStore.FileName);
            if (journal.Create(sidecarPath, SidecarBytes(sidecar, sidecarPath)) != MoveStatus.Moved)
            {
                throw new LibraryEditException($"{SidecarStore.FileName} already exists in {LibraryReindexer.RelativePath(root, folder)}");
            }
        }
        finally
        {
            journal.Finish();
        }

        if (move)
        {
            File.Delete(fullSource);
        }

        LibraryEditor.Reconcile(root);
        return new CreateResult(id, name, LibraryReindexer.RelativePath(root, folder));
    }

    /// <summary>What moving this file would do; nothing is changed.</summary>
    /// <exception cref="LibraryEditException"/>
    public static FileMoveResult PlanMove(string root, LibraryWork work, string file, LibraryWork? target, string workingDirectory)
    {
        var sidecar = LibraryEditor.Read(root, work);
        var entry = LibraryEditor.FindFile(root, work, sidecar, file, workingDirectory)
            ?? throw new LibraryEditException($"{work.Id} has no file '{file}'");
        if (target is not null && target.Id == work.Id)
        {
            throw new LibraryEditException("cannot move a file into its own work");
        }

        return new FileMoveResult(work.Id, target?.Id ?? default, entry.Name, SourceRemoved: sidecar.Files.Count < 2);
    }

    /// <summary>
    /// Moves a file to another work, or to a new one described by <paramref name="values"/> and the file's own metadata.
    /// Reading position and notes go with it. The last file of a work takes the work along.
    /// </summary>
    /// <param name="target">The work that receives the file; null makes a new work.</param>
    /// <exception cref="LibraryEditException"/>
    public static FileMoveResult MoveFile(string root, LibraryWork work, string file, LibraryWork? target, WorkValues values, string workingDirectory)
    {
        var plan = PlanMove(root, work, file, target, workingDirectory);
        var sourcePath = LibraryEditor.SidecarPath(root, work);
        var sidecar = SidecarStore.Read(sourcePath);
        var entry = sidecar.Files.First(f => f.Name == plan.File);
        var sourceDirectory = Path.Combine(root, work.Directory);
        var from = Path.Combine(sourceDirectory, entry.Name);
        if (!File.Exists(from))
        {
            throw new LibraryEditException($"{IndexWriter.JoinRelative(work.Directory, entry.Name)} does not exist");
        }

        LibraryWork receiver;
        string targetDirectory;
        WorkSidecar? created = null;
        WorkSidecar? targetSidecar = null;
        if (target is null && entry.Role == FileRole.Notes)
        {
            throw new LibraryEditException($"{entry.Name} is a notes file; it can only be moved to a work that exists");
        }

        if (target is null)
        {
            var id = Ulid.New();
            created = NewSidecar(id, ReadMetadata(from, entry.Name), values, entry.Name, entry.Progress) with
            {
                NotDuplicates = new NotDuplicates(plan.SourceRemoved ? [] : [work.Id], []),
            };
            targetDirectory = FolderNames.FreeFolder(FolderNames.WorkFolder(root, AuthorNames.Parse(created.Authors[0]).SortKey, created.Title, created.Year));
            receiver = new LibraryWork(id, LibraryReindexer.RelativePath(root, targetDirectory), created.Title);
        }
        else
        {
            receiver = target;
            targetDirectory = Path.Combine(root, target.Directory);
            targetSidecar = LibraryEditor.Read(root, target);
        }

        var name = FolderNames.UniqueFileName(targetDirectory, entry.Name);
        var journal = FileJournal.Begin(root, "move", undoable: true);
        try
        {
            LibraryEditor.Move(journal, from, Path.Combine(targetDirectory, name));
            if (created is not null)
            {
                var createdPath = Path.Combine(targetDirectory, SidecarStore.FileName);
                created = created with { Files = [new SidecarFile(name, entry.Progress, entry.Role)] };
                journal.Create(createdPath, SidecarBytes(created, createdPath));
            }
            else
            {
                var targetPath = LibraryEditor.SidecarPath(root, receiver);
                var updated = targetSidecar! with { Files = [.. targetSidecar.Files, new SidecarFile(name, entry.Progress, entry.Role)] };
                journal.Replace(targetPath, SidecarBytes(updated, targetPath));
            }

            CarryNotes(root, journal, work, receiver, new Dictionary<string, string> { [entry.Name] = name });
            if (plan.SourceRemoved)
            {
                if (sidecar.Cover is { } cover && File.Exists(Path.Combine(sourceDirectory, cover)))
                {
                    journal.Trash(Path.Combine(sourceDirectory, cover));
                }

                var notes = LibraryAnnotations.PathOf(root, work);
                if (File.Exists(notes))
                {
                    journal.Trash(notes);
                }

                journal.Trash(sourcePath);
            }
            else
            {
                var rest = sidecar with
                {
                    Files = [.. sidecar.Files.Where(f => f.Name != entry.Name)],
                    NotDuplicates = new NotDuplicates(
                        created is null ? sidecar.NotDuplicates.Works : [.. sidecar.NotDuplicates.Works.Append(receiver.Id).Distinct()],
                        [.. sidecar.NotDuplicates.Files.Where(p => p.A != entry.Name && p.B != entry.Name)]),
                };
                journal.Replace(sourcePath, SidecarBytes(rest, sourcePath));
            }
        }
        finally
        {
            journal.Finish();
        }

        if (plan.SourceRemoved)
        {
            LibraryEditor.RemoveFolderIfEmpty(root, sourceDirectory);
        }

        var oldPath = IndexWriter.JoinRelative(work.Directory, entry.Name);
        var newPath = IndexWriter.JoinRelative(receiver.Directory, name);
        var queue = LibraryEditor.LoadQueue(root);
        LibraryEditor.SaveQueue(root, new ReviewQueue(queue.Entries.Select(e => e.File == oldPath ? e with { File = newPath, Work = e.Work == work.Id ? receiver.Id : e.Work } : e)));
        LibraryEditor.Reconcile(root);
        return plan with { Target = receiver.Id };
    }

    // ---- helpers ----

    /// <summary>The full path of a source that is a supported file, not yet in the library.</summary>
    private static string CheckSource(string root, string source, string workingDirectory, bool notes = false)
    {
        var full = Path.GetFullPath(source, workingDirectory);
        if (Directory.Exists(full))
        {
            throw new LibraryEditException($"{source} is not a file");
        }

        if (!File.Exists(full))
        {
            throw new LibraryEditException($"{source} does not exist");
        }

        if (notes ? !BookExtractor.IsNotesFile(full) : !BookExtractor.IsSupported(full))
        {
            throw new LibraryEditException(notes
                ? $"{source} is not a notes file: only PDF and TXT files can be added with --notes"
                : $"{source} is not a supported format");
        }

        string sha;
        using (var stream = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16))
        {
            sha = Convert.ToHexStringLower(SHA256.HashData(stream));
        }

        using var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(root));
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT rel_path FROM files WHERE sha256 = $sha LIMIT 1";
        command.Parameters.AddWithValue("$sha", sha);
        if (command.ExecuteScalar() is string existing)
        {
            throw new LibraryEditException($"{source} is already in the library as {existing}");
        }

        return full;
    }

    private static void Place(FileJournal journal, string destination, string source)
    {
        switch (journal.CreateFrom(destination, source))
        {
            case MoveStatus.Collision:
                throw new LibraryEditException($"{Path.GetFileName(destination)} already exists");
            case MoveStatus.Missing:
                throw new LibraryEditException($"{source} does not exist");
        }
    }

    private static BookMetadata ReadMetadata(string fullPath, string shownAs)
    {
        try
        {
            return BookExtractor.ReadMetadata(fullPath);
        }
        catch (ExtractionException e)
        {
            throw new LibraryEditException($"{shownAs}: {e.Message}");
        }
    }

    private static WorkSidecar NewSidecar(Ulid id, BookMetadata metadata, WorkValues values, string fileName, FileProgress? progress)
    {
        var authors = values.Authors is { Count: > 0 } given ? given
            : metadata.Authors.Count > 0 ? metadata.Authors
            : [AuthorNames.UnknownAuthor];
        var title = !string.IsNullOrWhiteSpace(values.Title) ? values.Title
            : string.IsNullOrWhiteSpace(metadata.Title) ? Path.GetFileNameWithoutExtension(fileName)
            : metadata.Title;
        return new WorkSidecar
        {
            Id = id,
            Title = title,
            Authors = authors,
            Language = metadata.Language,
            Year = values.Year ?? metadata.Year,
            Isbn13 = metadata.Isbn13,
            Publisher = metadata.Publisher,
            Series = metadata.Series,
            SeriesIndex = metadata.SeriesIndex,
            Description = metadata.Description,
            Added = new DateTimeOffset(DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond, TimeSpan.Zero),
            Files = [new SidecarFile(fileName, progress)],
        }.From(metadata);
    }

    private static byte[] SidecarBytes(WorkSidecar sidecar, string path)
    {
        SidecarFormat.Validate(sidecar, path);
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(SidecarFormat.Serialize(sidecar));
    }

    private static void WriteNotes(FileJournal journal, string path, AnnotationsSidecar notes, bool exists)
    {
        AnnotationsFormat.Validate(notes, path);
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(AnnotationsFormat.Serialize(notes));
        if (exists)
        {
            journal.Replace(path, bytes);
        }
        else
        {
            journal.Create(path, bytes);
        }
    }

    /// <summary>
    /// The notes of the renamed files go from one work to the other (or to the new name in the same work), through the
    /// journal so that undo brings them back. Notes of other files stay.
    /// </summary>
    private static void CarryNotes(string root, FileJournal journal, LibraryWork source, LibraryWork target, IReadOnlyDictionary<string, string> renames)
    {
        var sourcePath = LibraryAnnotations.PathOf(root, source);
        AnnotationsSidecar outgoing;
        try
        {
            outgoing = AnnotationsStore.Read(sourcePath);
        }
        catch (SidecarException)
        {
            return; // notes that cannot be read stay where they are
        }

        var moving = outgoing.Annotations.Where(a => renames.ContainsKey(a.File)).ToList();
        if (moving.Count == 0)
        {
            return;
        }

        var carried = moving.Select(a => a with { File = renames[a.File] }).ToList();
        var staying = outgoing.Annotations.Where(a => !renames.ContainsKey(a.File)).ToList();
        if (source.Id == target.Id)
        {
            WriteNotes(journal, sourcePath, outgoing with { Annotations = [.. staying, .. carried] }, exists: true);
            return;
        }

        var targetPath = LibraryAnnotations.PathOf(root, target);
        var exists = File.Exists(targetPath);
        var into = AnnotationsStore.Read(targetPath);
        WriteNotes(journal, targetPath, into with { Annotations = [.. into.Annotations, .. carried] }, exists);
        if (staying.Count == 0 && outgoing.UnknownFields.Count == 0)
        {
            journal.Trash(sourcePath);
        }
        else
        {
            WriteNotes(journal, sourcePath, outgoing with { Annotations = staying }, exists: true);
        }
    }
}
