using Microsoft.Data.Sqlite;
using Shelf.Core.Search;
using Xunit;

namespace Shelf.Index.Tests;

public sealed class LibraryBrowseTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly SqliteConnection _connection;
    private int _fileCounter;

    public LibraryBrowseTests()
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

    private void Work(string name, string title, string author, int? year = null, string[]? tags = null, string format = "epub", string? addedAt = null)
    {
        var seq = (long)IndexDatabase.Scalar(_connection, "SELECT COALESCE(MAX(seq), 0) + 1 FROM works")!;
        using (var command = _connection.CreateCommand())
        {
            command.CommandText = """
                INSERT INTO works(seq, id, title, title_norm, authors_json, author_sort, language, year, read_status, dir_path, added_at, updated_at)
                VALUES ($seq, $id, $title, $norm, $authors, $sort, 'en', $year, 'unread', '.', $added, 'x')
                """;
            command.Parameters.AddWithValue("$seq", seq);
            command.Parameters.AddWithValue("$id", Id(name));
            command.Parameters.AddWithValue("$title", title);
            command.Parameters.AddWithValue("$norm", title.ToLowerInvariant());
            command.Parameters.AddWithValue("$authors", $"[\"{author}\"]");

            // The sort key keeps its accents, as in the real index.
            command.Parameters.AddWithValue("$sort", author);
            command.Parameters.AddWithValue("$year", (object?)year ?? DBNull.Value);
            command.Parameters.AddWithValue("$added", (object?)addedAt ?? DBNull.Value);
            command.ExecuteNonQuery();
        }

        IndexDatabase.Execute(_connection, $"INSERT INTO works_fts(rowid, title, subtitle, authors, description, tags) VALUES ({seq}, '{title}', NULL, '{author}', NULL, '{string.Join(' ', tags ?? [])}')");
        foreach (var tag in tags ?? [])
        {
            IndexDatabase.Execute(_connection, $"INSERT OR IGNORE INTO tags(path) VALUES ('{tag}')");
            IndexDatabase.Execute(_connection, $"INSERT INTO work_tags(work_id, tag_id) SELECT '{Id(name)}', id FROM tags WHERE path = '{tag}'");
        }

        var fileId = ++_fileCounter;
        IndexDatabase.Execute(_connection, $"INSERT INTO files(id, work_id, format, rel_path, size, mtime) VALUES ({fileId}, '{Id(name)}', '{format}', 'w/{fileId}.{format}', 1, 1)");
    }

    private BrowsePage Browse(string query = "", BrowseOptions? options = null) =>
        LibraryBrowse.Browse(_connection, QueryParser.Parse(query), options ?? new BrowseOptions());

    private static string[] Titles(BrowsePage page) => [.. page.Items.Select(i => i.Title)];

    [Fact]
    public void An_author_with_an_accent_sorts_with_the_letter_it_starts_with()
    {
        Work("A", "Zebra", "Zimmer, Hans");
        Work("B", "Alpha", "Ålander, Jördis");
        Work("C", "Beta", "Berg, Anna");

        Assert.Equal(["Alpha", "Beta", "Zebra"], Titles(Browse(options: new BrowseOptions(WorkSort.Author))));
        Assert.Equal(["Zebra", "Beta", "Alpha"], Titles(Browse(options: new BrowseOptions(WorkSort.Author, Descending: true))));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_work_without_a_year_comes_last_in_either_direction(bool descending)
    {
        Work("A", "Undated", "Ann");
        Work("B", "Old", "Bob", year: 1999);
        Work("C", "New", "Cy", year: 2020);

        var titles = Titles(Browse(options: new BrowseOptions(WorkSort.Year, descending)));

        Assert.Equal("Undated", titles[^1]);
        Assert.Equal(descending ? ["New", "Old", "Undated"] : ["Old", "New", "Undated"], titles);
    }

    [Fact]
    public void Equal_keys_are_ordered_by_title_and_then_by_ID_so_that_pages_do_not_overlap()
    {
        Work("B", "Same", "Ann", year: 2000);
        Work("A", "Same", "Ann", year: 2000);
        Work("C", "Same", "Ann", year: 2000);

        var first = Browse(options: new BrowseOptions(WorkSort.Year, Limit: 2));
        var second = Browse(options: new BrowseOptions(WorkSort.Year, Offset: 2, Limit: 2));

        Assert.Equal([Id("A"), Id("B")], first.Items.Select(i => i.Id.ToString()));
        Assert.Equal([Id("C")], second.Items.Select(i => i.Id.ToString()));
    }

    [Fact]
    public void A_page_beyond_the_end_is_empty_but_the_total_and_the_facets_stay()
    {
        Work("A", "One", "Ann", tags: ["x"]);

        var page = Browse(options: new BrowseOptions(Offset: 10));

        Assert.Empty(page.Items);
        Assert.Equal(1, page.Total);
        Assert.Equal([new FacetCount("x", 1)], page.Facets.Tags);
    }

    [Fact]
    public void A_work_with_several_files_of_one_format_counts_once_in_the_format_facet()
    {
        Work("A", "One", "Ann", format: "pdf");
        IndexDatabase.Execute(_connection, $"INSERT INTO files(id, work_id, format, rel_path, size, mtime) VALUES (99, '{Id("A")}', 'pdf', 'w/99.pdf', 1, 1)");

        var page = Browse();

        Assert.Equal([new FacetCount("pdf", 1)], page.Facets.Formats);
        Assert.Equal(["pdf"], page.Items[0].Formats);
    }

    [Fact]
    public void The_tag_and_author_facets_are_cut_at_the_limit_and_the_most_used_come_first()
    {
        for (var i = 0; i < LibraryBrowse.FacetLimit + 10; i++)
        {
            Work($"W{i:D3}", $"Title {i:D3}", $"Author {i:D3}", tags: [$"tag-{i:D3}", "common"]);
        }

        var page = Browse(options: new BrowseOptions(Limit: 1));

        Assert.Equal(LibraryBrowse.FacetLimit, page.Facets.Tags.Count);
        Assert.Equal(new FacetCount("common", LibraryBrowse.FacetLimit + 10), page.Facets.Tags[0]);
        Assert.Equal(LibraryBrowse.FacetLimit, page.Facets.Authors.Count);
    }

    [Fact]
    public void An_exclusion_removes_works_and_the_facets_follow()
    {
        Work("A", "Keep", "Ann", tags: ["a"]);
        Work("B", "Drop", "Bob", tags: ["b"]);

        var page = Browse("-tag:b");

        Assert.Equal(["Keep"], Titles(page));
        Assert.Equal([new FacetCount("a", 1)], page.Facets.Tags);
    }

    [Fact]
    public void Words_are_refused_because_a_list_does_not_rank()
    {
        Work("A", "One", "Ann");

        Assert.Throws<ArgumentException>(() => Browse("kubernetes"));
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(0, 0)]
    public void A_negative_offset_and_an_empty_page_are_refused(int offset, int limit) =>
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => Browse(options: new BrowseOptions(Offset: offset, Limit: limit)));
}
