using Xunit;

namespace Shelf.Organize.Tests;

public class JournalRetentionTests
{
    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

        public void Advance() => _now = _now.AddSeconds(1);

        public override DateTimeOffset GetUtcNow() => _now;
    }

    private static List<string> Runs(string root) =>
        [.. Directory.EnumerateFiles(Path.Combine(root, ".shelf", "journal"), "*.json").Select(p => Path.GetFileName(p)!).Order(StringComparer.Ordinal)];

    [Fact]
    public void Only_the_newest_journals_are_kept_with_their_backups()
    {
        using var dir = new TempDirectory();
        var clock = new Clock();
        var path = dir.Write("a/book.json", "v0");
        for (var i = 1; i <= JournalRetention.DefaultKeep + 5; i++)
        {
            var journal = FileJournal.Begin(dir.Path, $"run {i}", undoable: true, clock);
            journal.Replace(path, System.Text.Encoding.UTF8.GetBytes($"v{i}"));
            journal.Finish();
            clock.Advance();
        }

        // Begin keeps the newest 50 journals and then creates its own: 51 in all, as after every run since the 51st.
        var next = FileJournal.Begin(dir.Path, "last", undoable: true, clock);
        next.Finish();

        var runs = Runs(dir.Path);
        Assert.Equal(JournalRetention.DefaultKeep + 1, runs.Count);
        Assert.Equal(runs.Count - 1, Directory.EnumerateDirectories(Path.Combine(dir.Path, ".shelf", "journal"), "*.backup").Count());
        Assert.All(Directory.EnumerateDirectories(Path.Combine(dir.Path, ".shelf", "journal"), "*.backup"), backup =>
            Assert.Contains(Path.GetFileNameWithoutExtension(backup) + ".json", runs));
    }

    [Fact]
    public void The_last_runs_can_still_be_undone_after_pruning()
    {
        using var dir = new TempDirectory();
        var clock = new Clock();
        var path = dir.Write("a/book.json", "v0");
        for (var i = 1; i <= JournalRetention.DefaultKeep + 3; i++)
        {
            var journal = FileJournal.Begin(dir.Path, $"run {i}", undoable: true, clock);
            journal.Replace(path, System.Text.Encoding.UTF8.GetBytes($"v{i}"));
            journal.Finish();
            clock.Advance();
        }

        var undone = JournalUndo.Undo(dir.Path)!;

        Assert.Empty(undone.Skipped);
        Assert.Equal($"v{JournalRetention.DefaultKeep + 2}", File.ReadAllText(path));
    }

    [Fact]
    public void A_running_journal_is_never_deleted()
    {
        using var dir = new TempDirectory();
        var journal = FileJournal.Begin(dir.Path, "running", undoable: true);

        var removed = JournalRetention.Prune(dir.Path, keep: 1);

        Assert.Equal(0, removed);
        Assert.True(File.Exists(journal.Path));
    }

    [Fact]
    public void Nothing_happens_without_a_journal_folder()
    {
        using var dir = new TempDirectory();

        Assert.Equal(0, JournalRetention.Prune(dir.Path));
    }
}
