# Slice m0-spike-parsers: AOT Spike Part 2, VersOne.Epub and PdfPig

| | |
|---|---|
| **Slice ID** | `m0-spike-parsers` |
| **Branch** | `feat/m0-spike-parsers` |
| **Milestone** | M0 – Setup & Spikes |
| **Roadmap** | "AOT spike" work package, parser part only: VersOne.Epub, PdfPig (roadmap line 56); exit criterion "The AOT spike runs on all four platforms, or every incompatible library has a documented fallback (ReadyToRun, ...)" (line 64), parser part; "Cold start measurement" (line 58), parser delta only |
| **PRD references** | F-IMP-2 (OPF, PDF Info, XMP), F-IMP-4 (ISBN regex in first/last pages), F-IMP-5 (EPUB cover only), F-SRC-5 (location: PDF page, EPUB chapter + CFI), F-SEM-2 (chunk locator page/CFI), 6.1 (file metadata: page count), Q7 (scanned PDFs: metadata only), 9.1 (cold start < 150 ms; initial indexing of metadata + full text < 15 min at 10,000 works / 15,000 files), 9.2 (four RIDs, Native AOT, ReadyToRun fallback), 9.3 (robustness), 10.2 rows EPUB/PDF, risk table 14 ("Poor metadata in PDFs", "Native AOT incompatible with libraries") |
| **Inputs** | ADR 0001 rows 4 and 5 (VersOne.Epub 3.3.6, Unlicense, "no claim"; PdfPig 0.1.16, Apache-2.0, "no claim"; sources S23-S32), rows 6/7 (PDFtoImage/PDFium, only for the risk row and fallback), ADR 0004 and `docs/slices/m0-spike-storage.md` (spike/ADR/CI structure), `experiments/storage/` (props, `measure.sh`, `check.sh` pattern), `tests/fixtures/manifest.json` (13 fixtures), `tests/Shelf.TestData` (generator; README "Output" and "Measured reference run"), `tests/Shelf.TestData.Tests/FixtureTests.cs` (JIT behavior of both libraries on the fixtures, already green) |

---

## 1. Scope

**In scope**

1. Research (ADR 0001 method: source at the release tag, `.nupkg` listings, docs; no execution of downloads beyond the NuGet restore) of the facts in 3.2.
2. Reproducible AOT experiment in `experiments/parsers/` (not in `shelf.slnx`, not built by existing CI jobs): metadata, text, page count, language, ISBN scan and EPUB cover with both libraries, on the committed fixtures, on runtime-generated broken inputs, and on the generator output; JIT-vs-AOT differential output; binary size, cold start, throughput, peak memory; ReadyToRun variant for the fallback cost.
3. Portable `check.sh` and a **separate, non-required** workflow `.github/workflows/spike-parsers.yml` (four RIDs).
4. `docs/adr/0005-epub-pdf-parsers-aot.md`: criteria fixed before measuring, research (own source index S1...), measurements, decision per library, fallback, consequences and hand-offs.
5. Roadmap: note the parser part of the AOT spike (link ADR 0005) in M0.

**Requirements covered:** none implemented. The slice produces a decision (ADR 0005) and evidence for M0's exit criterion. PRD IDs above are enabled, not tagged (no feature files, section 2).

**Out of scope**

- Any product code: no `src/Shelf.Extraction`, no change to `src/` or `tests/` (every candidate helper, e.g. ISBN normalizer, CFI builder, XHTML-to-text, depends on decisions this ADR and Q6/Q7/Q9 make and belongs to the M1 extraction slice).
- **PDF cover rendering** (F-IMP-5 PDF part, PDFtoImage 5.4.0 + SkiaSharp + PDFium natives, ADR 0001 rows 6/7). Recorded as a separate risk row in ADR 0005; Q2 decides whether it gets its own spike slice.
- Converting covers to JPEG (`cover.jpg`, Q4); OCR (PRD Q7: ignore); language detection from text (Q16); chunking (F-SEM-2, M3); FTS insert (covered by ADR 0004).
- Encrypted/DRM files, MOBI/AZW3/CBZ (F-LIB-3).
- Other AOT spike libraries (ONNX Runtime, MCP SDK, ASP.NET Core + RazorSlices, CsToml four-platform check).
- PdfPig nightly `0.1.17-alpha-*` and other PdfPig packages unless R2 shows one is required for text extraction (Q5).
- Changes to `.github/workflows/build.yml`, `Directory.Build.props`, `Directory.Packages.props` (root), `global.json`, `shelf.slnx`, ADR 0001-0004, `tests/fixtures/`.

## 2. Feature Files

**None in this slice.** Same reasoning as `m0-spike-storage` section 2:

- `CLAUDE.md` rules 1-5 bind product behavior. This slice adds no product behavior and no code under `src/`; its deliverable is a decision backed by an experiment. Feature files now would specify `Shelf.Extraction` behavior before its design exists and would stay red (rule 6).
- The experiment's checks (3.4) play the role of concrete examples: every row has a fixed input and an expected output and runs on the AOT binary. Rows whose value is a fact to observe are marked **observation** (recorded, not gated).
- Rule 6 still applies: `dotnet test -c Release` on the solution stays green (nothing in the solution changes).

**Hand-off to M1/M4** (not written here; listed in ADR 0005 Consequences): the M1 extraction slice (`src/Shelf.Extraction`) turns rows X-E1..X-E7 and X-P1..X-P4 into `Examples` rows of a feature tagged `@F-IMP-2 @M1`; the ISBN rows (3.4 ISBN table, X-P2, I-rows) into `@F-IMP-4 @M1`; page/spine locator rows (L-rows) into `@F-SRC-5 @M1`; the broken-input rows (R-rows) into a robustness scenario outline tagged `@F-IMP-2 @M1` (error kinds per Q15); the EPUB cover rows into `@F-IMP-5` with the milestone set by Q3 (roadmap: M4).

