# Slice m4-covers: Cover Images for EPUB and PDF

| | |
|---|---|
| **Slice ID** | `m4-covers` |
| **Branch** | `feat/m4-library-core` (stacked: same branch as slices 1 to 4, separate commit) |
| **Milestone** | M4 – Web |
| **Roadmap** | M4 slice 5 |
| **PRD references** | F-IMP-5, 7.3, 9.2 (Native AOT), 9.3 (data safety), G7, risk table 14 |
| **Inputs** | [ADR 0010](../adr/0010-pdf-cover-rendering-aot.md) (PDFtoImage 5.4.0, JPEG q85 at 600 px, the pitfalls), [ADR 0005](../adr/0005-epub-pdf-parsers-aot.md) (EPUB cover in its original format), the prototype in `f6089e6:experiments/pdf-cover/` and `experiments/parsers/` |

---

## 1. Scope

**In scope**

1. `CoverExtractor` (`Shelf.Extraction`): the cover of an EPUB in the format of the embedded image (`jpg`, `png`, `gif`, `webp` by the magic bytes), and for a PDF a rendering of page 1 as JPEG (quality 85, longest edge 600 px).
2. `LibraryCovers` (`Shelf.Index`): stores `cover.<ext>` in the folder of a work and names it in the sidecar (`cover`); the index follows (`works.cover_path`).
3. When covers are made: after `import` (for the works it touched), after `scan` and `reindex` (for the works that have a file that was read in that run), and `shelf covers` for every work that lacks one.
4. `GET /api/v1/works/{id}/cover`, and `hasCover` in the list and in the details.
5. The platform decision of ADR 0010 (finding 2): the `CA1416` warning.

**Out of scope**: other sizes (thumbnails), a cover chosen or uploaded by the user, covers from the web (online lookup is F-IMP-6, Could), regenerating a cover that exists, WebP output.

## 2. Feature Files

`Features/Import/Covers.feature` (`@F-IMP-5 @M4`): an EPUB's cover is stored as it was embedded (byte for byte, PNG) and named in the sidecar; a PDF gets a JPEG of 600 px on the longest edge (3 PDFs: text, image only, no metadata); an EPUB cover is preferred to a rendering when a work has both; an EPUB without a cover and a PDF that cannot be rendered give no cover and no failure of the import (the reason is a warning); an existing cover is kept; the cover goes along with `organize` and into the trash with its work; the rule for scans (a file that was read gets a cover, a scan without changes opens no file); `shelf covers` (the work list, the counts, `--json`, nothing to do).

`Features/Web/WebCovers.feature` (`@F-IMP-5 @PRD-7.3 @M4`): the cover as an image with its media type, a conditional request, `hasCover` in the list and the details, a work without a cover, a cover that is gone from the disk, the token.

## 3. Design

