# ADR 0005: EPUB and PDF parsers, VersOne.Epub and PdfPig under Native AOT

| | |
|---|---|
| **Status** | Accepted for `osx-arm64`: VersOne.Epub 3.3.6 and PdfPig 0.1.16 pass all gates there under Native AOT and are adopted for M1; E2/P2 on `linux-x64`, `linux-arm64`, `osx-x64` are pending the first `spike-parsers` run (a failure there triggers the decision rule), see Decision |
| **Date** | 2026-10-16 |
| **Scope** | PRD F-IMP-2 (OPF, PDF Info, XMP), F-IMP-4 (ISBN regex in first/last pages), F-IMP-5 (EPUB cover only; the milestone stays M4), F-SRC-5 (location: PDF page, EPUB chapter + CFI), F-SEM-2 (chunk locator; enabling only), 6.1 (page count), Q7 (scanned PDFs: metadata only), 9.1 (cold start < 150 ms; initial indexing < 15 min at 10,000 works / 15,000 files), 9.2 (four RIDs, Native AOT, ReadyToRun fallback), 9.3 (robustness), 10.2 rows EPUB/PDF, risk table 14 ("Poor metadata in PDFs", "Native AOT incompatible with libraries") |
| **Roadmap** | M0 "AOT spike", parser part only (VersOne.Epub, PdfPig); exit criterion "every incompatible library has a documented fallback (ReadyToRun, ...)"; "Cold start measurement", parser delta only |
| **Inputs** | [ADR 0001](0001-dependency-licenses-and-maintenance.md) rows 4-7, [ADR 0003](0003-toml-parser.md) and [ADR 0004](0004-storage-sqlite-fts5-vector-search.md) (spike/ADR/CI structure), slice plan `docs/slices/m0-spike-parsers.md`, `tests/fixtures/manifest.json`, `tests/Shelf.TestData` |

Order of work, as for ADR 0003 and 0004: the sections Context to Method were written **before any measurement** was taken, but they are not committed separately (the implementer does not commit); the order is documented here and not provable from the repository history. The research (facts from source code at the release tags and from the package files) was done before the criteria were written; no gate was evaluated at that time.

## Context

shelf extracts metadata, full text, an ISBN and (for EPUB) a cover from EPUB and PDF files (F-IMP-2/4/5, F-SRC-5). ADR 0001 adopted VersOne.Epub 3.3.6 (Unlicense) and PdfPig 0.1.16 (Apache-2.0) with the note "no AOT claim" for both (corrected for PdfPig in R3). The CLI is a Native AOT binary (9.2) with a cold start below 150 ms (9.1). This ADR answers whether both libraries work under Native AOT on the four RIDs, how fast and how robust they are on the 13 committed fixtures, on broken inputs, and on the 15,000-file synthetic reference library, and what the fallback costs.

Owner decisions taken before the work (they fix scope and rules):

- **F-IMP-5 stays in M4** in the roadmap. The spike still measures EPUB cover extraction; the hand-off for covers is `@F-IMP-5 @M4` (not M1).
- **Cover storage:** the cover is stored as `cover.<ext>` in its **original image format** (no transcoding, no image library). PRD F-IMP-5 and the work-folder tree in the PRD are changed accordingly (see Consequences).
- **PDF cover rendering** (PDFtoImage 5.4.0, SkiaSharp, PDFium natives, ADR 0001 rows 6/7) is a separate later slice `m0-spike-pdf-cover`; here it is only a risk row. It is an open row in the roadmap's M0 table.
- **Budgets:** T1 15,000 files <= 5 min; 512 MB and 10 s for broken inputs; hand-written EPUB fallback <= 5 days as an estimate only. The hand-written EPUB reader is **not built**; the estimate and its reasoning are in this ADR.
- Other open questions of the plan follow the plan's recommendations (own source index S1..., optional owner corpus not awaited, `experiments/parsers/` kept until M0 closes, separate non-required workflow `spike-parsers.yml`).
- Source index of this ADR starts at S1; ADR 0001 sources are cited as "ADR 0001 Sxx".

## Options

- **A. VersOne.Epub 3.3.6 under Native AOT** (`netstandard2.0` assembly, trimmed with the application).
- **B. Hand-written EPUB reader** over `System.IO.Compression` and `System.Xml.Linq` (fallback for A; estimated, not built).
- **C. PdfPig 0.1.16 under Native AOT** (`net9.0` assemblies resolved for `net10.0`).
- **D. PDFium text API** (`FPDFText_*`) through own P/Invoke on the `libpdfium` of ADR 0001 row 7 (fallback for C; needs its own spike).
- **E. ReadyToRun** for the whole binary instead of Native AOT (PRD 9.2 fallback; an AOT binary cannot host a JIT-compiled library, so the fallback applies to everything).
- Not evaluated: Docnet.Core and other PDF libraries, AngleSharp/HtmlAgilityPack for XHTML (would need the ADR 0001 method), PdfPig nightly `0.1.17-alpha-*`, PdfPig add-on packages.

## Criteria (fixed before measuring)

### Gates, part E: VersOne.Epub (fail = fallback rule)

| ID | Gate |
|---|---|
| E1 | AOT publish of `proto-parsers-epub` and `proto-parsers-all` (`-p:TrimmerSingleWarn=false`) with zero `IL*`/AOT warnings; no `NoWarn`, no `SuppressTrimAnalysisWarnings`, no `TrimmerRootAssembly`, no ILLink descriptor/rd.xml, no `DynamicDependency` in experiment code. |
| E2 | All gated EPUB rows (X-E, L-E, C-E, I-E) pass on the AOT binary on `osx-arm64` and in `check.sh` on all four RIDs. |
| E3 | Differential: `dump` output of the AOT binary is byte-identical to the JIT run of the same code (`dotnet run -c Release`, `PublishAot` off) for all EPUB fixtures, all R-E inputs and the tiny library. |
| E4 | Robustness: every R-E row ends in `ok` or `error kind=<k>` in the child process within 10 s, exit code 0 or 3, no crash/kill, peak RSS < 512 MB (except rows marked observation). |
| E5 | Reference library, all 8,300 EPUBs: title, authors, language, isbn13 equal `files[].embedded` of `ground-truth.json` for 100 %; text non-empty for 100 %; cover present iff trait `cover`; zero errors. |

### Gates, part P: PdfPig

