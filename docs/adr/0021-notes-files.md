# ADR 0021: Notes files: role of a file

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-10-30 |
| **Scope** | PRD F-NOTEF-1 to F-NOTEF-7, F-LIB-3, Q7, 10.3 (table `files`), 10.4 (`book.json`) |
| **Roadmap** | M15 |
| **Inputs** | [ADR 0018](0018-annotations.md), PRD Q7 |

## Context

People keep the notes of an e-reader (the Kindle notebook as a searchable PDF, or a TXT export) and want them with the work and in the full text search, next to the Markdown notes of ADR 0018. Shelf had no way to attach such a file: a file of a work was always a book, TXT was only tracked, and nothing checked a file against being a book.

## Decisions

1. **A role on the file, not a new store.** Each entry of `files` in `book.json` gets `role` (`book` by default, absent means `book`; or `notes`). The file lives in the work folder like the others, so organize, merge, split, move, trash and undo carry it without new code paths. Rejected: a new `kind` in `annotations.json` (its quote/note schema does not fit a whole file) and a second folder.
2. **Index.** `files.role` (text, default `book`), schema version up; `shelf reindex` restores it from the sidecar. Chunks of a notes file go to `chunks` / `chunks_fts` like any other file; the search labels them by the role of the file. No second FTS table.
3. **Not a book.** Queries for formats, the preferred format, duplicates (SimHash, exact and probable) and embeddings filter `role = 'book'`.
4. **TXT.** A new `TextFileExtractor`: UTF-8 (BOM allowed), else Latin-1; no further detection. `TextChunker` as for Markdown.
5. **PDF.** The existing `PdfExtractor`. A PDF without a text layer is attached with text status "no text"; there is no OCR (Q7, G7).
6. **Only on purpose.** `import` and `scan` do not take a loose `.txt` in (F-LIB-3 stays); a notes file only comes in by `shelf add --notes` or the upload. A `.pdf` or `.txt` in a work folder that `book.json` lists with the role is read as a notes file by `scan` and `reindex`.
7. **Sidecars from before.** A `shelf` without this change ignores the unknown `role` and would read the file as a book; the file is not damaged.
