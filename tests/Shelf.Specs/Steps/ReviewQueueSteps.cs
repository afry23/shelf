using System.Globalization;
using Reqnroll;
using Shelf.Core.Model;
using Shelf.Core.Review;
using Xunit;

namespace Shelf.Specs.Steps;

[Binding]
public sealed class ReviewQueueSteps(ConfigSteps workspace)
{
    private ReviewQueue? _queue;
    private ReviewQueueException? _error;

    [Given("the review queue of {string} gets an entry of the kind {string} about {string} with the message {string}")]
    public void GivenTheQueueGetsAnEntry(string library, string kind, string file, string message) =>
        ReviewQueue.Append(QueuePath(library), new ReviewEntry(Ulid.New(), ReviewQueue.ParseKind(kind), DateTimeOffset.UtcNow, message, file));

    [Given("the review queue of {string} gets a grouping entry with the candidate {string} at {double} because {string}")]
    public void GivenTheQueueGetsAGroupingEntry(string library, string work, double confidence, string signal) =>
        ReviewQueue.Append(
            QueuePath(library),
            new ReviewEntry(
                Ulid.New(),
                ReviewKind.Grouping,
                DateTimeOffset.UtcNow,
                "May be the same work",
                "x.epub",
                Ulid.New(),
                [new ReviewCandidate(Ulid.Parse(work), confidence, [signal])]));

    [When("shelf reads the review queue of {string}")]
    public void WhenShelfReadsTheReviewQueue(string library)
    {
        try
        {
            _queue = ReviewQueue.Load(QueuePath(library));
        }
        catch (ReviewQueueException e)
        {
            _error = e;
        }
    }

    [When("shelf removes entry {int} from the review queue of {string}")]
    public void WhenShelfRemovesEntry(int number, string library)
    {
        var path = QueuePath(library);
        ReviewQueue.Remove(path, ReviewQueue.Load(path).Entries[number - 1].Id);
    }

    [When("shelf removes the review entry {string} from {string}")]
    public void WhenShelfRemovesTheReviewEntry(string id, string library)
    {
        try
        {
            ReviewQueue.Remove(QueuePath(library), Ulid.Parse(id));
        }
        catch (ReviewQueueException e)
        {
            _error = e;
        }
    }

    [Then("the review queue has {int} entry/entries")]
    public void ThenTheQueueHasEntries(int count) => Assert.Equal(count, Queue().Entries.Count);

    [Then("the review queue of {string} has {int} entry/entries")]
    public void ThenTheQueueOfHasEntries(string library, int count) =>
        Assert.Equal(count, ReviewQueue.Load(QueuePath(library)).Entries.Count);

    [Then("review entry {int} has the kind {string}")]
    public void ThenReviewEntryHasTheKind(int number, string kind) =>
        Assert.Equal(ReviewQueue.ParseKind(kind), ReviewQueue.Load(QueuePath("library")).Entries[number - 1].Kind);

    [Then("review entry {int} has a message containing {string}")]
    public void ThenReviewEntryHasAMessageContaining(int number, string text) =>
        Assert.Contains(text, ReviewQueue.Load(QueuePath("library")).Entries[number - 1].Message, StringComparison.Ordinal);

    [Then("the review entries include the kind {string}")]
    public void ThenTheReviewEntriesIncludeTheKind(string kind) =>
        Assert.Contains(ReviewQueue.Load(QueuePath("library")).Entries, e => e.Kind == ReviewQueue.ParseKind(kind));

    [Then("the review queue of {string} still has {int} entries")]
    public void ThenTheQueueStillHasEntries(string library, int count) =>
        Assert.Equal(count, ReviewQueue.Load(QueuePath(library)).Entries.Count);

    [Then("entry {int} has the kind {string}, the file {string} and the message {string}")]
    public void ThenEntryHasKindFileAndMessage(int number, string kind, string file, string message)
    {
        var entry = Queue().Entries[number - 1];
        Assert.Equal(ReviewQueue.ParseKind(kind), entry.Kind);
        Assert.Equal(file, entry.File);
        Assert.Equal(message, entry.Message);
    }

    [Then("entry {int} has the message {string}")]
    public void ThenEntryHasTheMessage(int number, string message) => Assert.Equal(message, Queue().Entries[number - 1].Message);

    [Then("entry {int} has the candidate {string} at {double} because {string}")]
    public void ThenEntryHasTheCandidate(int number, string work, double confidence, string signal)
    {
        var candidate = Assert.Single(Queue().Entries[number - 1].Candidates!);
        Assert.Equal(Ulid.Parse(work), candidate.Work);
        Assert.Equal(confidence, candidate.Confidence, 9);
        Assert.Equal([signal], candidate.Signals);
    }

    [Then("the review queue operation fails with an error containing {string}")]
    public void ThenTheOperationFails(string text)
    {
        Assert.NotNull(_error);
        Assert.Contains(text, _error.Message, StringComparison.Ordinal);
    }

    private ReviewQueue Queue()
    {
        Assert.Null(_error);
        return _queue ?? throw new InvalidOperationException("The queue was not read in this scenario.");
    }

    private string QueuePath(string library) => ReviewQueue.PathFor(Path.Combine(workspace.WorkingDirectory, library));
}
