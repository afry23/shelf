# Slice m4-api-files-dupes: Files with Range Requests and the Duplicate Actions

| | |
|---|---|
| **Slice ID** | `m4-api-files-dupes` |
| **Branch** | `feat/m4-library-core` (stacked: same branch as slices 1 to 3, separate commit) |
| **Milestone** | M4 – Web |
| **Roadmap** | M4 slice 4 |
| **PRD references** | 7.3 (`/api/v1/files/{id}`, range requests), F-RDR-3 (a file the reader can open at a location), F-DUP-4 (a delete is a move to `.shelf/trash/`), F-DUP-5 (comparison in the web UI), F-DUP-6 (not a duplicate) |
| **Inputs** | slices `m4-web-host` and `m4-api-read`; [ADR 0007](../adr/0007-web-backend-aot.md) (range cases W6); `LibraryDuplicates`, `DuplicateSession`, `FileJournal` |

---

## 1. Scope

**In scope**

1. `GET` and `HEAD /api/v1/files/{id}`: the file with HTTP range requests, `ETag` and `Last-Modified`, inline for the reader, as a download with `?download=true`.
2. A stable ID for a file (see 3) and `files[].id` in the details of a work.
3. `GET /api/v1/duplicates`: the groups of duplicate files, with what the comparison view needs.
4. The three actions of the duplicate view: `POST /api/v1/duplicates/keep`, `/trash` and `/ignore`. Files go to `.shelf/trash/` in one undoable journal; "not a duplicate" is written to the sidecars.
5. `LibraryService` gets `ResolveFile`, `Duplicates`, `KeepDuplicate`, `TrashDuplicate` and `IgnoreDuplicates`; `LibraryException` gets the kind `Conflict`.

**Out of scope**: covers (slice 5), the reader and its progress (8, M5), undo from the web (M5, Organize view), permanent deletion (stays `shelf trash empty`, PRD F-DUP-4).

## 2. Feature Files

`Features/Web/WebFiles.feature` (`@PRD-7.3 @F-RDR-3 @M4`): the whole file with its type (EPUB, PDF), the ID in the details, 6 range requests (first, middle, suffix, open end, one byte, whole file as a range), a range beyond the end (`416`), several ranges, `HEAD`, a conditional request (`304`), the download, a file name with spaces and brackets, 6 refusals (unknown work, unknown file, a malformed ID, a path in place of a name, a file that is gone from the disk, a missing token).

`Features/Web/WebDuplicates.feature` (`@F-DUP-4 @F-DUP-5 @F-DUP-6 @M4`): the list (3 groups, the kinds, the suggested file, the fields of a file), keep, trash, ignore, a work left without files, the whole session undone by `shelf undo` (folder equals the snapshot), the refusals (unknown file, a file that is no duplicate, a body without a file ID), nothing deleted for good.

## 3. Design

- **A file is named by its work and its name: `{workId}~{name}`** (`FileIds`). That is how the sidecar names it (`files[].name`, and `progress` in M5), it survives `reindex` (the index IDs `files.id` do not), and `~` never occurs in an ID, so the first `~` splits. Clients encode the name in the URL. An `organize` that renames a file changes its ID, as it changes its key in the sidecar; a deep link to a renamed file breaks. Recorded as a known limit.
- **The ID is resolved through the sidecar**, never through the disk: the name must be an entry of the work's `files`, so a name such as `../../x` finds nothing. The full path is then checked to lie inside the library, and the file must exist.
- **Range processing is the framework's** (`Results.File(..., enableRangeProcessing: true)`, ADR 0007 W6): one range is answered with `206`, a range beyond the end with `416` and `Content-Range: bytes */size`; several ranges are not supported and give the whole file. The `416` body is the problem document, like every status without a body.
- **Inline by default.** `?download=true` sets `Content-Disposition: attachment`. The types are `application/epub+zip`, `application/pdf` and `application/octet-stream`.
- **The validators** are an `ETag` made of size and modification time, and `Last-Modified`; a conditional request gets `304`.
- **Duplicates reuse the CLI's rules.** The groups come from `LibraryDuplicates.Find`, the actions from `DuplicateSession` (`Resolve`, `MarkNotDuplicates`) with the label `dupes`, so `shelf undo` takes a whole web action back like a CLI session.
- **Actions name a file, not a group.** A group is computed, it has no ID. `keep` trashes every other file of the group of the named file; `trash` trashes the named file only (the group has at least two files, so one stays); `ignore` marks every pair of the group as not duplicates (the rule of the CLI). Each action looks the group up again at the moment it runs: a file that is in no group (any more) is `409`, so the endpoint cannot delete an arbitrary file.
- **A work that is left without files** goes to the trash with its sidecar and cover (as in the CLI); the answer lists it in `worksRemoved`.
- **Nothing is deleted for good** by any endpoint; `shelf trash empty` is the only way (F-DUP-4).
- **Writes are serialized** with the write gate of the service, as the tag and metadata writes are.
- **Details get their own type.** `GET` and `PATCH /works/{id}` answer with `WorkDetail` (the fields of `BookDetails` and a file ID per file), so the MCP schema of `get_book` is not changed for a web need.

- **A `416` is made a problem document by the file result's wrapper** (`RangeProblemResult`). The status code pages only complete an answer that has no content type, and the file result has already set the type of the file; without the wrapper the empty body of a `416` was labelled `application/epub+zip` (found by the spec).

## 4. Native AOT

**Result (`osx-arm64`):** 0 warnings, binary 35 MiB. A native run against a fixture EPUB: the whole file, `bytes=100-199` and `bytes=-50` are byte for byte the same as the file (`cmp`); `bytes=99999999-` is `416` with `Content-Range: bytes */6234` and a problem document; `HEAD` gives the length and type; `If-None-Match` gives `304`. A library with a copy of a file: the list has one group, `keep` moves one file to the trash (`{"trashed":1,"worksRemoved":[]}`), the list is empty afterwards, and `shelf undo` from the command line gives the library back by hash. A `keep` on a file that is in no group is `409`.

## 5. Unit Tests

`FileIdsTests` (`Shelf.Library.Tests`): making and splitting an ID; names with `~`, spaces, brackets and non-ASCII letters; text that is no ID.

## 6. Open Questions

- **Pairs.** The view compares candidates side by side; `ignore` marks the whole group, as the CLI does. For a group of three, marking one pair is not possible yet. Decide with the UI (slice 7).
- **Renamed files.** If deep links must survive `organize`, the sidecar needs a stored file ID (additive, schema 1). Not needed for M4.
