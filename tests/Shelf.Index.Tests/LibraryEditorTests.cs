using Shelf.Core.Model;
using Shelf.Core.Review;
using Xunit;

namespace Shelf.Index.Tests;

public sealed class LibraryEditorTests : IDisposable
{
    private const string First = "01J9Z3K1000000000000000001";
    private const string Second = "01J9Z3K2000000000000000002";
    private const string Third = "01J9Z3K3000000000000000003";

    private readonly TempDirectory _temp = new();

    public LibraryEditorTests()
    {
        Directory.CreateDirectory(_temp.Combine(".shelf"));
        IndexDatabase.Create(IndexDatabase.PathFor(_temp.Path));
    }

    public void Dispose() => _temp.Dispose();

    private void Work(string folder, string id, string title, IReadOnlyList<string> tags, params (string Name, string Fixture)[] files)
    {
        var directory = _temp.Combine(folder);
        Directory.CreateDirectory(directory);
        foreach (var (name, fixture) in files)
        {
            File.Copy(TestFiles.Fixture(fixture), Path.Combine(directory, name));
        }

        SidecarStore.Write(Path.Combine(directory, SidecarStore.FileName), new WorkSidecar
        {
            Id = Ulid.Parse(id),
            Title = title,
            Authors = ["Ada Lovelace"],
            Tags = tags,
            Files = [.. files.Select(f => new SidecarFile(f.Name))],
        });
    }

    private LibraryWork Resolve(string reference) => WorkLookup.Resolve(_temp.Path, reference, _temp.Path);

    private void Reindex() => LibraryReindexer.Rebuild(_temp.Path);

    private const string Epub = "derived/epub3-isbn-series.epub";
    private const string Pdf = "derived/Event Streams in Practice.pdf";

    // ---- WorkLookup ----

    [Fact]
    public void Resolve_finds_a_work_by_a_unique_start_of_its_ID_in_any_case()
    {
        Work("a/one", First, "One", [], ("one.epub", Epub));
        Work("b/two", Second, "Two", [], ("two.pdf", Pdf));
        Reindex();

        Assert.Equal(Ulid.Parse(First), Resolve("01j9z3k1").Id);
        Assert.Equal(Ulid.Parse(Second), Resolve(Second).Id);
    }

    [Fact]
    public void Resolve_reports_a_start_that_fits_several_works_and_one_that_fits_none()
    {
        Work("a/one", First, "One", [], ("one.epub", Epub));
        Work("b/two", Second, "Two", [], ("two.pdf", Pdf));
        Reindex();

        Assert.Equal("'01J9Z3K' matches 2 works; use more of the ID", Assert.Throws<LibraryEditException>(() => Resolve("01J9Z3K")).Message);
        Assert.Equal("no work matches '01J9Z3K9'", Assert.Throws<LibraryEditException>(() => Resolve("01J9Z3K9")).Message);
    }

    [Theory]
    [InlineData("a/one")]
    [InlineData("a/one/")]
    [InlineData("a/one/one.epub")]
    public void Resolve_finds_a_work_by_the_path_of_its_folder_or_file_relative_to_the_library_root(string path)
    {
        Work("a/one", First, "One", [], ("one.epub", Epub));
        Reindex();

        Assert.Equal("a/one", WorkLookup.Resolve(_temp.Path, path, Path.GetTempPath()).Directory);
    }

    [Fact]
    public void Resolve_prefers_the_working_directory_for_paths_and_knows_dot()
    {
        Work("a/one", First, "One", [], ("one.epub", Epub));
        Reindex();

        Assert.Equal(Ulid.Parse(First), WorkLookup.Resolve(_temp.Path, ".", _temp.Combine("a", "one")).Id);
        Assert.Throws<LibraryEditException>(() => WorkLookup.Resolve(_temp.Path, ".", _temp.Path));
    }

