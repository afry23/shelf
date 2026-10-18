using System.Globalization;
using Microsoft.Data.Sqlite;
using Reqnroll;
using Shelf.Index;
using Shelf.Semantic;
using Xunit;

namespace Shelf.Specs.Steps;

/// <summary>Steps for the vector store (F-SEM-4): an index in a temporary library, opened and closed by the scenario.</summary>
[Binding]
public sealed class VectorSteps : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("shelf-vectors-").FullName;
    private int _dimensions;
    private string _model = string.Empty;
    private IVectorExtension _extension = SqliteVecExtension.Default;
    private SqliteConnection? _connection;
    private VectorStore? _store;
    private IReadOnlyList<VectorHit> _hits = [];
    private Exception? _error;

    [Given("an index for vectors of {int} dimensions and the model {string}")]
    public void GivenAnIndexForVectors(int dimensions, string model)
    {
        _dimensions = dimensions;
        _model = model;
        Directory.CreateDirectory(Path.Combine(_root, ".shelf"));
        IndexDatabase.Create(IndexDatabase.PathFor(_root));
    }

    [Given("the {word} backend")]
    public void GivenTheBackend(string backend)
    {
        if (backend == "sqlite-vec")
        {
            Assert.SkipUnless(File.Exists(SqliteVecExtension.LibraryPath), "run tools/fetch-sqlite-vec.sh and build again");
            _extension = SqliteVecExtension.Default;
            Open();
        }
        else
        {
            _extension = NoVectorExtension.Instance;
            Open();
            _extension = SqliteVecExtension.Default;
        }
    }

    [Given("the sqlite-vec library cannot be loaded")]
    public void GivenTheLibraryCannotBeLoaded() => _extension = NoVectorExtension.Instance;

    [When("the index is prepared")]
    public void WhenTheIndexIsPrepared() => Open();

    [Given("these work vectors are stored:")]
    public void GivenTheseWorkVectorsAreStored(DataTable vectors) => Store(VectorKind.Work, vectors);

    [Given("these chunk vectors are stored:")]
    public void GivenTheseChunkVectorsAreStored(DataTable vectors) => Store(VectorKind.Chunk, vectors);

    [When("the chunk vector {int} is deleted")]
    public void WhenTheChunkVectorIsDeleted(int id) => _store!.Delete(VectorKind.Chunk, id);

    [When("the {int} nearest {word} vectors to {string} are requested")]
    public void WhenTheNearestAreRequested(int k, string kind, string query) =>
        _hits = _store!.Nearest(KindOf(kind), Parse(query), k);

    [When("a work vector with the values {string} is stored")]
    public void WhenAWorkVectorIsStored(string values)
    {
        try
        {
            _store!.Put(VectorKind.Work, 1, Parse(values));
        }
        catch (ArgumentException e)
        {
            _error = e;
        }
    }

    [When("the index is closed and opened again")]
    public void WhenTheIndexIsClosedAndOpenedAgain() => Open();

    [When("the index is opened again")]
    public void WhenTheIndexIsOpenedAgain()
    {
        try
        {
            Open();
        }
        catch (VectorStoreException e)
        {
            _error = e;
        }
    }

    [When("the index is opened again for the model {string}")]
    public void WhenTheIndexIsOpenedAgainForTheModel(string model)
    {
        _model = model;
        Open();
    }

    [When("the index of the library is rebuilt with reindex")]
    public void WhenTheIndexIsRebuilt()
    {
        CloseConnection();
        LibraryReindexer.Rebuild(_root, includeContent: false, vectorExtension: _extension);
    }

    [Then("the hits are:")]
    public void ThenTheHitsAre(DataTable expected)
    {
        Assert.Equal(expected.RowCount, _hits.Count);
        for (var i = 0; i < _hits.Count; i++)
        {
            Assert.Equal(long.Parse(expected.Rows[i]["id"], CultureInfo.InvariantCulture), _hits[i].Id);
            Assert.Equal(double.Parse(expected.Rows[i]["distance"], CultureInfo.InvariantCulture), _hits[i].Distance, 3);
        }
    }

    [Then("there are {int} hits")]
    public void ThenThereAreHits(int count) => Assert.Equal(count, _hits.Count);

    [Then("the best hit has a distance of {double}")]
    public void ThenTheBestHitHasADistanceOf(double distance) => Assert.Equal(distance, _hits[0].Distance, 3);

    [Then("there are {int} work vectors and {int} chunk vectors")]
    public void ThenThereAreVectors(int works, int chunks)
    {
        Assert.Equal(works, _store!.Count(VectorKind.Work));
        Assert.Equal(chunks, _store.Count(VectorKind.Chunk));
    }

    [Then("storing fails with the message {string}")]
    public void ThenStoringFailsWithTheMessage(string message) =>
        Assert.Contains(message, Assert.IsAssignableFrom<ArgumentException>(_error).Message);

    [Then("opening fails with the message {string}")]
    public void ThenOpeningFailsWithTheMessage(string message) =>
        Assert.Contains(message, Assert.IsType<VectorStoreException>(_error).Message);

    [Then("the backend is {word}")]
    public void ThenTheBackendIs(string backend) =>
        Assert.Equal(backend == "sqlite-vec" ? VectorBackend.SqliteVec : VectorBackend.Plain, _store!.Backend);

    [Then("the stored model is {string}")]
    public void ThenTheStoredModelIs(string model) => Assert.Equal(model, _store!.StoredModel);

    [Then("the model is the same")]
    public void ThenTheModelIsTheSame() => Assert.True(_store!.IsCurrentModel);

    [Then("the model is different")]
    public void ThenTheModelIsDifferent() => Assert.False(_store!.IsCurrentModel);

    [Then("the index has no vector tables")]
    public void ThenTheIndexHasNoVectorTables()
    {
        using var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(_root));
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM sqlite_master WHERE name LIKE 'vec\\_%' ESCAPE '\\' OR name = 'semantic_meta'";
        Assert.Equal(0L, command.ExecuteScalar());
    }

    public void Dispose()
    {
        CloseConnection();
        SqliteConnection.ClearAllPools();
        Directory.Delete(_root, recursive: true);
    }

    private void Open()
    {
        CloseConnection();
        _connection = IndexDatabase.OpenWrite(IndexDatabase.PathFor(_root));
        _store = VectorStore.Open(_connection, _dimensions, _model, _extension);
    }

    private void CloseConnection()
    {
        _store = null;
        _connection?.Dispose();
        _connection = null;
    }

    private void Store(VectorKind kind, DataTable vectors)
    {
        foreach (var row in vectors.Rows)
        {
            _store!.Put(kind, long.Parse(row["id"], CultureInfo.InvariantCulture), Parse(row["vector"]));
        }
    }

    private static VectorKind KindOf(string kind) => kind == "work" ? VectorKind.Work : VectorKind.Chunk;

    private static float[] Parse(string values) =>
        [.. values.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(v => float.Parse(v, CultureInfo.InvariantCulture))];
}
