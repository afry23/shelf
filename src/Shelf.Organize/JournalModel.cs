namespace Shelf.Organize;

public enum JournalOpKind { Mkdir, Move, Trash, Create }

public enum JournalOpStatus { Pending, Done, Undone }

public enum JournalState { Running, Finished, Undone }

/// <summary>One step of a journal. Paths are relative to the library root and use '/'.</summary>
/// <param name="From">The original path; empty for <see cref="JournalOpKind.Mkdir"/> and <see cref="JournalOpKind.Create"/>.</param>
/// <param name="To">The new path, or the folder or file that was created.</param>
/// <param name="Sha256">Hash of the moved or created file (lower-case hex); <c>null</c> for <see cref="JournalOpKind.Mkdir"/>.</param>
public sealed record JournalOp(JournalOpKind Kind, string From, string To, string? Sha256, JournalOpStatus Status);

/// <summary>The content of one <c>.shelf/journal/&lt;timestamp&gt;.json</c>.</summary>
public sealed class JournalData(string label, bool undoable, DateTimeOffset started)
{
    public string Label { get; } = label;

    /// <summary>Only undoable journals are picked by <see cref="JournalUndo"/>; the others exist for crash recovery.</summary>
    public bool Undoable { get; } = undoable;

    public DateTimeOffset Started { get; } = started;

    public JournalState State { get; set; } = JournalState.Running;

    public List<JournalOp> Ops { get; } = [];
}

public enum MoveStatus { Moved, Collision, Missing }

public sealed class JournalException(string message, Exception? innerException = null) : Exception(message, innerException);