    [Theory]
    [InlineData("../elsewhere")]
    [InlineData("")]
    [InlineData("a")]
    public void Resolve_rejects_paths_that_are_no_work(string path)
    {
        Work("a/one", First, "One", [], ("one.epub", Epub));
        Reindex();

        Assert.Throws<LibraryEditException>(() => WorkLookup.Resolve(_temp.Path, path, _temp.Path));
    }

    [Fact]
    public void Labels_add_the_year_and_skip_unknown_works()
    {
        Work("a/one", First, "One", [], ("one.epub", Epub));
        Reindex();

        var labels = WorkLookup.Labels(_temp.Path, [Ulid.Parse(First), Ulid.Parse(Third)]);

        Assert.Equal("One", labels[Ulid.Parse(First)]);
        Assert.False(labels.ContainsKey(Ulid.Parse(Third)));
    }

    [Fact]
    public void Labels_are_empty_without_an_index()
    {
        File.Delete(IndexDatabase.PathFor(_temp.Path));

        Assert.Empty(WorkLookup.Labels(_temp.Path, [Ulid.Parse(First)]));
    }

    // ---- Merge ----

    [Fact]
    public void Merge_moves_files_renames_collisions_and_unites_the_marks()
    {
        Work("a/one", First, "One", ["devops"], ("book.epub", Epub));
        Work("b/two", Second, "Two", ["books", "devops"], ("book.epub", Epub), ("two.pdf", Pdf));
        Reindex();
        var path = Path.Combine(_temp.Path, "b", "two", SidecarStore.FileName);
        SidecarStore.Write(path, SidecarStore.Read(path) with
        {
            NotDuplicates = new NotDuplicates([Ulid.Parse(First), Ulid.Parse(Third)], [new FilePair("book.epub", "two.pdf")]),
        });

        var result = LibraryEditor.Merge(_temp.Path, Resolve(First), Resolve(Second));

        Assert.Equal(2, result.MovedFiles);
        Assert.Null(result.LeftoverFolder);
        var merged = SidecarStore.Read(_temp.Combine("a", "one", SidecarStore.FileName));
        Assert.Equal(["book.epub", "book (1).epub", "two.pdf"], merged.Files.Select(f => f.Name));
        Assert.Equal(["books", "devops"], merged.Tags);
        Assert.Equal([Ulid.Parse(Third)], merged.NotDuplicates.Works);
        Assert.Equal([new FilePair("book (1).epub", "two.pdf")], merged.NotDuplicates.Files);
        Assert.False(Directory.Exists(_temp.Combine("b")));
    }

    [Fact]
    public void Merge_keeps_a_folder_that_holds_files_shelf_does_not_know()
    {
        Work("a/one", First, "One", [], ("one.epub", Epub));
        Work("b/two", Second, "Two", [], ("two.pdf", Pdf));
        File.WriteAllText(_temp.Combine("b", "two", "notes.txt"), "mine");
        Reindex();

        var result = LibraryEditor.Merge(_temp.Path, Resolve(First), Resolve(Second));

        Assert.Equal("b/two", result.LeftoverFolder);
        Assert.True(File.Exists(_temp.Combine("b", "two", "notes.txt")));
        Assert.False(File.Exists(_temp.Combine("b", "two", SidecarStore.FileName)));
    }

    [Fact]
    public void Merge_rejects_a_work_with_itself_before_changing_anything()
    {
        Work("a/one", First, "One", [], ("one.epub", Epub));
        Reindex();
        var work = Resolve(First);

        Assert.Equal("cannot merge a work into itself", Assert.Throws<LibraryEditException>(() => LibraryEditor.Merge(_temp.Path, work, work)).Message);
        Assert.True(File.Exists(_temp.Combine("a", "one", "one.epub")));
    }

