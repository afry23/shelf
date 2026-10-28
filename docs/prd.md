# Product Requirements Document: E-Book Manager

| | |
|---|---|
| **Working title** | `shelf` |
| **Status** | v1.0 |
| **Date** | 2026-10-12 |
| **Author** | Afry |
| **Platforms** | macOS (arm64), Linux (x64) |
| **Technology** | C# / .NET 10 |

---

## 1. Summary

`shelf` is a fast, lean e-book manager for a single library that is sorted in a human-readable way on the file system. It offers:

- Tags
- Fast full-text search
- Semantic search
- Duplicate detection
- Automatic organization by a fixed pattern

It is operated via a CLI, an MCP server for AI assistants, and an optional web interface with an integrated PDF and EPUB reader.

Multiple formats of the same book (e.g. EPUB and PDF) are managed as **one work**.

## 2. Problem

Calibre is too slow and too extensive for this use case:

- Conversion, device sync, news download, and plugins are not needed.
- The library is coupled to Calibre's own database.
- Semantic search and MCP access are missing.

Existing alternatives were evaluated and rejected. Reasons include unreadable hash-based folder structures, missing tags or missing duplicate detection, and many are pure readers without management features.

## 3. Goals and Non-Goals

### 3.1 Goals

| ID | Goal |
|---|---|
| G1 | All e-books live in **one** root folder, sorted in a readable way, with no dependency on the tool. |
| G2 | Multiple formats of a book are managed as one work. |
| G3 | Search over metadata and full text responds in < 100 ms with 10,000 works. |
| G4 | Semantic search runs entirely locally and offline, in German and English. |
| G5 | Duplicates are reliably detected and safely cleaned up. |
| G6 | Three equal access paths: CLI, MCP server, web UI. |
| G7 | One single-file binary per platform, without external services. |

### 3.2 Non-Goals

- Format conversion (EPUB ↔ PDF ↔ MOBI)
- Plugin system
- Sync with e-readers (Kindle, Kobo, etc.)
- DRM removal
- Multi-user operation with permission management
- Cloud hosting or SaaS
- Reader functions beyond reading and annotating (highlights, notes), e.g. dictionary lookup or text-to-speech
- Sync of a library across multiple machines
- OCR for scanned PDFs without a text layer (metadata only)

## 4. Target Audience

**Primary:** a technically savvy individual user with a technical-book library (software, architecture, cloud). The collection comprises thousands of books, often in multiple formats, and is partly in German, partly in English. Work is done mostly in the terminal and with AI assistants.

## 5. Core Use Cases

| ID | As a user, I want to … |
|---|---|
| UC1 | … import an unsorted download folder so that the books are recognized, grouped, and filed. |
| UC2 | … find a book by title, author, tag, or content via the CLI in a fraction of a second. |
| UC3 | … search semantically, e.g. "books about event sourcing in distributed systems". |
| UC4 | … tag books, individually and in bulk. |
| UC5 | … find duplicates and clean them up with a preview. |
| UC6 | … let my AI assistant ask for suitable books or passages via MCP. |
| UC7 | … search and browse in the browser and read a PDF or EPUB directly. |
| UC8 | … re-sort the entire library by pattern, with preview and undo. |

## 6. Functional Requirements

Priority according to MoSCoW: **M** = Must, **S** = Should, **C** = Could.

### 6.1 Library and Data Model

| ID | Requirement | Prio |
|---|---|---|
| F-LIB-1 | One library = one root folder. Multiple libraries are possible via configuration. | M |
| F-LIB-2 | Supported formats: EPUB, PDF. | M |
| F-LIB-3 | Further formats (MOBI, AZW3, CBZ, TXT, MD) are tracked as files but not indexed by content. | S |
| F-LIB-4 | The data model separates **Work** and **File**. A work has 1..n files. | M |
| F-LIB-5 | Each work has a sidecar file `book.json` in the work folder. It is the **source of truth** for metadata, tags, reading progress, and "not a duplicate" markings, and holds the work ID (ULID, generated once, never changed). | M |
| F-LIB-6 | The SQLite database is exclusively an index and cache. `shelf reindex` rebuilds it completely from the file system and sidecars. | M |
| F-LIB-7 | Incremental scan: only new or changed files are processed (check via mtime + size, SHA-256 when in doubt). | M |
| F-LIB-8 | Optional watch mode: file changes in the root folder are detected automatically. | C |

**Metadata per work:**

- Required fields: title, authors
- Optional fields: subtitle, language, publisher, publication year, ISBN-10/13, series + volume, edition, citation key (for BibTeX and the like), description, tags, rating (1–5), reading status (unread / currently reading / read), date added, cover path

