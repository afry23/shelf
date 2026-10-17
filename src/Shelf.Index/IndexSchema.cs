namespace Shelf.Index;

/// <summary>
/// The SQL schema of <c>.shelf/index.db</c> (PRD 10.3, without the vector tables of M3). The database is an
/// index and a cache: it is never migrated, a different <see cref="Version"/> means "rebuild with reindex".
/// </summary>
/// <remarks>
/// Only single quotes for string literals (SQLite is built with DQS=0, ADR 0004). <c>works.seq</c> is the
/// rowid of <c>works_fts</c>; an explicit integer key survives VACUUM, an implicit rowid would not.
/// </remarks>
internal static class IndexSchema
{
    /// <summary>Stored in <c>PRAGMA user_version</c>. Increase it with every change of this schema.</summary>
    public const int Version = 2;

    public const string Tokenizer = "unicode61 remove_diacritics 2";

    public static readonly string[] Statements =
    [
        """
        CREATE TABLE works(
            seq INTEGER PRIMARY KEY,
            id TEXT NOT NULL UNIQUE,
            title TEXT NOT NULL,
            title_norm TEXT NOT NULL,
            subtitle TEXT,
            authors_json TEXT NOT NULL,
            author_sort TEXT NOT NULL,
            language TEXT,
            year INTEGER,
            isbn13 TEXT,
            publisher TEXT,
            series TEXT,
            series_index REAL,
            edition TEXT,
            description TEXT,
            rating INTEGER,
            read_status TEXT NOT NULL,
            cover_path TEXT,
            dir_path TEXT NOT NULL,
            added_at TEXT,
            updated_at TEXT NOT NULL)
        """,
        "CREATE INDEX works_isbn13 ON works(isbn13)",
        "CREATE INDEX works_title_norm ON works(title_norm)",
        """
        CREATE TABLE files(
            id INTEGER PRIMARY KEY,
            work_id TEXT NOT NULL REFERENCES works(id) ON DELETE CASCADE,
            format TEXT NOT NULL,
            rel_path TEXT NOT NULL UNIQUE,
            size INTEGER NOT NULL,
            sha256 TEXT,
            mtime INTEGER NOT NULL,
            page_count INTEGER,
            simhash INTEGER,
            text_status TEXT NOT NULL DEFAULT 'pending',
            embed_status TEXT NOT NULL DEFAULT 'pending')
        """,
        "CREATE INDEX files_work ON files(work_id)",
        "CREATE INDEX files_sha256 ON files(sha256)",
        "CREATE TABLE tags(id INTEGER PRIMARY KEY, path TEXT NOT NULL UNIQUE)",
        """
        CREATE TABLE work_tags(
            work_id TEXT NOT NULL REFERENCES works(id) ON DELETE CASCADE,
            tag_id INTEGER NOT NULL REFERENCES tags(id) ON DELETE CASCADE,
            PRIMARY KEY(work_id, tag_id)) WITHOUT ROWID
        """,
        """
        CREATE TABLE chunks(
            id INTEGER PRIMARY KEY,
            file_id INTEGER NOT NULL REFERENCES files(id) ON DELETE CASCADE,
            ordinal INTEGER NOT NULL,
            locator TEXT NOT NULL,
            text TEXT NOT NULL,
            UNIQUE(file_id, ordinal))
        """,
        $"CREATE VIRTUAL TABLE works_fts USING fts5(title, subtitle, authors, description, tags, tokenize='{Tokenizer}')",
        $"CREATE VIRTUAL TABLE chunks_fts USING fts5(text, content='chunks', content_rowid='id', tokenize='{Tokenizer}')",
        """
        CREATE TRIGGER chunks_ai AFTER INSERT ON chunks BEGIN
            INSERT INTO chunks_fts(rowid, text) VALUES (new.id, new.text);
        END
        """,
        """
        CREATE TRIGGER chunks_ad AFTER DELETE ON chunks BEGIN
            INSERT INTO chunks_fts(chunks_fts, rowid, text) VALUES ('delete', old.id, old.text);
        END
        """,
        """
        CREATE TRIGGER chunks_au AFTER UPDATE OF text ON chunks BEGIN
            INSERT INTO chunks_fts(chunks_fts, rowid, text) VALUES ('delete', old.id, old.text);
            INSERT INTO chunks_fts(rowid, text) VALUES (new.id, new.text);
        END
        """,
        """
        CREATE TABLE dupe_ignores(
            a_id TEXT NOT NULL,
            b_id TEXT NOT NULL,
            kind TEXT NOT NULL,
            owner TEXT NOT NULL,
            PRIMARY KEY(a_id, b_id, kind, owner),
            CHECK(a_id < b_id))
        """,
        """
        CREATE TABLE review_queue(
            id INTEGER PRIMARY KEY,
            kind TEXT NOT NULL,
            payload_json TEXT NOT NULL,
            created_at TEXT NOT NULL)
        """,
        """
        CREATE TABLE reading_progress(
            file_id INTEGER PRIMARY KEY REFERENCES files(id) ON DELETE CASCADE,
            locator TEXT NOT NULL,
            updated_at TEXT NOT NULL)
        """,
    ];

    /// <summary>Tables that hold data, in an order that is safe for deleting (children first).</summary>
    public static readonly string[] DataTablesChildrenFirst =
    [
        "chunks", "reading_progress", "work_tags", "files", "tags", "works", "dupe_ignores", "review_queue",
    ];
}