    [Fact]
    public void Merge_follows_the_review_queue_and_settles_entries_about_the_pair()
    {
        Work("a/one", First, "One", [], ("one.epub", Epub));
        Work("b/two", Second, "Two", [], ("two.pdf", Pdf));
        Work("c/three", Third, "Three", [], ("three.epub", Epub));
        Reindex();
        var queuePath = ReviewQueue.PathFor(_temp.Path);
        var settled = new ReviewEntry(Ulid.New(), ReviewKind.Grouping, DateTimeOffset.UtcNow, "m", "b/two/two.pdf", Ulid.Parse(Second), [new ReviewCandidate(Ulid.Parse(First), 0.6, ["x"])]);
        var kept = new ReviewEntry(Ulid.New(), ReviewKind.Grouping, DateTimeOffset.UtcNow, "m", "b/two/two.pdf", Ulid.Parse(Second), [new ReviewCandidate(Ulid.Parse(First), 0.6, ["x"]), new ReviewCandidate(Ulid.Parse(Third), 0.7, ["y"])]);
        var error = new ReviewEntry(Ulid.New(), ReviewKind.ExtractionError, DateTimeOffset.UtcNow, "bad", "gone/broken.epub");
        new ReviewQueue([settled, kept, error]).Save(queuePath);

        LibraryEditor.Merge(_temp.Path, Resolve(First), Resolve(Second));

        var queue = ReviewQueue.Load(queuePath);
        Assert.Equal([kept.Id, error.Id], queue.Entries.Select(e => e.Id));
        Assert.Equal(Ulid.Parse(First), queue.Entries[0].Work);
        Assert.Equal([Ulid.Parse(Third)], queue.Entries[0].Candidates!.Select(c => c.Work));
        Assert.Equal("a/one/two.pdf", queue.Entries[0].File);
        Assert.Equal("gone/broken.epub", queue.Entries[1].File);
    }

    // ---- Split ----

    [Fact]
    public void Split_makes_a_work_from_a_file_and_marks_the_two_apart()
    {
        Work("a/one", First, "One", ["devops"], ("one.epub", Epub), ("two.pdf", Pdf));
        Reindex();

        var result = LibraryEditor.Split(_temp.Path, Resolve(First), "two.pdf", _temp.Path);

        Assert.Equal("two.pdf", result.File);
        var created = SidecarStore.Read(_temp.Combine(result.Folder, SidecarStore.FileName));
        Assert.Equal(result.NewWork, created.Id);
        Assert.Equal(["two.pdf"], created.Files.Select(f => f.Name));
        Assert.Equal([Ulid.Parse(First)], created.NotDuplicates.Works);
        Assert.Empty(created.Tags);
        var remaining = SidecarStore.Read(_temp.Combine("a", "one", SidecarStore.FileName));
        Assert.Equal(["one.epub"], remaining.Files.Select(f => f.Name));
        Assert.Equal([result.NewWork], remaining.NotDuplicates.Works);
        Assert.True(File.Exists(_temp.Combine(result.Folder, "two.pdf")));
    }

    [Fact]
    public void Split_accepts_the_path_of_the_file()
    {
        Work("a/one", First, "One", [], ("one.epub", Epub), ("two.pdf", Pdf));
        Reindex();

        var result = LibraryEditor.Split(_temp.Path, Resolve(First), "a/one/two.pdf", _temp.Path);

        Assert.Equal("two.pdf", result.File);
    }

    [Theory]
    [InlineData("missing.pdf", "has no file 'missing.pdf'")]
    [InlineData("other/two.pdf", "has no file 'other/two.pdf'")]
    public void Split_rejects_a_file_the_work_does_not_have(string file, string message)
    {
        Work("a/one", First, "One", [], ("one.epub", Epub), ("two.pdf", Pdf));
        Reindex();

        Assert.Contains(message, Assert.Throws<LibraryEditException>(() => LibraryEditor.Split(_temp.Path, Resolve(First), file, _temp.Path)).Message, StringComparison.Ordinal);
        Assert.Equal(["one.epub", "two.pdf"], SidecarStore.Read(_temp.Combine("a", "one", SidecarStore.FileName)).Files.Select(f => f.Name));
    }