**Metadata per file:**

- Format, relative path, size, SHA-256, page count (PDF), indexing status

### 6.2 Import and Metadata Extraction

| ID | Requirement | Prio |
|---|---|---|
| F-IMP-1 | `shelf import <path>` takes files from any folder into the library. Default is copy; `--move` moves. | M |
| F-IMP-2 | Metadata is extracted from EPUB (OPF) and PDF (info dictionary, XMP). | M |
| F-IMP-3 | Fallback heuristic: if metadata is missing, it is parsed from the file name, e.g. `Author - Title (Year).pdf`. | M |
| F-IMP-4 | ISBN detection via regex in the first and last pages of the text. | S |
| F-IMP-5 | Cover extraction: the cover image for EPUB, a rendering of the first page for PDF. Stored as `cover.<ext>` in the work folder in the original image format of the source (no transcoding); for EPUB this is the format of the embedded cover, the PDF rendering is a JPEG (quality 85) of page 1 with the longest edge at 600 px, `cover.jpg` (decided 2026-10-17, ADR 0010). | S |
| F-IMP-6 | `shelf enrich <work>` looks a work up online: by ISBN, without one by title and author (only an exact normalized title with a shared author surname counts; several matches are listed for `--pick`). Open Library is asked first, Google Books if it knows nothing (`--source` limits it to one); the first answer wins, sources are not merged. A preview unless `--apply`; only empty fields are filled unless `--overwrite`. The cover is saved as `cover.<ext>` if the work has none (`--no-cover` skips it). **Off** by default, only on explicit command; no other command goes online. The work details of the web interface offer the same (preview, candidates, "Take over"; REST `POST /api/v1/works/{id}/enrich` and `…/enrich/apply`). | C |
| F-IMP-7 | Uncertain detections end up in a review queue (`shelf review`) instead of being filed incorrectly. The queue is persisted in `.shelf/review.json` and survives `shelf reindex`. | S |
| F-IMP-8 | `shelf import --from rclone:<remote>:<path>` fetches the EPUB and PDF files of a cloud folder (Google Drive, Dropbox, OneDrive, …) with the user's own `rclone` into a temporary folder and imports them like local files. Only on explicit command; `--move` is not allowed with `--from`. shelf holds no cloud credentials, they stay in the rclone config. | C |

### 6.3 Grouping Multiple Formats into One Work

| ID | Requirement | Prio |
|---|---|---|
| F-GRP-1 | On import, it is checked whether the file belongs to an existing work. The cascade stops at the first confident match: 1. same ISBN, 2. normalized title + author surname(s), 3. text fingerprint (SimHash over the first ~5,000 words of running text). | M |
| F-GRP-2 | Title normalization: lowercase, Unicode normalization (NFKD), removal of diacritics, punctuation, articles (der/die/das/the/a/an), and additions such as "2nd Edition" or "(German Edition)". | M |
| F-GRP-3 | Every grouping has a confidence score (0–1, see below). If it is below the auto-merge threshold, no automatic grouping takes place; a review entry is created instead. | M |
| F-GRP-4 | Manual correction via `shelf merge <work-a> <work-b>` and `shelf split <work> <file>`; the web interface merges works too (probable pairs, work details). | M |
| F-GRP-5 | Different **editions** are treated as different works but are marked as "related". | S |
| F-GRP-6 | All thresholds are configurable in `config.toml`. The start values below are calibrated against acceptance criterion 1 before M1 is completed. | M |
| F-GRP-7 | A file can be added to an existing work by hand: `shelf add <work> <file> [--replace <file>]` and the work details of the web interface (upload). Typical case: a new version of a book that is still being written (a Manning MEAP). With `--replace` the old file goes to `.shelf/trash/` and `shelf undo` brings it back; reading position and notes of the old file are carried over on request (`--carry-notes`). The same SHA-256 as a file already in the library is refused. | M |
| F-GRP-8 | A single file can be moved to another work or to a new work: `shelf move <work> <file> (--to <work> \| --new)` (dry run unless `--yes`) and the work details of the web interface. Reading position and notes go with the file. A work that loses its last file is removed like in F-DUP-7. `shelf undo` reverts the move. | M |
| F-GRP-9 | A work can be created by hand, without grouping: `shelf create <file> [--title ...]` and "New work" in the web interface. Given values replace the metadata read from the file. A work always has at least one file, so it is created together with its first file (F-LIB-4). | M |

**Confidence per signal (start values):**