## 3. Design

### 3.1 Files

```
experiments/parsers/                          NEW  not in shelf.slnx
  Directory.Build.props                       NEW  copy of experiments/storage (no root import, TreatWarningsAsErrors=false,
                                                   PublishAot=true, IsAotCompatible=true, InvariantGlobalization not set)
  Directory.Packages.props                    NEW  VersOne.Epub 3.3.6, PdfPig 0.1.16 (same pins as root)
  .gitignore                                  NEW  bin/ obj/ out/ data/ corpus/ results/* !results/measure.out !results/ci/
  README.md                                   NEW  how to run measure, check, corpus; nothing else
  measure.sh                                  NEW  full run, osx-arm64 (3.6)
  check.sh                                    NEW  portable functional run, any of the four RIDs (3.7)
  Shared/Harness.cs                           NEW  timing, nearest-rank percentiles, key=value output, peak RSS,
                                                   child-process runner with timeout (3.4 R-rows)
  Shared/Json.cs                              NEW  source-generated STJ contexts: manifest.json subset, ground-truth.json
                                                   subset (works[], files[]), dump output
  Baseline/  Baseline.csproj, Program.cs      NEW  proto-parsers-baseline: prints "ok", no parser reference
  Parsers/   Parsers.csproj, Program.cs,      NEW  proto-parsers-<variant>; MSBuild property Variant=epub|pdf|all
             Epub.cs, Pdf.cs, Isbn.cs,             (default all) sets DefineConstants and the PackageReferences, so the
             XhtmlText.cs, Mutations.cs,           size delta per library is measurable from one source tree
             Checks.cs, Bench.cs
.github/workflows/spike-parsers.yml           NEW  non-required four-RID check (3.7)
docs/adr/0005-epub-pdf-parsers-aot.md         NEW
docs/roadmap.md                               CHANGE  M0 AOT spike row: parser part, link ADR 0005
```

Assembly names: `proto-parsers-baseline`, `proto-parsers-epub`, `proto-parsers-pdf`, `proto-parsers-all`. Fixtures are read in place from `../../tests/fixtures/` (manifest-driven); generator output goes to the repository's git-ignored `artifacts/`; mutated inputs go to `Path.GetTempPath()/shelf-spike-parsers/<guid>/`. Nothing generated is committed except `results/measure.out` and `results/ci/<rid>.txt`.

### 3.2 Research (facts to verify; nothing below is verified)

ADR 0005 gets its own source index S1... (as ADR 0003/0004); ADR 0001 sources are cited as `ADR 0001 S27`.

| ID | Question | Where to look / how | Record in ADR |
|---|---|---|---|
| R1 | VersOne.Epub 3.3.6: TFMs and dependencies | `.nupkg` `lib/` folders and nuspec dependency groups; which TFM `net10.0` resolves (expected `netstandard2.0`) | TFM, dependency list |
| R2 | PdfPig 0.1.16: TFMs, assemblies in the package, sub-packages | `.nupkg` `lib/` listing (expected several assemblies: `UglyToad.PdfPig`, `.Core`, `.Fonts`, `.Tokenization`, `.Tokens`, maybe `.DocumentLayoutAnalysis`); NuGet search for `PdfPig.*` packages (filters such as DCT/JBIG2, rendering) and what they add | Assembly list; whether `ContentOrderTextExtractor`/`NearestNeighbourWordExtractor` ship in the main package (Q5) |
| R3 | AOT claims at the tags (ADR 0001 checked `master`/`a7bb3566`) | `VersOne.Epub.csproj` at the 3.3.6 tag/commit; each PdfPig `*.csproj` at the 0.1.16 tag: `IsTrimmable`, `IsAotCompatible`, `EnableTrimAnalyzer` | Claim per package |
| R4 | Reflection and dynamic code | grep both repos at the tags for `Activator.CreateInstance`, `Type.GetType`, `GetMethod`, `GetProperty`, `MakeGenericType`, `Expression.Compile`, `XmlSerializer`, `DataContractSerializer`, `XslCompiledTransform`, `RegexOptions.Compiled`, `dynamic` | Call sites and whether they are on the paths 3.4 uses (trimmer warns only for reachable code; DLA exporters with `XmlSerializer` are expected unreachable) |
| R5 | XML handling | VersOne: how OPF/NCX/nav/XHTML are loaded (`XDocument.Load`? `XmlReaderSettings`: `DtdProcessing`, `XmlResolver`); PdfPig: XMP API (`TryGetXmpMetadata`, `GetXDocument`) and settings; dotnet/runtime `v10.0.x` tag: defaults of `XDocument.Load` and `XmlReader.Create` for DTD processing and external resolution | Whether an external DTD/entity can be fetched (network/file) when parsing EPUB2 XHTML with `<!DOCTYPE ... xhtml11.dtd>` (generator writes this) and the R-E7/R-E8 inputs |
| R6 | Encodings / code pages | grep for `Encoding.GetEncoding`, `CodePagesEncodingProvider`, `Encoding.Latin1`; PdfPig's own tables (`WinAnsiEncoding`, `MacRomanEncoding`, `PdfDocEncoding`) vs. BCL; VersOne content decoding (BOM, XML declaration); .NET 10 docs: encodings available without `System.Text.Encoding.CodePages` registration | Whether either library needs code page 1252 from the BCL; what shelf must register (Q10) |
| R7 | Compression | VersOne: `System.IO.Compression.ZipArchive` (in-box, AOT-safe) or own; PdfPig: Flate via `DeflateStream`/`ZLibStream`, own LZW/ASCII85/RunLength; behavior for unsupported filters (DCT, JPX, JBIG2, CCITT) when only text is requested | Filter list and "decode on text extraction?" |
| R8 | Resources | PdfPig `GetManifestResourceStream` usage (Standard 14 AFM metrics, glyph lists, CMaps); names and total size | AOT keeps manifest resources; size contribution (3.6 step 3) |
| R9 | API surface used by the prototype | VersOne: `EpubReader.OpenBookAsync` (lazy `EpubBookRef`) vs `ReadBookAsync` (reads all content incl. images), `EpubReaderOptions` (package/content/XML options), `ReadingOrder`, `Navigation`, `ReadCoverAsync`/`CoverImage`, metadata model (`Identifiers`, `Creators` + `refines`/`file-as`, `MetaItems`), exception types; PdfPig: `PdfDocument.Open(path, ParsingOptions)` (`UseLenientParsing`, `SkipMissingFonts`, `Logger`), `Information`, `NumberOfPages`, `GetPage(n).Text`, `GetWords()`, `GetImages()`, catalog `/Lang` access, exception types | Exact members used; exception types for 3.4 classification |
| R10 | .NET 10 trimming/AOT facts | Docs: `TrimMode=full` under `PublishAot` trims all assemblies incl. `netstandard2.0` ones without `IsTrimmable`; `TrimmerSingleWarn` (IL2104 collapse per assembly); warning families IL2xxx/IL3xxx; `[GeneratedRegex]` vs `RegexOptions.Compiled` under AOT (interpreter fallback); `StackOverflowException` is not catchable | Short list; justifies child-process isolation in R-rows |
| R11 | Fallbacks | (a) ReadyToRun: `PublishReadyToRun` + `SelfContained` + `PublishSingleFile` (measured, 3.6); (b) hand-written EPUB reader: components (container.xml, OPF 2/3 incl. `refines`, NCX/nav, spine, cover rules: EPUB3 `properties="cover-image"`, EPUB2 `<meta name="cover">`, guide `type="cover"`), estimate in LOC and days; (c) PDF alternatives: ADR 0001 lists none for text; PDFium text API (`FPDFText_*`) via own P/Invoke on the `libpdfium` of ADR 0001 row 7; Docnet.Core and others **not evaluated** (would need the ADR 0001 method) | Option table with cost and risk |

