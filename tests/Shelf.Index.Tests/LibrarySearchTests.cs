using Microsoft.Data.Sqlite;
using Shelf.Core.Search;
using Xunit;

namespace Shelf.Index.Tests;

public sealed class LibrarySearchTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly SqliteConnection _connection;
    private int _fileCounter;

    public LibrarySearchTests()
    {
        var path = _temp.Combine("index.db");
        IndexDatabase.Create(path);
        _connection = IndexDatabase.OpenWrite(path);
    }

    public void Dispose()
    {
        _connection.Dispose();
        _temp.Dispose();
    }

    // The index holds ULIDs: "A" becomes 25 zeros and an A.
    private static string Id(string name) => name.PadLeft(26, '0');

    private void Work(string name, string title, string authors, string? description = null, string tags = "", int? year = null, string language = "en", string status = "unread")
    {
        var seq = (long)IndexDatabase.Scalar(_connection, "SELECT COALESCE(MAX(seq), 0) + 1 FROM works")!;
        using (var command = _connection.CreateCommand())
        {
            command.CommandText = """
                INSERT INTO works(seq, id, title, title_norm, authors_json, author_sort, language, year, description, read_status, dir_path, updated_at)
                VALUES ($seq, $id, $title, $norm, $authors, $sort, $language, $year, $description, $status, '.', 'x')
                """;
            command.Parameters.AddWithValue("$seq", seq);
            command.Parameters.AddWithValue("$id", Id(name));
            command.Parameters.AddWithValue("$title", title);
            command.Parameters.AddWithValue("$norm", title.ToLowerInvariant());
            command.Parameters.AddWithValue("$authors", $"[\"{authors}\"]");
            command.Parameters.AddWithValue("$sort", authors);
            command.Parameters.AddWithValue("$language", language);
            command.Parameters.AddWithValue("$year", (object?)year ?? DBNull.Value);
            command.Parameters.AddWithValue("$description", (object?)description ?? DBNull.Value);
            command.Parameters.AddWithValue("$status", status);
            command.ExecuteNonQuery();
        }

        using (var command = _connection.CreateCommand())
        {
            command.CommandText = "INSERT INTO works_fts(rowid, title, subtitle, authors, description, tags) VALUES ($seq, $title, NULL, $authors, $description, $tags)";
            command.Parameters.AddWithValue("$seq", seq);
            command.Parameters.AddWithValue("$title", title);
            command.Parameters.AddWithValue("$authors", authors);
            command.Parameters.AddWithValue("$description", (object?)description ?? DBNull.Value);
            command.Parameters.AddWithValue("$tags", tags);
            command.ExecuteNonQuery();
        }
    }

    private void Text(string work, string format, string locator, string text)
    {
        var fileId = ++_fileCounter;
        IndexDatabase.Execute(_connection, $"INSERT INTO files(id, work_id, format, rel_path, size, mtime) VALUES ({fileId}, '{Id(work)}', '{format}', 'w/{fileId}.{format}', 1, 1)");
        using var command = _connection.CreateCommand();
        command.CommandText = "INSERT INTO chunks(file_id, ordinal, locator, text) VALUES ($file, 0, $locator, $text)";
        command.Parameters.AddWithValue("$file", fileId);
        command.Parameters.AddWithValue("$locator", locator);
        command.Parameters.AddWithValue("$text", text);
        command.ExecuteNonQuery();
    }

    private List<string> Titles(string query, int limit = 20) =>
        [.. LibrarySearch.Search(_connection, QueryParser.Parse(query), limit).Select(h => h.Title)];

    [Fact]
    public void A_word_in_the_title_ranks_above_the_same_word_in_the_description()
    {
        Work("A", "Other Subject", "Ann", description: "kubernetes kubernetes kubernetes everywhere");
        Work("B", "Kubernetes Patterns", "Bob");

        Assert.Equal(["Kubernetes Patterns", "Other Subject"], Titles("kubernetes"));
    }

    [Fact]
    public void An_author_hit_ranks_above_a_tag_hit()
    {
        Work("A", "One", "Ann", tags: "smith");
        Work("B", "Two", "Smith");

        Assert.Equal(["Two", "One"], Titles("smith"));
    }

    [Fact]
    public void A_title_hit_ranks_above_a_hit_in_the_text()
    {
        Work("A", "No Match In Metadata", "Ann");
        Text("A", "epub", "epubcfi(/6/2[c]!)", "this passage mentions the zebra");
        Work("B", "Zebra Handbook", "Bob");

        Assert.Equal(["Zebra Handbook", "No Match In Metadata"], Titles("zebra"));
    }

    [Fact]
    public void A_work_with_both_a_metadata_hit_and_a_text_hit_ranks_first()
    {
        Work("A", "Zebra Handbook", "Ann");
        Text("A", "pdf", "page=3", "a zebra crosses the road");
        Work("B", "Zebra Facts", "Bob");

        Assert.Equal(["Zebra Handbook", "Zebra Facts"], Titles("zebra"));
    }

    [Fact]
    public void Words_in_different_sources_do_not_combine()
    {
        Work("A", "Zebra Handbook", "Ann");
        Text("A", "pdf", "page=1", "the giraffe is tall");

        Assert.Empty(Titles("zebra giraffe"));
    }

    [Fact]
    public void Hits_are_limited_per_work_and_carry_snippet_and_location()
    {
        Work("A", "Book", "Ann");
        for (var page = 1; page <= 5; page++)
        {
            Text("A", "pdf", $"page={page}", $"on page {page} the zebra appears with other words around it");
        }

        var hit = Assert.Single(LibrarySearch.Search(_connection, QueryParser.Parse("zebra"), hitsPerWork: 2));

        Assert.Equal(2, hit.Hits.Count);
        Assert.All(hit.Hits, h => Assert.Contains("[zebra]", h.Snippet, StringComparison.Ordinal));
        Assert.All(hit.Hits, h => Assert.StartsWith("page ", h.Where, StringComparison.Ordinal));
        Assert.Equal(["pdf"], hit.Formats);
    }

    [Fact]
    public void Zero_hits_per_work_gives_works_without_snippets()
    {
        Work("A", "Book", "Ann");
        Text("A", "pdf", "page=1", "the zebra");

        var hit = Assert.Single(LibrarySearch.Search(_connection, QueryParser.Parse("zebra"), hitsPerWork: 0));

        Assert.Empty(hit.Hits);
    }

    [Fact]
    public void A_negated_word_removes_a_work_that_has_it_only_in_the_text()
    {
        Work("A", "Zebra Handbook", "Ann");
        Text("A", "pdf", "page=1", "also a giraffe");
        Work("B", "Zebra Facts", "Bob");

        Assert.Equal(["Zebra Facts"], Titles("zebra -giraffe"));
    }

    [Fact]
    public void Only_negated_words_list_the_works_without_them_in_author_order()
    {
        Work("A", "Two", "Zed");
        Work("B", "One", "Abe");
        Work("C", "Skipped", "Cy", description: "unwanted");

        Assert.Equal(["One", "Two"], Titles("-unwanted"));
    }

    [Fact]
    public void A_negated_year_keeps_works_without_a_year()
    {
        Work("A", "Dated", "Ann", year: 2015);
        Work("B", "Undated", "Bob");

        Assert.Equal(["Undated"], Titles("-year:2015"));
        Assert.DoesNotContain("Undated", Titles("year:2015..2016"));
    }

    [Fact]
    public void A_language_filter_includes_regional_variants()
    {
        Work("A", "British", "Ann", language: "en-GB");
        Work("B", "German", "Bob", language: "de");

        Assert.Equal(["British"], Titles("lang:en"));
        Assert.Equal(["British"], Titles("lang:EN-gb"));
        Assert.Equal(["German"], Titles("-lang:en"));
    }

    [Fact]
    public void A_status_filter_and_its_negation()
    {
        Work("A", "Read", "Ann", status: "read");
        Work("B", "Fresh", "Bob");

        Assert.Equal(["Read"], Titles("status:read"));
        Assert.Equal(["Fresh"], Titles("-status:read"));
    }

    [Fact]
    public void Quotes_and_other_characters_in_a_term_cannot_break_the_query()
    {
        Work("A", "Say \"Hello\" Often", "Ann");

        foreach (var text in new[] { "\"", "a\"b", "'", "(", ")", "NEAR(", "x OR", "*x", "a:b:c", "\\", "%", "_", "--", "a--b", "[", "{", "}" })
        {
            var exception = Record.Exception(() => Titles(text));
            Assert.Null(exception);
        }
    }

    [Fact]
    public void The_limit_applies_to_works()
    {
        for (var i = 0; i < 5; i++)
        {
            Work($"W{i}", $"Zebra {i}", "Ann");
        }

        Assert.Equal(3, Titles("zebra", limit: 3).Count);
    }

    [Fact]
    public void Details_of_a_hit_come_from_the_work()
    {
        Work("A", "Zebra Handbook", "Ann Smith", year: 2020, language: "de");

        var hit = Assert.Single(LibrarySearch.Search(_connection, QueryParser.Parse("zebra")));

        Assert.Equal(Id("A"), hit.WorkId.ToString());
        Assert.Equal(["Ann Smith"], hit.Authors);
        Assert.Equal(2020, hit.Year);
        Assert.Equal("de", hit.Language);
    }
}