| ID | Gate |
|---|---|
| P1 | As E1 for `proto-parsers-pdf` and `proto-parsers-all`. |
| P2 | All gated PDF rows (X-P, L-P, I-P) pass on the AOT binary on `osx-arm64` and on all four RIDs. |
| P3 | As E3 for all PDF fixtures, R-P inputs and the tiny library. |
| P4 | As E4 for R-P rows. |
| P5 | Reference library, all 6,700 PDFs: page count equals `files[].pageCount` for 100 %; title/authors equal `embedded` for files with `pdf-info`; empty for `pdf-no-metadata`; ISBN found and equal to the work's `isbn13` for 100 % of `pdf-isbn-in-text`; zero ISBNs reported for files with neither `pdf-isbn-in-text` nor an ISBN in Info `Keywords`; zero errors. |

### Gates, part T: throughput and memory (`osx-arm64`, AOT `proto-parsers-all`, warm page cache, AC power; owner-confirmed)

| ID | Gate |
|---|---|
| T1 | `bench` over all 15,000 files of the reference library (metadata + full text + ISBN scan, no cover), threads = processor count: total wall time <= 5 min (one third of the PRD 9.1 budget "initial indexing of metadata + full text < 15 min"; the rest is for hashing, grouping, FTS insert, sidecars). |
| T2 | Peak RSS of the T1 run <= 1 GB. |
| T3 | Large PDFs (two synthetic 100,000-word PDFs): full text per file <= 10 s, peak RSS <= 500 MB, single-threaded. |

**Targets, not gates:** process cold start `meta <fixture>` p95 < 150 ms per fixture; per-file p95 single-threaded per format. **Extrapolation** (recorded as M1 risk, never a gate): words/s and MB/s from T1 applied to a "realistic" library (15,000 files, 100,000 words per book, PDFs of 300 pages with embedded fonts and Flate), stating that the synthetic files are small, uncompressed and Helvetica-only.

### Decision rule

1. E1-E5 pass: VersOne.Epub 3.3.6 adopted for M1 under Native AOT (pin).
2. P1-P5 pass: PdfPig 0.1.16 adopted for M1 under Native AOT (pin).
3. E fails (E1, E3, or E2/E5 caused by the library): hand-written EPUB reader for M1 if the estimate is <= 5 days; VersOne.Epub stays test-only (`Shelf.TestData.Tests`). Estimate > 5 days: escalate (hand-written vs. ReadyToRun for the whole binary).
4. P fails: no hand-written PDF parser. Record the cause and escalate with the options in order: (a) upstream fix if the cause is one annotatable call site (issue/PR, pin a later version); (b) PDFium text API via P/Invoke, shared with PDF cover rendering, needs its own spike; (c) ReadyToRun for the whole binary (PRD 9.2) with the measured R2R cold start and size. Not decided alone.
5. E4/P4 fail through an uncatchable failure (stack overflow, native crash, hang): library adoptable only with input limits or per-file process isolation in M1; record the input and escalate.
6. A T1-T3 miss alone does not reject a library: record the phase profile and the gap as an M1 risk; escalate if the miss is > 2x.

### Checks

Every row has a fixed input and an expected output and runs on the AOT binary: `pass <id>` / `fail <id>: <detail>` / `observed <id>: <value>`; summary `passed X/Y`; exit 1 if a gated row fails. Text comparisons use the text after whitespace collapsing and NFC. The rows and expected values are those of the plan section 3.4 (EPUB X-E1..X-E7 with cover rows C-E and locator rows L-E, PDF X-P1..X-P4 with L-P, ISBN rows I1-I7, broken-input rows R-E1..R-E13 and R-P1..R-P8); they are reproduced in the committed `experiments/parsers/Parsers/Checks.cs` and in `results/measure.out`. Expected values of upstream fixtures that the plan marked (c) are known literary text and were confirmed on the first JIT run (see Measurements).

## Method

- `experiments/parsers/` (not in `shelf.slnx`, not built by `build.yml`): `Baseline` (prints `ok`) and `Parsers` (`Variant=epub|pdf|all` selects the package references; assemblies `proto-parsers-epub`, `-pdf`, `-all`). The experiment `Directory.Build.props` does not import the root props (warnings are collected, not fatal).
- Commands: `info`, `epub-check`, `pdf-check`, `isbn-check`, `mutate`, `robust`/`robust-one` (child process per input, 10 s kill timeout), `meta`, `extract`, `dump`, `verify`, `bench`, `corpus`.
- `measure.sh` (`osx-arm64`, AC power) writes `results/measure.out`; `check.sh <rid>` is the portable functional subset for the four RIDs; `.github/workflows/spike-parsers.yml` runs it (non-required, not part of the `ci` aggregate).
- Timing as in ADR 0003/0004: `zsh $EPOCHREALTIME` loop (5 warmups, 50 runs, nearest-rank p95), because `hyperfine` is not installed.

## Research

Facts from source code at the release tags and from the package files (the `.nupkg` files restored from NuGet; nothing downloaded was executed). Sources are in the index at the end.