- **Where the code is.** `CoverExtractor` is in `Shelf.Extraction` (F-IMP-5 is "extraction" in the PRD, and the project already holds the EPUB and PDF readers). It adds PDFtoImage 5.4.0 and SkiaSharp 4.150.1; their natives (`libpdfium`, `libSkiaSharp`) come with the NuGet packages for the RID and land next to the binary (ADR 0010 R2), so no fetch script is needed (unlike `vec0`).
- **EPUB: the image as it is.** `book.Content.Cover` (VersOne.Epub: the EPUB 3 `cover-image` property, the EPUB 2 `meta name="cover"`, the guide), no transcoding, no image library. The extension comes from the magic bytes, not from the declared media type; an image of an unknown kind is no cover. An image over 20 MiB is no cover.
- **PDF: page 1 as JPEG q85, 600 px.** The page size is read first; `Width = 600` for a landscape or square page, `Height = 600` for a portrait page, with `WithAspectRatio`. `Save*` of PDFtoImage encodes at quality 100, so the bitmap is encoded with SkiaSharp (ADR 0010 R4). A PDF without pages raises `ArgumentOutOfRangeException` in PDFtoImage (ADR 0010 R6) and is mapped like the other bad PDFs.
- **Errors never stop an import.** An unreadable file is an `ExtractionException` (`bad-pdf`, `encrypted`, `io`); `LibraryCovers` collects them as warnings (`cover: <path>: <reason>`), the work stays without a cover and the run goes on.
- **One render at a time.** PDFium is not thread-safe (ADR 0010 R3); a process-wide lock surrounds the rendering, and covers are made one after the other.
- **A work gets one cover: the first file that has one**, EPUBs before PDFs (the embedded image is the better picture), in the order of the sidecar. A work that has a cover whose file exists is left alone.
- **`cover.<ext>` is written next to the files** through a temporary name and a rename, then the sidecar names it. Covers are derived data and can be made again; a crash in between leaves a `cover.*` that the next run finds and adopts. That is why covers are not journaled: nothing that existed is replaced or removed.
- **The cost rule.** The scan without changes must stay below 3 s on 10,000 works (M1 exit criterion): it may open no file. A cover is therefore made only for a work that has a file which was read in the run (new, changed, or any file after a `reindex`). Libraries from before M4 are filled with `shelf covers` (or a `reindex`). Works that have no cover in any file are tried again by `shelf covers` and by a `reindex`, never by a plain scan.
- **The index is told about a new cover, not rebuilt.** `LibraryCovers` writes the sidecar and then updates `works.cover_path` and `works.updated_at` (the time of the sidecar that names the cover) of exactly those works, so that a scan does not read the sidecar again. It does not call the full reconcile of `LibraryEditor`: that is a scan, and after a `reindex` it indexed a work that the rebuild had just refused (found by the existing `Reindex.feature`).
- **`merge` and `split` know about covers.** `merge` keeps the cover of the target and sends the cover of the source to the trash; a target without a cover takes the source's over. Without this the cover stayed in the folder of the work that was merged away and kept the folder from being removed (found by the existing `Merge.feature`). `split` makes a cover for the new work.
- **`shelf covers`** works on the whole library: `Created N covers; M works have none.` With `--json`: `created`, `existing`, `without` (the works that have no cover and no file that gives one), `problems`. Exit code 0 unless the index is unusable (1).
- **Platform analyzer (ADR 0010 finding 2).** PDFtoImage declares a list of platforms; calling it from an assembly without a platform list gives `CA1416` ("reachable on all platforms"), and warnings are errors. shelf supports exactly `osx-arm64` and `linux-x64` (PRD 9.2). The two call sites are surrounded by `#pragma warning disable CA1416` with that reason: a class-level `SupportedOSPlatform` only moves the warning to the callers, and an assembly-level one would have to be repeated in every assembly that calls `Shelf.Extraction`.
- **The cover over HTTP.** `GET /api/v1/works/{id}/cover` sends the file with `ETag` and `Last-Modified` (a conditional request gets `304`); `404` with a reason for a work without a cover or a file that is gone. The grid asks `hasCover` of the list before it requests an image.

## 4. Native AOT

**Result (`osx-arm64`):** 0 warnings (the two `CA1416` warnings of the prototype are handled by the pragma); `libpdfium.dylib` (6.8 MB) and `libSkiaSharp.dylib` (15 MB) land next to the binary, which grows from 35 to 36 MiB. A native run imports a PDF (text), the arXiv paper, an EPUB with a cover and an EPUB without one: three `cover.*` files are made (two JPEG, one PNG), `shelf covers` has nothing to do (`{"created":0,"existing":3,"without":1,"problems":0}`), the list says `hasCover` true, true, true, false, `GET /works/{id}/cover` is `200 image/jpeg` and byte for byte the file on disk, a conditional request is `304`, and the work without a cover is `404 'Quiet Systems' has no cover`.

`linux-x64` is checked by the `aot` job (slice 9): PDFium and SkiaSharp for Linux have not run anywhere yet (ADR 0010: not verified). The `THIRD-PARTY-NOTICES` file with the licenses of PDFium and SkiaSharp (ADR 0010) does not exist yet; it belongs to the M5 distribution work.

## 5. Unit Tests

`CoverExtractorTests` (`Shelf.Extraction.Tests`, 33): the longest edge for portrait, landscape and square pages (hand-written PDFs), only page 1 of several, the same bytes twice, the fixture PDFs, an EPUB cover in each of the four formats and one of an unknown kind, an EPUB without a cover, a file that is no EPUB, a PDF with zero pages, empty, random and truncated files, a missing file, a file of another kind, the kind of an image from its first bytes. The choice between several files, an existing cover, the adoption of a `cover.*`, the rule for scans, `merge` and `split` are covered by the specs; `LibraryCovers` needs no unit test of its own.

## 6. Open Questions

- **Large libraries.** 10,000 covers made by one `shelf covers` run take about 2 to 3 minutes at the 1.6 to 7.4 ms per page measured in ADR 0010 plus file writes; measured in slice 9 on the reference library.
- **Rendering other pages** (a cover page that is blank) is not planned.
