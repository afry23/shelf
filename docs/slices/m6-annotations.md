# Slice m6-annotations: highlights and Markdown notes

| | |
|---|---|
| **Slice ID** | `m6-annotations` (one branch for the five planned parts: core, API, UI, search/CLI/MCP, PDF) |
| **Branch** | `feat/annotations` |
| **Milestone** | M6 - Annotations |
| **PRD references** | F-ANN-1 to F-ANN-6 |
| **Inputs** | [ADR 0018](../adr/0018-annotations.md), [slice m5-reader-progress](m5-reader-progress.md) |

---

## 1. Scope

**In scope**

1. `annotations.json` next to `book.json` (model, format and store in `Shelf.Core`), mirrored in the index (`annotations`, `annotations_fts`; schema version 6); `reindex` and the scan fill the mirror from the files; organize, merge and split take the file along (organize and merge/split rename the `file` of an annotation).
2. `LibraryService` and `/api/v1`: list (file, work), add, change (note, color), delete.
3. Reader: EPUB (foliate-js overlay, CFI) and PDF (areas of the page as fractions, drawn over the page): select, choose a color, "Highlight"; notes panel with Markdown (marked + DOMPurify), a note for the whole file, color, delete; the book page lists them with links into the reader.
4. Search finds the marked text and the notes (hit shows "highlight, page 2" or "note"); `shelf notes list|add|rm|export`; MCP `get_annotations`, and `add_note` unless `mcp.read_only`.

**Out of scope**: autosave of a note while typing (a note is saved with "Save note"), undo through the journal (ADR 0018), highlights in the CLI or MCP (they need a place in the text; a person marks them in the reader), re-anchoring a highlight by its quote when the file was replaced (the quote is stored for it).

## 2. Feature Files

- `Features/Annotations/Annotations.feature`: the API, refused input, reindex, organize, split.
- `Features/Annotations/NotesCommand.feature`, `Features/Search/SearchAnnotations.feature`, `Features/Mcp/McpAnnotations.feature` (and the tool lists in the other MCP features).
- `Features/Web/UiAnnotationsEpub.feature`, `Features/Web/UiAnnotationsPdf.feature` (`@ui`).

## 3. Design

See [ADR 0018](../adr/0018-annotations.md). A PDF page has no text positions that a locator could name, so a PDF highlight keeps `rects` (x, y, width, height as fractions of the page). In an EPUB the CFI is the place and the overlay of foliate-js draws it; the reader reports the number of drawn highlights as `data-highlights` on the book element (for the browser scenarios, the overlay lives in a closed shadow root).

## 4. Open Questions

- Re-anchoring a highlight from its quote when the CFI no longer fits.
- Annotations in the library view (a filter such as `has:notes`).