| ID | Result |
|---|---|
| R1 | VersOne.Epub 3.3.6: `lib/` has `net46`, `netstandard1.3`, `netstandard2.0`; `net10.0` resolves `netstandard2.0` (185 KB `VersOne.Epub.dll`). The nuspec dependency group for `netstandard2.0` is empty (no dependencies). License Unlicense. (S3) |
| R2 | PdfPig 0.1.16: `lib/` has `net462`, `net471`, `net6.0`, `net8.0`, `net9.0`, `netstandard2.0`, `netstandard2.1`; `net10.0` resolves `net9.0` (no dependencies in that group). One package contains seven assemblies: `UglyToad.PdfPig` (4.1 MB), `.Core`, `.Fonts` (1.0 MB), `.Tokenization`, `.Tokens`, `.DocumentLayoutAnalysis` (0.24 MB) and `.Package`. `ContentOrderTextExtractor` and `NearestNeighbourWordExtractor` ship in the main package (DLA assembly), no sub-package is needed for them. Add-on packages exist on NuGet (`PdfPig.Rendering.Skia`, `PdfPig.Filters.Dct.JpegLibrary`, `.Jbig2.PdfboxJbig2`, `.Jpx.OpenJpeg`, search of 2026-10-16); they are for rendering and image filters and are not needed for text. (S3, S4) |
| R3 | AOT claims at the tags: VersOne.Epub (tag `v3.3.6`, commit `0fed46c`) sets none of `IsTrimmable`, `IsAotCompatible`, `EnableTrimAnalyzer`, `PublishTrimmed` and has no target framework newer than `netstandard2.0`/`net46` (no analyzers possible). PdfPig (tag `v0.1.16`, commit `a7bb356`, the commit named in the nuspec): none of the six `src/` projects (`PdfPig`, `.Core`, `.Fonts`, `.Tokenization`, `.Tokens`, `.DocumentLayoutAnalysis`) nor `tools/UglyToad.PdfPig.Package` (seven projects in the package) sets them in its own `*.csproj`, **but `src/Directory.Build.targets` sets `IsTrimmable=true` for target frameworks compatible with `net6.0` and `IsAotCompatible=true` for those compatible with `net8.0`**, so the `net9.0` assemblies resolved for `net10.0` are flagged AOT-compatible. This corrects ADR 0001's "no AOT claim" for PdfPig (ADR 0001 read the project files only); the measured result is unchanged. (S1, S2) |
| R4 | Reflection and dynamic code, grep at the tags: VersOne.Epub has one hit, `typeof(EpubRemoteContentLoader).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()` (user-agent string of the remote content downloader, not on the local-file paths). PdfPig has `new XmlSerializer(typeof(...))` in four methods of the DLA exporters (`AltoXmlTextExporter`, `PageXmlTextExporter`), unreachable from the prototype, and `Assembly.GetManifestResourceStream` for embedded resources (R8). No `Activator.CreateInstance`, `Type.GetType`, `MakeGenericType`, `Expression.Compile`, `XslCompiledTransform`, `RegexOptions.Compiled` or `dynamic` in either. `PdfDocument.AddPageFactory<TPage, TPageFactory>` carries a `DynamicallyAccessedMembers` annotation (partial awareness, not an AOT claim). (S1, S2) |
| R5 | XML: VersOne.Epub loads every XML part with `XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, Async = true })` and `XDocument.Load(reader)` (`Utils/XmlUtils.cs`), and wraps `XmlException` into `EpubContainerException` ("package file is not a valid XML file", `PackageReader.cs`). With `DtdProcessing.Ignore` an entity declared in a DOCTYPE is not defined, so any reference to it is a parse error; no external entity can be fetched. PdfPig `XmpMetadata.GetXDocument()` is `XDocument.Parse(OtherEncodings.BytesAsLatin1String(GetXmlBytes()))`: it decodes the UTF-8 XMP packet as Latin-1 (non-ASCII text becomes mojibake, measured in X-P2) and, in dotnet/runtime `v10.0.0`, `XDocument.Parse` uses `DtdProcessing.Parse` with `MaxCharactersFromEntities = 10,000,000` and no resolver (the default `XmlReaderSettings` resolver is null unless an app switch enables it). (S1, S2, S5) |
| R6 | Encodings: PdfPig `NameTokenizer` registers `CodePagesEncodingProvider.Instance` in its static constructor and decodes names with `windows-1252`; `OtherEncodings` uses `ISO-8859-1`, the TrueType name table parser asks for `ISO-10646-UCS-2`. VersOne.Epub has no `Encoding` call; it reads text with `new StreamReader(stream)` (UTF-8 with BOM detection) and ignores the XML declaration of the chapter (source-based). So a code page provider is needed only by an XML reader that honors a declared `windows-1252` (variant (a), R-E11). (S1, S2) |
| R7 | Compression: VersOne.Epub uses the in-box `System.IO.Compression.ZipArchive`/`ZipFile`. PdfPig filters in `Filters/`: Ascii85, AsciiHex, CCITT, DCT, Flate (`DeflateStream`), JBIG2, JPX, LZW, RunLength; `DctDecodeFilter.IsSupported` is `false` and `Decode` throws `NotSupportedException` (JBIG2 and JPX are likewise placeholders); these are image filters, text extraction does not call them. (S1, S2) |
| R8 | PdfPig embedded resources: `Resources/CMap` 3.3 MB, `Resources/ICC` 4 KB (main assembly), `AdobeFontMetrics` 652 KB and `GlyphList` 88 KB (Fonts assembly); read through `GetManifestResourceStream` (CMapParser, GlyphListFactory, Standard14). X-P2 (Standard-14 Helvetica metrics) works in the AOT binary. (S2) |
| R9 | API used by the prototype. VersOne.Epub: `EpubReader.OpenBookAsync(path)` (lazy `EpubBookRef`), `ReadBookAsync` for the X-E9 comparison, `Schema.Package.Metadata` (`Titles`, `Creators` with `FileAs`/`Role`, `Languages`, `Identifiers` with `Scheme`, `MetaItems` with `Name`/`Property`/`Refines`), `Schema.Package.Spine.Items`/`Manifest`/`Guide`, `GetReadingOrderAsync()` (all spine items in order, throws on a missing manifest item or file), `GetNavigationAsync()`, `Content.Cover` (rules in `BookCoverReader.cs`: EPUB 3 `cover-image` property, then EPUB 2 `<meta name="cover">`, then the guide reference of type `cover`), `EpubLocalTextContentFileRef.ReadContentAsync()`/`GetContentStreamAsync()`. Exceptions: `EpubContainerException`, `EpubPackageException`, `EpubContentException`, `InvalidDataException` (from ZipArchive). PdfPig: `PdfDocument.Open(path, ParsingOptions)` (`UseLenientParsing` default true, `MaxStackDepth` 256), `Information`, `NumberOfPages`, `GetPage(n)` (`Text`, `GetWords()`, `NumberOfImages`), `TryGetXmpMetadata`, `Structure.Catalog.CatalogDictionary` for `/Lang`. Exceptions: `PdfDocumentFormatException`, `PdfDocumentStackDepthException` (both in `UglyToad.PdfPig.Core`), `PdfDocumentEncryptedException`, and unclassified runtime exceptions (R-P6). (S1, S2) |
| R10 | .NET docs: Native AOT forbids dynamic loading and runtime code generation; `TrimmerSingleWarn=false` shows each trim warning instead of one per assembly; `RegexOptions.Compiled` is ignored by the source generator and `[GeneratedRegex]` is preferred. Not verified from the docs: the exact trim mode applied to assemblies without `IsTrimmable` under `PublishAot`; the zero-warning result (E1/P1) with `TrimmerSingleWarn=false` is the evidence. A stack overflow cannot be caught in .NET (not looked up in the docs; no row overflowed, R-P8 and R-E12 end with a controlled outcome). (S6) |
| R11 | Fallbacks: see "Fallback cost" below. |

