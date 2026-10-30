# Slice m15-notes-files: Notes Files

| | |
|---|---|
| **Slice ID** | `m15-notes-files` |
| **Branch** | `feat/notes-files` |
| **Milestone** | M15 – Notes Files |
| **PRD references** | F-NOTEF-1 to F-NOTEF-7 |
| **Inputs** | `BookExtractor`, `PdfExtractor`, `TextChunker`, `WorkSidecar`, `IndexSchema`, `LibrarySearch`, `AddCommand`, `NotesCommand`, `LibraryMcpServer`, `Api`, `Work.svelte` |

---

## 1. Scope

**In scope**

1. `role` per file entry in `book.json` (`book` default, `notes`; `schema` stays 1). Index column `files.role`.
2. `TextFileExtractor` for `.txt` (UTF-8 with or without BOM, else Latin-1), chunked with `TextChunker`.
3. `shelf add <work> <file> --notes` and the upload with `?role=notes`; only `.pdf` and `.txt`.
4. Notes files take no part in duplicate detection, the preferred format and embedding.
5. Search hits from a notes file get the label "Notes file".
6. `shelf notes files`, REST file role, MCP `get_work`, the block "Notes files" on the work page.

**Not in scope**

- OCR; handwriting without a text layer is kept, not searched (Q7).
- Importing the Kindle `My Clippings.txt` as annotations.
- Syncing with a device.

## 2. Feature Files

`Features/NotesFiles/NotesFilesAttach.feature`, `NotesFilesSearch.feature`, `NotesFilesWork.feature`; the web and MCP features get a scenario each.

## 3. Verification

Complete BDD suite and unit tests green; `npm run check`; `shelf reindex` restores the roles.