### 3.3 Decision criteria (write into ADR 0005 with status `Proposed` and commit before measuring)

**Gates, part E: VersOne.Epub** (fail = fallback rule)

| ID | Gate |
|---|---|
| E1 | AOT publish of `proto-parsers-epub` and `proto-parsers-all` (`-p:TrimmerSingleWarn=false`) with zero `IL*`/AOT warnings; no `NoWarn`, no `SuppressTrimAnalysisWarnings`, no `TrimmerRootAssembly`, no ILLink descriptor/rd.xml, no `DynamicDependency` in experiment code. |
| E2 | All gated EPUB rows of 3.4 (X-E, L-E, C-E, I-E) pass on the AOT binary on `osx-arm64` and in `check.sh` on all four RIDs. |
| E3 | Differential: `dump` output of the AOT binary is byte-identical to the JIT run of the same code (`dotnet run -c Release`, `PublishAot` off) for all EPUB fixtures, all R-E inputs and the tiny library. |
| E4 | Robustness: every R-E row ends in `ok` or `error kind=<k>` in the child process within 10 s, exit code 0 or 3, no crash/kill, peak RSS < 512 MB (except rows marked observation). |
| E5 | Reference library, all 8,300 EPUBs: title, authors, language, isbn13 equal `files[].embedded` of `ground-truth.json` for 100 %; text non-empty for 100 %; cover present iff trait `cover`; zero errors. |

**Gates, part P: PdfPig**

| ID | Gate |
|---|---|
| P1 | As E1 for `proto-parsers-pdf` and `proto-parsers-all`. |
| P2 | All gated PDF rows of 3.4 (X-P, L-P, I-P) pass on the AOT binary on `osx-arm64` and on all four RIDs. |
| P3 | As E3 for all PDF fixtures, R-P inputs and the tiny library. |
| P4 | As E4 for R-P rows. |
| P5 | Reference library, all 6,700 PDFs: page count equals `files[].pageCount` for 100 %; title/authors equal `embedded` for files with `pdf-info`; empty for `pdf-no-metadata`; ISBN found and equal to the work's `isbn13` for 100 % of `pdf-isbn-in-text`; zero ISBNs reported for files with neither `pdf-isbn-in-text` nor an ISBN in Info `Keywords`; zero errors. |

**Gates, part T: throughput and memory** (`osx-arm64`, AOT `proto-parsers-all`, warm page cache, AC power; values are proposals, Q1)

| ID | Gate |
|---|---|
| T1 | `bench` over all 15,000 files of the reference library (metadata + full text + ISBN scan, no cover), threads = processor count: total wall time ≤ 5 min (one third of the PRD 9.1 budget "initial indexing of metadata + full text < 15 min"; the rest is for hashing, grouping, FTS insert, sidecars). |
| T2 | Peak RSS of the T1 run ≤ 1 GB. |
| T3 | Large PDFs (L-rows 2 synthetic 100,000-word PDFs): full text per file ≤ 10 s, peak RSS ≤ 500 MB single-threaded. |

**Targets, not gates:** process cold start `meta <fixture>` p95 < 150 ms per fixture (PRD 9.1 is about CLI output; import is not cold-start-bound); per-file p95 single-threaded recorded per format. **Extrapolation** (recorded as M1 risk, never a gate): words/s and MB/s from T1 applied to a "realistic" library (15,000 files, 100,000 words per book, PDFs 300 pages with embedded fonts and Flate), stating that synthetic files are small, uncompressed, Helvetica-only.

**Decision rule** (proposal, Q1):

