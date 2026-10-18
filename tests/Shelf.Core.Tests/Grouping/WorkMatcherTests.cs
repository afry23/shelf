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

    [Theory]
    [InlineData(2002, 2012, null, null, true)]
    [InlineData(2002, 2002, "9780321127426", "9780321200686", true)]
    [InlineData(2002, 2002, null, null, false)]
    [InlineData(null, 2002, null, null, false)]
    public void A_match_with_another_year_or_ISBN_is_another_edition(int? known, int? incoming, string? knownIsbn, string? incomingIsbn, bool edition)
    {
        var matcher = new WorkMatcher(GroupingSettings.Default, [new WorkCandidate(Id(1), Sig("Patterns", "Martin Fowler", known, knownIsbn))]);

        var decision = matcher.Decide(File("Patterns", "Martin Fowler", incoming, incomingIsbn));

        var match = Assert.Single(decision.Matches);
        Assert.Equal(edition, match.IsOtherEdition);
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

    [Fact]
    public void Different_ISBNs_make_a_title_and_author_match_a_review_case_not_a_merge()
    {
        var matcher = new WorkMatcher(GroupingSettings.Default, [new WorkCandidate(Id(1), Sig("Dune", "Frank Herbert", null, "9780306406157"))]);

        var decision = matcher.Decide(File("Dune", "Frank Herbert", null, "9780000000019"));

        Assert.Equal(GroupingAction.CreateNewWork, decision.Action);
        var match = Assert.Single(decision.Matches);
        Assert.Equal(0.6, match.Confidence);
        Assert.Contains("ISBN differs", match.Signals);
    }

    [Fact]
    public void A_missing_ISBN_on_one_side_is_no_conflict()
    {
        var matcher = new WorkMatcher(GroupingSettings.Default, [new WorkCandidate(Id(1), Sig("Dune", "Frank Herbert", null, "9780306406157"))]);

        Assert.Equal(Id(1), matcher.Decide(File("Dune", "Frank Herbert")).WorkId);
    }

    [Fact]
    public void Clearly_different_texts_make_a_title_and_author_match_a_review_case()
    {
        var matcher = new WorkMatcher(GroupingSettings.Default, [new WorkCandidate(Id(1), Sig("Dune", "Frank Herbert"), [0UL])]);

        var far = matcher.Decide(File("Dune", "Frank Herbert", fingerprint: 0xFFFFUL));   // 16 bits away, above max_hamming
        var near = matcher.Decide(File("Dune", "Frank Herbert", fingerprint: 0b11111UL)); // 5 bits away, still the same text

        Assert.Equal(GroupingAction.CreateNewWork, far.Action);
        Assert.Contains("texts differ", far.Matches[0].Signals);
        Assert.Equal(Id(1), near.WorkId);
    }

    [Fact]
    public void The_same_ISBN_is_trusted_even_if_the_texts_differ()
    {
        var matcher = new WorkMatcher(GroupingSettings.Default, [new WorkCandidate(Id(1), Sig("Dune", "Frank Herbert", null, "9780306406157"), [0UL])]);

        var decision = matcher.Decide(File("Other", "Someone", null, "9780306406157", 0xFFFFFFFFUL));

        Assert.Equal(Id(1), decision.WorkId);
        Assert.Equal(1.0, decision.Confidence);
    }

    [Fact]
    public void A_review_entry_names_at_most_five_candidates_the_best_first()
    {
        var candidates = Enumerable.Range(1, 8).Select(n => new WorkCandidate(Id(n), Sig("Dune", "Frank Herbert", 1950 + n))).ToList();
        var matcher = new WorkMatcher(GroupingSettings.Default, candidates);

        var decision = matcher.Decide(File("Dune", "Frank Herbert", 1990));

        Assert.Equal(WorkMatcher.MaxReviewCandidates, decision.Matches.Count);
        Assert.Equal([Id(1), Id(2), Id(3), Id(4), Id(5)], decision.Matches.Select(m => m.WorkId));
    }
}
