using System.Security.Cryptography;
using Xunit;

namespace Shelf.Organize.Tests;

public class JournalTests
{
    private static string Sha(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    [Fact]
    public void Move_moves_the_file_and_journals_mkdir_and_move_as_done()
    {
        using var dir = new TempDirectory();
        var from = dir.Write("a/book.epub", "content");
        var journal = FileJournal.Begin(dir.Path, "organize", undoable: true);

        var status = journal.Move(from, dir.Combine("b", "c", "book.epub"));
        journal.Finish();

        Assert.Equal(MoveStatus.Moved, status);
        Assert.False(File.Exists(from));
        Assert.Equal("content", File.ReadAllText(dir.Combine("b", "c", "book.epub")));
        Assert.Equal(
            [JournalOpKind.Mkdir, JournalOpKind.Mkdir, JournalOpKind.Move],
            journal.Ops.Select(o => o.Kind));
        Assert.All(journal.Ops, o => Assert.Equal(JournalOpStatus.Done, o.Status));
        Assert.StartsWith(Path.Combine(dir.Path, ".shelf", "journal"), journal.Path);
    }

    [Fact]
    public void Move_never_overwrites_a_taken_target()
    {
        using var dir = new TempDirectory();
        var from = dir.Write("a/book.epub", "new");
        var target = dir.Write("b/book.epub", "precious");
        var journal = FileJournal.Begin(dir.Path, "organize", undoable: true);

        var status = journal.Move(from, target);

        Assert.Equal(MoveStatus.Collision, status);
        Assert.Equal("precious", File.ReadAllText(target));
        Assert.Equal("new", File.ReadAllText(from));
        Assert.Empty(journal.Ops);
    }

    [Fact]
    public void Move_reports_a_missing_source()
    {
        using var dir = new TempDirectory();
        var journal = FileJournal.Begin(dir.Path, "organize", undoable: true);

        Assert.Equal(MoveStatus.Missing, journal.Move(dir.Combine("nope.epub"), dir.Combine("x.epub")));
    }

    [Fact]
    public void Paths_outside_the_library_are_refused()
    {
        using var dir = new TempDirectory();
        using var other = new TempDirectory();
        var from = other.Write("a.epub", "x");
        var journal = FileJournal.Begin(dir.Path, "organize", undoable: true);

        Assert.Throws<JournalException>(() => journal.Move(from, dir.Combine("a.epub")));
        Assert.Throws<JournalException>(() => journal.Move(dir.Write("in.epub", "x"), other.Combine("in.epub")));
        Assert.Equal("x", File.ReadAllText(from));
    }

    [Fact]
    public void Undo_restores_the_exact_initial_state()
    {
        using var dir = new TempDirectory();
        var one = dir.Write("a/one.epub", "one");
        var two = dir.Write("a/two.pdf", "two");
        var before = new[] { Sha(one), Sha(two) };
        var journal = FileJournal.Begin(dir.Path, "organize", undoable: true);
        journal.Move(one, dir.Combine("Author", "Title", "one.epub"));
        journal.Move(two, dir.Combine("Author", "Title", "two.pdf"));
        journal.Finish();

        var result = JournalUndo.Undo(dir.Path);

        Assert.NotNull(result);
        Assert.Equal(2, result.Reverted);
        Assert.Empty(result.Skipped);
        Assert.Equal(before, new[] { Sha(one), Sha(two) });
        Assert.False(Directory.Exists(dir.Combine("Author")));
        Assert.Null(JournalUndo.Undo(dir.Path));
    }

    [Fact]
    public void Undo_refuses_a_file_that_changed_since()
    {
        using var dir = new TempDirectory();
        var from = dir.Write("a/one.epub", "one");
        var journal = FileJournal.Begin(dir.Path, "organize", undoable: true);
        journal.Move(from, dir.Combine("b", "one.epub"));
        journal.Finish();
        File.WriteAllText(dir.Combine("b", "one.epub"), "edited");

        var result = JournalUndo.Undo(dir.Path)!;

        Assert.Equal(0, result.Reverted);
        Assert.Equal("changed since", Assert.Single(result.Skipped).Reason);
        Assert.Equal("edited", File.ReadAllText(dir.Combine("b", "one.epub")));
        Assert.False(File.Exists(from));
    }

    [Fact]
    public void Undo_never_overwrites_a_file_that_took_the_old_place()
    {
        using var dir = new TempDirectory();
        var from = dir.Write("a/one.epub", "one");
        var journal = FileJournal.Begin(dir.Path, "organize", undoable: true);
        journal.Move(from, dir.Combine("b", "one.epub"));
        journal.Finish();
        dir.Write("a/one.epub", "someone else");

        var result = JournalUndo.Undo(dir.Path)!;

        Assert.Equal("target taken", Assert.Single(result.Skipped).Reason);
        Assert.Equal("someone else", File.ReadAllText(from));
        Assert.Equal("one", File.ReadAllText(dir.Combine("b", "one.epub")));
    }

    [Fact]
    public void Undo_ignores_journals_that_are_not_undoable()
    {
        using var dir = new TempDirectory();
        var from = dir.Write("a/one.epub", "one");
        var journal = FileJournal.Begin(dir.Path, "merge", undoable: false);
        journal.Move(from, dir.Combine("b", "one.epub"));
        journal.Finish();

        Assert.Null(JournalUndo.Undo(dir.Path));
        Assert.True(File.Exists(dir.Combine("b", "one.epub")));
    }

    [Fact]
    public void Undo_reverts_only_the_last_run()
    {
        using var dir = new TempDirectory();
        var clock = new FakeClock();
        var first = FileJournal.Begin(dir.Path, "first", undoable: true, clock);
        first.Move(dir.Write("a.epub", "a"), dir.Combine("x", "a.epub"));
        first.Finish();
        clock.Advance();
        var second = FileJournal.Begin(dir.Path, "second", undoable: true, clock);
        second.Move(dir.Write("b.epub", "b"), dir.Combine("y", "b.epub"));
        second.Finish();

        var result = JournalUndo.Undo(dir.Path)!;

        Assert.Equal("second", result.Label);
        Assert.True(File.Exists(dir.Combine("b.epub")));
        Assert.True(File.Exists(dir.Combine("x", "a.epub")));
    }

    [Fact]
    public void Trash_moves_into_the_trash_without_overwriting_and_can_be_undone()
    {
        using var dir = new TempDirectory();
        var one = dir.Write("a/one.epub", "one");
        var other = dir.Write("a/sub/one.epub", "other");
        var journal = FileJournal.Begin(dir.Path, "dupes", undoable: true);

        Assert.Equal(MoveStatus.Moved, journal.Trash(one));
        Assert.Equal(MoveStatus.Moved, journal.Trash(other));
        journal.Finish();

        var trashed = Directory.EnumerateFiles(dir.Combine(".shelf", "trash"), "*", SearchOption.AllDirectories).ToList();
        Assert.Equal(2, trashed.Count);
        Assert.False(File.Exists(one));

        JournalUndo.Undo(dir.Path);

        Assert.Equal("one", File.ReadAllText(one));
        Assert.Equal("other", File.ReadAllText(other));
        Assert.False(Directory.Exists(dir.Combine(".shelf", "trash")));
    }

    [Fact]
    public void Trash_name_taken_in_the_trash_gets_a_number()
    {
        using var dir = new TempDirectory();
        var journal = FileJournal.Begin(dir.Path, "dupes", undoable: true);
        var path = dir.Write("a/one.epub", "1");
        journal.Trash(path);
        dir.Write("a/one.epub", "2");

        journal.Trash(path);

        var names = Directory.EnumerateFiles(dir.Combine(".shelf", "trash"), "*", SearchOption.AllDirectories)
            .Select(Path.GetFileName).Order().ToList();
        Assert.Equal(["one (2).epub", "one.epub"], names);
    }

    [Fact]
    public void Trash_empty_deletes_for_good_and_counts()
    {
        using var dir = new TempDirectory();
        var journal = FileJournal.Begin(dir.Path, "dupes", undoable: true);
        journal.Trash(dir.Write("a/one.epub", "12345"));
        journal.Trash(dir.Write("a/two.epub", "123"));
        journal.Finish();

        var result = TrashEmptier.Empty(dir.Path);

        Assert.Equal(new TrashEmptyResult(2, 8), result);
        Assert.Empty(Directory.EnumerateFileSystemEntries(dir.Combine(".shelf", "trash")));
        Assert.Equal(new TrashEmptyResult(0, 0), TrashEmptier.Empty(dir.Path));
    }

    [Fact]
    public void A_pending_step_that_never_ran_is_dropped_and_the_file_stays()
    {
        using var dir = new TempDirectory();
        var from = dir.Write("a/one.epub", "one");
        var journal = FileJournal.Begin(dir.Path, "organize", undoable: true);
        // The crash: the step was journaled as pending, the move did not happen.
        PendingStep(journal, from, dir.Combine("b", "one.epub"));

        var report = JournalRecovery.Recover(dir.Path);

        Assert.Equal(1, report.Journals);
        Assert.Equal(1, report.Dropped);
        Assert.True(File.Exists(from));
        Assert.Null(JournalUndo.Undo(dir.Path));
    }

    [Fact]
    public void A_pending_step_that_ran_is_completed_and_can_be_undone()
    {
        using var dir = new TempDirectory();
        var from = dir.Write("a/one.epub", "one");
        var to = dir.Combine("b", "one.epub");
        var journal = FileJournal.Begin(dir.Path, "organize", undoable: true);
        PendingStep(journal, from, to);
        Directory.CreateDirectory(dir.Combine("b"));
        File.Move(from, to); // the crash: the move happened, "done" was not written

        var report = JournalRecovery.Recover(dir.Path);

        Assert.Equal(1, report.Completed);
        var result = JournalUndo.Undo(dir.Path)!;
        Assert.Equal(1, result.Reverted);
        Assert.Equal("one", File.ReadAllText(from));
    }

    [Fact]
    public void Begin_recovers_an_unfinished_journal_of_an_earlier_run()
    {
        using var dir = new TempDirectory();
        var first = FileJournal.Begin(dir.Path, "organize", undoable: true);
        first.Move(dir.Write("a/one.epub", "one"), dir.Combine("b", "one.epub"));
        // no Finish(): the process died

        FileJournal.Begin(dir.Path, "next", undoable: true);

        Assert.Equal("one", File.ReadAllText(dir.Combine("b", "one.epub")));
        Assert.Contains("\"state\": \"finished\"", File.ReadAllText(first.Path));
    }

    [Fact]
    public void A_file_that_is_gone_from_both_places_is_reported()
    {
        using var dir = new TempDirectory();
        var journal = FileJournal.Begin(dir.Path, "organize", undoable: true);
        PendingStep(journal, dir.Combine("a", "lost.epub"), dir.Combine("b", "lost.epub"));

        var report = JournalRecovery.Recover(dir.Path);

        Assert.Single(report.Problems);
    }

    [Fact]
    public void A_damaged_journal_is_reported_with_its_name()
    {
        using var dir = new TempDirectory();
        Directory.CreateDirectory(dir.Combine(".shelf", "journal"));
        File.WriteAllText(dir.Combine(".shelf", "journal", "20260101T000000.000Z.json"), "{ nope");

        var e = Assert.Throws<JournalException>(() => JournalUndo.Undo(dir.Path));
        Assert.Contains("20260101T000000.000Z.json", e.Message);
    }

    [Fact]
    public void Journals_of_the_same_millisecond_get_distinct_names()
    {
        using var dir = new TempDirectory();
        var clock = new FakeClock();
        var a = FileJournal.Begin(dir.Path, "a", undoable: true, clock);
        var b = FileJournal.Begin(dir.Path, "b", undoable: true, clock);

        Assert.NotEqual(a.Path, b.Path);
        Assert.True(string.CompareOrdinal(a.Path, b.Path) < 0);
    }

    // Simulates a crash between "pending" and the move by writing the pending step through the real format.
    private static void PendingStep(FileJournal journal, string from, string to)
    {
        var root = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(journal.Path)))!;
        var data = JournalFormat.Parse(journal.Path);
        var sha = File.Exists(from) ? LibraryPaths.Hash(from) : "00";
        data.Ops.Add(new JournalOp(JournalOpKind.Move, LibraryPaths.Relative(root, from), LibraryPaths.Relative(root, to), sha, JournalOpStatus.Pending));
        LibraryPaths.WriteAtomic(journal.Path, JournalFormat.Serialize(data));
    }

    private sealed class FakeClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

        public void Advance() => _now = _now.AddSeconds(1);

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