1. E1-E5 pass → VersOne.Epub 3.3.6 adopted for M1 under Native AOT (pin).
2. P1-P5 pass → PdfPig 0.1.16 adopted for M1 under Native AOT (pin).
3. E fails (E1, E3, or E2/E5 caused by the library) → hand-written EPUB reader over `System.IO.Compression` + `System.Xml` for M1 if the R11 estimate is ≤ 5 days; VersOne.Epub stays test-only (`Shelf.TestData.Tests`). Estimate > 5 days → escalate (hand-written vs. ReadyToRun for the whole binary).
4. P fails → no hand-written PDF parser. Record cause and escalate with the options, in order: (a) upstream fix if the cause is one annotatable call site (issue/PR, pin a later version); (b) PDFium text API via P/Invoke, shared with PDF cover rendering, needs its own spike (Q2); (c) ReadyToRun for the whole binary (PRD 9.2) with the measured R2R cold start and size. Do not decide alone.
5. E4/P4 fail through an uncatchable failure (stack overflow, native crash, hang) → library adoptable only with input limits or per-file process isolation in M1; record the input, escalate (Q15).
6. T1-T3 miss alone does not reject a library: record the phase profile (open/metadata/text/ISBN) and the gap as an M1 risk; escalate if the miss is > 2x.

### 3.4 Checks

Output per row: `pass <id>` / `fail <id>: <detail>` / `observed <id>: <value>`; summary `passed X/Y`; exit 1 if any gated row fails. Text comparisons use the text after whitespace collapsing (all runs of Unicode whitespace → one space) and NFC. Expected values of upstream fixtures marked (c) are known literary text: confirm once on the first JIT run; if the file differs, stop and correct the row with a note in the ADR, never silently.

**EPUB metadata, text, cover** (`proto-parsers-* epub-check`; `OpenBookAsync`, lazy):

| ID | Fixture | Title | Authors | Language | Version | Cover | Text contains | Identifier / series |
|---|---|---|---|---|---|---|---|---|
| X-E1 | `upstream/gutenberg/pg201-flatland.epub2.epub` | `Flatland: A Romance of Many Dimensions` | `Edwin Abbott Abbott` | starts `en` | 2.0 | yes | `I call our world Flatland` (c) | record |
| X-E2 | `upstream/gutenberg/pg201-flatland.epub3.epub` | as X-E1 | as X-E1 | starts `en` | 3.0 | yes | as X-E1 | record |
| X-E3 | `upstream/gutenberg/pg22367-die-verwandlung.epub3.epub` | `Die Verwandlung` | `Franz Kafka` | starts `de` | 3.0 | yes | `Als Gregor Samsa eines Morgens aus unruhigen Träumen erwachte` (c) | record |
| X-E4 | `upstream/standardebooks/edwin-a-abbott_flatland.epub` | `Flatland` | `Edwin A. Abbott` | starts `en` | 3.0 | yes | `I call our world Flatland` (c) | record |
| X-E5 | `derived/epub3-isbn-series.epub` | `Event Streams in Practice` | `Jördis Ålander` | `en` | 3.0 | no | non-empty | `urn:isbn:9780000000019`; series `Streaming Foundations` index 2 (EPUB3 `belongs-to-collection`) |
| X-E6 | `derived/epub2-isbn-cover.epub` | `Größenordnungen verteilter Systeme` | `Lukas Weiß`, `Ana Núñez` | `de` | 2.0 | yes, PNG | non-empty | `9780000000026` scheme `ISBN`; series `Verteilte Systeme` index 1 (`calibre:series`) |
| X-E7 | `derived/Jane Doe - Quiet Systems (2004).epub` | empty or null | none | null | 3.0 | no | non-empty | record |
| X-E8 (observation) | X-E5 | – | – | – | – | – | – | author `file-as` = `Ålander, Jördis` and role, as exposed by VersOne |
| X-E9 (observation) | all EPUBs | – | – | – | – | – | – | timings `OpenBookAsync` + metadata only vs. `ReadBookAsync` |

ISBN normalization for X-E5/X-E6: strip `urn:isbn:`, check digit valid, result `9780000000019` / `9780000000026`.

**EPUB cover** (C-E): for X-E1..X-E4 and X-E6: cover bytes non-empty; media type by magic bytes (`FF D8 FF` JPEG, `89 50 4E 47` PNG) and declared manifest media type recorded; for X-E6 PNG 64 × 96 (IHDR); X-E5 and X-E7 no cover. Observation: cover detection rule that matched (EPUB3 property / EPUB2 meta / guide), cover bytes size.

**EPUB locators** (L-E, observation unless marked): per spine item: index, `idref`, `href`, `linear`, character count; chapter-level CFI `epubcfi(/6/{2·(i+1)}[idref]!)`; nav/NCX entry count and how many map to a spine item (+ fragment). Gate L-E1: for X-E5 the spine has the generator's chapter count (read from the nav, every nav entry maps to a spine item). Gate L-E2: every spine `href` of every fixture in X-E1..X-E7 resolves to a content file.

**XHTML to text** (two variants in `XhtmlText.cs`, both AOT-safe): (a) `XmlReader` with `DtdProcessing.Ignore`, `XmlResolver = null`, block elements → line break; (b) tolerant state-machine tag stripper + `WebUtility.HtmlDecode`. Gate rows use (b); (a) is recorded per document (`ok` / exception type), because element-level CFI in M1 needs a DOM path (Q7, Q9).

**PDF** (`pdf-check`; `ParsingOptions { UseLenientParsing = true }` unless stated):

