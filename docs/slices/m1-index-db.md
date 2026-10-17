# Slice m1-index-db: Index Database and `reindex`

| | |
|---|---|
| **Slice ID** | `m1-index-db` |
| **Branch** | `feat/m1-index-db` (stacked on `feat/m1-init`) |
| **Milestone** | M1 – Foundation |
| **Roadmap** | M1 plan slice 5; CLI command `reindex`; ADR 0004 hand-off (WAL policy) |
| **PRD references** | F-LIB-6, 9.3 (WAL, concurrent processes), 10.3 (schema), 12.3 (acceptance criterion 3) |
| **Inputs** | [ADR 0004](../adr/0004-storage-sqlite-fts5-vector-search.md), [ADR 0001](../adr/0001-dependency-licenses-and-maintenance.md) rows 8 and 9, `Shelf.Core.Model` (sidecars) |

---

## 1. Scope

**In scope**

1. New project `Shelf.Index` (`IsAotCompatible`) with Microsoft.Data.Sqlite 10.0.12 and SQLitePCLRaw.bundle_e_sqlite3 2.1.12, pinned in `Directory.Packages.props` as ADR 0004 asks; tests in `Shelf.Index.Tests`.
2. Schema of PRD 10.3 without vectors: `works`, `files`, `tags`, `work_tags`, `chunks`, `works_fts`, `chunks_fts` (external content, kept in sync by triggers), `dupe_ignores`, `review_queue`, `reading_progress`. `PRAGMA user_version` holds the schema version; the index is never migrated, a different version means rebuild.
3. Connection setup and WAL policy (see ADR 0004, "M1 follow-up").
4. `LibraryReindexer.Rebuild`: walks the library for `book.json` (skips dot-folders), reads the sidecars, and replaces the metadata part of the index in **one transaction**: works, files (path, format, size, mtime), tags, reading progress, "not a duplicate" markings, `works_fts`. Readers see the old index until the commit; a failure keeps it.
5. `shelf reindex [--library <name|path>] [--json]`, with `LibraryContext` (library lookup + config loading). Problems (invalid sidecar, duplicate work ID, a listed file that does not exist) are reported as `warning:` lines, the rest is still indexed, the exit code is 1.
6. `shelf init` now creates `index.db` as part of its all-or-nothing step.
7. A file that is not a database is replaced; a locked or busy index is never deleted.

**Requirements covered:** F-LIB-6, acceptance criterion 3 (`@PRD-12.3`), `@PRD-9.3` (WAL).

**Out of scope**

- Hash, page count, SimHash, text and chunks of files: filled by the scan pipeline (`m1-import-scan`), which `reindex` will call; today `sha256` is NULL and `text_status` is `pending`.
- `review_queue` from `.shelf/review.json`: the entry format is defined in `m1-grouping`; the table exists and stays empty.
- Real `title_norm` and `author_sort`: placeholders (lowercase title, first author as written) until `m1-grouping` defines normalization and surname rules; reindex then fills them.
- Search queries and bm25 weights: `m1-search`.

## 2. Feature Files

`Features/Library/Reindex.feature` (`@F-LIB-6 @PRD-12.3 @M1`): counts per table (outline), JSON output, library from the working directory, deleting the index loses nothing, idempotence, sidecar edits, WAL mode without leftover file, rebuild of another schema version, three kinds of broken input (outline), empty library, missing library (outline). `Init.feature`: the index exists after `init`.

## 3. Design

- Dependencies: `Shelf.Cli → Shelf.Index → Shelf.Core`. `LibraryInitializer` (Core) takes an `Action<string>` to create the index, so Core does not depend on SQLite and cleanup still covers it.
- Decision without ADR: a work needs at least one existing file; each missing file is one problem; a work with none is skipped.
- Decision without ADR: `reindex` exits 1 if anything was skipped, so scripts notice a half-indexed library.
- Native AOT: published on `osx-arm64` with 0 warnings; `reindex` of a small library takes about 7 ms. `libe_sqlite3.dylib` is placed next to the binary (ADR 0004 R10). `linux-x64` is covered by the CI AOT job in slice `m1-ci-perf`.
- Symlinked folders follow the .NET default of `Directory.EnumerateFiles` (not verified for loops); revisit in `m1-import-scan` when the scanner walks the same tree.

## 4. Unit Tests

`IndexDatabaseTests` (version, pragmas as decided, read-only reader, missing/other version, `DQS=0`, chunk trigger, schema drop with data, checkpoint with a busy reader, WAL size under the realistic reader pattern), `LibraryReindexerTests` (root sidecar, hidden folders, corrupt file replaced, all files missing, pair ordering, failed rebuild keeps the old index).

## 5. Open Questions

None.

## 6. Implementation Notes

- `PRAGMA busy_timeout` read back as 0 although the connection string had a timeout; an explicit pragma fixed it.
- Microsoft.Data.Sqlite starts transactions with `BEGIN IMMEDIATE` by default; readers must pass `deferred: true`.
- Dropping the schema must go children first: with foreign keys on, `DROP TABLE works` deletes rows and needs `files`.