## Measurements

Environment (from `results/measure.out`, run of 2026-10-16 19:00-19:06 UTC): macOS 26.6.2 arm64, Apple M4 (`hw.ncpu` 10 = 4 performance + 6 efficiency cores), 24 GiB RAM, .NET SDK 10.0.401, runtime 10.0.12, machine on **AC power** (`pmset`: "Now drawing from 'AC Power'"), warm page cache. `hyperfine` is not installed, so the fallback of ADR 0003 was used (zsh `$EPOCHREALTIME` loop, 5 warmups, 50 runs, nearest-rank p95). The owner corpus (`corpus/`) was **not** supplied; `corpus` was only exercised on the 13 fixtures.

### AOT publish, sizes, culture (E1, P1)

- Warnings (all codes, `-p:TrimmerSingleWarn=false`): **0** for `Baseline`, `Variant=epub`, `Variant=pdf`, `Variant=all`, both code page variants and the ReadyToRun publish. No `NoWarn`, `SuppressTrimAnalysisWarnings`, root descriptor or `DynamicDependency` is used (the experiment `Directory.Build.props` sets neither).
- Sizes (bytes): baseline 1,236,104; `epub` 9,400,448 (+8,164,344 for VersOne.Epub with `System.Xml.Linq` and `ZipArchive`, not split further); `pdf` 16,228,176 (+14,992,072 for PdfPig); `all` 16,907,040 (+15,670,936). `CodePagesEncodingProvider` adds **0 B** to `all` (PdfPig references it itself, R6) and 743,472 B to `epub` alone. ReadyToRun, self-contained, single file: **107,458,434 B** (6.4 times the AOT binary).
- `info`, `isbn-check`, `epub-check`, `pdf-check` produce byte-identical output under `LANG=de_DE.UTF-8 LC_ALL=de_DE.UTF-8` (timing rows X-E9 excluded); `dump` of the fixtures under `de_DE` equals the invariant dump.

### Check rows (AOT binary, `osx-arm64`)

