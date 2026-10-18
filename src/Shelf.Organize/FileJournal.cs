using System.Globalization;

namespace Shelf.Organize;

/// <summary>
/// The undo log of one run (F-ORG-6) and the crash safety of file operations (PRD 9.3). Every step is written to
/// <c>.shelf/journal/&lt;timestamp&gt;.json</c> as <c>pending</c> before it is executed and as <c>done</c> after.
/// A move never overwrites (F-ORG-7); a taken target is reported as <see cref="MoveStatus.Collision"/>.
/// </summary>
/// <remarks>All paths passed in must be inside the library root. One writer at a time.</remarks>
public sealed class FileJournal
{
    private readonly string _root;
    private readonly string _path;
    private readonly string _stamp;
    private readonly JournalData _data;

    private FileJournal(string root, string path, string stamp, JournalData data)
    {
        _root = root;
        _path = path;
        _stamp = stamp;
        _data = data;
    }

    public string Path => _path;

    public IReadOnlyList<JournalOp> Ops => _data.Ops;

    /// <summary>Recovers unfinished journals of earlier runs, then starts a new one.</summary>
    /// <param name="undoable">Whether <c>shelf undo</c> may revert this run; sidecar-changing runs pass <c>false</c>.</param>
    /// <exception cref="JournalException"/>
    public static FileJournal Begin(string root, string label, bool undoable, TimeProvider? clock = null)
    {
        root = Normalize(root);
        clock ??= TimeProvider.System;
        JournalRecovery.Recover(root);

        var directory = LibraryPaths.JournalDirectory(root);
        Directory.CreateDirectory(directory);
        var now = clock.GetUtcNow();
        var stamp = now.ToString("yyyyMMdd'T'HHmmss'.'fff'Z'", CultureInfo.InvariantCulture);
        for (var n = 2; File.Exists(System.IO.Path.Combine(directory, stamp + ".json")); n++)
        {
            stamp = $"{now:yyyyMMdd'T'HHmmss'.'fff'Z'}_{n}";
        }

        var journal = new FileJournal(root, System.IO.Path.Combine(directory, stamp + ".json"), stamp, new JournalData(label, undoable, now));
        journal.Save();
        return journal;
    }

    /// <summary>Moves a file; the target is never overwritten and missing parent folders are created (and journaled).</summary>
    public MoveStatus Move(string from, string to) => Execute(JournalOpKind.Move, System.IO.Path.GetFullPath(from), System.IO.Path.GetFullPath(to));

    /// <summary>Moves a file to <c>.shelf/trash/&lt;timestamp&gt;/&lt;relative path&gt;</c>; a taken trash name gets a number.</summary>
    public MoveStatus Trash(string path)
    {
        var full = System.IO.Path.GetFullPath(path);
        var relative = LibraryPaths.Relative(_root, full);
        var target = System.IO.Path.Combine(LibraryPaths.TrashDirectory(_root), _stamp, relative.Replace('/', System.IO.Path.DirectorySeparatorChar));
        var free = target;
        var name = System.IO.Path.GetFileNameWithoutExtension(target);
        var extension = System.IO.Path.GetExtension(target);
        for (var n = 2; File.Exists(free) || Directory.Exists(free); n++)
        {
            free = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(target)!, $"{name} ({n}){extension}");
        }

        return Execute(JournalOpKind.Trash, full, free);
    }

    /// <summary>
    /// Writes a new file that must not exist yet. Undo deletes it again, if it is unchanged by then.
    /// </summary>
    public MoveStatus Create(string path, byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var full = System.IO.Path.GetFullPath(path);
        var relative = LibraryPaths.Relative(_root, full);
        if (File.Exists(full) || Directory.Exists(full))
        {
            return MoveStatus.Collision;
        }

        EnsureParents(System.IO.Path.GetDirectoryName(full)!);
        var op = new JournalOp(JournalOpKind.Create, string.Empty, relative, LibraryPaths.Hash(content), JournalOpStatus.Pending);
        _data.Ops.Add(op);
        Save();
        LibraryPaths.WriteAtomic(full, content);
        _data.Ops[^1] = op with { Status = JournalOpStatus.Done };
        Save();
        return MoveStatus.Moved;
    }

    /// <summary>
    /// Replaces the content of a file. The old file is kept next to the journal (<c>&lt;timestamp&gt;.backup/</c>), so
    /// undo can bring back its exact bytes.
    /// </summary>
    /// <exception cref="JournalException">The file does not exist.</exception>
    public void Replace(string path, byte[] content)
    {
        var full = System.IO.Path.GetFullPath(path);
        var relative = LibraryPaths.Relative(_root, full);
        var backup = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(_path)!, _stamp + ".backup", relative.Replace('/', System.IO.Path.DirectorySeparatorChar));
        var free = backup;
        for (var n = 2; File.Exists(free); n++)
        {
            // The same file replaced twice in one run: every old version is kept.
            free = $"{backup}.{n}";
        }

        if (Execute(JournalOpKind.Move, full, free) != MoveStatus.Moved)
        {
            throw new JournalException($"cannot replace '{relative}': the file is missing");
        }

        if (Create(full, content) != MoveStatus.Moved)
        {
            throw new JournalException($"cannot replace '{relative}': the path was taken again");
        }
    }

    /// <summary>Marks the run as complete. Without it, the next run recovers the journal.</summary>
    public void Finish()
    {
        _data.State = JournalState.Finished;
        Save();
    }

    private MoveStatus Execute(JournalOpKind kind, string from, string to)
    {
        var relativeFrom = LibraryPaths.Relative(_root, from);
        var relativeTo = LibraryPaths.Relative(_root, to);
        if (!File.Exists(from))
        {
            return MoveStatus.Missing;
        }

        if (File.Exists(to) || Directory.Exists(to))
        {
            return MoveStatus.Collision;
        }

        EnsureParents(System.IO.Path.GetDirectoryName(to)!);
        var op = new JournalOp(kind, relativeFrom, relativeTo, LibraryPaths.Hash(from), JournalOpStatus.Pending);
        _data.Ops.Add(op);
        Save();
        try
        {
            File.Move(from, to, overwrite: false);
        }
        catch (IOException) when (File.Exists(to))
        {
            _data.Ops.RemoveAt(_data.Ops.Count - 1);
            Save();
            return MoveStatus.Collision;
        }

        _data.Ops[^1] = op with { Status = JournalOpStatus.Done };
        Save();
        return MoveStatus.Moved;
    }

    private void EnsureParents(string directory)
    {
        var missing = new Stack<string>();
        for (var current = directory; !Directory.Exists(current); current = System.IO.Path.GetDirectoryName(current)!)
        {
            missing.Push(current);
        }

        while (missing.Count > 0)
        {
            var folder = missing.Pop();
            _data.Ops.Add(new JournalOp(JournalOpKind.Mkdir, string.Empty, LibraryPaths.Relative(_root, folder), null, JournalOpStatus.Pending));
            Save();
            Directory.CreateDirectory(folder);
            _data.Ops[^1] = _data.Ops[^1] with { Status = JournalOpStatus.Done };
            Save();
        }
    }

    private void Save() => LibraryPaths.WriteAtomic(_path, JournalFormat.Serialize(_data));

    internal static string Normalize(string root)
    {
        ArgumentException.ThrowIfNullOrEmpty(root);
        return System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(root));
    }
}
