# Slice m4-api-read: Library, Search, Details, Metadata and Tags over HTTP

| | |
|---|---|
| **Slice ID** | `m4-api-read` |
| **Branch** | `feat/m4-library-core` (stacked: same branch as slices 1 and 2, separate commit) |
| **Milestone** | M4 – Web |
| **Roadmap** | M4 slice 3 |
| **PRD references** | 7.3 (REST/JSON under `/api/v1`, feature parity with the CLI for reads, tagging and metadata editing), 8.2 (views Library, Search, Work details), F-SRC-1 to F-SRC-5, F-SEM-1 to F-SEM-5, F-TAG-5, F-LIB-6 |
| **Inputs** | slice `m4-library-core` (`LibraryService`), slice `m4-web-host` (host, token, problem documents) |

---

## 1. Scope

**In scope**

1. `GET /api/v1/works`: the library as a list. Filters (the filter part of the query language), sorting, paging, and facet counts for the filtered set (tags, authors, languages, years, formats, reading status).
2. `GET /api/v1/search`: one search for the three modes of the search view: `fulltext`, `semantic`, `hybrid`; at the level of works or passages.
3. `GET /api/v1/works/{id}`: details with files and table of contents.
4. `PATCH /api/v1/works/{id}`: change metadata (JSON merge patch, RFC 7396: a field that is absent stays, a field set to `null` is removed).
5. `GET /api/v1/tags` (tree with counts) and `PUT /api/v1/works/{id}/tags` (the tag set of a work, replaced).
6. `LibraryService` gets `Browse`, `EditMetadata` and `SetTags`; `Shelf.Index` gets `LibraryBrowse`.
7. `LibraryException` gets a kind (`Invalid`, `NotFound`), so that a missing work is a 404 and a bad value a 400.

**Out of scope**: files and duplicates (slice 4), covers (5), the UI (6, 7), reading progress (M5), `web.read_only` (see 6).

## 2. Feature Files

`Features/Web/WebWorks.feature` (`@PRD-7.3 @PRD-8.2 @M4`): the list (default order, 4 sort orders, 9 filters, words refused, 3 invalid filters, paging and its limits, facets and that they follow the filter, the fields of an item), the details (fields, a prefix of the ID, unknown, ambiguous, a path instead of an ID).

`Features/Web/WebSearch.feature` (`@F-SRC-1 @F-SEM-1 @F-SEM-5 @M4`): full text (works with places in the text), semantic at both levels, hybrid, the note for an incomplete index, the errors (empty query, unknown mode and level, a limit out of range, filters in a hybrid query).

`Features/Web/WebEdit.feature` (`@PRD-7.3 @F-TAG-5 @M4`): metadata (9 fields changed, the removal with `null`, 8 invalid values, an unknown field, a body that is no object), the tag set (add, remove, replace, normalization, an invalid tag changes nothing), the list of tags with counts, and that `undo` takes a tag change back.

## 3. Design

- **IDs only.** `WorkLookup` also reads folder and file paths; the API takes the ID of a work or the start of one. A reference that is not shaped like an ID is `404` without looking at the file system.
- **`q` of `/works` takes filters only** (`tag:`, `author:`, `title:`, `lang:`, `year:`, `format:`, `status:`, and `-` in front). Words are the job of `/search`; a query with words is `400`, so that the list never ranks.
- **Facets follow the filter.** Each facet counts the works of the filtered set (drill-down), tags by full path, authors by name. Tags and authors are cut at 50 values, the others are complete. The counts come from SQL over the filtered IDs.
- **One search resource, three modes.** The answer has one shape (`results[]` with `passages[]`), whatever the mode, so the UI renders one list. `semantic` and `hybrid` need the embeddings; the `note` of the service says when they are missing or incomplete.
- **The model is opened on the first semantic request** and kept (as in MCP); `WebOptions` carries the model catalog and the embedder factory.
- **Merge patch for metadata.** Fields: `title`, `subtitle`, `authors`, `language`, `year`, `isbn`, `publisher`, `series`, `seriesIndex`, `edition`, `description`, `rating`, `readStatus`. The rules are those of `shelf edit` (year 1 to 9999, rating 1 to 5, a valid ISBN-10 or ISBN-13, status `unread`, `reading` or `read`, a title that is not empty). `isbn` sets `isbn13` (and `isbn10` when given as ISBN-10). Tags are not part of the patch: `PUT .../tags` owns them.
- **Tags are replaced as a set.** The service normalizes all names first (a refused name changes nothing; names are trimmed, not folded: `DevOps` and `devops` are two tags), counts what is added and removed, and calls the new `LibraryTags.SetTags` under the write gate. That is one journal, so `shelf undo` takes the whole change back in one step (two calls of `Add` and `Remove` would have been two steps).
- **The author sort is made on folded keys.** The sort key of an author keeps its accents (`Ålander` sorts after `Zimmer` in SQLite's byte order), so `LibraryBrowse` orders by author in C# on `TextFolding.Fold` (made public for that) over one short key per work.
- **`BookDetails` gained `edition` and `rating`** (a work view edits both). That adds two members to the output schema of the MCP tool `get_book`; the Mcp specs are unchanged and green.
- **Writes are guarded within a process** by the write gate of the service. Across processes the sidecar is replaced atomically and the index waits for the SQLite busy timeout (5 s, `IndexDatabase`).
- **Unknown fields in a body** are `400` (a typo must not be silently ignored).
- **Duplicate rules**: the rules of `shelf edit` are not yet shared with the CLI command (it keeps its inline checks); moving both onto `Shelf.Core` helpers is a follow-up, recorded here so that it is not forgotten.

## 4. Native AOT

Request and response types are on `WebJsonContext` (including `JsonObject` for the merge patch). **Result (`osx-arm64`):** 0 warnings, binary 35 MiB. A native run against the fixture library answers the list with facets, a filter, the refusal of words, a search, a semantic search without a model (`200`, `total 0`, the note "no embeddings yet; run: shelf reindex --embeddings"), a `PATCH` (and the refusal of `rating: 9`), a `PUT .../tags`, the tag tree, the refusal of a path as an ID (`404`) and an OpenAPI document with all six paths. Non-ASCII text is written as `\uXXXX` escapes (valid JSON, the default encoder); a relaxed encoder would shorten German text and is a choice for later.

## 5. Unit Tests

`LibraryBrowseTests` (`Shelf.Index.Tests`, 10): accented authors, works without a year in both directions, stable pages for equal keys, a page beyond the end, a format counted once, the cut of the facets, exclusions, words refused. `MetadataPatchTests` (`Shelf.Library.Tests`, 41): every field, null and blank, the ISBN forms, 23 refusals. The tag set is covered by the specs (add, remove, replace, refuse, undo) and needs no extra unit test.

## 6. Open Questions

- **`web.read_only`** (like `mcp.read_only`): not added. The token protects the writes, and PRD 7.3 asks for parity with the CLI. Decide in M5 if a shared, read-only server is wanted.
- **File IDs** for slice 4: `files.id` of the index changes with `reindex`, so a deep link `/read/{fileId}` would break. Slice 4 decides on a stable ID.
