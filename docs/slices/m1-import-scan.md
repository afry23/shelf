# Slice m1-import-scan: Import, Scan and Content in the Index

| | |
|---|---|
| **Slice ID** | `m1-import-scan` |
| **Branch** | `feat/m1-import-scan` (stacked on `feat/m1-grouping`) |
| **Milestone** | M1 – Foundation |
| **Roadmap** | M1 CLI commands `import`, `scan`, `reindex`; area "Import and extraction"; exit criterion performance |
| **PRD references** | F-IMP-1, F-LIB-7, F-GRP-1, F-GRP-3, F-GRP-6, F-IMP-7, 9.1, 9.3, 6.3 |
| **Inputs** | slices `m1-extraction`, `m1-grouping`, `m1-index-db`; [ADR 0005](../adr/0005-epub-pdf-parsers-aot.md) (at most 4 workers) |

---

## 1. Scope

**In scope**

1. `shelf import <path>... [--move] [--dry-run] [--library]` (F-IMP-1): files or folders (recursive; other file types are counted and ignored). Per file: read it (hash, metadata, text, fingerprint), decide the work, place it, write the sidecar, update the index, queue what is unsure.
   - New work: folder `Author / Title (Year)` (author in "Surname, Given" form, names made safe: `:` → ` -`, slashes → `-`, reserved characters dropped, 120 bytes); a taken folder gets ` (2)`, a taken file name ` (1)`. Nothing is ever overwritten.
   - Existing work: the file joins that work's folder and sidecar.
   - Copy is verified (size; with `--move` also the hash) before the source is deleted. Duplicates (same SHA-256 as a file in the library) are skipped and left in place.
   - Review entries (F-IMP-7): `grouping` (a work it may belong to), `weak-metadata` (title or authors from the file name, or no author), `extraction-error` (unreadable file). The same broken file does not create a second entry.
   - Unknown author: the sidecar gets `Unknown Author`, which has no surname key and never matches.
   - `--dry-run`: the same decisions on the same data, nothing written (also not the queue).
2. `shelf scan [--library]` (F-LIB-7): only what changed.
   - A sidecar with the recorded time (to 100 ns) is not read; a new, changed, moved or deleted work folder is synced; a changed ID in a folder replaces that work.
   - A file with the recorded size and time (ms) is not hashed; a changed one is hashed and, if the hash is new, read again; a touched one (same hash) only gets the new time.
   - A file next to a `book.json` that the index lacks joins that work (the name is added to the sidecar); a file elsewhere is reported as `untracked`, never moved.
   - A file that disappears leaves the index, **not** the sidecar. A file that fails to read is marked `failed`, queued once, and removed from the queue when a later scan reads it.
3. `shelf reindex` now also reads the content (`--metadata-only` keeps the fast variant of slice `m1-index-db`).
4. Content in the index: `files.sha256`, `page_count`, `simhash`, `text_status` (`done`, `none` for a scan without text, `failed`), `chunks` of about 500 words with their locator, `chunks_fts`.
5. Up to 4 files are read in parallel, in batches; results are applied in input order, so the outcome does not depend on timing.
6. Index schema version 2: `dupe_ignores.owner`, so a changed sidecar replaces exactly its own markings. Modification times are stored in milliseconds (files) and 100 ns (sidecars): a file rewritten within the same second is still seen.

**Requirements covered:** F-IMP-1, F-LIB-7, F-GRP-1, F-GRP-3, F-GRP-6 (calibration), F-IMP-7.

**Out of scope**

- Moving files out of a work folder, `merge`, `split`: `m1-commands`. Duplicate handling beyond the skip at import: M2 (F-DUP).
- The journal for file operations (PRD 9.3) and process isolation of extraction (ADR 0005 Q15): M2 / later; the order copy → sidecar → index → delete source keeps the source until the end.
- Performance work and CI benchmarks: `m1-ci-perf`.

## 2. Feature Files

`Features/Import/Import.feature` (`@F-IMP-1 @F-GRP-1 @F-GRP-3 @F-IMP-7`), `Features/Import/Scan.feature` (`@F-LIB-7`); `Reindex.feature` now works on real fixture files and gains the content and `--metadata-only` scenarios. Unit tests: `TextChunker`, `FolderNames`, scanner edge cases, matcher guards, `IndexWriter` across transactions.

