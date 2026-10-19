# Slice m5-reader-progress: Reading Progress and the Preferred Format

| | |
|---|---|
| **Slice ID** | `m5-reader-progress` |
| **Branch** | `feat/m5-reader-progress` (stacked on `feat/m5-probable-dupes`) |
| **Milestone** | M5 – Polish |
| **Roadmap** | M5 slice 4 |
| **PRD references** | F-RDR-4 (progress per file in `book.json`, resumed on opening, writes debounced), F-RDR-5 (preferred format, default EPUB) |
| **Inputs** | [slice m4-reader](m4-reader.md), PRD 10.4 (`files[].progress`) |

---

## 1. Scope

**In scope**

1. `PUT /api/v1/files/{id}/progress` with `{ "locator": "page=2" }` or `{ "locator": "epubcfi(...)" }`: the position is stored in `book.json` (`files[].progress`, `locator` and `updatedAt`) and in the index.
2. `files[].progress` in `GET /api/v1/works/{id}` (and in the MCP `get_book`, which shares the type).
3. The readers send the position while a person reads and when the reader is closed, the tab is hidden or the page is left; they open at the saved position unless the address names a place.
4. `reader.preferred_format` (`epub` or `pdf`, default `epub`) in the library or global config; `GET /works/{id}` names the file that `Read` opens in `readFile`. The book page shows "Continue reading" and the place ("Page 2") when the file has a position.

**Out of scope**: bookmarks (F-RDR-6, Could), a percentage or "last read" list on the library view, clearing a position, progress for formats without a reader, a `web.read_only` switch (none exists; progress is a write like the others of the web API).

## 2. Feature Files

- `Features/Web/WebReadingProgress.feature` (`@F-RDR-4 @F-RDR-5 @M5`): saving for both formats, a file does not touch the other file of the work, a later position replaces the earlier, survives `reindex` and shows in `shelf show`, refused locators (8 cases, nothing on disk changes), unknown files (404), the file to read with and without the setting.
- `Features/Web/UiReadingProgress.feature` (`@ui`): PDF and EPUB are saved on leaving the reader and open there; the address wins; reload; each file keeps its own position; "Continue reading"; "Read" opens the preferred format.
- `Features/Configuration/Settings.feature`: the key, its override order, `mobi` and a number are refused.

## 3. Design

- **No scan per save.** `LibraryEditor.SetProgress` writes the sidecar (atomic, as every write) and changes the one row of `reading_progress` in the index. `LibraryEditor.Update` would scan the whole library (0.9 s on the reference library) for a write that happens every 30 seconds. The sidecar stays the source of truth; the next scan reads the same value.
- **No journal entry.** A position is not a change of the library that a person would undo; the undo history would fill with reading.
- **The same position changes nothing.** `SaveProgress` compares with the stored locator and returns without writing; the `updatedAt` of a position that has not moved stays.
- **The server checks the kind of location**: `page=N` (N ≥ 1) for a PDF, `epubcfi(...)` (at most 2,000 characters) for an EPUB. Anything else is a 400 with the reason, nothing is written.
- **Debounce in the browser** (`reader/progress.ts`): a move is sent at once when the last send is more than 30 s ago, otherwise after the rest of the 30 s; the last move is sent on close, `visibilitychange` to hidden and `pagehide` (`fetch` with `keepalive`, so it finishes while the page goes away). A failed send is not shown; the next move sends again.
- **Opening is not a move.** The PDF reader ignores page changes until `pagesinit` is done (PDF.js reports page 1 while it starts, which would overwrite the saved position); the EPUB reader treats its first relocation without an address as the starting point. Opening a book for the first time writes nothing.
- **Order of places**: the address (`#page=`, `#cfi=`), then the saved position, then the start.
- **Preferred format**: the first file of the configured format, else the first EPUB or PDF, else the first file. A work with one format is read in that format whatever the setting.

## 4. Result

- BDD and unit tests green (2599 tests, 26 skipped for the missing e5 model); 20 scenarios over HTTP and 8 in the browser are new.
- The write path was not measured on the reference library; it reads and writes one sidecar and one row.

## 5. Open Questions

- The debounce lives in TypeScript and has no unit test (the UI has no test runner; `npm run check` only). The scenarios cover that a position is saved on leaving, not the 30 s rule.
- A saved EPUB position is a CFI. If the file is replaced (same name, other content) the CFI may point nowhere; foliate-js then opens at the start.
- `merge` and `split` carry the position with the file (`LibraryEditor` copies `Progress`); no scenario covers it. Whether `organize` keeps it when it renames a file is not checked.
