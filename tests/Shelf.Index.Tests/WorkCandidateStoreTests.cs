using Shelf.Core.Grouping;
using Shelf.Core.Model;
using Xunit;

namespace Shelf.Index.Tests;

public sealed class WorkCandidateStoreTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Works_come_back_with_surnames_year_isbn_and_the_fingerprints_of_all_their_files()
    {
        var path = _temp.Combine("index.db");
        IndexDatabase.Create(path);
        using var connection = IndexDatabase.OpenWrite(path);
        IndexDatabase.Execute(connection, "INSERT INTO works(id, title, title_norm, authors_json, author_sort, year, isbn13, read_status, dir_path, updated_at) VALUES ('01J9Z3K0000000000000000001', 'The Hobbit', 'hobbit', '[\"J. R. R. Tolkien\",\"Kleppmann, Martin\"]', 'Tolkien, J. R. R.', 1937, '9780261102217', 'unread', '.', 'x')");
        IndexDatabase.Execute(connection, "INSERT INTO works(id, title, title_norm, authors_json, author_sort, read_status, dir_path, updated_at) VALUES ('01J9Z3K0000000000000000002', 'Dune', 'dune', '[\"Frank Herbert\"]', 'Herbert, Frank', 'unread', '.', 'x')");
        // One fingerprint with the top bit set: stored as a negative signed integer, read back as the same bits.
        IndexDatabase.Execute(connection, "INSERT INTO files(work_id, format, rel_path, size, mtime, simhash) VALUES ('01J9Z3K0000000000000000001', 'epub', 'a.epub', 1, 1, -1)");
        IndexDatabase.Execute(connection, "INSERT INTO files(work_id, format, rel_path, size, mtime, simhash) VALUES ('01J9Z3K0000000000000000001', 'pdf', 'a.pdf', 1, 1, 5)");
        IndexDatabase.Execute(connection, "INSERT INTO files(work_id, format, rel_path, size, mtime) VALUES ('01J9Z3K0000000000000000002', 'pdf', 'd.pdf', 1, 1)");

        var candidates = WorkCandidateStore.Load(connection);

        Assert.Equal(2, candidates.Count);
        var hobbit = candidates[0];
        Assert.Equal(Ulid.Parse("01J9Z3K0000000000000000001"), hobbit.Id);
        Assert.Equal("hobbit", hobbit.Signature.TitleNorm);
        Assert.Equal(["tolkien", "kleppmann"], hobbit.Signature.SurnameKeys);
        Assert.Equal(1937, hobbit.Signature.Year);
        Assert.Equal("9780261102217", hobbit.Signature.Isbn13);
        Assert.Equal([5UL, ulong.MaxValue], hobbit.Fingerprints.Order());
        Assert.Null(candidates[1].Signature.Year);
        Assert.Null(candidates[1].Signature.Isbn13);
        Assert.Empty(candidates[1].Fingerprints);
    }
}
