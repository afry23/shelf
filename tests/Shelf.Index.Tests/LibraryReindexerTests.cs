using Microsoft.Data.Sqlite;
using Shelf.Core.Model;
using Xunit;

namespace Shelf.Index.Tests;

public sealed class LibraryReindexerTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private void Work(string folder, string id, string title, params string[] files)
    {
        var directory = folder == "." ? _temp.Path : _temp.Combine(folder);
        Directory.CreateDirectory(directory);
        foreach (var file in files)
        {
            File.WriteAllText(Path.Combine(directory, file), "data");
        }

        SidecarStore.Write(
            Path.Combine(directory, SidecarStore.FileName),
            new WorkSidecar
            {
                Id = Ulid.Parse(id),
                Title = title,
                Authors = ["Ada Lovelace"],
                Files = [.. files.Select(f => new SidecarFile(f))],
            });
    }

    private static long Count(SqliteConnection connection, string sql) => (long)IndexDatabase.Scalar(connection, sql)!;

    [Fact]
    public void A_sidecar_in_the_library_root_gives_paths_without_a_folder_prefix()
    {
        Directory.CreateDirectory(_temp.Combine(".shelf"));
        Work(".", "01J9Z3K0000000000000000001", "Root work", "a.epub");

        var result = LibraryReindexer.Rebuild(_temp.Path, includeContent: false);

        Assert.Equal(1, result.Works);
        using var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(_temp.Path));
        Assert.Equal("a.epub", IndexDatabase.Scalar(connection, "SELECT rel_path FROM files"));
        Assert.Equal(".", IndexDatabase.Scalar(connection, "SELECT dir_path FROM works"));
    }

    [Fact]
    public void Hidden_folders_such_as_dot_git_and_dot_shelf_are_not_searched()
    {
        Directory.CreateDirectory(_temp.Combine(".shelf"));
        Work("A/Visible", "01J9Z3K0000000000000000001", "Visible", "v.epub");
        Work(".git/Hidden", "01J9Z3K0000000000000000002", "Hidden", "h.epub");

        var result = LibraryReindexer.Rebuild(_temp.Path, includeContent: false);

        Assert.Equal(1, result.Works);
        Assert.Empty(result.Problems);
    }

    [Fact]
    public void A_file_that_is_not_a_database_is_replaced()
    {
        Directory.CreateDirectory(_temp.Combine(".shelf"));
        File.WriteAllText(IndexDatabase.PathFor(_temp.Path), "this is not sqlite, but it is long enough to look like a header....");
        Work("A/B", "01J9Z3K0000000000000000001", "T", "t.epub");

        var result = LibraryReindexer.Rebuild(_temp.Path, includeContent: false);

        Assert.Equal(1, result.Works);
        using var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(_temp.Path));
        Assert.Equal(1L, Count(connection, "SELECT COUNT(*) FROM works"));
    }

    [Fact]
    public void A_work_whose_files_are_all_missing_is_not_indexed_but_each_missing_file_is_reported()
    {
        Directory.CreateDirectory(_temp.Combine(".shelf"));
        Work("A/B", "01J9Z3K0000000000000000001", "T", "t.epub", "t.pdf");
        File.Delete(_temp.Combine("A", "B", "t.epub"));
        File.Delete(_temp.Combine("A", "B", "t.pdf"));

        var result = LibraryReindexer.Rebuild(_temp.Path, includeContent: false);

        Assert.Equal(0, result.Works);
        Assert.Equal(2, result.Problems.Count);
    }

    [Fact]
    public void Not_duplicate_pairs_are_stored_once_in_a_fixed_order()
    {
        Directory.CreateDirectory(_temp.Combine(".shelf"));
        var directory = _temp.Combine("A", "B");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "a.epub"), "x");
        File.WriteAllText(Path.Combine(directory, "b.epub"), "x");
        SidecarStore.Write(
            Path.Combine(directory, SidecarStore.FileName),
            new WorkSidecar
            {
                Id = Ulid.Parse("01J9Z3K0000000000000000001"),
                Title = "T",
                Authors = ["A"],
                Files = [new SidecarFile("a.epub"), new SidecarFile("b.epub")],
                NotDuplicates = new NotDuplicates([], [new FilePair("b.epub", "a.epub"), new FilePair("a.epub", "b.epub")]),
            });

        LibraryReindexer.Rebuild(_temp.Path, includeContent: false);

        using var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(_temp.Path));
        Assert.Equal(1L, Count(connection, "SELECT COUNT(*) FROM dupe_ignores"));
        Assert.Equal("A/B/a.epub", IndexDatabase.Scalar(connection, "SELECT a_id FROM dupe_ignores"));
    }

    [Fact]
    public void A_failed_rebuild_leaves_the_old_index_in_place()
    {
        Directory.CreateDirectory(_temp.Combine(".shelf"));
        Work("A/B", "01J9Z3K0000000000000000001", "First", "t.epub");
        LibraryReindexer.Rebuild(_temp.Path, includeContent: false);

        // A reader transaction cannot block a WAL writer, so force a failure inside the transaction instead:
        // a second writer holds the lock for longer than the 5 s busy timeout.
        using (var other = IndexDatabase.OpenWrite(IndexDatabase.PathFor(_temp.Path)))
        {
            IndexDatabase.Execute(other, "BEGIN IMMEDIATE");
            Assert.Throws<SqliteException>(() => LibraryReindexer.Rebuild(_temp.Path, includeContent: false));
            IndexDatabase.Execute(other, "ROLLBACK");
        }

        using var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(_temp.Path));
        Assert.Equal("First", IndexDatabase.Scalar(connection, "SELECT title FROM works"));
    }
}