| ID | Fixture | Info Title | Info Author | XMP | Pages | Text | ISBN |
|---|---|---|---|---|---|---|---|
| X-P1 | `upstream/pdf/arxiv-2403.02174v1.pdf` | record (manifest: `On Hilbert's 16th Problem`) | record (manifest: `Lars Andersen`) | present; record `dc:title`, `dc:creator` | record | page 1 contains `Hilbert` | none expected; record |
| X-P2 | `derived/Event Streams in Practice.pdf` | `Event Streams in Practice` | `Jördis Ålander` (UTF-16BE string) | present, `dc:title` = Info Title | record | page 1 contains `by Jördis Ålander` (WinAnsi ö/Å) and `ISBN 978-0-00-000001-9` | `9780000000019` |
| X-P3 | `derived/Jane Doe - Quiet Systems (2004).pdf` | empty or null | empty or null | absent | record | page 1 starts `Quiet Systems` | none |
| X-P4 | `derived/scanned-page.pdf` | `Scanned Page` | empty or null | record | 1 | empty after whitespace collapse; classified `no-text-layer`; 1 image on page 1 | none |
| X-P5 (observation) | X-P1 | – | – | – | – | `page.Text` vs. `GetWords()` vs. `ContentOrderTextExtractor` (if in package, R2): word count page 1, whether `On Hilbert` appears space-separated | – |
| X-P6 (observation) | all PDFs | – | – | – | – | catalog `/Lang`, XMP `dc:language` | – |
| X-P7 (observation) | X-P1 | – | – | – | – | `UseLenientParsing` false vs. true: same output? | – |

**PDF locators** (L-P): gate L-P1: locator `page=N` 1-based for every page, count equals `NumberOfPages`; L-P2 (T3): two synthetic 100,000-word PDFs (`generate --seed 42 --works 5 --files 5 --words-min 100000 --words-max 100000` gives 2 single-file PDFs, about 220 pages each): page count equals ground truth, time and peak RSS per file.

**ISBN** (I-rows; `Isbn.cs`, `[GeneratedRegex]`, never `RegexOptions.Compiled`): text normalized NFKC, U+2010-U+2015 and U+2212 → `-`; candidate `(?<![0-9])(?:ISBN(?:-1[03])?:?\s*)?((?:97[89][-\s]?)?(?:[0-9][-\s]?){9}[0-9Xx])(?![0-9])`; separators stripped; valid ISBN-13 check digit, or valid ISBN-10 converted to 978-prefixed ISBN-13; first valid hit wins. Scan scope (spike proposal, Q6): PDF pages 1-5 and the last 3; EPUB first 3 and last 2 spine items.

| ID | Input text | Expected |
|---|---|---|
| I1 | `ISBN 978-0-00-000001-9` | `9780000000019` |
| I2 | `ISBN-13: 9780000000026` | `9780000000026` |
| I3 | `ISBN 0-306-40615-2` | `9780306406157` |
| I4 | `ISBN 978‐0‐306‐40615‐7` (U+2010) | `9780306406157` |
| I5 | `ISBN 978-0-306-40615-8` (bad check digit) | none |
| I6 | `call 0306406152 now` (no prefix, valid ISBN-10) | record (precision question, Q6) |
| I7 | `12345678901234` | none |
| I-E | X-E5..X-E7, X-E1..X-E4 text | none in synthetic EPUB text (generator writes ISBNs to the OPF only); upstream: record |
| I-P | X-P2 / X-P3 | as table above |

**Broken inputs** (R-rows; `robust` runs each input in a child process `Environment.ProcessPath robust-one <file>` with a 10 s kill timeout; child exit 0 = ok, 3 = controlled error with `error kind=<k> type=<exception type>`; anything else or timeout = fail). Inputs are written by `mutate` (deterministic, from committed fixtures) into the temp dir:

| ID | Kind | Input | Expected |
|---|---|---|---|
| R-E1 | gate | `derived/malformed-opf.epub` | controlled error |
| R-E2 | gate | `derived/not-a-zip.epub` | controlled error |
| R-E3 | gate | zero-byte `.epub` | controlled error |
| R-E4 | gate | `epub3-isbn-series.epub` truncated to 50 % | controlled error |
| R-E5 | gate | X-E5 without `META-INF/container.xml` | controlled error |
| R-E6 | gate | container.xml points to a missing OPF | controlled error |
| R-E7 | gate | spine `itemref` to a missing manifest item; manifest `href` to a missing chapter | controlled error or ok with the chapter skipped (record which) |
| R-E8 | gate | OPF with `<!DOCTYPE package [<!ENTITY x SYSTEM "file:///etc/hosts">]>` and `&x;` in `dc:title` | controlled error, or title not containing `localhost` (no external entity resolved) |
| R-E9 | gate | OPF with a "billion laughs" entity set in `dc:title` | controlled error within 10 s, RSS < 512 MB |
| R-E10 | observation | chapter XHTML with XHTML 1.1 DOCTYPE and `&nbsp;` | text variants (a)/(b) result |
| R-E11 | observation | chapter declared `encoding="windows-1252"` with byte `0xE4` | with and without `CodePagesEncodingProvider` registered (`--codepages`): text contains `ä` or error |
| R-E12 | observation | chapter with 10,000 nested `<div>` | ok / error / crash |
| R-E13 | observation | chapter entry of 256 MiB zeros, deflated (about 256 KB on disk) | time, peak RSS, outcome (input for a size limit in M1) |
| R-P1 | gate | zero-byte `.pdf` | controlled error |
| R-P2 | gate | `not-a-zip.epub` bytes as `.pdf` | controlled error |
| R-P3 | gate | `arxiv-2403.02174v1.pdf` truncated to 50 % | controlled error or ok with fewer pages (record) |
| R-P4 | gate | X-P2 with `startxref` value replaced by `0` | controlled error or ok (lenient reconstruction; record) |
| R-P5 | gate | X-P3 with a page `/Kids` loop (a `/Pages` node listing itself) | controlled error or ok, within 10 s |
| R-P6 | gate | content stream with `/Length 2147483647` | controlled error or ok |
| R-P7 | gate | X-P2 with a `/Filter /FlateDecode` stream of random bytes as page content | controlled error or ok with empty page text |
| R-P8 | observation | object with 100,000 nested arrays `[[[...]]]` | ok / error / crash (stack overflow is uncatchable, R10) |

