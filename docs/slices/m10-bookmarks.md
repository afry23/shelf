# Slice m10-bookmarks: Bookmarks

| | |
|---|---|
| **Slice ID** | `m10-bookmarks` |
| **Branch** | `feat/bookmarks` |
| **Milestone** | M10 – Bookmarks and Tag Suggestions |
| **PRD references** | F-RDR-6, F-ANN-3, 7.2 |
| **Inputs** | `Annotation`, `AnnotationsFormat`, `LibraryService` (annotations), `NotesCommand`, `LibraryMcpServer`, reader views |

---

## 1. Scope

**In scope**

1. `AnnotationKind.Bookmark` in `Shelf.Core`; `Locators.Problem` moves from `LibraryService` to `Shelf.Core` so that the CLI checks the same rule.
2. `LibraryService.AddAnnotation` (one bookmark per locator, `created` out parameter), `AddBookmark`, `UpdateAnnotation` (label only).
3. REST: `POST /files/{id}/annotations` with `kind: bookmark` (201, or 200 if it was there).
4. CLI `shelf notes bookmark <work> <locator> [label] [--file]`; `notes list` and the Markdown export show bookmarks.
5. MCP `add_bookmark` (write tool).
6. UI: a bookmark button in both readers, bookmarks in the notes list of the reader and the work page.

**Not in scope**

- Bookmarks in search hits (the label is searchable like a note; no extra hit type).
- Following a bookmark of an EPUB after the file was replaced (the CFI may break; ADR 0018).

## 2. Feature Files

`Annotations/Bookmarks.feature`, `Mcp/McpBookmarks.feature`, `Web/UiBookmarks.feature`; tool lists in `McpServer.feature`, `McpHttp.feature`, `McpReadOnly.feature`.

## 3. Verification

Complete BDD suite and unit tests green; `npm run check`; bookmarks survive `shelf reindex`.
