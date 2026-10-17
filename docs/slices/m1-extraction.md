# Slice m1-extraction: Metadata, Text and ISBN from EPUB and PDF

| | |
|---|---|
| **Slice ID** | `m1-extraction` |
| **Branch** | `feat/m1-extraction` (stacked on `feat/m1-index-db`) |
| **Milestone** | M1 – Foundation |
| **Roadmap** | M1 area "Import and extraction"; ADR 0005 hand-off |
| **PRD references** | F-IMP-2, F-IMP-3, F-IMP-4, F-SRC-5, 6.1 (page count), Q6 (ISBN scope), Q12 (language) |
| **Inputs** | [ADR 0005](../adr/0005-epub-pdf-parsers-aot.md) and the prototype in commit `f6089e6` (`experiments/parsers`), `tests/fixtures`, [ADR 0001](../adr/0001-dependency-licenses-and-maintenance.md) rows 4 and 5 |

---

## 1. Scope

**In scope**

1. New project `Shelf.Extraction`, the only project that references VersOne.Epub 3.3.6 and PdfPig 0.1.16.
2. `BookExtractor.ReadMetadata` / `Extract` for EPUB and PDF:
   - EPUB: title, creators (role `aut` or no role; other roles only if there is no author), `dc:language` as written, year from `dc:date`, publisher, description, series (EPUB 3 collection with `group-position`, calibre), identifiers → ISBN.
   - PDF: XMP (`dc:title`, `dc:creator` list, `dc:language`, `dc:identifier`) from the raw bytes with a hardened `XmlReader`, else the Info dictionary (Author split at `;`, `&`, `and`); page count; ISBN from XMP/Info before text.
3. Text by chapter (EPUB, tolerant XHTML scanner) or page (PDF, `GetWords()`), each piece with its locator: `epubcfi(/6/{2(n+1)}[idref]!)` or `page=N`. Pages and chapters without text are skipped; a PDF without any text (a scan) is a result with no segments, not an error.
4. ISBN (F-IMP-4): metadata identifiers first; then the text of PDF pages 1-5 and the last 3, and of the first 3 and last 2 spine items of an EPUB. ISBN-10 only with the word ISBN; `urn:uuid:` is never an ISBN. `Isbn` and `Locators` live in `Shelf.Core.Model` so that grouping, the index and the readers share them.
5. File name fallback (F-IMP-3): `Author - Title (Year).ext` and variants fill a missing title, authors and year; the origin of title and authors is recorded (`FileContent`, `FileName`, `None`) so that grouping can treat file-name evidence as weak.
6. Errors are `ExtractionException` with a kind: `not-a-container`, `bad-epub`, `bad-pdf`, `encrypted`, `unsupported`, `io`, `too-large`, `other`. A PDF without pages is `bad-pdf`. An EPUB archive entry above 64 MiB (or 1 GiB in total) unpacked is refused before any parser reads it.

**Requirements covered:** F-IMP-2, F-IMP-3, F-IMP-4, F-SRC-5 (extraction side; hits with snippets come with `m1-search`).

**Out of scope**

- Chunking of segments (400-600 words for FTS/embeddings), SimHash, hashing, the index writes and worker pool: `m1-import-scan` and `m1-grouping`.
- Covers (F-IMP-5, M4).
- Language detection for PDFs without `dc:language` (ADR 0005 Q12): the language stays empty; decided here, no detection in M1.
- Process isolation (Q15): not done in M1, see Design.

## 2. Feature Files

All in `Features/Import/`, steps in `Steps/ExtractionSteps.cs`:

- `MetadataExtraction.feature` (`@F-IMP-2 @M1`): OPF and PDF metadata on the fixtures, series, no metadata → nothing, no guessed language.
- `FileNameMetadata.feature` (`@F-IMP-3 @M1`): 14 name patterns, rejected years, embedded-wins/file-name-fills on the fixtures.
- `IsbnDetection.feature` (`@F-IMP-4 @M1`): text, identifiers, UUID false positives, ISBN of fixtures.
- `TextLocators.feature` (`@F-SRC-5 @M1`): page and chapter locators, CFI escaping, page parsing, scanned PDF, 7-page PDF.
- `ExtractionErrors.feature` (`@F-IMP-2 @M1`): broken fixtures, six generated broken files, a cut-off PDF, an oversized entry, the file stays unchanged.

Expected values come from the fixture generator (`DerivedFixtures`) and from the raw PDF (`/Count`), not from the extractor.

## 3. Design

- Both parsers are used as ADR 0005 conditions require: `GetWords()` text, XMP from raw bytes, lenient PDF parsing, strict EPUB reading with the spine index as CFI step, every exception → a per-file error. `OutOfMemoryException` is not caught.
- Decision without ADR: **XMP wins over the Info dictionary** (XMP is UTF-8 by definition; Info may use PDFDocEncoding).
- Decision without ADR (**Q15, process isolation**): extraction runs in-process in M1. A crafted file can still exhaust memory below the size limits or overflow the stack (ADR 0005 R-E12/R-P8); the index and sidecars are transactional, so no data is damaged, but a `reindex`/`import` run would end. Revisit with real books or in M5; the import pipeline calls extraction through one method, so a child-process variant can replace it.
- A missing file is checked first: PdfPig reports it as `InvalidOperationException`.
- AOT: a scratch console project referencing `Shelf.Extraction` published with `PublishAot` on `osx-arm64` without warnings and gave the same results on all 13 fixtures. `Shelf.Cli` does not reference the project yet (slice `m1-import-scan`), so the CI AOT job checks it from there.

## 4. Unit Tests

`Shelf.Extraction.Tests`: tolerant XHTML scanner (blocks, skipped elements, entities, quoted `>`, comments, CDATA, truncated input), exception classification and kind names, missing file, size limits, metadata-only reading, no invention without the fallback, helpers. `Shelf.Core.Tests`: ISBN check digits, X, conversion, full-width digits and dashes, runs of digits, UUIDs; locator bounds and page parsing.

## 5. Open Questions

None for the owner. Noted for later: Q12 (language of PDFs) stays open as "no detection"; the EPUB `dc:language` is stored as written (`en-GB`), normalisation for search filters (`lang:`) is a `m1-search` decision.

## 6. Implementation Notes

- The first spine item of many EPUBs is a cover page without text: it is skipped, so the first locator of a book can be `epubcfi(/6/4[...]!)`.
- Reqnroll treats `(a|b)` in a step text as a Cucumber expression; regex steps need `^...$`.