The R-row outcome is also part of `dump` (E3/P3), so JIT and AOT must classify identically.

**Generator data** (`verify <ground-truth.json>`): E5/P5 over the reference library; in `check.sh` over the tiny preset and a 200-work / 300-file library.

### 3.5 Prototype commands (`proto-parsers-<variant>`)

Writes only to the `TextWriter`s from `Main`; numbers with `InvariantCulture`; one `key=value ...` line per measurement.

| Command | Purpose |
|---|---|
| `info` | variant, runtime RID, `RuntimeInformation.OSDescription`, library assembly versions |
| `epub-check <fixtures-dir>` / `pdf-check <fixtures-dir>` | X-, C-, L-, I-rows from `manifest.json` + the tables above |
| `isbn-check` | I1-I7 |
| `mutate <fixtures-dir> <out-dir>` | writes R-row inputs; prints name + SHA-256 per file |
| `robust <dir>` / `robust-one <file>` | R-rows (child processes) |
| `meta <file>` | one file: open + metadata + page count, print (cold start) |
| `extract <file> [--text] [--cover <out>]` | one file: full extraction, print summary (L-P2/T3) |
| `dump <file\|dir> [--codepages]` | canonical JSON (ordinal-sorted keys, invariant culture, text as SHA-256 + length + first 200 chars) for E3/P3 |
| `verify <ground-truth.json> [--limit N]` | E5/P5 |
| `bench <ground-truth.json> --format epub\|pdf\|all --threads 1\|N [--limit N]` | per file open/metadata/text/ISBN timings p50/p95/max, total wall, files/s, MB/s, words/s, `Process.PeakWorkingSet64` (T1, T2) |
| `corpus <dir>` | 3.8; prints aggregates only |

### 3.6 `measure.sh` (osx-arm64, full run)

bash 3.2, `set -euo pipefail`, structure of `experiments/storage/measure.sh` (environment block incl. power source, zsh `$EPOCHREALTIME` timing fallback, `metric`/`verdict` helpers). Steps:

1. Publish `Baseline` and `Parsers` with `Variant=epub|pdf|all` (`-c Release -r osx-arm64 -p:TrimmerSingleWarn=false`); warnings by code per variant (E1, P1). Also `Parsers` `Variant=all` as ReadyToRun (`-p:PublishAot=false -p:PublishReadyToRun=true -p:SelfContained=true -p:PublishSingleFile=true`) to `out/Parsers-r2r` (fallback cost).
2. Size table: binary per variant, delta vs. Baseline (EPUB, PDF, both), R2R single file; `--codepages` delta (a second `all` build with `CodePagesEncodingProvider` referenced).
3. `info`, `isbn-check`, `epub-check`, `pdf-check` on the AOT binary; again under `LANG=de_DE.UTF-8 LC_ALL=de_DE.UTF-8` (output must be identical).
4. `mutate`, `robust` (E4, P4); `/usr/bin/time -l` for peak RSS of R-E9/R-E13/R-P8.
5. Differential: `dump` over fixtures, mutations, tiny library with the AOT binary and with `dotnet run -c Release --project Parsers` (JIT); `cmp` (E3, P3).
6. If `artifacts/synthetic-library/ground-truth.json` is missing: print the generator command (`dotnet run --project tests/Shelf.TestData -c Release -- generate --out artifacts/synthetic-library --preset reference`) and exit 1; never generate implicitly. Same for `artifacts/large-pdf/` (command in L-P2) and `artifacts/tiny/` (`generate --preset tiny --seed 42`).
7. `verify` on the reference library (E5, P5).
8. `bench` all 15,000 files: `--threads $(sysctl -n hw.ncpu)` (T1, T2, under `/usr/bin/time -l`) and `--threads 1` per format (per-file p50/p95).
9. `extract --text` for the two large PDFs under `/usr/bin/time -l` (T3).
10. Cold start (5 warmups, 50 runs): Baseline, `meta` per fixture for AOT `all` and for R2R.
11. If `corpus/` exists and is non-empty: `corpus corpus/` (3.8), aggregates only.
12. Summary block: every gate with pass/fail and value; extrapolation table.

`results/measure.out` is the unedited output of `./measure.sh > results/measure.out 2>&1` (committed).

### 3.7 Four-platform check and CI

`check.sh <rid>` (bash, portable: `uname`, `stat -c`/`-f`, no zsh, no timing gates):

1. Publish `Parsers` `Variant=all` AOT for `<rid>`; fail on any `IL*`/AOT warning; print binary size.
2. `info`, `isbn-check`, `epub-check`, `pdf-check`, `mutate` + `robust`.
3. Generate tiny (`--preset tiny --seed 42`) and 200/300 libraries into `$RUNNER_TEMP` with `dotnet run --project tests/Shelf.TestData -c Release`; `verify` both; `bench --threads 1 --limit 300` (informational timing).
4. Differential `dump` AOT vs. JIT over fixtures, mutations, tiny; `cmp`.
5. Write `results/ci/<rid>.txt` (uploaded; the implementer copies the four files into the repo for the ADR).