| Signal | Confidence |
|---|---|
| Same ISBN-13 | 1.0 |
| Normalized title + author surname(s) identical, year identical or missing | 0.95 |
| Normalized title + author surname(s) identical, year different | 0.6 (probably another edition, see F-GRP-5) |
| Text fingerprint (64 bit SimHash of word triples), Hamming distance ≤ 3 (`auto_hamming`) | 0.9 |
| Text fingerprint, Hamming distance 4–5 (`max_hamming`) | 0.6 |

Calibrated on the generated reference library (1000 works, 1500 files): the files of one work are at most 5 bits apart, different works at least 6. Two guards keep a wrong merge from happening: a title + author match is capped at 0.6 if both files carry an ISBN-13 and the two differ, or if both have a fingerprint and the distance is above `max_hamming` (see `docs/slices/m1-import-scan.md`). Both guards do not apply when the work has no file of the format of the new file yet (a PDF next to an EPUB): print and e-book carry different ISBNs, and the text of a PDF differs from the EPUB (measured on real books: usually more than `max_hamming` bits), so title, author and a year that does not contradict decide. Text cannot tell such a pair from two editions; an edition whose PDF has no year can therefore join the work of the other edition, and `shelf split` undoes it. The fingerprint ignores the first 1500 words (front matter), page numbers and the hyphens at line ends, and the PDF extractor removes running heads and page numbers.

Acceptance check (criterion 1, `Features/Grouping/Calibration.feature`): generated libraries of 389 works / 500 files (100 works as EPUB + PDF, plus exact copies, editions, same-title pairs and series), seeds 1, 2, 3, 42: with these start values 99-100 of 100 pairs are merged, no work holds files of different books, and the library has at most 4 works more than the ground truth. The values are final for M1.

**Thresholds (start values):**

| Confidence | Action |
|---|---|
| ≥ 0.9 | Merge automatically |
| ≥ 0.5 and < 0.9 | Create review entry |
| < 0.5 | Create new work |

### 6.4 Folder Structure and Organization

| ID | Requirement | Prio |
|---|---|---|
| F-ORG-1 | Default pattern: see below. | M |
| F-ORG-2 | The pattern is configurable with placeholders: `{author_sort}`, `{author}`, `{title}`, `{year}`, `{series}`, `{series_index}`, `{language}`, `{first_tag}`, `{ext}`. | M |
| F-ORG-3 | File names are sanitized in a platform-safe way: no forbidden characters, max. 120 characters per segment, Unicode NFC, no trailing dots or spaces. | M |
| F-ORG-4 | Multiple authors: only the first author appears in the folder name. From four authors on, `et al.` is appended. | M |
| F-ORG-5 | `shelf organize` is a **dry run** by default and shows a diff (old → new). Only `--apply` executes. | M |
| F-ORG-6 | Every run writes an undo log to `.shelf/journal/<timestamp>.json`. `shelf undo` reverts the last operation. | M |
| F-ORG-7 | Collisions (target path already exists) are never overwritten. They are reported and skipped. | M |
| F-ORG-8 | Empty folders are removed after moving. | S |

**Default pattern:**

```
<root>/
  Fowler, Martin/
    Patterns of Enterprise Application Architecture (2002)/
      Patterns of Enterprise Application Architecture.epub
      Patterns of Enterprise Application Architecture.pdf
      book.json
      cover.<ext>
  Kleppmann, Martin/
    Designing Data-Intensive Applications (2017)/
      ...
  .shelf/
    index.db
    review.json
    journal/
    models/
    trash/
```

**Rationale:** Two levels (author → work) remain navigable in Finder or the file manager. Works as their own folders keep all formats, the cover, and the metadata together. The year in the folder name separates editions.

### 6.5 Tags

| ID | Requirement | Prio |
|---|---|---|
| F-TAG-1 | Free, hierarchical tags with `/` as separator, e.g. `architektur/ddd` or `cloud/azure`. | M |
| F-TAG-2 | Tags are stored in `book.json` and mirrored in the index. | M |
| F-TAG-3 | Bulk tagging: `shelf tag add <tag> --query "<search>"`. | M |
| F-TAG-4 | Rename and merge tags library-wide: `shelf tag rename`, `shelf tag merge`. | M |
| F-TAG-5 | Tag overview with the number of works per tag. | M |
| F-TAG-6 | Tag suggestions based on semantic similarity to already tagged works. | C |

### 6.6 Search (Metadata and Full Text)

