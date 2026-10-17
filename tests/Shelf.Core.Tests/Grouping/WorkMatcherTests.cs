using Shelf.Core.Configuration;
using Shelf.Core.Grouping;
using Shelf.Core.Model;
using Xunit;

namespace Shelf.Core.Tests.Grouping;

public sealed class WorkMatcherTests
{
    private static Ulid Id(long n) => Ulid.Create(n, new byte[10]);

    private static WorkSignature Sig(string title, string author, int? year = null, string? isbn = null) =>
        WorkSignature.Create(title, [author], year, isbn);

    private static IncomingFile File(string title, string author, int? year = null, string? isbn = null, ulong? fingerprint = null) =>
        new("f.epub", Sig(title, author, year, isbn), fingerprint);

    [Fact]
    public void Two_works_with_the_same_ISBN_are_ambiguous_and_never_merged()
    {
        var matcher = new WorkMatcher(GroupingSettings.Default, [
            new WorkCandidate(Id(1), Sig("A", "X Y", null, "9780000000019")),
            new WorkCandidate(Id(2), Sig("B", "Z Y", null, "9780000000019")),
        ]);

        var decision = matcher.Decide(File("C", "Q R", null, "9780000000019"));

        Assert.Equal(GroupingAction.CreateNewWork, decision.Action);
        Assert.Equal([Id(1), Id(2)], decision.Matches.Select(m => m.WorkId));
    }

    [Fact]
    public void Signals_of_every_stage_are_collected_for_the_same_work()
    {
        var matcher = new WorkMatcher(GroupingSettings.Default, [
            new WorkCandidate(Id(1), Sig("Dune", "Frank Herbert", 1965), [0b1111UL]),
        ]);

        var decision = matcher.Decide(File("Dune", "F. Herbert", 1970, fingerprint: 0b1110UL));

        // Title and author match with another year (0.6), the fingerprint is 1 bit away (0.9): merged, both reasons named.
        Assert.Equal(GroupingAction.MergeIntoExisting, decision.Action);
        var match = Assert.Single(decision.Matches);
        Assert.Equal(0.9, match.Confidence);
        Assert.Contains("title and author match, year differs", match.Signals);
        Assert.Contains("text fingerprint, distance 1", match.Signals);
    }

    [Fact]
    public void Review_matches_are_sorted_by_confidence_then_work()
    {
        var matcher = new WorkMatcher(GroupingSettings.Default, [
            new WorkCandidate(Id(3), Sig("Dune", "Frank Herbert", 1984)),
            new WorkCandidate(Id(2), Sig("Dune", "Frank Herbert", 1965)),
            new WorkCandidate(Id(1), Sig("Other", "X Y"), [0UL]),
        ]);

        var decision = matcher.Decide(File("Dune", "Frank Herbert", 1950, fingerprint: 0b11111UL));

        Assert.Equal(GroupingAction.CreateNewWork, decision.Action);
        Assert.Equal([Id(1), Id(2), Id(3)], decision.Matches.Select(m => m.WorkId));
        Assert.All(decision.Matches, m => Assert.Equal(0.6, m.Confidence));
    }

    [Fact]
    public void A_fingerprint_added_after_a_merge_attracts_the_next_file()
    {
        var matcher = new WorkMatcher(GroupingSettings.Default, [new WorkCandidate(Id(1), Sig("Dune", "Frank Herbert"))]);
        matcher.AddFingerprint(Id(1), 0xFFUL);

        var decision = matcher.Decide(File("Unknown", "Nobody", fingerprint: 0xFFUL));

        Assert.Equal(Id(1), decision.WorkId);
    }

    [Fact]
    public void A_file_without_any_signal_is_a_new_work_without_candidates()
    {
        var matcher = new WorkMatcher(GroupingSettings.Default, [new WorkCandidate(Id(1), Sig("Dune", "Frank Herbert"))]);

        var decision = matcher.Decide(new IncomingFile("f.epub", WorkSignature.Create(null, [], null, null), null));

        Assert.Equal(GroupingAction.CreateNewWork, decision.Action);
        Assert.Empty(decision.Matches);
        Assert.Equal(0, decision.Confidence);
    }
}