`.github/workflows/spike-parsers.yml`: copy of `spike-storage.yml` with: name `spike-parsers`, header comment pointing to ADR 0005, triggers `workflow_dispatch` and `pull_request` with `paths: [experiments/parsers/**, .github/workflows/spike-parsers.yml]`, `permissions: contents: read`, same concurrency/env/defaults, same matrix (`linux-x64`/`ubuntu-24.04`/`x86_64`, `linux-arm64`/`ubuntu-24.04-arm`/`aarch64`, `osx-arm64`/`macos-15`/`arm64`, `osx-x64`/`macos-15-intel`/`x86_64`), architecture assertion, `actions/checkout@v7`, Linux `clang zlib1g-dev`, `actions/setup-dotnet@v6` 10.0.x, step `./experiments/parsers/check.sh "${{ matrix.rid }}"`, upload `experiments/parsers/results/ci/` as `parsers-check-<rid>` (`actions/upload-artifact@v7`). Not added to the `ci` aggregate job; removed (or merged into the aggregation slice's spike job) when M0 closes.

### 3.8 Real-world corpus (optional, owner-supplied, never committed)

The fixtures are 13 small files and the generator output is lenient and synthetic: uncompressed PDF streams, Helvetica + WinAnsi only, no embedded fonts/CMaps/Type0, no images except one bitmap, small well-formed EPUBs with one XHTML style. The spike therefore cannot prove robustness on real books. Proposal: the owner may copy real files into `experiments/parsers/corpus/` (git-ignored). `corpus` runs every file through `robust-one` (child process, 30 s timeout) and `extract`, and prints only aggregates: counts per format and outcome, error kinds, timing p50/p95/max, peak RSS max, pages/s; per-file lines carry the first 12 hex digits of the SHA-256, never names, titles or text (privacy, committed `measure.out`). Not a gate; ADR 0005 states whether it was run and on how many files.

### 3.9 AOT and native-dependency risks

- **No AOT claim** for either library (ADR 0001); `netstandard2.0` assemblies are trimmed fully without trim annotations. Zero warnings only proves the reached call graph; E3/P3 (JIT vs. AOT identical output) is the guard against silently trimmed behavior.
- **XML:** `XDocument`/`XmlReader` are AOT-safe; `XmlSerializer`/XSLT are not (PdfPig DLA exporters, expected unreachable, R4). DTD/external entity handling (R5, R-E8/R-E9) is a security and network-access risk (PRD 9.4: network only for model download).
- **Encodings:** windows-1252 and other code pages are not available without `CodePagesEncodingProvider` (R6, R-E11); registering it adds size (measured).
- **Regex:** ISBN regex via `[GeneratedRegex]`; `RegexOptions.Compiled` silently falls back to the interpreter under AOT.
- **Resources:** PdfPig's embedded font metrics must survive trimming (R8; X-P2 Standard-14 Helvetica exercises them).
- **Uncatchable failures:** stack overflow on deeply nested input (R-E12, R-P8) and hangs kill or block the whole process; the experiment isolates per file, M1 may need limits (Q15).
- **Memory:** `ReadBookAsync` loads all content incl. images; decompression bombs (R-E13).
- **No native dependencies** in this slice (both libraries are managed). **Separate risk row:** PDF cover rendering via PDFtoImage 5.4.0 (claims AOT) pulls SkiaSharp and PDFium natives per RID (ADR 0001 rows 6/7): native side files (G7), PDFium not thread-safe, single maintainers. Not measured here (Q2).
- **ReadyToRun fallback** applies to the whole single binary (an AOT binary cannot host a JIT library), so its cost is measured here (size, cold start) for the owner.

## 4. Unit Tests

No product unit tests (no product code). Self-checks inside the prototype that fail the run if broken:

| Unit | Self-check |
|---|---|
| Percentiles | `[1..10]` → median 5.5, p95 10; `[3,1,2]` → median 2 (nearest rank, as `measure.sh`) |
| ISBN | I1-I7; ISBN-10 → 13 conversion `0306406152` → `9780306406157`; check digit of `978000000001` is `9` |
| Cover magic | JPEG/PNG/GIF/unknown byte prefixes → media type; PNG IHDR 64 × 96 parse |
| CFI string | spine index 0, idref `ch1` → `epubcfi(/6/2[ch1]!)`; index 4 → `/6/10` |
| Whitespace/NFC | `"a \n\tb"` → `"a b"`; `"ü"` → `"ü"` |
| Mutations | `mutate` twice → identical SHA-256 list |
| Dump | same input under `de_DE` and invariant culture → identical bytes |

Hand-off to M1 unit tests (ADR 0005 Consequences, not written here): ISBN normalizer/validator, CFI builder, XHTML-to-text, cover media-type detection, error-kind classification.

## 5. Step Order

1. **Criteria first.** Create `docs/adr/0005-epub-pdf-parsers-aot.md` (status `Proposed`): context (PRD F-IMP-2/4/5, F-SRC-5, 9.1, 9.2, Q7, risks; ADR 0001 rows 4-7), options (A: VersOne.Epub 3.3.6 AOT; B: hand-written EPUB reader; C: PdfPig 0.1.16 AOT; D: PDFium text API; E: ReadyToRun), criteria 3.3, method 3.4-3.8. Get Q1 confirmed. Commit before any measurement.
2. **Research** R1-R11, source index. If R4/R5 shows a blocker on paper, stop and ask. Commit.
3. **Scaffold** `experiments/parsers/` (props, `.gitignore`, README, `Shared/`, `Baseline`). Confirm `dotnet build -c Release` at the root builds nothing under `experiments/`.
4. **ISBN + harness** self-checks, `isbn-check`; publish AOT, check warnings.
5. **EPUB part** (`Variant=epub`): `epub-check`, cover, locators, both XHTML variants; publish AOT (E1).
6. **PDF part** (`Variant=pdf`): `pdf-check`, locators (P1).
7. **Robustness:** `mutate`, `robust`, `robust-one`.
8. **Differential:** `dump`, JIT vs. AOT by hand once.
9. **Generator data:** generate tiny, large-PDF and reference libraries (commands in 3.6); `verify`, `bench`, `extract`.
10. **`measure.sh`**, full run on AC power. Commit `results/measure.out`.
11. **`check.sh` and workflow**; push; four RIDs green; copy `results/ci/<rid>.txt` into the repo.
12. **Optional** corpus run if the owner supplied files (3.8).
13. **Decide.** Fill ADR: environment, research table, results (warnings per variant, check rows, differential, robustness table, verify counts, bench tables, large PDFs, sizes incl. R2R and code pages, cold start, four-RID table, extrapolation), gate table, decision per rule 3.3, R11 fallback cost table, risk row PDF cover, consequences (pins VersOne.Epub 3.3.6 / PdfPig 0.1.16; `ParsingOptions`; XML settings shelf must use; code page decision; hand-offs to M1 `Shelf.Extraction` with the tags of section 2; re-check triggers: VersOne.Epub 4.x, PdfPig 0.2/1.0, .NET 11; delete `experiments/parsers/` and `spike-parsers.yml` at M0 close). Status `Accepted`, or stop and ask if rule 3.3 step 3 (> 5 days), 4 or 5 applies.
14. **Roadmap:** M0 "AOT spike" row: append "Parser part (VersOne.Epub, PdfPig): [ADR 0005](adr/0005-epub-pdf-parsers-aot.md)" with the result; other libraries stay open.
15. **Quality gates:** `dotnet format --verify-no-changes`, `dotnet build -c Release`, `dotnet test --no-build -c Release` green (solution unchanged); `git status` shows nothing under `experiments/parsers/{out,data,corpus}`.
16. Push, open the PR (gate table and four-RID results in the description); `ci` green; `spike-parsers` green on four RIDs.

## 6. Open Questions

- **Q1: Gates and budgets** (3.3): T1 ≤ 5 min (one third of PRD 9.1's 15 min), T2 ≤ 1 GB, T3 ≤ 10 s / 500 MB, 10 s child timeout, 512 MB RSS for broken inputs, 5-day limit for the hand-written EPUB reader. The PRD gives only the end-to-end 15 min. Confirm before step 10.
- **Q2: PDF cover spike.** PDFtoImage/SkiaSharp/PDFium are native per RID and not in the roadmap's AOT spike list. Recommendation: its own small M0 slice (`m0-spike-pdf-cover`, same structure) because four-RID native packaging is M0's purpose and affects G7; alternative: first task of M4.
- **Q3: F-IMP-5 milestone.** Roadmap places F-IMP-5 in M4 ("pulled into M4"); the hand-off brief mentioned `@F-IMP-5 @M1`. Recommendation: EPUB cover extraction is cheap and could move to M1 (cover bytes in the work folder) with the PDF part in M4; owner decides; tag accordingly.
- **Q4: `cover.jpg`.** EPUB covers are JPEG or PNG (X-E6 is PNG). PRD says `cover.jpg`: transcoding needs an image library (SkiaSharp comes with PDFtoImage). Recommendation: store the original as `cover.<ext>` and change the PRD wording, or decide with Q2.
- **Q5: PdfPig sub-packages / text extractor.** Whether `page.Text` suffices or the DLA extractors are needed (X-P5), and whether they ship in the main package (R2). Recommendation: decide from X-P5; no additional package without an ADR 0001 check.
- **Q6: ISBN scope.** PRD says "first and last pages"; EPUB has no pages. Proposal: PDF pages 1-5 and last 3, EPUB first 3 and last 2 spine items; accept ISBN-10 only with an `ISBN` prefix (I6); text ISBNs rank below OPF/Info/XMP identifiers; with several distinct ISBNs (print vs. e-book) take the first and record the others. Confirm.
- **Q7: Text and locator model.** EPUB "chapter" = spine item or TOC entry? CFI granularity: spine-level (`/6/N[id]!`) is enough for F-SRC-5 deep links; chunk locators (F-SEM-2) and reader progress need element-level CFIs and a DOM-capable XHTML parse. PDF locator `page=N`, 1-based (as in PRD 10.4). Recommendation: spine item = chapter, element-level CFI designed in M1 using the R-E10 / variant (a) results.
- **Q8: Hand-written EPUB fallback prototype now?** Recommendation: no; only the R11 cost estimate. Build it only if E fails (rule step 3). Alternative: a 1-day time-boxed OPF-only reader as a second differential reference.
- **Q9: XHTML-to-text.** XmlReader (strict, DOM paths, fails on undeclared entities) vs. tolerant stripper vs. a third-party HTML parser (AngleSharp/HtmlAgilityPack: not evaluated, would need ADR 0001 and AOT checks). Decided in M1 with the recorded data.
- **Q10: Code pages.** Register `CodePagesEncodingProvider` in shelf? Depends on R6/R-E11 and the size delta. Recommendation: only if a fixture or corpus file needs it.
- **Q11: Owner corpus** (3.8): optional, local, aggregates only. Confirm the privacy rule (hashes only in committed output) and whether the spike waits for it (recommendation: no).
- **Q12: Language for PDFs.** PRD lists `language` as an optional work field but does not say where it comes from for PDFs (catalog `/Lang`, XMP `dc:language`, none) or whether text-based detection is wanted. X-P6 records availability. Owner decision for M1.
- **Q13: CI.** Recommendation: the separate, non-required `spike-parsers.yml` now (as ADR 0004). Confirm.
- **Q14: Keep `experiments/parsers/`** until M0 closes, then delete with an ADR note pointing to the last commit. Recommended.
- **Q15: Error model and isolation in M1.** Which error kinds become review entries (F-IMP-7) vs. "metadata only" (PRD Q7) vs. skip; whether M1 needs input limits (max entry size, nesting) or per-file process isolation, depending on R-E12/R-E13/R-P8.
- **Q16: Source numbering:** own index S1... (as ADR 0003/0004). Recommended.
