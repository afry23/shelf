using Microsoft.Data.Sqlite;
using Shelf.Core.Model;
using Xunit;

namespace Shelf.Index.Tests;

public sealed class ScannerTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public ScannerTests()
    {
        Directory.CreateDirectory(_temp.Combine(".shelf"));
        IndexDatabase.Create(IndexDatabase.PathFor(_temp.Path));
    }

    public void Dispose() => _temp.Dispose();

    private string Work(string folder, string id, string title, params (string Name, string Fixture)[] files)
    {
        var directory = _temp.Combine(folder);
        Directory.CreateDirectory(directory);
        foreach (var (name, fixture) in files)
        {
            File.Copy(TestFiles.Fixture(fixture), Path.Combine(directory, name));
        }

        var path = Path.Combine(directory, SidecarStore.FileName);
        SidecarStore.Write(path, new WorkSidecar
        {
            Id = Ulid.Parse(id),
            Title = title,
            Authors = ["Ada Lovelace"],
            Files = [.. files.Select(f => new SidecarFile(f.Name))],
        });
        return path;
    }

    private long Count(string sql)
    {
        using var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(_temp.Path));
        return (long)IndexDatabase.Scalar(connection, sql)!;
    }

    private object? Scalar(string sql)
    {
        using var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(_temp.Path));
        return IndexDatabase.Scalar(connection, sql);
    }

    private const string Epub = "derived/epub3-isbn-series.epub";

    [Fact]
    public void A_writer_can_run_several_transactions_one_after_the_other()
    {
        // Regression: a prepared command keeps the transaction it was created in; the second one must still work.
        Work("A/One", "01J9Z3K0000000000000000001", "One", ("a.epub", Epub));
        Work("A/Two", "01J9Z3K0000000000000000002", "Two", ("b.epub", Epub));

        var result = LibraryReindexer.Rebuild(_temp.Path);

        Assert.Equal(2, result.Works);
        Assert.Empty(result.Problems);
        Assert.Equal(2, Count("SELECT COUNT(*) FROM files WHERE sha256 IS NOT NULL"));
    }

    [Fact]
    public void An_ID_changed_by_hand_replaces_the_work_of_that_folder()
    {
        var sidecar = Work("A/One", "01J9Z3K0000000000000000001", "One", ("a.epub", Epub));
        LibraryReindexer.Rebuild(_temp.Path);
        File.WriteAllText(sidecar, File.ReadAllText(sidecar).Replace("01J9Z3K0000000000000000001", "01J9Z3K0000000000000000009", StringComparison.Ordinal));

        var result = LibraryScanner.Scan(_temp.Path);

        Assert.Equal(1, result.NewWorks);
        Assert.Equal(1, result.RemovedWorks);
        Assert.Equal(1, Count("SELECT COUNT(*) FROM works"));
        Assert.Equal(1, Count("SELECT COUNT(*) FROM works WHERE id = '01J9Z3K0000000000000000009'"));
        Assert.Equal(1, Count("SELECT COUNT(*) FROM files"));
    }

    [Fact]
    public void The_same_work_ID_in_two_folders_is_reported_and_only_the_first_is_indexed()
    {
        Work("A/One", "01J9Z3K0000000000000000001", "One", ("a.epub", Epub));
        Work("B/Copy", "01J9Z3K0000000000000000001", "Copy", ("b.epub", Epub));
        LibraryReindexer.Rebuild(_temp.Path, includeContent: false);

        var result = LibraryScanner.Scan(_temp.Path);

        Assert.Equal(1, result.Works);
        var problem = Assert.Single(result.Problems);
        Assert.Contains("duplicate work ID 01J9Z3K0000000000000000001", problem.Message, StringComparison.Ordinal);
        Assert.Equal("One", (string?)Scalar("SELECT title FROM works"));
    }

    [Fact]
    public void A_file_that_comes_back_is_indexed_again_without_changing_the_sidecar()
    {
        var sidecar = Work("A/One", "01J9Z3K0000000000000000001", "One", ("a.epub", Epub));
        LibraryReindexer.Rebuild(_temp.Path);
        var text = File.ReadAllText(sidecar);
        File.Delete(_temp.Combine("A", "One", "a.epub"));
        Assert.Equal(1, LibraryScanner.Scan(_temp.Path).RemovedFiles);

        File.Copy(TestFiles.Fixture(Epub), _temp.Combine("A", "One", "a.epub"));
        var result = LibraryScanner.Scan(_temp.Path);

        Assert.Equal(1, result.NewFiles);
        Assert.Equal(1, Count("SELECT COUNT(*) FROM files WHERE sha256 IS NOT NULL"));
        Assert.Equal(text, File.ReadAllText(sidecar));
    }

    [Fact]
    public void A_second_scan_after_a_change_finds_nothing_more()
    {
        Work("A/One", "01J9Z3K0000000000000000001", "One", ("a.epub", Epub));
        LibraryReindexer.Rebuild(_temp.Path);
        File.Copy(TestFiles.Fixture("derived/epub2-isbn-cover.epub"), _temp.Combine("A", "One", "extra.epub"));
        Assert.Equal(1, LibraryScanner.Scan(_temp.Path).NewFiles);

        var again = LibraryScanner.Scan(_temp.Path);

        Assert.Equal(0, again.NewFiles + again.ChangedFiles + again.RemovedFiles + again.UpdatedWorks);
        Assert.Equal(2, again.UnchangedFiles);
    }

    [Fact]
    public void Files_in_hidden_folders_are_not_untracked()
    {
        Work("A/One", "01J9Z3K0000000000000000001", "One", ("a.epub", Epub));
        LibraryReindexer.Rebuild(_temp.Path);
        Directory.CreateDirectory(_temp.Combine(".git"));
        File.Copy(TestFiles.Fixture(Epub), _temp.Combine(".git", "x.epub"));

        Assert.Empty(LibraryScanner.Scan(_temp.Path).Untracked);
    }

    [Fact]
    public void The_scan_needs_an_index()
    {
        File.Delete(IndexDatabase.PathFor(_temp.Path));

        Assert.Throws<IndexException>(() => LibraryScanner.Scan(_temp.Path));
    }

    [Fact]
    public void A_database_error_in_the_middle_leaves_no_half_written_work()
    {
        Work("A/One", "01J9Z3K0000000000000000001", "One", ("a.epub", Epub));
        LibraryReindexer.Rebuild(_temp.Path);
        using var blocker = IndexDatabase.OpenWrite(IndexDatabase.PathFor(_temp.Path));
        IndexDatabase.Execute(blocker, "BEGIN IMMEDIATE");
        Work("A/Two", "01J9Z3K0000000000000000002", "Two", ("b.epub", Epub));

        Assert.Throws<SqliteException>(() => LibraryScanner.Scan(_temp.Path));
        IndexDatabase.Execute(blocker, "ROLLBACK");

        Assert.Equal(1, Count("SELECT COUNT(*) FROM works"));
    }
}