    [Fact]
    public void Split_rejects_the_only_file_of_a_work()
    {
        Work("a/one", First, "One", [], ("one.epub", Epub));
        Reindex();

        Assert.Contains("has only one file", Assert.Throws<LibraryEditException>(() => LibraryEditor.Split(_temp.Path, Resolve(First), "one.epub", _temp.Path)).Message, StringComparison.Ordinal);
    }

    // ---- other edits ----

    [Fact]
    public void MarkNotDuplicates_writes_both_sidecars_once()
    {
        Work("a/one", First, "One", [], ("one.epub", Epub));
        Work("b/two", Second, "Two", [], ("two.pdf", Pdf));
        Reindex();

        LibraryEditor.MarkNotDuplicates(_temp.Path, Resolve(First), Resolve(Second));
        LibraryEditor.MarkNotDuplicates(_temp.Path, Resolve(Second), Resolve(First));

        Assert.Equal([Ulid.Parse(Second)], SidecarStore.Read(_temp.Combine("a", "one", SidecarStore.FileName)).NotDuplicates.Works);
        Assert.Equal([Ulid.Parse(First)], SidecarStore.Read(_temp.Combine("b", "two", SidecarStore.FileName)).NotDuplicates.Works);
    }

    [Fact]
    public void Update_writes_the_sidecar_and_the_index_follows()
    {
        Work("a/one", First, "One", [], ("one.epub", Epub));
        Reindex();

        LibraryEditor.Update(_temp.Path, Resolve(First), s => s with { Title = "Uno" });

        Assert.Equal("Uno", SidecarStore.Read(_temp.Combine("a", "one", SidecarStore.FileName)).Title);
        Assert.Equal("Uno", Resolve(First).Title);
    }

    [Fact]
    public void Update_leaves_the_sidecar_alone_when_the_change_is_invalid()
    {
        Work("a/one", First, "One", [], ("one.epub", Epub));
        Reindex();

        Assert.Throws<SidecarException>(() => LibraryEditor.Update(_temp.Path, Resolve(First), s => s with { Title = " " }));

        Assert.Equal("One", SidecarStore.Read(_temp.Combine("a", "one", SidecarStore.FileName)).Title);
    }

    [Fact]
    public void RemoveReviewEntry_updates_the_file_and_the_copy_in_the_index()
    {
        Work("a/one", First, "One", [], ("one.epub", Epub));
        Reindex();
        var entry = new ReviewEntry(Ulid.New(), ReviewKind.WeakMetadata, DateTimeOffset.UtcNow, "weak", "a/one/one.epub");
        ReviewQueue.Append(ReviewQueue.PathFor(_temp.Path), entry);

        LibraryEditor.RemoveReviewEntry(_temp.Path, entry.Id);

        Assert.Empty(ReviewQueue.Load(ReviewQueue.PathFor(_temp.Path)).Entries);
        using var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(_temp.Path));
        Assert.Equal(0L, IndexDatabase.Scalar(connection, "SELECT COUNT(*) FROM review_queue"));
    }

    // ---- statistics ----

    [Fact]
    public void Statistics_count_works_files_formats_authors_and_tags()
    {
        Work("a/one", First, "One", ["devops", "books"], ("one.epub", Epub), ("one.pdf", Pdf));
        Work("b/two", Second, "Two", ["devops"], ("two.epub", Epub));
        Reindex();

        using var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(_temp.Path));
        var stats = LibraryStatistics.Load(connection);

        Assert.Equal(2, stats.Works);
        Assert.Equal(3, stats.Files);
        Assert.Equal(1, stats.Authors);
        Assert.Equal(2, stats.Tags);
        Assert.Equal(2, stats.Unread);
        Assert.Equal(0, stats.Read);
        Assert.Equal([("epub", 2), ("pdf", 1)], stats.Formats.Select(f => (f.Format, f.Files)));
        Assert.True(stats.Bytes > 0);
        Assert.Empty(stats.Languages);
    }
}
