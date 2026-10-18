using Microsoft.Data.Sqlite;
using Reqnroll;
using Shelf.Index;
using Shelf.Semantic;
using Shelf.TestData;
using Xunit;

namespace Shelf.Specs.Steps;

/// <summary>Steps for the embedding index: a word-hashing fake instead of the model, and a look into the vector tables.</summary>
[Binding]
public sealed class EmbeddingIndexSteps(ScenarioEnvironment environment, ConfigSteps workspace)
{
    private FakeEmbedder Embedder => environment.Embedder ?? throw new InvalidOperationException("No fake embedder in this scenario.");

    [Given("the embedder is a word-hashing fake of {int} dimensions")]
    public void GivenTheEmbedderIsAFake(int dimensions) => environment.Embedder = new FakeEmbedder(dimensions, environment);

    [Given("the model {string} embeds with {int} dimensions")]
    public void GivenTheModelEmbedsWith(string model, int dimensions) =>
        environment.ModelEmbedders[model] = new FakeEmbedder(dimensions, environment, Embedder.Calls);

    [Given("the embedder stops the run after {int} embeddings")]
    public void GivenTheEmbedderStopsTheRunAfter(int count) => Embedder.StopAfter = count;

    [Given("the embedder no longer stops the run")]
    public void GivenTheEmbedderNoLongerStops()
    {
        Embedder.StopAfter = null;
        environment.Interrupt = new CancellationTokenSource();
    }

    [Given("the embedder calls are forgotten")]
    public void GivenTheEmbedderCallsAreForgotten() => Embedder.Calls.Clear();

    [Given("a sample EPUB {string} with the title {string} and one chapter of {int} words")]
    public void GivenASampleEpubWithOneChapter(string name, string title, int words)
    {
        var path = Path.Combine(workspace.WorkingDirectory, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var text = string.Join(' ', Enumerable.Range(1, words).Select(i => $"word{i}"));
        File.WriteAllBytes(path, SampleBooks.Epub(title, ["Some Author"], [("", text)]));
        var sidecar = Path.Combine(Path.GetDirectoryName(path)!, "book.json");
        File.WriteAllText(
            sidecar,
            $$"""{ "schema": 1, "id": "01J9Z3K0000000000000000099", "title": "{{title}}", "authors": ["Some Author"], "files": [{ "name": "{{Path.GetFileName(path)}}" }] }""");
    }

    [Then("the embedder was given the text containing {string}")]
    public void ThenTheEmbedderWasGivenTheTextContaining(string part) =>
        Assert.Contains(Embedder.Calls, c => c.Text.Contains(part, StringComparison.Ordinal));

    [Then("the embedder was not given the text containing {string}")]
    public void ThenTheEmbedderWasNotGivenTheTextContaining(string part) =>
        Assert.DoesNotContain(Embedder.Calls, c => c.Text.Contains(part, StringComparison.Ordinal));

    [Then("the embedder embedded no queries")]
    public void ThenTheEmbedderEmbeddedNoQueries() => Assert.DoesNotContain(Embedder.Calls, c => c.Kind == "query");

    [Then("the embedder was called {int} times")]
    public void ThenTheEmbedderWasCalledTimes(int count) => Assert.Equal(count, Embedder.Calls.Count);

    [Then("the embedder was called fewer than {int} times")]
    public void ThenTheEmbedderWasCalledFewerThan(int count) => Assert.InRange(Embedder.Calls.Count, 1, count - 1);

    [Then("the index holds {int} work vector(s) and {int} passage vector(s)")]
    public void ThenTheIndexHoldsVectors(int works, int passages) => CheckVectors(works, passages, null);

    [Then("the index holds {int} work vector(s) and {int} passage vector(s) of {int} dimensions")]
    public void ThenTheIndexHoldsVectorsOfDimensions(int works, int passages, int dimensions) => CheckVectors(works, passages, dimensions);

    private void CheckVectors(int works, int passages, int? dimensions)
    {
        using var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(Path.Combine(workspace.WorkingDirectory, "library")));
        var store = VectorStore.OpenExisting(connection);
        Assert.Equal(works, store?.Count(VectorKind.Work) ?? 0);
        Assert.Equal(passages, store?.Count(VectorKind.Chunk) ?? 0);
        if (dimensions is { } expected)
        {
            Assert.Equal(expected, store!.Dimensions);
        }

        SqliteConnection.ClearAllPools();
    }
}