`Shelf.TestData` gets `SampleBooks` (public): single EPUB/PDF with chosen title, authors, ISBN and text seed.

## 3. Calibration against the reference library (F-GRP-6)

Method: a library of 1000 works / 1500 files (`generate --works 1000 --files 1500 --seed 42`) is imported; each file's work is compared with `ground-truth.json` (by full path; five file names occur twice).

| Version | Works (true 1000) | Works with files of several true works | True works split |
|---|---|---|---|
| single-word fingerprint (slice `m1-grouping`) | 882 | 108 | – |
| word triples | 983 | 18 | 1 |
| + guards (ISBN differs / texts differ), `max_hamming` 10 | 999 | 2 | 3 |
| + `max_hamming` 5, German edition notes | **1002** | **0** | 2 |

- **Fingerprint:** with single words the files of different works are within 3 bits for thousands of pairs (same language and field: same words). With word triples different works are at least 6 bits apart, the files of one work at most 5; the first 5000 words, each triple once.
- **Guards** (beyond the PRD table): a title + author match is capped at 0.6 if both files have an ISBN-13 and they differ (another edition), or both have a fingerprint above `max_hamming` (another text). Equal ISBNs are not questioned. Later change: the ISBN guard is skipped for a file of a format that the work does not have yet if the fingerprints are at most `auto_hamming` apart (PDF and EPUB of one book rarely share an ISBN). The fingerprint skips the first 1500 words, drops page numbers and joins words split at line ends; `PdfExtractor` removes running heads (`RunningHeads`). `IndexSchema.Version` 4: run `shelf reindex` to recompute the fingerprints.
- **Defaults:** `max_hamming` 10 → **5**; `auto_hamming` stays 3. At 5 the files of one work still match and the edition pairs of the generator (6 and 8 bits) are caught. The PRD table is updated.
- **Edition notes** the generator uses and the normalizer now knows: `(Deutsche Ausgabe)`, `, Neuausgabe`, `(3. überarbeitete Auflage)`.
- **Review entries** name at most five candidates; before, one entry listed hundreds (`review.json` 60 MB).
- The two split works are a PDF without any metadata (title only in the file name, text 6+ bits from the EPUB) and an edition-suffix title; they stay separate and are reviewed. Safe direction.
- Not yet a scenario: the evaluation is a script run by hand. `m1-calibration` turns it into the acceptance scenario (500 files / 100 pairs).

## 4. Performance (first measurement, `osx-arm64`, Native AOT binary, M4)

| Run | Result |
|---|---|
| import of 1475 files (60 MB) | 13 s (70 s before the queue file was saved every 25 entries instead of after each entry) |
| unchanged scan, 1475 files / 882 works | 0.38 s (0.59 s before the review mirror was left alone when nothing changed) |
| index size | 129 MB (chunk text and its full-text index) |

Extrapolated to the reference library (15,000 files): import about 2.5 min (target initial indexing < 15 min), unchanged scan **about 3.8 s (target < 3 s): open, to be tuned in `m1-ci-perf`** (two walks of the tree, the queue file is parsed on every scan).

## 5. Design notes

- `IndexWriter` is the only place that writes the index tables; reindex, scan and import share it. It assigns its own transaction to every command: a prepared command keeps the transaction it was created in (Microsoft.Data.Sqlite), which broke the second transaction.
- Known behavior: macOS (APFS) is case-insensitive and treats `ß` and `ss` as equal, so "Weiß, Carmen" and "Weiss, Carmen" are one folder. The first scan adjusts the stored spelling to the one on disk ("2 works updated").
- Workflow for a folder of loose books: `shelf init <folder>`, then `shelf import --move <folder>` (the books are moved into work folders). `scan` only reports such files.
- Decision without ADR: a duplicate (same content) is skipped at import although duplicates belong to M2; importing it again would double the file.

## 6. Open Questions

None for the owner. For `m1-ci-perf`: scan time, the AOT job on `linux-x64` (the CLI now contains the parsers and SQLite).
