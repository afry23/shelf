using Shelf.Core.Organization;
using Shelf.Index;
using Shelf.Organize;

namespace Shelf.Library;

public sealed partial class LibraryService
{
    /// <summary>The dry run of the organization pattern (F-ORG-5), and the run that <see cref="UndoLast"/> would revert.</summary>
    /// <param name="pattern">The pattern to try; without it the configured one.</param>
    public OrganizePreview PreviewOrganize(string? pattern = null)
    {
        var proposal = ProposeOrganization(pattern);
        return new OrganizePreview(
            proposal.Pattern,
            proposal.Files,
            proposal.Works,
            proposal.Unchanged,
            proposal.Truncated,
            proposal.Moves,
            proposal.Collisions,
            NextUndo());
    }

    /// <summary>
    /// Moves the files to the folders and names of the pattern in one undoable journal (F-ORG-6). Collisions are never overwritten:
    /// the work stays as it is and is reported (F-ORG-7).
    /// </summary>
    /// <param name="pattern">The pattern that was previewed; without it the configured one.</param>
    public OrganizeOutcome ApplyOrganize(string? pattern = null)
    {
        PathPattern chosen;
        try
        {
            chosen = pattern is null ? settings.Organize.Pattern : PathPattern.Parse(pattern);
        }
        catch (PatternException e)
        {
            throw new LibraryException($"pattern: {e.Message}");
        }

        lock (_writeGate)
        {
            try
            {
                var result = LibraryOrganizer.Organize(root, chosen, apply: true, settings.Organize.PaperPattern);
                return new OrganizeOutcome(
                    chosen.Text,
                    result.Files,
                    result.Works,
                    result.Unchanged,
                    [.. result.Collisions.Take(MaxMoves).Select(c => new SkippedMove(c.Work.ToString(), c.From, c.To, c.Reason))]);
            }
            catch (Exception e) when (e is IndexException or LibraryEditException or JournalException or Shelf.Core.Model.SidecarException)
            {
                throw new LibraryException(e.Message);
            }
        }
    }

    /// <summary>Reverts the last undoable run, whatever it was (organize, a tag change, a duplicate run): the same as <c>shelf undo</c>.</summary>
    /// <exception cref="LibraryException">Conflict: there is nothing to undo.</exception>
    public UndoOutcome UndoLast()
    {
        lock (_writeGate)
        {
            try
            {
                var result = LibraryOrganizer.Undo(root)
                    ?? throw new LibraryException("there is nothing to undo", LibraryErrorKind.Conflict);
                return new UndoOutcome(result.Label, result.Reverted, [.. result.Skipped.Select(s => new UndoSkipped(s.Path, s.Reason))]);
            }
            catch (Exception e) when (e is IndexException or JournalException)
            {
                throw new LibraryException(e.Message);
            }
        }
    }

    private string? NextUndo()
    {
        try
        {
            return JournalUndo.NextLabel(root);
        }
        catch (JournalException)
        {
            return null;
        }
    }
}
