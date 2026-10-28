using Shelf.Core.Model;
using Shelf.Index;
using Shelf.Organize;

namespace Shelf.Library;

public sealed partial class LibraryService
{
    /// <summary>
    /// Adds an uploaded file to a work (F-GRP-7), as <c>shelf add</c> does; with <paramref name="replaceFileId"/> the old
    /// file goes to the trash. It can be undone with <c>shelf undo</c>.
    /// </summary>
    /// <param name="fileName">The name the file had where it came from; only its last part is used.</param>
    /// <param name="replaceFileId">The ID of a file of the same work that the new one replaces.</param>
    /// <exception cref="LibraryException">NotFound: no such work or file. Conflict: the file is in the library already.</exception>
    public AddFileOutcome AddFile(string workId, ReceivedFile file, string? replaceFileId = null, bool carryNotes = false)
    {
        if (!WorkLookup.LooksLikeId(workId))
        {
            throw new LibraryException($"'{workId}' is not the ID of a work");
        }

        var work = Find(workId);
        string? replace = null;
        if (replaceFileId is not null)
        {
            if (!FileIds.TryParse(replaceFileId, out _, out _))
            {
                throw new LibraryException($"'{replaceFileId}' is not the ID of a file (the ID of a work, a tilde and the file name)");
            }

            var existing = ResolveFile(replaceFileId);
            if (existing.Work != work.Id)
            {
                throw new LibraryException($"'{replaceFileId}' is not a file of the work {work.Id}", LibraryErrorKind.Conflict);
            }

            replace = existing.Name;
        }
        else if (carryNotes)
        {
            throw new LibraryException("carryNotes needs a file to replace");
        }

        lock (_writeGate)
        {
            try
            {
                var result = LibraryFiles.AddFile(root, work, file.Path, new AddOptions(Replace: replace, CarryNotes: carryNotes), root);
                return new AddFileOutcome(result.Work.ToString(), result.File, result.Replaced);
            }
            catch (Exception e) when (e is LibraryEditException or SidecarException or JournalException or IndexException)
            {
                throw new LibraryException(e.Message, KindOf(e));
            }
        }
    }

    /// <summary>Makes a work from an uploaded file (F-GRP-9), as <c>shelf create</c> does. It can be undone with <c>shelf undo</c>.</summary>
    /// <exception cref="LibraryException">Conflict: the file is in the library already.</exception>
    public CreateWorkOutcome CreateWork(ReceivedFile file, WorkValues values)
    {
        lock (_writeGate)
        {
            try
            {
                var result = LibraryFiles.CreateWork(root, file.Path, values, move: false, root);

                // A file without a cover is no reason to fail a work that was made.
                LibraryCovers.FillWorks(root, [result.Work]);
                return new CreateWorkOutcome(result.Work.ToString(), result.File, result.Folder);
            }
            catch (Exception e) when (e is LibraryEditException or SidecarException or JournalException or IndexException)
            {
                throw new LibraryException(e.Message, KindOf(e));
            }
        }
    }

    /// <summary>
    /// Moves a file to another work, or to a new one (F-GRP-8), as <c>shelf move --yes</c> does. Reading position and notes
    /// go with it; the last file of a work takes the work along. It can be undone with <c>shelf undo</c>.
    /// </summary>
    /// <param name="targetWorkId">The work that receives the file; null makes a new work of it, described by <paramref name="values"/>.</param>
    /// <exception cref="LibraryException">NotFound: no such work or file. Conflict: the target is the work of the file.</exception>
    public MoveFileOutcome MoveFile(string fileId, string? targetWorkId, WorkValues values)
    {
        if (!FileIds.TryParse(fileId, out _, out _))
        {
            throw new LibraryException($"'{fileId}' is not the ID of a file (the ID of a work, a tilde and the file name)");
        }

        if (targetWorkId is not null && !WorkLookup.LooksLikeId(targetWorkId))
        {
            throw new LibraryException($"'{targetWorkId}' is not the ID of a work");
        }

        var file = ResolveFile(fileId);
        var work = Find(file.Work.ToString());
        var target = targetWorkId is null ? null : Find(targetWorkId);
        if (target is not null && target.Id == work.Id)
        {
            throw new LibraryException("cannot move a file into its own work", LibraryErrorKind.Conflict);
        }

        lock (_writeGate)
        {
            try
            {
                var result = LibraryFiles.MoveFile(root, work, file.Name, target, values, root);
                if (target is null)
                {
                    LibraryCovers.FillWorks(root, [result.Target]);
                }

                return new MoveFileOutcome(result.Source.ToString(), result.Target.ToString(), result.File, result.SourceRemoved);
            }
            catch (Exception e) when (e is LibraryEditException or SidecarException or JournalException or IndexException)
            {
                throw new LibraryException(e.Message, KindOf(e));
            }
        }
    }

    private static LibraryErrorKind KindOf(Exception e) =>
        e switch
        {
            LibraryEditException { NotFound: true } => LibraryErrorKind.NotFound,
            LibraryEditException { Message: var m } when m.Contains("is already in the library", StringComparison.Ordinal) => LibraryErrorKind.Conflict,
            _ => LibraryErrorKind.Invalid,
        };

    /// <summary>
    /// Saves an uploaded file in a folder of its own below <c>.shelf/uploads</c>, named like the original, for
    /// <see cref="AddFile"/> or <see cref="CreateWork"/>. Dispose it to delete it.
    /// </summary>
    /// <param name="fileName">The name the file had where it came from; only its last part is used.</param>
    /// <exception cref="LibraryException">The file has no name.</exception>
    public async Task<ReceivedFile> ReceiveAsync(Stream content, string fileName, CancellationToken cancellationToken = default)
    {
        var name = Path.GetFileName(fileName.Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new LibraryException("the file needs a name");
        }

        var folder = Path.Combine(root, ".shelf", "uploads", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var received = new ReceivedFile(folder, Path.Combine(folder, name));
        try
        {
            await using var target = new FileStream(received.Path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1 << 16, useAsync: true);
            await content.CopyToAsync(target, cancellationToken);
            return received;
        }
        catch
        {
            received.Dispose();
            throw;
        }
    }
}

/// <summary>A file received from outside, kept in a folder of its own until it is disposed.</summary>
public sealed class ReceivedFile : IDisposable
{
    private readonly string _folder;

    internal ReceivedFile(string folder, string path)
    {
        _folder = folder;
        Path = path;
    }

    public string Path { get; }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, recursive: true);

            // The folder for all uploads goes with the last one; a second upload that is still arriving keeps it.
            Directory.Delete(System.IO.Path.GetDirectoryName(_folder)!);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A leftover in .shelf/uploads is harmless; the result of the request is what counts.
        }
    }
}