| ID | Requirement | Prio |
|---|---|---|
| F-SRC-1 | Full-text index over metadata and book content using SQLite FTS5. | M |
| F-SRC-2 | Ranking with BM25. Metadata fields (title, author) are weighted higher than content. | M |
| F-SRC-3 | Query language: free text, phrases `"..."`, field filters (`author:`, `title:`, `tag:`, `lang:`, `year:>2015`, `format:pdf`, `status:unread`), negation `-tag:x`, prefix `kube*`. | M |
| F-SRC-4 | Fuzzy search for typos in title and author via trigrams. | S |
| F-SRC-5 | Content hits with snippet and location: page (PDF) or chapter + CFI (EPUB). | M |
| F-SRC-6 | Language-dependent stemming for German and English. | S |
| F-SRC-7 | Title scope: `--title-only` searches only title and subtitle (words, phrases, prefixes, stemming; filters still apply). | S |

### 6.7 Semantic Search

| ID | Requirement | Prio |
|---|---|---|
| F-SEM-1 | Local embeddings with a multilingual model via ONNX Runtime, default `multilingual-e5-small`. No network access at runtime. | M |
| F-SEM-2 | Content is split into chunks of approx. 400–600 tokens with overlap. Each chunk stores its location (page or CFI). | M |
| F-SEM-3 | Additionally, there is one embedding per work built from title, description, table of contents, and tags for work-level searches. | M |
| F-SEM-4 | Vector storage in SQLite via the `sqlite-vec` extension, i.e. no additional database. | M |
| F-SEM-5 | Hybrid search: FTS and vector results are combined via Reciprocal Rank Fusion. | S |
| F-SEM-6 | "Similar books" for a given work. | S |
| F-SEM-7 | Indexing runs in the background, can be interrupted, and resumes where it left off. Progress is displayed. | M |
| F-SEM-8 | The model is replaceable. Changing the model triggers a re-indexing of the embeddings. | S |
| F-SEM-9 | Text that is no prose is not embedded: table of contents, front matter (preface, foreword, acknowledgments, copyright), index, bibliography, glossary and source code. It stays in the full-text index. | S |
| F-SEM-10 | Title scope for semantic and hybrid search: one extra embedding per work from title and subtitle only; `--title-only` compares the query with it (works only, no passages). | S |

### 6.8 Duplicate Detection

