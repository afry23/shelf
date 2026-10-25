using System.Globalization;
using Microsoft.Data.Sqlite;
using Reqnroll;
using Shelf.Index;
using Xunit;

namespace Shelf.Specs.Steps;

/// <summary>Looks into the index database of the scenario's library (the folder "library").</summary>
[Binding]
public sealed class IndexSteps(ConfigSteps workspace)
{
    private const string Library = "library";

    [Given("the index has the schema version {int}")]
    public void GivenTheIndexHasTheSchemaVersion(int version)
    {
        using var connection = new SqliteConnection($"Data Source={IndexPath()};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA user_version = {version.ToString(CultureInfo.InvariantCulture)}";
        command.ExecuteNonQuery();
    }

    [Then("the table {string} of the index has {int} rows")]
    public void ThenTheTableHasRows(string table, int rows)
    {
        Assert.Matches("^[a-z_]+$", table);
        Assert.Equal((long)rows, Scalar($"SELECT COUNT(*) FROM {table}"));
    }

    [Then("the table {string} of the index has at least {int} rows")]
    public void ThenTheTableHasAtLeastRows(string table, int rows)
    {
        Assert.Matches("^[a-z_]+$", table);
        Assert.True((long)Scalar($"SELECT COUNT(*) FROM {table}")! >= rows, $"{table} has fewer than {rows} rows");
    }

    [Then("the chunk index finds the word {string}")]
    public void ThenTheChunkIndexFindsTheWord(string word) =>
        Assert.True((long)Scalar("SELECT COUNT(*) FROM chunks_fts WHERE chunks_fts MATCH $v", word)! > 0, $"'{word}' is not in the chunk index");

    [Then("the file {string} in the index has the text status {string}")]
    public void ThenTheFileHasTheTextStatus(string name, string status) =>
        Assert.Equal(status, Scalar("SELECT text_status FROM files WHERE rel_path = $v OR rel_path LIKE '%/' || $v", name));

    [Then("the file {string} in the index has a hash and a fingerprint")]
    public void ThenTheFileHasAHashAndAFingerprint(string name)
    {
        Assert.Equal(64, ((string?)Scalar("SELECT sha256 FROM files WHERE rel_path = $v OR rel_path LIKE '%/' || $v", name))?.Length);
        Assert.NotNull(Scalar("SELECT simhash FROM files WHERE rel_path = $v OR rel_path LIKE '%/' || $v", name));
    }

    [Then("the file {string} in the index has the path {string}")]
    public void ThenTheFileHasThePath(string name, string path) =>
        Assert.Equal(path, Scalar("SELECT rel_path FROM files WHERE rel_path LIKE '%/' || $v OR rel_path = $v", name));

    [Then("the work {string} in the index has the title {string}")]
    public void ThenTheWorkHasTheTitle(string id, string title) =>
        Assert.Equal(title, Scalar("SELECT title FROM works WHERE id = $v", id));

    [Then("the work {string} in the index has the publisher {string}")]
    public void ThenTheWorkHasThePublisher(string id, string publisher) =>
        Assert.Equal(publisher, Scalar("SELECT publisher FROM works WHERE id = $v", id));

    [Then("the work {string} in the index has no publisher")]
    public void ThenTheWorkHasNoPublisher(string id) =>
        Assert.True(Scalar("SELECT publisher FROM works WHERE id = $v", id) is null or DBNull);

    [Then("the work {string} in the index has the normalized title {string} and the author sort key {string}")]
    public void ThenTheWorkHasTheNormalizedTitleAndSortKey(string id, string title, string sortKey)
    {
        Assert.Equal(title, Scalar("SELECT title_norm FROM works WHERE id = $v", id));
        Assert.Equal(sortKey, Scalar("SELECT author_sort FROM works WHERE id = $v", id));
    }

    [Then("the work {string} in the index has the tags {string}")]
    public void ThenTheWorkHasTheTags(string id, string tags)
    {
        var actual = Column(
            "SELECT t.path FROM work_tags wt JOIN tags t ON t.id = wt.tag_id WHERE wt.work_id = $v ORDER BY t.path",
            id);
        Assert.Equal(tags.Length == 0 ? [] : tags.Split(", "), actual);
    }

    [Then("the work {string} in the index has the rating {int} and the status {string}")]
    public void ThenTheWorkHasTheRatingAndStatus(string id, int rating, string status)
    {
        Assert.Equal((long)rating, Scalar("SELECT rating FROM works WHERE id = $v", id));
        Assert.Equal(status, Scalar("SELECT read_status FROM works WHERE id = $v", id));
    }

    [Then("the file {string} in the index has the progress {string}")]
    public void ThenTheFileHasTheProgress(string name, string locator) =>
        Assert.Equal(
            locator,
            Scalar("SELECT p.locator FROM reading_progress p JOIN files f ON f.id = p.file_id WHERE f.rel_path LIKE '%/' || $v", name));

    [Then("the full-text index finds the work {string} for the query {string}")]
    public void ThenTheFullTextIndexFindsTheWork(string id, string query) =>
        Assert.Equal(
            id,
            Scalar("SELECT w.id FROM works_fts JOIN works w ON w.seq = works_fts.rowid WHERE works_fts MATCH $v", query));

    [Then("the index database is in WAL mode")]
    public void ThenTheIndexDatabaseIsInWalMode() =>
        Assert.Equal("wal", Scalar("PRAGMA journal_mode"));

    private string IndexPath() => IndexDatabase.PathFor(Path.Combine(workspace.WorkingDirectory, Library));

    private object? Scalar(string sql, string? value = null)
    {
        using var connection = IndexDatabase.OpenRead(IndexPath());
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        if (value is not null)
        {
            command.Parameters.AddWithValue("$v", value);
        }

        return command.ExecuteScalar();
    }

    private List<string> Column(string sql, string value)
    {
        using var connection = IndexDatabase.OpenRead(IndexPath());
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$v", value);
        using var reader = command.ExecuteReader();
        var values = new List<string>();
        while (reader.Read())
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }
}
