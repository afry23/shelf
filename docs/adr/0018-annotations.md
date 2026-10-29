# ADR 0018: Annotations: storage, locators and Markdown rendering

| | |
|---|---|
| **Status** | Accepted: `annotations.json` per work; `marked` 18.1.1 and `dompurify` 3.4.17 adopted for the browser bundle (exact pins in `package.json`) |
| **Date** | 2026-10-25 |
| **Scope** | PRD F-ANN-1 to F-ANN-6, Q5, 10.3 (tables `annotations`, `annotations_fts`) |
| **Roadmap** | M6 |
| **Inputs** | [ADR 0001](0001-dependency-licenses-and-maintenance.md), [ADR 0009](0009-frontend-toolchain-licenses-and-maintenance.md), [ADR 0016](0016-reader-libraries.md), the npm registry (read on 2026-10-25) |

## Context

People want highlights and notes while they read. Q5 left the format open and preferred a separate `annotations.json`. The library must stay usable without `shelf` (G1), so the files in the work folder are the source of truth and the index is a mirror (F-LIB-6).

## Decisions

1. **Storage.** `annotations.json` next to `book.json`, written atomically (temp file, then move), with a `schema` field and unknown fields kept. `book.json` stays small. The file moves with the work on organize, merge and split.
2. **Entry.** `id` (ULID), `file` (file name), `kind` (`highlight` or `note`), `locator`, `quote` (`exact`, `prefix`, `suffix`), `color`, `note` (Markdown), `createdAt`, `updatedAt`. A `note` has no locator and belongs to a file.
3. **Locator.** EPUB: the CFI. PDF: `page=N` plus `rects`, the marked areas as fractions of the page (x, y, width, height). The quote lets a reader re-anchor a highlight when the file changed.
4. **No journal.** Like reading progress, edits of annotations are not undoable through the journal; they would fill the history.
5. **Index.** Tables `annotations` and `annotations_fts` (external content, triggers, like `chunks_fts`). The schema version rises; `shelf reindex` rebuilds the mirror.
6. **Markdown.** Notes are stored as Markdown text. The UI renders them with `marked` and sanitizes the HTML with `dompurify`. The content security policy stays as it is (no inline script, no eval).
7. **Export.** A work is exported as one Markdown file: the metadata as YAML front matter, then highlights and notes grouped by file. The file name is the cite key (made safe by `PathSanitizer`); a work without a cite key is refused instead of getting a made-up name. CLI and web share `WorkExport` (`Shelf.Core`).

## Addendum: bookmarks (M10, F-RDR-6)

A bookmark is a third `kind`, `bookmark`: a `locator` and nothing else; its `note` is an optional label. It reuses the file, the mirror and the carry over organize, merge and split. A file has at most one bookmark per locator; setting it again returns the existing one (HTTP 200 instead of 201). The index needs no change (`annotations.kind` is text) and the label is searchable like a note. A `shelf` without this change refuses an `annotations.json` that holds a bookmark (unknown kind); the file is not damaged.

## Libraries

| # | Candidate | Version | License | GPL-3.0 compatible | Maintenance | Verdict |
|---|---|---|---|---|---|---|
| R1 | `marked` | 18.1.1 (modified 2026-10-12) | MIT | yes | ok: several maintainers, recent releases | adopt |
| R2 | `dompurify` | 3.4.17 (modified 2026-10-12) | MPL-2.0 OR Apache-2.0 | yes (Apache-2.0, ADR 0001 S1) | ok: Cure53, recent releases | adopt |

Both ship to the browser, so ADR 0001 applies fully. `THIRD-PARTY-NOTICES` gets both entries (open item of M5 distribution). It is an engineering assessment, not legal advice.

## Risks

- foliate-js is unstable (ADR 0016 flag). Its annotation API is checked against the pinned 1.0.1 before the UI slice.
- CFIs break when a file is replaced; the quote is the fallback.
