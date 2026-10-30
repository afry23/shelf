using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Shelf.Core.Grouping;
using Shelf.Core.Model;
using Shelf.Core.Review;
using Shelf.Core.Search;
using Shelf.Extraction;

namespace Shelf.Index;

/// <summary>
/// All writes to the index tables, shared by reindex, scan and import. Prepared commands are reused; the caller
/// owns the transaction. <c>owner</c> of <c>dupe_ignores</c> is the work whose sidecar holds the marking, so a
/// changed sidecar replaces exactly its own markings.
/// </summary>
internal sealed class IndexWriter : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly List<SqliteCommand> _commands = [];
    private readonly Dictionary<string, long> _tagIds = new(StringComparer.Ordinal);
    private SqliteTransaction? _transaction;

    private SqliteCommand? _insertWork;
    private SqliteCommand? _updateWork;
    private SqliteCommand? _insertFile;
    private SqliteCommand? _insertProgress;
    private SqliteCommand? _insertTag;
    private SqliteCommand? _selectTag;
    private SqliteCommand? _insertWorkTag;
    private SqliteCommand? _insertIgnore;
    private SqliteCommand? _insertFts;
    private SqliteCommand? _insertChunk;

    public IndexWriter(SqliteConnection connection) => _connection = connection;

    /// <summary>
    /// Starts a transaction for everything this writer does until it is committed or disposed. The writer assigns
    /// it to every command before running it: a prepared command keeps the transaction it was created in, which
    /// would be a finished one at the second use.
    /// </summary>
    public WriterTransaction BeginTransaction()
    {
        _transaction = _connection.BeginTransaction();
        return new WriterTransaction(this, _transaction);
    }

    internal sealed class WriterTransaction(IndexWriter writer, SqliteTransaction transaction) : IDisposable
    {
        public void Commit() => transaction.Commit();

        public void Dispose()
        {
            transaction.Dispose();
            writer._transaction = null;
        }
    }

    public void Dispose()
    {
        foreach (var command in _commands)
        {
            command.Dispose();
        }
    }

    // ---- works ----

    /// <summary>Inserts the work with its tags, "not a duplicate" markings and full-text row; returns <c>works.seq</c>.</summary>
    public long InsertWork(WorkSidecar s, string dirPath, DateTime sidecarModifiedUtc)
    {
        var command = _insertWork ??= Prepare("""
            INSERT INTO works(id, title, title_norm, subtitle, authors_json, author_sort, language, year, isbn13, publisher,
                              series, series_index, edition, cite_key, description, kind, doi, arxiv_id, venue, site, source_url, abstract, rating, read_status, cover_path, dir_path, added_at, finished_at, reading_list_at, updated_at)
            VALUES ($id, $title, $titleNorm, $subtitle, $authors, $authorSort, $language, $year, $isbn13, $publisher,
                    $series, $seriesIndex, $edition, $citeKey, $description, $kind, $doi, $arxivId, $venue, $site, $sourceUrl, $abstract, $rating, $status, $cover, $dir, $added, $finished, $readingList, $updated)
            RETURNING seq
            """);
        BindWork(command, s, dirPath, sidecarModifiedUtc);
        var seq = ScalarOf(command);
        InsertDetails(s, dirPath, seq);
        return seq;
    }

    /// <summary>Replaces the metadata of an existing work (not its files).</summary>
    public void UpdateWork(WorkSidecar s, string dirPath, DateTime sidecarModifiedUtc)
    {
        var id = s.Id.ToString();
        var command = _updateWork ??= Prepare("""
            UPDATE works SET title = $title, title_norm = $titleNorm, subtitle = $subtitle, authors_json = $authors,
                author_sort = $authorSort, language = $language, year = $year, isbn13 = $isbn13, publisher = $publisher,
                series = $series, series_index = $seriesIndex, edition = $edition, cite_key = $citeKey, description = $description,
                kind = $kind, doi = $doi, arxiv_id = $arxivId, venue = $venue, site = $site, source_url = $sourceUrl, abstract = $abstract,
                rating = $rating, read_status = $status, cover_path = $cover, dir_path = $dir, added_at = $added, finished_at = $finished, reading_list_at = $readingList,
                updated_at = $updated
            WHERE id = $id
            RETURNING seq
            """);
        BindWork(command, s, dirPath, sidecarModifiedUtc);
        var seq = ScalarOf(command);
        Execute("DELETE FROM work_tags WHERE work_id = $id", ("$id", id));
        Execute("DELETE FROM dupe_ignores WHERE owner = $id", ("$id", id));
        Execute("DELETE FROM works_fts WHERE rowid = $seq", ("$seq", seq));
        InsertDetails(s, dirPath, seq);
    }

    /// <summary>Sets the sidecar time stored with the work (after shelf itself changed the sidecar).</summary>
    public void SetWorkModified(string workId, DateTime sidecarModifiedUtc) =>
        Execute("UPDATE works SET updated_at = $t WHERE id = $id", ("$t", ModifiedStamp(sidecarModifiedUtc)), ("$id", workId));

    public void DeleteWork(string workId)
    {
        // Children first: the chunk triggers keep chunks_fts in step. Foreign keys do the rest.
        Execute("DELETE FROM chunks WHERE file_id IN (SELECT id FROM files WHERE work_id = $id)", ("$id", workId));
        Execute("DELETE FROM works_fts WHERE rowid = (SELECT seq FROM works WHERE id = $id)", ("$id", workId));
        Execute("DELETE FROM dupe_ignores WHERE owner = $id", ("$id", workId));
        Execute("DELETE FROM works WHERE id = $id", ("$id", workId));
    }

    /// <summary>Removes tags that no work uses any more.</summary>
    public void PruneTags()
    {
        Execute("DELETE FROM tags WHERE id NOT IN (SELECT tag_id FROM work_tags)");
        _tagIds.Clear();
    }

    // ---- files ----

    public long InsertFile(string workId, string name, string relPath, long size, long mtime, FileProgress? progress, FileRole role = FileRole.Book)
    {
        var command = _insertFile ??= Prepare(
            "INSERT INTO files(work_id, format, rel_path, size, mtime, role) VALUES ($work, $format, $path, $size, $mtime, $role) RETURNING id");
        Bind(command, "$role", role == FileRole.Notes ? "notes" : "book");
        Bind(command, "$work", workId);
        Bind(command, "$format", FormatOf(name));
        Bind(command, "$path", relPath);
        Bind(command, "$size", size);
        Bind(command, "$mtime", mtime);
        var id = ScalarOf(command);
        SetProgress(id, progress);
        return id;
    }

    public void SetRole(long fileId, FileRole role) =>
        Execute("UPDATE files SET role = $role WHERE id = $f AND role <> $role", ("$role", role == FileRole.Notes ? "notes" : "book"), ("$f", fileId));

    public void SetProgress(long fileId, FileProgress? progress)
    {
        Execute("DELETE FROM reading_progress WHERE file_id = $f", ("$f", fileId));
        if (progress is null)
        {
            return;
        }

        var command = _insertProgress ??= Prepare(
            "INSERT INTO reading_progress(file_id, locator, updated_at) VALUES ($file, $locator, $updated)");
        Bind(command, "$file", fileId);
        Bind(command, "$locator", progress.Locator);
        Bind(command, "$updated", Timestamp(progress.UpdatedAt.UtcDateTime));
        NonQuery(command);
    }

    /// <summary>Gives the file at this path of the library its reading position, or none.</summary>
    public void SetProgressOfPath(string relPath, FileProgress? progress)
    {
        Execute("DELETE FROM reading_progress WHERE file_id IN (SELECT id FROM files WHERE rel_path = $p)", ("$p", relPath));
        if (progress is not null)
        {
            Execute(
                "INSERT INTO reading_progress(file_id, locator, updated_at) SELECT id, $l, $u FROM files WHERE rel_path = $p",
                ("$l", progress.Locator),
                ("$u", Timestamp(progress.UpdatedAt.UtcDateTime)),
                ("$p", relPath));
        }
    }

    /// <summary>Gives the work the annotations of its <c>annotations.json</c> in the index, and none other.</summary>
    public void ReplaceAnnotations(string workId, IEnumerable<Annotation> annotations)
    {
        Execute("DELETE FROM annotations WHERE work_id = $w", ("$w", workId));
        foreach (var a in annotations)
        {
            // A note that moved here from another work is still listed there until that work is synced.
            Execute("DELETE FROM annotations WHERE uid = $uid AND work_id <> $w", ("$uid", a.Id.ToString()), ("$w", workId));
            Execute(
                "INSERT INTO annotations(uid, work_id, file_name, kind, locator, quote, color, note, created_at, updated_at) "
                + "VALUES ($uid, $w, $f, $k, $l, $q, $c, $n, $cr, $up)",
                ("$uid", a.Id.ToString()),
                ("$w", workId),
                ("$f", a.File),
                ("$k", AnnotationsFormat.KindName(a.Kind)),
                ("$l", a.Locator),
                ("$q", a.Quote?.Exact),
                ("$c", a.Color),
                ("$n", a.Note),
                ("$cr", Timestamp(a.CreatedAt.UtcDateTime)),
                ("$up", Timestamp(a.UpdatedAt.UtcDateTime)));
        }
    }

    /// <summary>Gives the work the references of its <c>references.json</c> in the index, and none other. A DOI is kept in lower case.</summary>
    public void ReplaceReferences(string workId, IEnumerable<Reference> references)
    {
        Execute("DELETE FROM work_refs WHERE work_id = $w", ("$w", workId));
        var position = 0;
        foreach (var r in references)
        {
            Execute(
                "INSERT INTO work_refs(work_id, position, doi, title, year, author, unstructured) VALUES ($w, $p, $d, $t, $y, $a, $u)",
                ("$w", workId),
                ("$p", position++),
                ("$d", r.Doi is null ? null : PaperIds.Normalize(r.Doi)),
                ("$t", r.Title),
                ("$y", r.Year),
                ("$a", r.Author),
                ("$u", r.Unstructured));
        }
    }

    public void UpdateFilePath(long fileId, string relPath) =>
        Execute("UPDATE files SET rel_path = $p WHERE id = $id", ("$p", relPath), ("$id", fileId));

    public void UpdateFileStat(long fileId, long size, long mtime) =>
        Execute("UPDATE files SET size = $s, mtime = $m WHERE id = $id", ("$s", size), ("$m", mtime), ("$id", fileId));

    public void DeleteFile(long fileId)
    {
        Execute("DELETE FROM chunks WHERE file_id = $id", ("$id", fileId));
        Execute("DELETE FROM files WHERE id = $id", ("$id", fileId));
    }

    /// <summary>Stores what reading a file produced: hash, page count, fingerprint, status and chunks (replacing old ones).</summary>
    public void SetContent(long fileId, long size, long mtime, string? sha256, int? pageCount, ulong? fingerprint, string textStatus, IReadOnlyList<TextChunk> chunks)
    {
        Execute("DELETE FROM chunks WHERE file_id = $id", ("$id", fileId));
        Execute(
            "UPDATE files SET size = $size, mtime = $mtime, sha256 = $sha, page_count = $pages, simhash = $simhash, text_status = $status, embed_status = CASE role WHEN 'book' THEN 'pending' ELSE 'skipped' END WHERE id = $id",
            ("$size", size),
            ("$mtime", mtime),
            ("$sha", sha256),
            ("$pages", pageCount),
            ("$simhash", fingerprint is { } f ? unchecked((long)f) : null),
            ("$status", textStatus),
            ("$id", fileId));

        var insert = _insertChunk ??= Prepare("INSERT INTO chunks(file_id, ordinal, locator, text, embed_skip) VALUES ($file, $ordinal, $locator, $text, $skip)");
        foreach (var chunk in chunks)
        {
            Bind(insert, "$file", fileId);
            Bind(insert, "$ordinal", chunk.Ordinal);
            Bind(insert, "$locator", chunk.Locator);
            Bind(insert, "$text", chunk.Text);
            Bind(insert, "$skip", chunk.Kind == ContentKind.Body ? null : chunk.Kind.ToString().ToLowerInvariant());
            NonQuery(insert);
        }
    }

    // ---- review queue ----

    /// <summary>Makes <c>review_queue</c> a copy of the queue file.</summary>
    public void ReplaceReviewMirror(ReviewQueue queue)
    {
        Execute("DELETE FROM review_queue");
        foreach (var entry in queue.Entries)
        {
            Execute(
                "INSERT INTO review_queue(kind, payload_json, created_at) VALUES ($kind, $payload, $created)",
                ("$kind", ReviewQueue.KindName(entry.Kind)),
                ("$payload", ReviewQueue.EntryToJson(entry)),
                ("$created", ReviewQueue.FormatTime(entry.CreatedAt)));
        }
    }

    // ---- helpers ----

    public static string Timestamp(DateTime utc) => utc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    /// <summary>
    /// The modification time of a sidecar as stored in <c>works.updated_at</c>, to 100 ns: a sidecar that is
    /// rewritten within the same second must still look changed.
    /// </summary>
    public static string ModifiedStamp(DateTime utc) => utc.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture);

    /// <summary>The modification time of a file as stored in <c>files.mtime</c>: Unix milliseconds.</summary>
    public static long Mtime(FileInfo info) => new DateTimeOffset(info.LastWriteTimeUtc).ToUnixTimeMilliseconds();

    public static string FormatOf(string fileName) => Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant() switch
    {
        "epub" => "epub",
        "pdf" => "pdf",
        var other when other.Length > 0 => other,
        _ => "other",
    };

    public static string JoinRelative(string directory, string name) => directory == "." ? name : directory + "/" + name;

    private void BindWork(SqliteCommand command, WorkSidecar s, string dirPath, DateTime sidecarModifiedUtc)
    {
        Bind(command, "$id", s.Id.ToString());
        Bind(command, "$title", s.Title);
        Bind(command, "$titleNorm", TitleNormalizer.Normalize(s.Title));
        Bind(command, "$subtitle", s.Subtitle);
        Bind(command, "$authors", JsonArray(s.Authors));
        Bind(command, "$authorSort", AuthorNames.Parse(s.Authors[0]).SortKey);
        Bind(command, "$language", s.Language);
        Bind(command, "$year", s.Year);
        Bind(command, "$isbn13", s.Isbn13);
        Bind(command, "$publisher", s.Publisher);
        Bind(command, "$series", s.Series);
        Bind(command, "$seriesIndex", s.SeriesIndex);
        Bind(command, "$edition", s.Edition);
        Bind(command, "$citeKey", s.CiteKey);
        Bind(command, "$description", s.Description);
        Bind(command, "$kind", s.Kind switch { WorkKind.Paper => "paper", WorkKind.Article => "article", _ => "book" });
        Bind(command, "$doi", s.Doi is null ? null : PaperIds.Normalize(s.Doi));
        Bind(command, "$arxivId", s.ArxivId);
        Bind(command, "$venue", s.Venue);
        Bind(command, "$site", s.Site);
        Bind(command, "$sourceUrl", s.SourceUrl);
        Bind(command, "$abstract", s.Abstract);
        Bind(command, "$rating", s.Rating);
        Bind(command, "$status", s.ReadStatus.ToString().ToLowerInvariant());
        Bind(command, "$cover", s.Cover is null ? null : JoinRelative(dirPath, s.Cover));
        Bind(command, "$dir", dirPath);
        Bind(command, "$added", s.Added is { } added ? Timestamp(added.UtcDateTime) : null);
        Bind(command, "$finished", s.ReadStatus == ReadStatus.Read && s.FinishedAt is { } finished ? Timestamp(finished.UtcDateTime) : null);
        Bind(command, "$readingList", s.ReadStatus != ReadStatus.Read && s.ReadingListAt is { } onList ? Timestamp(onList.UtcDateTime) : null);
        Bind(command, "$updated", ModifiedStamp(sidecarModifiedUtc));
    }

    private void InsertDetails(WorkSidecar s, string dirPath, long seq)
    {
        var id = s.Id.ToString();
        var insertTag = _insertTag ??= Prepare("INSERT OR IGNORE INTO tags(path) VALUES ($path)");
        var selectTag = _selectTag ??= Prepare("SELECT id FROM tags WHERE path = $path");
        var insertWorkTag = _insertWorkTag ??= Prepare("INSERT OR IGNORE INTO work_tags(work_id, tag_id) VALUES ($work, $tag)");
        foreach (var tag in s.Tags)
        {
            if (!_tagIds.TryGetValue(tag, out var tagId))
            {
                Bind(insertTag, "$path", tag);
                NonQuery(insertTag);
                Bind(selectTag, "$path", tag);
                tagId = ScalarOf(selectTag);
                _tagIds[tag] = tagId;
            }

            Bind(insertWorkTag, "$work", id);
            Bind(insertWorkTag, "$tag", tagId);
            NonQuery(insertWorkTag);
        }

        foreach (var other in s.NotDuplicates.Works)
        {
            InsertIgnore(id, other.ToString(), "work", id);
        }

        foreach (var pair in s.NotDuplicates.Files)
        {
            InsertIgnore(JoinRelative(dirPath, pair.A), JoinRelative(dirPath, pair.B), "file", id);
        }

        var fts = _insertFts ??= Prepare(
            "INSERT INTO works_fts(rowid, title, subtitle, authors, description, tags, stems_en, stems_de, publisher) VALUES ($seq, $title, $subtitle, $authors, $description, $tags, $stemsEn, $stemsDe, $publisher)");
        Bind(fts, "$seq", seq);
        Bind(fts, "$title", s.Title);
        Bind(fts, "$subtitle", s.Subtitle);
        Bind(fts, "$authors", string.Join(' ', s.Authors));

        // The abstract, keywords and venue of a paper are searched like the description of a book (F-PAP-7).
        var described = string.Join(' ', new[] { s.Description, s.Abstract, s.Venue, string.Join(' ', s.Keywords) }.Where(t => !string.IsNullOrWhiteSpace(t)));
        Bind(fts, "$description", described.Length == 0 ? null : described);
        Bind(fts, "$tags", string.Join(' ', s.Tags));
        Bind(fts, "$publisher", s.Publisher);

        // Stems of the words of title, subtitle, description and tags, in the column of the language of the work
        // (F-SRC-6); names of authors are not stemmed.
        var stemmer = Stemmer.For(s.Language);
        var stems = stemmer is null
            ? null
            : string.Join(' ', new[] { s.Title, s.Subtitle, described, string.Join(' ', s.Tags) }.SelectMany(Stemmer.Words).SelectMany(w => StemsOf(w, stemmer)));
        Bind(fts, "$stemsEn", stemmer == Stemmer.English ? stems : null);
        Bind(fts, "$stemsDe", stemmer == Stemmer.German ? stems : null);
        NonQuery(fts);
    }

    // The stem and the stem of the stem: Snowball German turns "systeme" into "system" but "system" into "syst",
    // and a search for either must find both.
    private static IEnumerable<string> StemsOf(string word, string stemmer)
    {
        var stem = Stemmer.Stem(word, stemmer);
        yield return stem;
        var again = Stemmer.Stem(stem, stemmer);
        if (again != stem)
        {
            yield return again;
        }
    }

    private void InsertIgnore(string x, string y, string kind, string owner)
    {
        // The pair is unordered; the CHECK constraint wants a_id < b_id.
        var (a, b) = string.CompareOrdinal(x, y) <= 0 ? (x, y) : (y, x);
        if (a == b)
        {
            return;
        }

        var command = _insertIgnore ??= Prepare("INSERT OR IGNORE INTO dupe_ignores(a_id, b_id, kind, owner) VALUES ($a, $b, $kind, $owner)");
        Bind(command, "$a", a);
        Bind(command, "$b", b);
        Bind(command, "$kind", kind);
        Bind(command, "$owner", owner);
        NonQuery(command);
    }

    private SqliteCommand Prepare(string sql)
    {
        var command = _connection.CreateCommand();
        command.CommandText = sql;
        _commands.Add(command);
        return command;
    }

    private void Execute(string sql, params (string Name, object? Value)[] parameters)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        NonQuery(command);
    }

    private long ScalarOf(SqliteCommand command)
    {
        command.Transaction = _transaction;
        return (long)command.ExecuteScalar()!;
    }

    private void NonQuery(SqliteCommand command)
    {
        command.Transaction = _transaction;
        command.ExecuteNonQuery();
    }

    private static void Bind(SqliteCommand command, string name, object? value)
    {
        if (command.Parameters.Contains(name))
        {
            command.Parameters[name].Value = value ?? DBNull.Value;
        }
        else
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }
    }

    private static string JsonArray(IReadOnlyList<string> values)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            writer.WriteStartArray();
            foreach (var value in values)
            {
                writer.WriteStringValue(value);
            }

            writer.WriteEndArray();
        }

        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }
}