- `isbn-check` 9/9 gated (I1-I5, I7, conversion, check digit, and the identifier rules `I-identifier`, which was **added post hoc** after the first `verify` run found the UUID false positives). I6 `call 0306406152 now` yields **none** (ISBN-10 without `ISBN` prefix rejected, Q6); with the prefix `9780306406157`.
- `epub-check` **32/32** gated rows: X-E1..X-E7, C-E, L-E1, L-E2, I-E, and 7 cross-checks against `manifest.json`. The upstream text rows marked (c) in the plan (`I call our world Flatland`, `Als Gregor Samsa eines Morgens aus unruhigen Träumen erwachte`) **matched on the first JIT run**, no row had to be corrected. X-E4 language is `en-GB` (rule "starts with `en`"). Covers: Gutenberg EPUB 2/3 PNG 70,019 B (declared `image/png`, rule `epub2-meta` / `epub3-property`), Gutenberg German JPEG 36,133 B, Standard Ebooks JPEG 302,255 B (`epub3-property`), X-E6 PNG 64 x 96 (18,596 B, `epub2-meta`). X-E5: `urn:isbn:9780000000019`, series `Streaming Foundations` #2 (`belongs-to-collection` + `group-position`); X-E6: `9780000000026` scheme `ISBN`, series `Verteilte Systeme` #1 (`calibre:series`). X-E8: author `Jördis Ålander|fileAs=|role=` (VersOne.Epub does not apply the `refines` metas `file-as`/`role` of EPUB 3 to the creator; the metas are only in `MetaItems`). Locators: spine counts 5/4/3/33 (upstream) and 5 (synthetic); the Gutenberg nav has 35 entries mapped onto 4-5 spine items (fragments), Standard Ebooks 33 of 33; CFI of the first item e.g. `epubcfi(/6/2[coverpage-wrapper]!)`. X-E9: `OpenBookAsync` + metadata 0.09-0.28 ms median vs. `ReadBookAsync` 0.12-3.55 ms (the 1 MB Standard Ebooks file) in the warm process.
- `pdf-check` **14/14** gated rows. X-P1 (arXiv, Ghostscript): Info Title and Author are **empty**, XMP present but `dc:title`/`dc:creator` empty (the manifest title `On Hilbert's 16th Problem` does not come from the file's metadata), 7 pages, page 1 contains `Hilbert`. X-P2: Title/Author from the Info dictionary (UTF-16BE `Jördis Ålander` decoded correctly), XMP title and creator equal, text `by Jördis Ålander` and `ISBN 978-0-00-000001-9`, ISBN `9780000000019`. X-P3: no metadata, page 1 starts `Quiet Systems by Jane Doe ...`. X-P4: Title `Scanned Page`, 1 page, 1 image, no text, classified `no-text-layer`. X-P6: no catalog `/Lang` and no `dc:language` in any PDF fixture. X-P7: `UseLenientParsing` false and true give identical output on the arXiv fixture.
- **X-P5 (decides Q5):** on arXiv page 1 `Page.Text` has 1,778 characters but only **5** whitespace-separated words (word spaces missing), `GetWords()` gives 390 words and `ContentOrderTextExtractor` 323 words; none contains `On Hilbert` space-separated (title font). On the synthetic PDFs `Page.Text` also drops the line breaks (`Quiet Systemsby Jane Doe` in the first run). The prototype therefore builds the text from `GetWords()` joined by spaces; `Page.Text` must not be used for FTS or ISBN scanning.
- **XMP:** `XmpMetadata.GetXDocument()` returns `JÃ¶rdis Ã lander` for X-P2 (R5); the prototype parses `GetXmlBytes()` itself with an `XmlReader` (DTDs ignored, no resolver) and gets `Jördis Ålander`.

### Robustness (E4, P4; child process per input, 10 s timeout, peak RSS from the child)

| Row | Outcome (AOT, identical under JIT) | Peak RSS |
|---|---|---|
| R-E1 malformed OPF | error `EpubContainerException` (kind `bad-epub`) | 17 MiB |
| R-E2 not a zip, R-E3 zero bytes, R-E4 truncated 50 % | error `InvalidDataException` (`not-a-container`) | 14 MiB |
| R-E5 no container.xml, R-E6 missing OPF | error `EpubContainerException` | 16-17 MiB |
| R-E7a spine `itemref` without manifest item | error `EpubPackageException` | 18 MiB |
| R-E7b manifest `href` to a missing chapter | error `EpubContentException` | 18 MiB |
| R-E8 external entity `file:///etc/hosts` | error `EpubContainerException` (inner `XmlException`: "Reference to undeclared entity 'x'" under `DtdProcessing.Ignore`; no external resolution) | 17 MiB |
| R-E9 billion laughs | error `EpubContainerException` (inner `XmlException`: "Reference to undeclared entity 'a9'"), 4 ms | 17 MiB |
| R-P1 zero bytes, R-P2 zip bytes, R-P3 truncated 50 % | error `PdfDocumentFormatException` (`bad-pdf`) | 14-15 MiB |
| R-P4 `startxref` 0 | **ok**, 1 page, text read (lenient reconstruction) | 29 MiB |
| R-P5 `/Kids` loop | **ok with 0 pages** and no text (no error, no hang) | 26 MiB |
| R-P6 `/Length 2147483647` | error `System.IndexOutOfRangeException` (kind `other`: not a PdfPig type) | 27 MiB |
| R-P7 random bytes under `/FlateDecode` | **ok**, 1 page, empty text | 27 MiB |

Observations: R-E14 (added after the first review, observation row): an OPF with a public DOCTYPE (`-//IDPF//DTD OPF 2.0//EN`) and no entities parses normally (ok, 5 spine items), so the `DtdProcessing.Ignore` setting does not reject the DOCTYPE found in real EPUB 2 packages; the inner `XmlException` messages of the rejected rows are recorded in `results/measure.out` (R-E1: mismatched end tag; R-E8/R-E9: "Reference to undeclared entity"). Not tested: a public DOCTYPE in the NCX or in chapter files together with named entities from that DTD (chapters with `&nbsp;`: R-E10 below). R-E10 (XHTML 1.1 DOCTYPE with `&nbsp;`): variant (b) decodes it (`Alpha Beta`), variant (a) fails with `XmlException` (named entity undefined, DTD ignored). R-E11 (`windows-1252`, byte `0xE4`): variant (a) fails with `XmlException` without a registered provider and returns `ä` with `CodePagesEncodingProvider` (`--codepages` build). R-E12 (10,000 nested `<div>`): ok, no overflow in either variant. R-E13 (256 MiB of zeros, 256 KB on disk): **ok after 1.1 s with a peak RSS of 2,875 MiB (2.8 GiB)** (VersOne.Epub reads the entry into a string, the stripper and the strict reader copy it): an M1 input limit is needed. R-P8 (100,000 nested arrays): `PdfDocumentStackDepthException` in 0 ms (PdfPig's `MaxStackDepth` 256), no stack overflow. No row timed out, crashed or exceeded 512 MiB except the observation R-E13.

`mutate` is deterministic (two runs, identical SHA-256 list).

### Differential (E3, P3)

`dump` (canonical JSON per input) of the AOT binary and of the JIT run (`dotnet build -p:PublishAot=false`) is **byte-identical** for the 13 fixtures (16,422 B), the tiny library (30 files, 41,720 B) and the 17 mutated inputs of the gated R-rows run in isolated child processes (2,982 B; the observation rows R-E10..R-E14 and R-P8 are skipped by `dump --isolate` because their outcome depends on memory and time, e.g. R-E13 uses 2.8 GiB; they stay in the `robust` output), for EPUB and PDF lines separately.

### Reference library (E5, P5; 15,000 files, 8,300 EPUBs and 6,700 PDFs)

`verify` passes everything: EPUB title, authors, language, ISBN-13 (`OpfIsbn`), text non-empty, cover iff trait, zero errors **8,300/8,300** each; PDF page count, title, authors (split at `;`), XMP present iff trait and `dc:title` equal, text ISBN equals the work's ISBN iff `pdf-isbn-in-text` and none otherwise, combined ISBN (Info Keywords, then text) equals `embedded`, zero errors **6,700/6,700** each. Wall time 46.2 s (10 threads). The first run of `verify` found 3 EPUBs wrongly reporting an ISBN: the OPF identifier was `urn:uuid:...` and the first implementation stripped everything that is not a digit; the 13 digits of a UUID form a valid ISBN-13 check digit in 1 of 10 cases. `FromIdentifier` now accepts only `urn:isbn:`/`isbn:` prefixes or identifiers made exclusively of digits, `X`, spaces and hyphens (self-check row `I-identifier`). This is an M1 hazard for any ISBN extraction from identifiers.

### Throughput and memory (T1, T2, T3; AOT `all`, metadata + full text + ISBN, no cover)

- **T1:** all 15,000 files, 10 threads: **47.0 s** wall, 319 files/s, 12.3 MB/s, 1.77 M words/s, zero errors (limit 300 s). **T2:** peak RSS **142 MiB** under `/usr/bin/time -l` (in-process `getrusage`: 142 MiB; limit 1 GiB).
- Single thread, per format (nearest-rank, ms per file): EPUB 8,300 files in 2.38 s (3,488 files/s, 19.3 M words/s, total p50 0.26, p95 0.43, max 4.3); PDF 6,700 files in 48.6 s (138 files/s, 0.77 M words/s; open p50 0.07, text p50 6.7 / p95 9.3 / max 24.0, ISBN scan p50 0.03, total p50 7.2 / p95 9.9 / max 36.0).
- **Thread scaling (observation, 3,000 files):** EPUB 0.81 s (1 thread), 0.46 s (2), 0.35 s (4), 0.38 s (10); PDF 21.9 s, 13.6 s, 10.2 s, **19.5 s**. PdfPig stops scaling at about 4 threads on this 4 performance + 6 efficiency core machine and is slower at 10 threads than at 4 (cause not investigated: efficiency cores, lock contention or memory bandwidth). The 10-thread speedup over one thread for the whole library is only 1.08x.
- **T3:** the two 100,000-word PDFs (180 and 187 pages, ground-truth page counts match): full text in 0.14-0.15 s per process, peak RSS 36-37 MiB (limits 10 s, 500 MiB). The text phase of one large PDF takes 118-125 ms (about 0.8 M words/s).
- **Extrapolation (M1 risk, not a gate):** a realistic library of 8,300 EPUBs and 6,700 PDFs with 100,000 words each takes about 43 s (EPUB) + **872 s** (PDF) = 915 s on one thread (printed by `measure.sh`), 847 s (14.1 min) with the measured 1.08x speedup of the 10-thread run (also printed), and about **425 s** with the 2.15x speedup of the 4-thread PDF run (915 / 2.15; derived in this ADR from the scaling rows, not printed by `measure.sh`), against the 15 min end-to-end budget of which extraction gets one third (300 s) in the T1 split. Caveat: the synthetic files are small, uncompressed, Helvetica-only, 6-16 pages; real books have embedded fonts, Flate streams and complex layout, so this number is more likely too low than too high. M1 must measure on real books (owner corpus) before it promises the budget.
- Phase profile (single thread, PDF): the text phase (`GetWords`) is 93 % of the per-file time (6.7 of 7.2 ms at p50); EPUB spends 0.08 ms (open) and 0.14 ms (text) at p50.

### Cold start (target)

`proto-parsers-baseline` process: median 3.3 ms, p95 3.6 ms. `meta <fixture>` on the AOT `all` binary, p95 over all 13 fixtures including the error fixtures: EPUB **5.9-15.1 ms** (5.9 ms is `not-a-zip.epub`, which fails early; valid EPUBs 8.9-15.1 ms), PDF **24.2-30.8 ms**, maximum 30.75 ms (arXiv PDF); ReadyToRun single file: EPUB 36.2-54.0 ms (`not-a-zip.epub` 36.2), PDF 71.4-85.6 ms, maximum 85.57 ms. Both are below the 150 ms target of PRD 9.1; the PdfPig delta over the baseline is about 21-27 ms in AOT (static initialization of fonts and CMaps is not split further). Timing noise between two runs of the same binary is a few ms (one valid EPUB fixture varied between 9 and 15 ms p95).

### Four platforms

`check.sh osx-arm64` ran locally (`results/ci/osx-arm64.txt`). The other three RIDs (`linux-x64`, `linux-arm64`, `osx-x64`) can only be run by the `spike-parsers` workflow, which the implementer did not run; its first run on the pull request decides E2/P2 there (see Decision). The R-E13 observation needs about 3 GiB of memory on the runner (2.8 GiB peak here); if a runner has too little memory that row (observation, 10 s timeout) may be killed without failing a gate.

## Gate results (`osx-arm64`, AOT binary)

| Gate | Result | Measured |
|---|---|---|
| E1 AOT publish `epub` and `all`, 0 warnings | **pass** | 0 / 0 |
| E2 EPUB rows | **pass** on `osx-arm64`; three other RIDs pending CI | 32/32 rows, 0 failed |
| E3 differential | **pass** | byte-identical (fixtures, tiny, 17 gated mutations) |
| E4 robustness R-E | **pass** | 10 gated rows controlled, <= 18 MiB, <= 15 ms |
| E5 8,300 EPUBs | **pass** | 8,300/8,300 for every check, 0 errors |
| P1 AOT publish `pdf` and `all`, 0 warnings | **pass** | 0 / 0 |
| P2 PDF rows | **pass** on `osx-arm64`; three other RIDs pending CI | 14/14 rows, 0 failed |
| P3 differential | **pass** | byte-identical |
| P4 robustness R-P | **pass** | 7 gated rows controlled (3 ok: R-P4, R-P5, R-P7; 3 `PdfDocumentFormatException`: R-P1/2/3; 1 `IndexOutOfRangeException`: R-P6), <= 29 MiB |
| P5 6,700 PDFs | **pass** | 6,700/6,700 for every check, 0 errors |
| T1 15,000 files <= 5 min | **pass** | 47.0 s |
| T2 peak RSS <= 1 GiB | **pass** | 142 MiB |
| T3 large PDFs <= 10 s / 500 MiB | **pass** | 0.14 s / 37 MiB |
| Target: cold start p95 < 150 ms | met | AOT 30.75 ms max; ReadyToRun 85.57 ms max |

## Decision

**Option A, VersOne.Epub 3.3.6, and option C, PdfPig 0.1.16, are adopted for M1 under Native AOT (pins)**: all of E1-E5, P1-P5 and T1-T3 pass on `osx-arm64`. Decision rule steps 1 and 2 apply; steps 3-6 are not triggered on this platform. E2/P2 on `linux-x64`, `linux-arm64` and `osx-x64` are pending the first `spike-parsers` run: an E failure there triggers step 3 (hand-written EPUB reader, estimated below at 3-4 days, within the 5-day limit), a P failure triggers step 4 (escalate with the options in order, no decision alone).

The adoption carries conditions that M1 must implement (they are findings, not gate failures):

1. **PDF text from `GetWords()`, never `Page.Text`** (X-P5). Cost: 0.77 M words/s per thread (see extrapolation).
2. **XMP from the raw bytes** with an `XmlReader` (DTD ignored, no resolver), not from `GetXDocument()`.
3. **Treat any `Exception` from either library as a per-file error** (R-P6 throws `IndexOutOfRangeException`; R-P5 returns zero pages without an error: zero pages of a non-empty PDF is a review entry, not success).
4. **Input limits** for EPUB entries: R-E13 (256 KB on disk) costs 2.8 GiB of memory with VersOne.Epub's string read; limit the uncompressed entry size (for example 64 MiB, to be chosen with real books) before reading and decide process isolation per Q15 (not needed for the other rows).
5. **At most 4 extraction workers** until measured otherwise on other hardware (PDF scaling).

## Fallback cost (R11)

| Option | Cost and risk |
|---|---|
| E. ReadyToRun for the whole binary (PRD 9.2) | Measured: single-file self-contained `proto-parsers-all` is **107.5 MB** (AOT 16.9 MB, 6.4x), cold start `meta` p95 up to **85.6 ms** (AOT 30.8 ms), still below 150 ms; zero publish warnings; JIT at run time. Works on all four RIDs by construction (no per-library AOT dependence) but applies to the whole binary and conflicts with G7 in size. |
| B. Hand-written EPUB reader (not built) | **Estimate 3-4 days, within the 5-day limit.** Reasoning: VersOne.Epub is about 7,200 lines (3,100 in `Readers/`) because it covers EPUB 2/3, remote content, SMIL media overlays, options and events that shelf does not need. What shelf needs: `container.xml` (about 40 lines), OPF package with Dublin Core metadata, `refines` (file-as, role, group-position), EPUB 3 collections and calibre series, identifiers (about 250), manifest/spine/guide and href resolution (relative to the OPF directory, percent-decoding, fragments; about 120), cover rules (about 60), nav/NCX entry counts (about 120, optional for M1), ZIP access with `ZipArchive` and an entry size limit (about 60), error classification (about 60), which sums to about 710 lines; with the tolerant XHTML-to-text stripper (about 150, needed with either reader) the total is roughly **860 lines plus tests**. For scale, the prototype's `Epub.cs` (327 lines) and `XhtmlText.cs` (165 lines) already implement the extraction logic on top of VersOne.Epub; a replacement reader replaces the library calls, not that logic. The 8,300-EPUB `verify` and the 17 gated mutated inputs here already exist as an oracle. The risk is real-world EPUB quirks the 13 fixtures do not show, not the amount of code. VersOne.Epub would remain test-only (`Shelf.TestData.Tests`). |
| D. PDFium text API (`FPDFText_*`) through P/Invoke | Not needed (P passes on `osx-arm64`). If P2 fails on another RID: needs its own spike together with PDF cover rendering (below); PDFium is not thread-safe; not evaluated here. Docnet.Core and other libraries are not evaluated (they need the ADR 0001 method). |
| Upstream fix | Not needed. The three PdfPig defects found (XMP decoding, `Page.Text` spacing, `IndexOutOfRangeException` on a huge `/Length`) are worked around in shelf; reporting them upstream is optional. |

**Risk row: PDF cover rendering (not measured here).** F-IMP-5 for PDF (rendering the first page) needs PDFtoImage 5.4.0, SkiaSharp and PDFium natives per RID (ADR 0001 rows 6/7): native side files (G7), PDFium not thread-safe, single maintainers. PdfPig has no page renderer; the add-on `PdfPig.Rendering.Skia` exists (S4) and is **not evaluated**. Owner decision: a separate later slice `m0-spike-pdf-cover`, an open row in the roadmap's M0 table. Until then F-IMP-5 stays `@M4` and nothing in M1 depends on it.

## Consequences

- **Pins:** VersOne.Epub 3.3.6, PdfPig 0.1.16 (already pinned in the root `Directory.Packages.props`; nothing changes there). Reference both from `Shelf.Extraction` only.
- **PRD change (owner decision, Q4):** F-IMP-5 and the work-folder tree in `docs/prd.md` now say that the cover is stored as **`cover.<ext>` in the original image format** of the source (no transcoding, no image library) instead of `cover.jpg`. The extension comes from the magic bytes (`FF D8 FF` jpg, `89 50 4E 47` png, `GIF8` gif, `RIFF....WEBP` webp; helper `Covers.Magic`/`Covers.Extension` in the prototype), not from the declared manifest media type (they agreed on all fixtures). The EPUB fixtures deliver JPEG and PNG; there is no `cover.jpg` mention left in `docs/` outside the slice plan.
- **Hand-off M1 (`src/Shelf.Extraction`), feature files:** the slice that implements extraction turns X-E1..X-E7 and X-P1..X-P4 into `Examples` rows of a feature tagged `@F-IMP-2 @M1`; the ISBN rows (I1-I7, X-E5/X-E6/X-P2 ISBNs, the UUID false-positive rule) into `@F-IMP-4 @M1`; the locator rows (L-E: spine index and chapter CFI `epubcfi(/6/{2*(i+1)}[idref]!)`; L-P: `page=N`, 1-based) into `@F-SRC-5 @M1`; the broken-input rows (R-rows, error kinds `not-a-container`, `bad-epub`, `bad-pdf`, `encrypted`, `unsupported`, `io`, `memory`, `other`, to be mapped to review entries per Q15) into a robustness `Scenario Outline` tagged `@F-IMP-2 @M1`. **EPUB cover rows (C-E) become `@F-IMP-5 @M4`** (not M1), together with the `cover.<ext>` storage rule; the PDF cover part stays with `m0-spike-pdf-cover`.
- **Hand-off M1, unit tests:** ISBN normalizer/validator (regex `[GeneratedRegex]`, NFKC and dash mapping, ISBN-13 check digit, ISBN-10 conversion only with an `ISBN` prefix, `FromIdentifier` rejecting `urn:uuid:`), CFI builder, tolerant XHTML-to-text (block tags to line breaks, `script`/`style`/`head` skipped, `WebUtility.HtmlDecode`; the strict `XmlReader` variant is unusable for real XHTML with named entities, R-E10, and needs a DOM path for element-level CFI, Q7/Q9), cover media-type detection, error-kind classification, `GetWords()` text assembly.
- **Settings shelf must use:** `PdfDocument.Open(path, new ParsingOptions { UseLenientParsing = true })` (identical output to strict on the arXiv fixture, X-P7; needed for R-P4); VersOne `EpubReader.OpenBookAsync(path)` with default options (strict: missing parts are errors; `GetReadingOrderAsync()` returns the whole spine in order, so spine index = CFI step); VersOne's own XML settings are safe (DTD ignored, no resolver); every XML shelf parses itself (XMP, strict XHTML) must use `DtdProcessing.Ignore` and `XmlResolver = null`; do not call `XmpMetadata.GetXDocument()`.
- **Code pages (Q10):** the gated paths need no registration. If shelf ever needs `windows-1252` (a strict XML path for chapters that declare it, R-E11), register `CodePagesEncodingProvider.Instance` explicitly at startup and do not rely on PdfPig's static constructor, which runs only when PDF name tokens are parsed. Explicit registration adds 0 B to the binary that contains PdfPig (743 KB to an EPUB-only binary). VersOne.Epub's text path ignores the declaration and decodes as UTF-8 (source-based).
- **Language (Q12):** no PDF fixture carries catalog `/Lang` or XMP `dc:language` (X-P6); EPUB language comes from `dc:language`; for PDFs the PRD needs an owner decision (text-based detection or none).
- **Q6 (ISBN scope):** PDF pages 1-5 and the last 3, EPUB first 3 and last 2 spine items, ISBN-10 only with the `ISBN` prefix, OPF/Info identifiers before text hits; confirmed by the reference library (zero false positives from text on 6,700 PDFs and 8,300 EPUBs).
- **Re-check triggers:** VersOne.Epub 4.x, PdfPig 0.2/1.0 (or the `0.1.17-alpha` nightly if the `Page.Text` spacing or XMP decoding is fixed), .NET 11, a `spike-parsers` result that contradicts E2/P2 on another RID, the first run on real books (owner corpus) contradicting the extrapolation, a decision on PDF cover rendering.
- **Workflow and prototype:** `.github/workflows/spike-parsers.yml` is not required, is not part of the `ci` aggregate of `build.yml`, and runs `check.sh` on `ubuntu-24.04`, `ubuntu-24.04-arm`, `macos-15`, `macos-15-intel`. It was written but not run by the implementer. It and `experiments/parsers/` are deleted when M0 closes, with a note pointing to the last commit that contains them.
- **Not verified in this ADR:** NCX/nav documents with a public DOCTYPE (only the OPF was tested, R-E14); `linux-x64`, `linux-arm64`, `osx-x64` (AOT publish warnings, check rows, differential); real-world EPUBs and PDFs (embedded fonts, Type0/CID fonts, CMaps, DRM, huge files); validity of the generated files beyond the lenient readers (`epubcheck`, `qpdf` not available); the cause of the PdfPig scaling stop; the trim mode applied to non-annotated assemblies (documentation not checked); the claim that VersOne.Epub ignores a declared chapter encoding (source only, not measured); stack overflow behavior beyond R-E12 and R-P8; text quality of `GetWords()` on multi-column or right-to-left layouts; whether `Process.PeakWorkingSet64` is zero on macOS only for this runtime (observed 0, replaced by `getrusage`).

## Deviations from the plan

- **E5 failed on the first `verify` run** (3 of 8,300 EPUBs: `urn:uuid:` identifiers read as ISBN-13). The prototype's identifier rule was then changed post hoc (`Isbn.FromIdentifier`), the gate E5 itself is unchanged, the self-check row `I-identifier` was added afterwards, and `results/measure.out` is the run after the fix.
- **P5 wording:** an earlier draft of the criteria section reworded P5 ("zero ISBNs reported from the text for files without `pdf-isbn-in-text`"); it is restored to the plan's wording above. The implementation checks the text ISBN as "equals the work's ISBN for `pdf-isbn-in-text`, none otherwise" plus the combined (Info Keywords, then text) ISBN against `embedded` for every file, which covers the plan's clause.
- **E3/P3 scope:** the differential covers the 17 gated R-rows only. R-E10..R-E14 and R-P8 are excluded by `dump --isolate`, because their outcome depends on memory and time. The criterion text is unchanged. The JIT reference was built with `dotnet build -p:PublishAot=false` and run with `dotnet <dll>`, not with `dotnet run -c Release` as in the plan.
- **R-E14 is new** and not in the plan's R-row table (observation: OPF with a public DOCTYPE and no entities).
- Q3/Q4/Q2 owner decisions recorded above (EPUB cover `@M4`, `cover.<ext>`, PDF cover spike separate); the plan's open Q1/Q8 budgets and the estimate-only fallback were followed.
- `R-E7` is split into R-E7a (spine item without manifest entry) and R-E7b (manifest `href` to a missing chapter); both are gated.
- Peak RSS of child processes is taken from the child itself (`getrusage`, because `Process.PeakWorkingSet64` is 0 on macOS); `/usr/bin/time -l` is used for the named rows in `measure.sh` and for T2.
- The PDF text of the prototype is built from `GetWords()` instead of `Page.Text` (X-P5 result); the plan had `page.Text` in the gate rows. The XMP is parsed from the raw bytes (R5).
- `measure.sh` adds a thread-scaling observation and an AOT/JIT per-format comparison; `check.sh` generates the 200/300 library with `--seed 42`.
- The criteria section was written before measuring but, as in ADR 0003/0004, not committed separately (the implementer does not commit).

## Source index

Retrieved 2026-10-16. ADR 0001 sources are cited as "ADR 0001 Sxx"; this index starts at S1.

| ID | Source |
|---|---|
| S1 | `https://github.com/vers-one/EpubReader` at tag `v3.3.6` (commit `0fed46ce1c4f5eb76418682d72d1b4254735db51`, shallow clone): `Source/VersOne.Epub/VersOne.Epub.csproj`, `Utils/XmlUtils.cs`, `Readers/BookCoverReader.cs`, `SpineReader.cs`, `PackageReader.cs`, `ContainerFileReader.cs`, `Content/Loaders/EpubLocalContentLoader.cs`, `EpubRemoteContentLoader.cs`, `Entities/EpubBookRef.cs`, option classes; grep for reflection, XML, encoding and compression calls; non-blank, non-doc-comment line counts |
| S2 | `https://github.com/UglyToad/PdfPig` at tag `v0.1.16` (annotated tag object `49ddb62`, commit `a7bb35662bbbf405efddad50aedc9bcdcf515afc`, the commit of the nuspec): the six `src/*/*.csproj` files, `src/Directory.Build.targets` (`IsTrimmable`/`IsAotCompatible`), `tools/UglyToad.PdfPig.Package/*.csproj`, `PdfDocument.cs`, `ParsingOptions.cs`, `Content/XmpMetadata.cs`, `Content/DocumentInformation.cs`, `Page.cs`, `Tokenization/NameTokenizer.cs`, `Core/OtherEncodings.cs`, `Filters/*`, `Resources/*`, `DocumentLayoutAnalysis/TextExtractor/ContentOrderTextExtractor.cs`; grep for reflection, XML, encoding and resource calls |
| S3 | Local `.nupkg` contents restored by NuGet in `~/.nuget/packages/versone.epub/3.3.6` and `~/.nuget/packages/pdfpig/0.1.16`: nuspec files (dependency groups, license, repository commit) and the `lib/<tfm>/` listings with sizes |
| S4 | `https://azuresearch-usnc.nuget.org/query?q=pdfpig&prerelease=false&take=20` (add-on packages `PdfPig.Rendering.Skia 0.1.16.6`, `PdfPig.Filters.*`) |
| S5 | `https://raw.githubusercontent.com/dotnet/runtime/v10.0.0/src/libraries/System.Private.Xml.Linq/src/System/Xml/Linq/XNode.cs` (`GetXmlReaderSettings`) and `.../System.Private.Xml/src/System/Xml/Core/XmlReaderSettings.cs` (`AllowDefaultResolver`, `MaxCharactersFromEntities`) at tag `v10.0.0` (the SDK in use runs runtime 10.0.12; the XML defaults were not compared between the two tags) |
| S6 | `https://raw.githubusercontent.com/dotnet/docs/main/docs/core/deploying/native-aot/index.md`, `docs/core/deploying/trimming/trimming-options.md` (`TrimmerSingleWarn`), `docs/standard/base-types/regular-expression-source-generators.md` (`RegexOptions.Compiled`) |
| S7 | Measurements: `experiments/parsers/results/measure.out` (run of 2026-10-16) and `results/ci/osx-arm64.txt` |
