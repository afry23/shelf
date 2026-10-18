using Reqnroll;
using Shelf.Semantic;
using Xunit;

namespace Shelf.Specs.Steps;

/// <summary>
/// Steps for the embedding features. The real model (about 470 MB) is not in the repository: the scenarios use the
/// folder named by <c>SHELF_E5_DIR</c> and are skipped when it is not set.
/// </summary>
[Binding]
public sealed class SemanticSteps(ConfigSteps workspace) : IDisposable
{
    private const string ModelDirVariable = "SHELF_E5_DIR";

    private static readonly Lazy<E5Embedder?> SharedEmbedder = new(() => FindModel() is { } folder ? E5Embedder.Open(folder) : null);
    private static readonly Lazy<E5Tokenizer?> SharedTokenizer = new(() => FindModel() is { } folder ? E5Tokenizer.Load(folder.TokenizerPath) : null);

    private IEmbedder? _embedder;
    private E5Tokenizer? _tokenizer;
    private string? _emptyDirectory;
    private int[] _ids = [];
    private float[] _vector = [];
    private float[] _otherVector = [];
    private string? _closest;
    private string? _error;

    [Given("the model files of {string} are available")]
    public void GivenTheModelFilesAreAvailable(string name)
    {
        _tokenizer = SharedTokenizer.Value;
        Assert.SkipWhen(_tokenizer is null, $"set {ModelDirVariable} to a folder with the files of {name}");
    }

    [Given("the model {string} is installed")]
    public void GivenTheModelIsInstalled(string name)
    {
        _embedder = SharedEmbedder.Value;
        Assert.SkipWhen(_embedder is null, $"set {ModelDirVariable} to a folder with the files of {name}");
    }

    /// <summary>Links the folder of <c>SHELF_E5_DIR</c> into the library, so that the real model is "installed" without a copy of 470 MB.</summary>
    [Given("the real embedding model is installed in the library {string}")]
    public void GivenTheRealModelIsInstalledInTheLibrary(string library)
    {
        var source = FindModel();
        Assert.SkipWhen(source is null, $"set {ModelDirVariable} to a folder with the files of multilingual-e5-small");
        var models = Path.Combine(workspace.WorkingDirectory, library, ".shelf", "models");
        Directory.CreateDirectory(models);
        Directory.CreateSymbolicLink(Path.Combine(models, "multilingual-e5-small"), source!.Directory);
    }

    [Given("the model folder is empty")]
    public void GivenTheModelFolderIsEmpty()
    {
        _emptyDirectory = Directory.CreateTempSubdirectory("shelf-model-").FullName;
    }

    [When("the text {string} is tokenized")]
    public void WhenTheTextIsTokenized(string text) => _ids = _tokenizer!.Encode(text);

    [When("the text of {int} repeated words is tokenized")]
    public void WhenTheTextOfRepeatedWordsIsTokenized(int words) =>
        _ids = _tokenizer!.Encode(string.Join(' ', Enumerable.Repeat("the", words)));

    [Then("the token IDs are {string}")]
    public void ThenTheTokenIdsAre(string ids) =>
        Assert.Equal(ids.Split(' ').Select(int.Parse), _ids);

    [Then("the token IDs number {int}")]
    public void ThenTheTokenIdsNumber(int count) => Assert.Equal(count, _ids.Length);

    [Then("the last token ID is {int}")]
    public void ThenTheLastTokenIdIs(int id) => Assert.Equal(id, _ids[^1]);

    [When("the passage {string} is embedded twice")]
    public void WhenThePassageIsEmbeddedTwice(string text)
    {
        _vector = _embedder!.EmbedPassage(text);
        _otherVector = _embedder.EmbedPassage(text);
    }

    [When("the {word} {string} is embedded")]
    public void WhenTheKindIsEmbedded(string kind, string text)
    {
        if (_emptyDirectory is not null)
        {
            try
            {
                using var _ = E5Embedder.Open(new ModelFolder(_emptyDirectory));
            }
            catch (ModelNotInstalledException ex)
            {
                _error = ex.Message;
            }

            return;
        }

        _vector = kind == "query" ? _embedder!.EmbedQuery(text) : _embedder!.EmbedPassage(text);
    }

    [Then("the vector equals the embedding of the raw text {string}")]
    public void ThenTheVectorEqualsTheEmbeddingOfTheRawText(string raw) =>
        Assert.Equal(((E5Embedder)_embedder!).EmbedRaw(raw), _vector);

    [Then("the vector has {int} dimensions")]
    public void ThenTheVectorHasDimensions(int dimensions) => Assert.Equal(dimensions, _vector.Length);

    [Then("the vector has a length of {int} within {double}")]
    public void ThenTheVectorHasALengthOf(int length, double tolerance) =>
        Assert.InRange(Math.Sqrt(_vector.Sum(v => (double)v * v)), length - tolerance, length + tolerance);

    [Then("both vectors are equal")]
    public void ThenBothVectorsAreEqual() => Assert.Equal(_vector, _otherVector);

    [When("the query {string} is compared with these passages:")]
    public void WhenTheQueryIsComparedWith(string query, DataTable passages)
    {
        var q = _embedder!.EmbedQuery(query);
        _closest = passages.Rows
            .Select(r => r["passage"])
            .MaxBy(p => _embedder.EmbedPassage(p).Zip(q, (a, b) => (double)a * b).Sum());
    }

    [Then("the closest passage is {string}")]
    public void ThenTheClosestPassageIs(string passage) => Assert.Equal(passage, _closest);

    [Then("embedding fails with the message {string}")]
    public void ThenEmbeddingFailsWithTheMessage(string message) =>
        Assert.StartsWith(message, _error);

    public void Dispose()
    {
        if (_emptyDirectory is not null)
        {
            Directory.Delete(_emptyDirectory, recursive: true);
        }
    }

    private static ModelFolder? FindModel()
    {
        var path = Environment.GetEnvironmentVariable(ModelDirVariable);
        return path is not null && new ModelFolder(path) is { IsInstalled: true } folder ? folder : null;
    }
}
