using Shelf.Core.Model;
using Shelf.Core.Review;
using Xunit;

namespace Shelf.Core.Tests.Review;

public sealed class ReviewQueueTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("shelf-review-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private string Path => System.IO.Path.Combine(_directory, "review.json");

    private static ReviewEntry Entry(string message, ReviewKind kind = ReviewKind.WeakMetadata) =>
        new(Ulid.New(), kind, new DateTimeOffset(2026, 10, 17, 12, 30, 5, TimeSpan.FromHours(2)), message, "a/b.pdf");

    [Fact]
    public void An_entry_with_all_fields_survives_a_save_and_load()
    {
        var entry = new ReviewEntry(
            Ulid.New(),
            ReviewKind.Grouping,
            new DateTimeOffset(2026, 10, 17, 12, 30, 5, TimeSpan.Zero),
            "Möglicherweise \"dasselbe\" Werk",
            "Dune/dune.epub",
            Ulid.New(),
            [new ReviewCandidate(Ulid.New(), 0.6, ["title and author match, year differs", "text fingerprint, distance 5"])]);
        var queue = new ReviewQueue([entry]);

        queue.Save(Path);
        var loaded = ReviewQueue.Load(Path);

        var read = Assert.Single(loaded.Entries);
        Assert.Equal(entry.Id, read.Id);
        Assert.Equal(entry.Kind, read.Kind);
        Assert.Equal(entry.CreatedAt, read.CreatedAt);
        Assert.Equal(entry.Message, read.Message);
        Assert.Equal(entry.File, read.File);
        Assert.Equal(entry.Work, read.Work);
        Assert.Equal(entry.Candidates![0].Signals, read.Candidates![0].Signals);
        Assert.Equal(0.6, read.Candidates[0].Confidence);
        Assert.Equal(loaded.Serialize(), queue.Serialize());
    }

    [Fact]
    public void The_file_has_the_empty_queue_text_of_a_new_library()
    {
        new ReviewQueue().Save(Path);

        Assert.Equal(Shelf.Core.Libraries.LibraryInitializer.EmptyReviewQueue, File.ReadAllText(Path));
    }

    [Fact]
    public void A_missing_file_is_an_empty_queue() =>
        Assert.Empty(ReviewQueue.Load(System.IO.Path.Combine(_directory, "nowhere", "review.json")).Entries);

    [Fact]
    public void Append_and_remove_work_on_the_file()
    {
        var first = Entry("first");
        var second = Entry("second");

        ReviewQueue.Append(Path, first);
        ReviewQueue.Append(Path, second);
        ReviewQueue.Remove(Path, first.Id);

        Assert.Equal(["second"], ReviewQueue.Load(Path).Entries.Select(e => e.Message));
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [Fact]
    public void Kind_names_round_trip()
    {
        foreach (var kind in Enum.GetValues<ReviewKind>())
        {
            Assert.Equal(kind, ReviewQueue.ParseKind(ReviewQueue.KindName(kind)));
        }
    }

    [Fact]
    public void A_damaged_file_is_not_replaced_by_an_append()
    {
        File.WriteAllText(Path, "{ broken");

        Assert.Throws<ReviewQueueException>(() => ReviewQueue.Append(Path, Entry("x")));

        Assert.Equal("{ broken", File.ReadAllText(Path));
    }

    [Fact]
    public void The_payload_of_one_entry_is_a_single_line()
    {
        var json = ReviewQueue.EntryToJson(Entry("m"));

        Assert.DoesNotContain('\n', json);
        Assert.StartsWith("{\"id\":", json, StringComparison.Ordinal);
    }
}
