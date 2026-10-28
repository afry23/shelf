# Slice m7-work-files: Add, Move and Create by Hand

| | |
|---|---|
| **Slice ID** | `m7-work-files` |
| **Branch** | `feat/work-files` |
| **Milestone** | M7 – Work Files by Hand |
| **PRD references** | F-GRP-7, F-GRP-8, F-GRP-9, 7.1, 7.3, 8.2 view Work details |
| **Inputs** | `LibraryEditor` (Merge, Split), `LibraryDeleter`, `LibraryImporter`, `FileJournal` |

---

## 1. Scope

**In scope**

1. `LibraryFiles.AddFile`: a file joins an existing work; with `Replace` the old file goes to the trash.
2. `LibraryFiles.MoveFile`: a file moves to another work or to a new one; undoable.
3. `LibraryFiles.CreateWork`: a work from one file with given metadata, no grouping.
4. CLI `shelf add`, `shelf create`, `shelf move`; REST `POST /works`, `POST /works/{id}/files`, `POST /files/{id}/move`; buttons in the work and library views.

**Not in scope**

- Works without a file (the invariant "at least one file" stays).
- MCP (PRD 7.2: no move via MCP).

## 2. Feature Files

`Features/Commands/Add.feature`, `Create.feature`, `Move.feature`; `Features/Web/WebAddFile.feature`, `WebCreateWork.feature`, `WebMoveFile.feature`, `UiWorkFiles.feature`; tagged `@F-GRP-7`/`@F-GRP-8`/`@F-GRP-9` and `@M7`.

## 3. Design

- **Files first, sidecar next, index last**, then `Reconcile`, like merge and split.
- **Undo.** Add with `--replace`, move and create use an undoable journal. If the journal cannot revert a combination of move, replace and create, the operation is not undoable, as merge and split, and the PRD text says so.
- **Uploads** are the raw body of the request (`application/octet-stream`, name in `?name=`), not a multipart form: no form binding (Native AOT), no buffering in memory, and a browser cannot send it cross-site without a preflight. The body is saved to `.shelf/uploads/<guid>/<name>`, handled like a path and deleted afterwards. The size limit is 2 GiB per request.
- **Index.** `IndexWriter.ReplaceAnnotations` removes a note with the same ID from another work first, because a note that moves between works is listed twice until both works are synced.
- **Undo.** Add, create and move are undoable (`FileJournal.CreateFrom` journals a file copied in from outside).