| ID | Requirement | Prio |
|---|---|---|
| F-DUP-1 | **Exact duplicates:** same SHA-256 → suggestion to remove all but one file. | M |
| F-DUP-2 | **Same work, same format, different file** (e.g. two EPUBs from different sources) → suggestion with a comparison of size, page count, and metadata completeness. | M |
| F-DUP-3 | **Probable duplicates** across work boundaries, via MinHash/SimHash of the text or high embedding similarity → for manual review only. Start values (configurable, see F-GRP-6): SimHash Hamming distance ≤ 3, or work-level cosine similarity ≥ 0.95. The cosine threshold must be calibrated on the test library, since e5 models also score unrelated texts at 0.7–0.85. | S |
| F-DUP-4 | Deleting always means: move to `.shelf/trash/`. Permanent deletion only via `shelf trash empty`. | M |
| F-DUP-5 | Interactive mode in the CLI and comparison view in the web UI. | M |
| F-DUP-6 | Pairs can be marked as "not a duplicate". They are then not suggested again. The marking is stored in `book.json` (across works: on both sides as the other work's ID; within a work: as a file pair). | S |
| F-DUP-7 | A single file or a whole work can be deleted on purpose: `shelf delete <work> [file]` (dry run unless `--yes`) and the work details in the web interface. It goes to `.shelf/trash/` like every deletion (F-DUP-4) and `shelf undo` brings it back. The last file of a work takes the work along (sidecar, cover, notes). Not offered via MCP. | M |

## 7. Interfaces

### 7.1 CLI

Goal: cold start to first output < 150 ms (Native AOT, see chapter 9).

```
shelf init <root>                         Create library
shelf import <path> [--move] [--dry-run]  Import files
shelf import --from rclone:<spec> [--dry-run]  Import files from a cloud folder via rclone
shelf scan                                Incremental scan
shelf reindex [--embeddings]              Rebuild index; --embeddings only makes the missing embeddings
shelf search <query> [--title-only] [--limit] [--json]   Metadata/full-text search
shelf semantic <text> [--chunks] [--hybrid] [--title-only] [--json]  Semantic search (--hybrid: combined with full text)
shelf model install [<folder>]            Install the embedding model (download, or offline from a folder)
shelf show <work-id|path>                 Details of a work
shelf edit <work-id> [--title ...]        Change metadata (also --add-related / --remove-related)
shelf enrich <work-id> [--apply] [--overwrite] [--pick n] [--source s] [--no-cover]  Load metadata and cover online (preview unless --apply)
shelf tag add|rm|list|rename|merge        Manage tags
shelf dupes [--interactive]               Find and clean up duplicates
shelf trash empty [--yes]                 Delete the trash for good (dry run without --yes)
shelf merge <a> <b> / split <work> <file>
shelf add <work> <file> [--replace <file>] [--carry-notes] [--move]  Add a file to a work (new version); --replace trashes the old file
shelf create <file> [--title ...] [--author ...] [--year ...] [--move]  Create a work by hand from a file
shelf move <work> <file> (--to <work> | --new) [--yes]  Move a file to another or a new work (dry run without --yes)
shelf delete <work> [file] [--yes]      Move a file or a whole work to the trash (dry run without --yes)
shelf review                              Review uncertain assignments
shelf organize [--apply] [--pattern ...]  Organize by pattern
shelf undo                                Revert the last organize, tag or dupes run
shelf open <work-id> [--format pdf]       Open with default app
shelf serve [--port 8420]                 Start web UI + API
shelf mcp [--http]                        MCP server (stdio, optionally HTTP)
shelf stats                               Statistics
```

Further requirements for the CLI:

- Every command supports `--json` for machine-readable output.
- Exit codes: 0 = success, 1 = error, 2 = usage error.
- Colored, tabular output in the terminal. `NO_COLOR` is respected.
- Shell completion for bash, zsh, and fish.

### 7.2 MCP Server

| Tool | Description |
|---|---|
| `search_books` | Metadata/full-text search with the same query language as the CLI |
| `semantic_search` | Semantic search at work or chunk level, with locations |
| `get_book` | Metadata, formats, tags, and table of contents of a work |
| `read_passage` | Text excerpt at a location (page/CFI). Parameter `maxChars`: default 4,000, hard maximum 20,000 (configurable) |
| `similar_books` | Similar works to a work |
| `list_tags` | Tag tree with counts |
| `add_tags` / `remove_tags` | Change tags (write) |
| `find_duplicates` | List duplicate candidates (read-only) |
| `propose_organization` | Dry run of the organization pattern as a diff (read-only) |

Resources:

- `shelf://book/{id}` for metadata
- `shelf://book/{id}/toc` for the table of contents

Requirements for the MCP server:

- Transport: stdio (M), Streamable HTTP (S).
- Write tools can be disabled via configuration (`mcp.read_only = true`; the config keys use snake case). The hard maximum of `read_passage` is `mcp.max_chars`.
- Destructive operations (delete, move) are **not** offered via MCP.

### 7.3 Web API

- REST/JSON under `/api/v1/…`. Feature parity with the CLI applies for read operations, tagging, metadata editing, duplicate review, merging works, and adding, moving and creating (F-GRP-7 to F-GRP-9; a file is uploaded as the body of the request, its name in `?name=`).
- Files are served via `/api/v1/files/{id}`, with HTTP range requests for large PDFs.
- An OpenAPI specification is generated automatically.

## 8. Web Interface and Reader

### 8.1 General

| ID | Requirement | Prio |
|---|---|---|
| F-WEB-1 | Started via `shelf serve`. By default, it binds only to `127.0.0.1`. | M |
| F-WEB-2 | If bound to other interfaces, an access token (`--token` or configuration) is mandatory. | M |
| F-WEB-3 | The UI is embedded into the binary as static assets. No Node.js at runtime. | M |
| F-WEB-4 | Responsive for desktop and tablet, light and dark mode. | M |
| F-WEB-5 | Keyboard control: `/` focuses the search, `j`/`k` navigates, `Enter` opens. | S |

### 8.2 Views

| View | Content | Prio |
|---|---|---|
| **Library** | Grid (covers) or list; faceted filters for tags, author, language, year, format, reading status; sorting | M |
| **Search** | One search field with a Full text / Semantic / Hybrid toggle and an Everything / Titles only scope toggle; hits with snippet and jump into the reader at the location | M |
| **Work details** | Metadata (editable), formats, tags, similar books, merge another work into this one (F-GRP-4), add a file or a new version (F-GRP-7), move a file to another work (F-GRP-8), delete a file or the work (F-DUP-7), load details from the internet with preview and "Take over" (F-IMP-6), "Read" and "Download" buttons | M |
| **Tags** | Tag tree, rename/merge via UI | S |
| **Duplicates** | Side-by-side comparison of candidates with actions "keep", "move to trash", "not a duplicate"; for probable pairs also "merge the other into this one" (F-GRP-4); for different files of one book also "split into own work" (F-GRP-4) | M |
| **Review** | Confirm or correct uncertain assignments | S |
| **Organize** | Diff preview of the organization pattern, execute, undo | S |

### 8.3 Reader

| ID | Requirement | Prio |
|---|---|---|
| F-RDR-1 | PDF reader based on **PDF.js**: page navigation, zoom, text search, table of contents (outline). | M |
| F-RDR-2 | EPUB reader based on **foliate-js**: reflow, font size, theme, table of contents, text search. | M |
| F-RDR-3 | Deep links to locations (`/read/{fileId}#page=42` or `#cfi=...`). Search hits jump directly there. | M |
| F-RDR-4 | Reading progress is saved per file in `book.json` and resumed on opening. Writes are debounced (e.g. at most every 30 s and on closing the reader). | S |
| F-RDR-5 | If a work has multiple formats, the preferred format for the reader is configurable (default: EPUB). | S |
| F-RDR-6 | Bookmarks. | C |
| F-ANN-1 | In the reader a person selects text and creates a **highlight** with a color. Highlights are drawn again when the file is opened. EPUB first, PDF second. | S |
| F-ANN-2 | A highlight or a whole file can carry a **note in Markdown**. A note without a place in the text (a note for the file) is possible. | S |
| F-ANN-3 | Highlights and notes are stored in `annotations.json` next to `book.json` (source of truth, same atomic writes). The index mirrors them and `shelf reindex` restores the mirror. They move with the work on organize, merge and split. | S |
| F-ANN-4 | The web UI lists the highlights and notes in the reader and on the work page. Notes are shown as rendered Markdown and are edited in the reader and on the work page. | S |
| F-ANN-5 | Notes and highlighted text are searchable (full text); a hit links to the place in the reader. | S |
| F-ANN-6 | CLI `shelf notes list/add/rm/export` and MCP tools `get_annotations` (always) and `add_note` (not with `mcp.read_only`). | S |
| F-ANN-7 | A work can be exported as **one Markdown file** with its metadata (YAML front matter), highlights and notes. The file is named `<cite key>.md`; a work without a cite key is not exported. Available as `shelf notes export <work> [--out <folder>]` and as a download on the work page (`GET /api/v1/works/{id}/export.md`). | S |

## 9. Non-Functional Requirements

### 9.1 Performance

Reference: 10,000 works, approx. 15,000 files, Apple M-series or a current x64 laptop.

| Metric | Target value |
|---|---|
| CLI cold start to output | < 150 ms |
| Metadata/full-text search (p95) | < 100 ms |
| Semantic search, work level (p95) | < 300 ms incl. query embedding |
| Semantic search, chunk level (p95) | < 800 ms |
| Incremental scan without changes | < 3 s |
| Initial indexing of metadata + full text | < 15 min |
| Initial indexing of embeddings (CPU) | Background job; goal < 6 h, resumable at any time |
| Web UI: load library view | < 500 ms |
| Idle RAM (`serve`) | < 200 MB |

### 9.2 Platform and Operation

- Self-contained single-file binaries for `osx-arm64` and `linux-x64`. `osx-x64` and `linux-arm64` are not supported.
- CLI, MCP server, and web host ship as **one** binary (decision Q2). Native AOT is the goal; if a required library is not AOT-compatible, ReadyToRun is the fallback.
- No external services (no Python, no Docker, no vector DB) at runtime.
- The embedding model is downloaded once on first need and stored in `.shelf/models/`. Offline installation is possible via `shelf model install <path>`.
- Configuration: `~/.config/shelf/config.toml` (XDG) for global, `<root>/.shelf/config.toml` per library.
- Distribution via GitHub Releases and a Homebrew tap. Optionally also as a `dotnet tool`.

### 9.3 Data Safety and Robustness

- No file access outside the root folder, except during an explicit import.
- All file operations run transactionally via the journal so that recovery after a crash is possible.
- The library remains fully usable without `shelf`: readable folders and readable `book.json`.
- Write access to the SQLite database in WAL mode. CLI, web, and MCP can run in parallel.
- A library is used on one machine only (decision Q6). Sync via iCloud, Syncthing, or similar is not supported.

### 9.4 Privacy

- No telemetry.
- Network access only for the model download, the optional, explicitly triggered online metadata lookup (`shelf enrich`: ISBN, title and author of one work go to Open Library and Google Books), and the explicitly triggered `rclone` call of `shelf import --from` (shelf itself opens no connection for it).

## 10. Architecture

```
┌─────────────┐  ┌──────────────┐  ┌───────────────────┐
│  CLI        │  │  MCP server  │  │  Web (ASP.NET)    │
│  (Spectre)  │  │  (C# MCP SDK)│  │  API + SPA assets │
└──────┬──────┘  └──────┬───────┘  └─────────┬─────────┘
       └────────────────┼────────────────────┘
                 ┌──────▼──────┐
                 │ Shelf.Core  │  Use cases, domain model
                 └──────┬──────┘
   ┌──────────┬─────────┼──────────┬──────────────┐
   ▼          ▼         ▼          ▼              ▼
 Extract   Index      Vector     Organize      Dedupe
 (EPUB,    (SQLite    (ONNX +    (Pattern,     (Hash,
  PDF)      FTS5)     sqlite-vec) Journal)     SimHash)
```

### 10.1 Project Structure (Proposal)

```
src/
  Shelf.Core/          Domain, use cases, query language
  Shelf.Extraction/    EPUB/PDF parsing, metadata, text, cover
  Shelf.Index/         SQLite, FTS5, migrations
  Shelf.Semantic/      Tokenizer, ONNX embeddings, sqlite-vec, chunking
  Shelf.Organize/      Pattern engine, journal, undo
  Shelf.Cli/           Entry point, commands
  Shelf.Mcp/           MCP tools
  Shelf.Web/           ASP.NET Core Minimal APIs, embedded UI
  Shelf.Web.Ui/        Frontend (build time), PDF.js, foliate-js
tests/
  Shelf.Specs/         BDD feature files (Reqnroll) and step definitions
  Shelf.*.Tests/       Unit tests
  fixtures/            Sample EPUBs/PDFs (public domain)
```

### 10.2 Technology Candidates

The candidates must be checked for license and maintenance status before the project starts.

| Area | Candidate |
|---|---|
| Runtime | .NET 10 (LTS) |
| CLI | Spectre.Console.Cli or System.CommandLine |
| EPUB | VersOne.Epub |
| PDF text/metadata | UglyToad.PdfPig |
| PDF cover rendering | PDFium binding (e.g. PDFtoImage) |
| Database | Microsoft.Data.Sqlite + FTS5 |
| Vector search | sqlite-vec (native extension, shipped per platform) |
| Embeddings | Microsoft.ML.OnnxRuntime + tokenizer (Microsoft.ML.Tokenizers) |
| MCP | Official ModelContextProtocol C# SDK |
| Web backend | ASP.NET Core Minimal APIs |
| Web frontend | Svelte 5 + Vite + TypeScript as a single-page application against the REST API (decision Q1). Node.js is a build-time dependency only; the build output is embedded into the binary (F-WEB-3) |
| PDF reader | PDF.js (Apache 2.0) |
| EPUB reader | foliate-js (MIT) |
| Logging | Microsoft.Extensions.Logging, output to stderr (important for MCP stdio) |
| BDD | Reqnroll (Gherkin feature files in English; process rules in `CLAUDE.md`) |

### 10.3 Database Schema (Core)

```
works(id, title, title_norm, subtitle, authors_json, author_sort, language,
      year, isbn13, publisher, series, series_index, edition, description,
      rating, read_status, cover_path, dir_path, added_at, updated_at)
files(id, work_id, format, rel_path, size, sha256, mtime, page_count,
      simhash, text_status, embed_status)
tags(id, path)               -- hierarchical, e.g. "architektur/ddd"
work_tags(work_id, tag_id)
chunks(id, file_id, ordinal, locator, text)   -- locator = page or CFI
works_fts  (FTS5: title, subtitle, authors, description, tags)
chunks_fts (FTS5: text)
vec_works  (sqlite-vec: work_id, embedding)
vec_chunks (sqlite-vec: chunk_id, embedding)
dupe_ignores(a_id, b_id)                      -- mirrored from book.json
review_queue(id, kind, payload_json, created_at) -- mirrored from .shelf/review.json
reading_progress(file_id, locator, updated_at) -- mirrored from book.json
annotations(id, work_id, file_id, kind, locator, quote, color, note, created_at, updated_at) -- mirrored from annotations.json
annotations_fts (FTS5: quote, note)
```

### 10.4 `book.json` (Sidecar)

```json
{
  "schema": 1,
  "id": "01J9Z3K…",
  "title": "Designing Data-Intensive Applications",
  "authors": ["Martin Kleppmann"],
  "language": "en",
  "year": 2017,
  "isbn13": "9781449373320",
  "publisher": "O'Reilly",
  "tags": ["architektur/verteilte-systeme", "datenbanken"],
  "rating": 5,
  "readStatus": "read",
  "files": [
    {
      "name": "Designing Data-Intensive Applications.epub",
      "progress": { "locator": "epubcfi(/6/14!/4/2/10)", "updatedAt": "2026-10-12T19:04:00Z" }
    },
    {
      "name": "Designing Data-Intensive Applications.pdf",
      "progress": { "locator": "page=212", "updatedAt": "2026-09-30T08:12:00Z" }
    }
  ],
  "notDuplicates": {
    "works": ["01J8XQ2…"],
    "files": [["Designing Data-Intensive Applications.epub",
               "Designing Data-Intensive Applications (1).epub"]]
  },
  "related": ["01J7AB1…"]
}
```

`related` (F-GRP-5, added in M2) lists the works that are another edition of this one. It is optional (left out when empty), written on both works, and `schema` stays 1: a shelf that does not know the field keeps it as an unknown field.

## 11. Milestones

| Phase | Content | Result |
|---|---|---|
| **M1 – Foundation** | Data model, sidecars, EPUB/PDF extraction, scan/import, grouping, FTS search, basic CLI commands | Usable CLI for import and search |
| **M2 – Order** | Pattern engine, `organize` with dry run/journal/undo, tags, exact duplicates | Library is sorted and tagged |
| **M3 – Semantics & MCP** | Chunking, embeddings, sqlite-vec, hybrid search, MCP server | AI assistant can use the library |
| **M4 – Web** | API, library/search/detail views, PDF and EPUB reader with deep links | Complete UI |
| **M5 – Polish** | Fuzzy duplicates, review queue in UI, similar books, reading progress, Homebrew release | Release 1.0 |

## 12. Acceptance Criteria (Selection)

1. A folder with 500 mixed files, including 100 works as EPUB + PDF, is imported. At least 95 % of the format pairs are correctly merged into one work, and there are **no** incorrect automatic merges.
2. `shelf organize --apply` followed by `shelf undo` restores the exact initial state (verified by hash comparison).
3. After deleting `.shelf/index.db`, `shelf reindex` restores all metadata and tags from the sidecars.
4. The semantic search "Bücher über Messaging zwischen Microservices" returns the expected technical books in the top 5 in a test library, even with a German query and an English book.
5. Clicking a search hit in the web UI opens the reader at the location (PDF page or EPUB CFI).
6. Claude Desktop can call `search_books`, `semantic_search`, and `read_passage` via MCP without log output disturbing the stdio protocol.
7. All performance targets from 9.1 are met on the reference hardware.

## 13. Open Questions and Decisions

### 13.1 Decisions

| # | Question | Decision |
|---|---|---|
| Q2 | One binary or two? | **One binary** for CLI, MCP server, and web host. |
| Q3 | Embedding model | Start with **multilingual-e5-small**. The model stays replaceable (F-SEM-8); bge-m3 can be evaluated later. |
| Q4 | Work ID | **ULID**, generated once on import and stored in `book.json`. It never changes. |
| Q5 | Annotations/highlights | **Added after 1.0** (F-ANN-1 to F-ANN-6): a separate sidecar `annotations.json` per work, mirrored in the index; EPUB before PDF; notes in Markdown. See [ADR 0018](adr/0018-annotations.md). |
| Q6 | Sync scenario | **No sync.** A library is used on one machine only. |
| Q7 | Scanned PDFs without a text layer | **Ignore.** Such files are tracked with metadata only; no OCR. |
| Q1 | Frontend technology | **Svelte** (Svelte 5 + Vite + TypeScript, single-page application without SvelteKit), decided 2026-10-17 by the project owner. It replaces the earlier choice htmx + RazorSlices (RazorSlices: pre-1.0, single maintainer, Native AOT claim unverified). The server only provides JSON under `/api/v1`, files and the embedded static UI. The UI is built at build time and embedded as static assets (F-WEB-3); Node.js is needed for the build and for UI development, not at runtime. Razor Pages/MVC stay excluded (not Native AOT-compatible, Q2). |

### 13.2 Open

| # | Topic | Question |
|---|---|---|
| Q8 | ONNX Runtime on `osx-x64` | **Obsolete.** `osx-x64` is not a supported platform (9.2), so the missing `osx-x64` native in `Microsoft.ML.OnnxRuntime` since 1.24.1 does not matter. See [ADR 0001](adr/0001-dependency-licenses-and-maintenance.md). |

## 14. Risks

| Risk | Impact | Mitigation |
|---|---|---|
| Poor metadata in PDFs | Incorrect grouping and sorting | Confidence scores, review queue, file-name heuristic, ISBN search in text |
| sqlite-vec as a native dependency | Build/distribution effort per platform | Prebuilt binaries in the package; fallback: brute-force cosine in C# (fast enough at 10k works) |
| Initial embedding indexing takes long | Poor first experience | Background job, work level first, chunks afterwards, resumable |
| Native AOT incompatible with libraries | Slower CLI start | Early AOT spike in M0 (incl. ASP.NET Core Minimal APIs, MCP SDK, ONNX Runtime); ReadyToRun if necessary |
