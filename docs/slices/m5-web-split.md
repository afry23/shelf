# Slice m5-web-split: Splitting a File Off a Work in the Web UI

| | |
|---|---|
| **Slice ID** | `m5-web-split` |
| **Branch** | `feat/web-edit-split` |
| **Milestone** | M5 – Polish |
| **PRD references** | F-GRP-4, F-DUP-5 (web part), 7.3, 8.2 view Duplicates |
| **Inputs** | `LibraryEditor.Split`, `shelf split`, slices [m4-api-files-dupes](m4-api-files-dupes.md), [m5-web-views](m5-web-views.md) |

---

## 1. Scope

**In scope**

1. `POST /api/v1/files/{id}/split`: the file becomes a work of its own, as with `shelf split` (F-GRP-4). The answer names the old work (`work`), the new one (`newWork`), the `file` and the `folder` of the new work.
2. A button "Split into own work" on the file cards of a group "Different files of one book" in the Duplicates view. It asks for a second click, like keep and trash. The message names the new work and links to it, so that the person can correct its title there.

**Not in scope**

- Editing the details of a work in the browser: it exists (`PATCH /works/{id}`, "Edit details" in the work view).
- A split button in the work view for works that are not in a duplicate group.
- Undo of a split: `shelf split` is not undoable either, and the message says so.

## 2. Feature Files

`Features/Web/WebSplit.feature` (over HTTP) and `UiSplit.feature` (in the browser), tagged `@F-GRP-4 @F-DUP-5 @M5`.

## 3. Design

- **Same library code as the CLI.** `LibraryService.SplitFile` calls `LibraryEditor.Split` under the write gate and then reads the file of the new work for a cover.
- **Errors by kind.** A malformed file ID is 400, an unknown work or file 404, a work with only this file 409.
- **The title comes from the file.** The new work is described by the metadata of the file, as in the CLI. When a file carries the title of another book (a copied PDF), the new work has that title and its folder gets a number (`C# Brain Teasers (2)`); the person corrects the title in the work view.
- **Only for "different files of one book".** Identical files are copies, not different books, so the button is not shown there.
