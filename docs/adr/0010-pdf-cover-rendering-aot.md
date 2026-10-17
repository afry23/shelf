# ADR 0010: PDF cover rendering with PDFtoImage under Native AOT

| | |
|---|---|
| **Status** | Accepted for `osx-arm64`: PDFtoImage 5.4.0 with SkiaSharp and PDFium renders covers under Native AOT and inside the combined binary; K1-K9 pass; the format of `cover.<ext>` for PDFs awaits the owner (data in K3); `linux-x64` not verified |
| **Date** | 2026-10-17 |
| **Scope** | PRD F-IMP-5 (cover for PDF: a rendering of the first page; the format is decided here), 9.2 (Native AOT, ReadyToRun fallback), 10.2 (PDF cover rendering row), G7, risk table 14 |
| **Roadmap** | M0 "PDF cover rendering spike" (roadmap line 57) and the "AOT spike" exit criterion for this library, on `osx-arm64` only |
| **Inputs** | [ADR 0001](0001-dependency-licenses-and-maintenance.md) rows 6 and 7, [ADR 0005](0005-epub-pdf-parsers-aot.md) (risk row, option D), [ADR 0008](0008-combined-aot-binary.md), slice plan `docs/slices/m0-spike-pdf-cover.md` |

Order of work: the criteria were committed with status `Proposed` before `experiments/pdf-cover/` existed (commit `5293ef3`), so they are provable from the repository history. Findings, measurements, decision and consequences were added after the run.

## Context

F-IMP-5 asks for a cover image for every work. For EPUB the embedded cover is stored in its original format (measured in ADR 0005). For PDF the first page has to be rendered, and the PRD leaves the output format to this spike. ADR 0001 adopted PDFtoImage 5.4.0 (MIT) with SkiaSharp 4.150.1 and PDFium 152.0.7961 as `adopt with fallback`; its AOT value is `claimed` (`IsAotCompatible=true`), never measured. Risks recorded there: a single maintainer, PDFium is not thread-safe, and SkiaSharp is a second large native library only to obtain a bitmap.

Owner decisions taken before the work (they fix scope):

- **Size:** the longest edge of the cover is **600 px**.
- **Platform:** `osx-arm64` only, run locally, no CI workflow. `linux-x64` (`libpdfium.so`, SkiaSharp Linux natives) is not verified.
- **Combined binary:** the spike also checks that PDFtoImage works inside `proto-shelf` of ADR 0008.
- **Performance** (render time, sizes) is recorded as observation only.
- The format of `cover.<ext>` for PDFs is decided by the owner after the data of K3; the spike only recommends.
- `osx-x64` and `linux-arm64` are not supported platforms (PRD 9.2).

## Options

- **A. PDFtoImage 5.4.0** (renders with PDFium, uses SkiaSharp for the bitmap and the encoders).
- **B. PDFium through P/Invoke** without PDFtoImage (ADR 0005 option D); still needs an image encoder.
- **C. `PdfPig.Rendering.Skia`** (not evaluated in ADR 0005, S4 there).
- **D. ReadyToRun** instead of Native AOT for the whole binary (PRD 9.2 fallback; an AOT binary cannot host JIT-compiled code, so the fallback applies to everything).

## Criteria (fixed before measuring)

### Gates (`osx-arm64`, AOT binary `proto-cover`)

| ID | Gate |
|---|---|
| K1 | `dotnet publish -c Release -r osx-arm64 -p:TrimmerSingleWarn=false`: 0 `IL*`/`AOT*` warnings |
| K2 | page 1 of each of the four fixture PDFs rendered with the longest edge = 600 px: the output decodes with `sips`; the longest edge is exactly 600 px; the other edge is within 1 px of the page aspect ratio; the image is not blank (standard deviation of the luminance >= 5 on a 0-255 scale) |
| K3 | from one rendering per fixture: PNG, JPEG (quality 85) and WebP (quality 80) all decode with `sips` and report the same pixel size; file sizes are recorded |
| K4 | determinism: rendering the same page twice gives the same pixel hash; the AOT and the JIT build give the same pixel hash for every fixture |
| K5 | robustness, one child process per input with a 10 s timeout: an empty file, random bytes, a file truncated at 50 %, a zero-page PDF and a password-protected PDF each end with a controlled error line (error kind, exit code 3, no crash, no hang) and a peak RSS below 512 MB |
| K6 | thread safety: 8 threads render 5 pages each from two PDFs at the same time without any lock of ours; every result has the pixel hash of the sequential rendering; no exception |
| K7 | the publish directory copied to an empty location, other working directory: K2 on one fixture passes; the files next to the binary and their sizes are recorded |
| K8 | `experiments/combined`: `proto-shelf cover` gives the same pixel hashes as `proto-cover` on three fixtures; the AOT publish of the combined app still has 0 warnings and its rows C1-C8 still pass |
| K9 | the normalized result dump of K2-K6 (pixel hashes, sizes, error kinds) is identical between the AOT and the JIT build |

**Observations (recorded, not gated):** X-1 render time per page (first call and median of 20 calls per fixture), sizes of `proto-cover`, `libpdfium.dylib` and `libSkiaSharp.dylib`, sizes of the three formats per fixture.

### Decision rule

1. K1-K9 pass: PDFtoImage 5.4.0 with SkiaSharp and PDFium is adopted for F-IMP-5 (M4) under Native AOT; the format recommendation comes from the K3 data, the owner decides.
2. K1 warnings or a runtime failure of PDFtoImage: record the exact warning or exception. Evaluate in order (a) another PDFtoImage entry point, (b) option B, (c) option C, (d) option D. **Escalate with the options; no decision alone.**
3. K5 or K6 fails (crash, hang, memory, wrong pixels under parallel calls): record the failure mode; the mitigation is a process-wide lock around rendering and, for crashes on hostile input, rendering in a child process. **Escalate if the mitigation changes the architecture.**
4. K8 fails: record the warnings or failure; isolate with the single-spike configuration; escalate.
5. `linux-x64` stays "not verified" in every outcome and is listed as a hand-off.

### Checks

`experiments/pdf-cover/check.sh` publishes the AOT and JIT builds of `proto-cover`, runs K2-K7 and K9 with `sips` as the independent decoder and calls the K8 rows of `experiments/combined/check.sh`. Commands and fixtures are specified in the slice plan sections 3.3 and 3.5.

## Method

Research of R1-R6 (slice plan 3.2) from the PDFtoImage repository at the tag `v5.4.0`, the `.nuspec` of the package on the NuGet flat container, and the repository README. The source files read: `Conversion*.cs`, `RenderOptions.cs`, `Exceptions/`, the project file, `src/FrameworkTests/AotConsole/`. No code was executed in this phase. Measurement follows in `experiments/pdf-cover/`.

## Research

Statuses: **verified** = read in the cited source; **unverified** = not looked up.

| ID | Finding | Status | Source |
|---|---|---|---|
| R1 | `PDFtoImage.Conversion` offers `ToImage(Stream pdf, Index page, bool leaveOpen, string? password, RenderOptions options)` returning an `SKBitmap`, `GetPageCount`, `GetPageSize` / `GetPageSizes` (`SizeF` in points), and `SavePng` / `SaveJpeg` / `SaveWebp` for files and streams; the same overloads exist for `byte[]` and Base64 input. `RenderOptions` (a record struct) has `Dpi` (default 300), `Width`, `Height` (nullable), `WithAspectRatio` (one of width and height must then be null), `AntiAliasing`, `BackgroundColor` (default white), `Rotation`, `Bounds`, `Grayscale`, `WithAnnotations`, `WithFormFill`, `UseTiling`. A 600 px longest edge therefore needs the page size first and then `Width = 600` (landscape or square) or `Height = 600` (portrait) with `WithAspectRatio = true` | verified | S1, S2 |
| R2 | The package `net10.0` group depends on `SkiaSharp` 4.150.1 with `SkiaSharp.NativeAssets.macOS` / `.Linux.NoDependencies` / `.Win32` and on `bblanchon.PDFium.macOS` / `.Linux` / `.Win32` 152.0.7961 (the same versions as ADR 0001). The project uses `LibraryImport` (`Internals/NativeMethods.LibraryImport.cs`) next to a `DllImport` variant and a `LibraryLoader`; which of the two is compiled for `net10.0`, and where the natives land on an AOT publish, was not looked up | partly verified | S3, S1 |
| R3 | README, section "No parallelization support": PDFium is **not thread-safe**; all calls into PDFium are protected with locks inside PDFtoImage, so only one PDF is processed at a time; for real parallelism the README recommends several processes. This matches the ADR 0001 note. Which entry points take the lock was not read | documented | S4 |
| R4 | The `Save*` methods encode with a fixed quality of 100 (`SaveImpl` calls `bitmap.Encode(stream, format, 100)`), so a JPEG quality of 85 or a WebP quality of 80 requires `ToImage` and SkiaSharp's own encoder (`SKBitmap.Encode` / `SKImage.Encode` with `SKEncodedImageFormat.Jpeg`, `.Webp`, `.Png`). Whether the SkiaSharp native binary of 4.150.1 for macOS includes all three encoders was not looked up | partly verified | S1 |
| R5 | macOS signing and quarantine of `libpdfium.dylib` and `libSkiaSharp.dylib`: not looked up | unverified | - |
| R6 | Exceptions of the PDFtoImage namespace: `PdfException` base with `PdfPasswordProtectedException`, `PdfInvalidFormatException`, `PdfCannotOpenFileException`, `PdfPageNotFoundException`, `PdfUnsupportedSecuritySchemeException` and `PdfUnknownException`. How a zero-page PDF or a damaged cross-reference table is reported was not looked up | partly verified | S1 |

Additional finding: the PDFtoImage repository contains its own Native AOT smoke test `src/FrameworkTests/AotConsole` (`PublishAot=true`, `SelfContained=true`, `InvariantGlobalization=true`, with Dockerfiles for Ubuntu and Alpine AOT builds) that renders `SocialPreview.pdf` and checks the size 5333 x 2666. It is evidence for the AOT claim by the maintainer, not a measurement of ours (S5). The project sets `IsAotCompatible=true` for TFMs compatible with net7.0 (ADR 0001 S38).

### Source index

| ID | Source |
|---|---|
| S1 | `sungaila/PDFtoImage` at tag `v5.4.0` (`gh api repos/sungaila/PDFtoImage/contents/<path>?ref=v5.4.0`): `src/PDFtoImage/Conversion.cs`, `Conversion.Stream.cs`, `Conversion.ByteArray.cs`, `RenderOptions.cs`, `Exceptions/*.cs`, `PDFtoImage.csproj` |
| S2 | `src/PDFtoImage/RenderOptions.cs` at tag `v5.4.0` (parameter documentation) |
| S3 | nuspec `pdftoimage` 5.4.0 (`https://api.nuget.org/v3-flatcontainer/pdftoimage/5.4.0/pdftoimage.nuspec`) |
| S4 | `README.md` of the repository at tag `v5.4.0`, section "No parallelization support" |
| S5 | `src/FrameworkTests/AotConsole/AotConsole.csproj` and `Program.cs` at tag `v5.4.0` |

Repository: https://github.com/sungaila/PDFtoImage, tag `v5.4.0`.

## Research updates from the measurement

- **R1 (scaling):** `ToImage(byte[], 0, options: new RenderOptions(Dpi: 300, Width: 600 | null, Height: null | 600, WithAspectRatio: true))` after `GetPageSize(byte[], 0)` gives the 600 px longest edge exactly; the short edge is the floor of the aspect-ratio value (612 x 792 pt gives 463 x 600). `Index` 0 is the first page.
- **R2 (natives):** the AOT publish places `libpdfium.dylib` (7,151,568 B) and `libSkiaSharp.dylib` (15,811,648 B) next to the binary; with only these two files and `proto-cover` the program runs from an empty directory (K7). The binding finds them without any configuration.
- **R3 (threads):** confirmed by behavior: 8 threads without a lock of ours give correct results (K6); the README says PDFtoImage locks around PDFium itself, so the calls run one after another. The measured wall time does not show a speed-up (see observations).
- **R4 (encoders):** `SKImage.Encode(SKEncodedImageFormat.Png | Jpeg | Webp, quality)` works in the SkiaSharp 4.150.1 native binary for macOS; all three formats were produced and decoded. The `Save*` methods of PDFtoImage cannot be used for quality 85 / 80 (fixed quality 100).
- **R5 (signing, quarantine):** not examined. Both dylibs loaded from the freshly published directory and from a copy without any step on our side.
- **R6 (errors):** an empty file, random bytes and a truncated PDF raise `PdfInvalidFormatException`; a password-protected PDF raises `PdfPasswordProtectedException`; **a PDF with zero pages raises `System.ArgumentOutOfRangeException`** ("The page number must be between 0 and -1") from `GetPageSize` / `ToImage`, not a `PdfException`.

## Measurements

`experiments/pdf-cover/check.sh`, `osx-arm64`, macOS 26.6.2 (Apple Silicon), .NET SDK 10.0.401, PDFtoImage 5.4.0, SkiaSharp 4.150.1 (assembly 4.150.0.0), PDFium 152.0.7961. Raw output: `experiments/pdf-cover/results/local/osx-arm64.txt`.

| Gate | Result | Value |
|---|---|---|
| K1 AOT publish, 0 warnings | **pass** | 0 `IL*`/`AOT*` warnings; 2 `CA1416` platform-analyzer warnings (see findings) |
| K2 rendering | **pass** | page 1 of all four fixtures: 463 x 600 (three Letter pages) and 423 x 600 (A4); luminance standard deviation 23.5 to 34.4 (not blank); every file decodes with `sips` |
| K3 formats | **pass** | PNG, JPEG (q85) and WebP (q80) of every fixture decode with `sips` at the same pixel size |
| K4 determinism | **pass** | repeat renders and the AOT and JIT builds give identical pixel hashes |
| K5 robustness | **pass** | empty, random, truncated, zero-page and encrypted: exit 3 with a controlled error line, peak RSS 17 MB, no timeout |
| K6 threads | **pass** | 8 threads x 5 renders: 0 mismatches, 0 exceptions |
| K7 copied directory | **pass** | rendering from a copy in an empty location with another working directory equals the original |
| K8 combined binary | **pass** | `proto-shelf cover` gives the same pixel hashes as `proto-cover` on three fixtures; combined C1 still 0 AOT/trim warnings, C2-C8 and the cover rows pass, also from the copied directory |
| K9 AOT vs JIT | **pass** | normalized dump of K2-K6 identical |

**K3 file sizes (bytes), same 600 px rendering:**

| Fixture | Size | PNG | JPEG q85 | WebP q80 |
|---|---|---|---|---|
| text layer (`Event Streams in Practice`) | 463 x 600 | 67,122 | 41,578 | 25,854 |
| no metadata (`Quiet Systems`) | 463 x 600 | 62,049 | 40,307 | 24,812 |
| image only (`scanned-page`) | 463 x 600 | 147,045 | 68,795 | 41,316 |
| arXiv paper | 423 x 600 | 75,840 | 37,853 | 20,310 |

**K5 per input:** `empty`, `random`, `truncated`: `error kind=bad-pdf type=PdfInvalidFormatException`; `zeropage`: `kind=io type=ArgumentOutOfRangeException`; `encrypted`: `kind=encrypted type=PdfPasswordProtectedException`. The `kind` names are the experiment's own mapping.

| Observation | Value |
|---|---|
| X-1 render time (render + JPEG encode, one 600 px page) | first call 5.0 to 20.0 ms, median 1.6 to 1.8 ms for the three generated PDFs, 7.4 ms for the arXiv paper |
| X-1 stress wall time | 40 renders on 8 threads: 153 ms; two sequential first renders: 23 ms (cold start included); no speed-up from threads is claimed |
| X-1 sizes | `proto-cover` 2,050,792 B; `libpdfium.dylib` 7,151,568 B; `libSkiaSharp.dylib` 15,811,648 B |
| X-1 combined binary | `proto-shelf` 30,571,192 B (30,404,136 B before, +167,056 B); the new side files are `libpdfium.dylib` and `libSkiaSharp.dylib` (22.9 MB together); `--version` cold start still about 8 ms median |

### Observations and pitfalls found while running

1. **A zero-page PDF is not reported as a `PdfException`** (R6). The product must check `GetPageCount` first or catch `ArgumentOutOfRangeException`.
2. **`CA1416` warnings.** The two PDFtoImage calls compile with "This call site is reachable on all platforms" because the library declares a platform list (`[SupportedOSPlatform]`). The MSBuild item `<SupportedOSPlatform Include="macos" />` / `linux` did not remove them; assembly-level `[assembly: SupportedOSPlatform("macos")]` and `[assembly: SupportedOSPlatform("linux")]` removed them in a JIT build (the committed experiment keeps the warnings; the fix was not published as AOT). A class-level attribute only moved the warning to the callers. The roadmap plans warnings as errors, so the product needs this decision.
3. **`Save*` encodes at quality 100** (R4); the product calls `ToImage` and SkiaSharp's encoder to choose a quality.
4. **SkiaSharp dominates the native size** (15.8 MB against 7.2 MB for PDFium) although only a bitmap and an encoder are needed from it.
5. **WebP decoding was only checked with `sips` on macOS 26.6.2**; support in other tools and OS versions was not examined, and the visual quality of JPEG q85 against WebP q80 was not compared.

## Gate results

K1-K9 all pass on `osx-arm64`.

## Decision

**PDFtoImage 5.4.0 with SkiaSharp 4.150.1 and PDFium 152.0.7961 is adopted for F-IMP-5 (M4) under Native AOT (decision rule step 1).** No fallback (PDFium via P/Invoke, `PdfPig.Rendering.Skia`, ReadyToRun) was needed and none was evaluated. Rendering is one process-wide serialized operation (R3); the import must not rely on parallel rendering inside one process.

**Format of `cover.<ext>` for PDFs: owner decision pending.** What the data shows at 600 px: WebP is the smallest (20 to 41 KB), JPEG is about 1.6 to 1.9 times larger (38 to 69 KB), PNG is about 1.5 to 2.1 times larger than JPEG (62 to 147 KB). All three decode; JPEG and PNG are the formats EPUB covers normally already have. Options: (a) **JPEG q85**: smallest common denominator for every viewer and tool, moderate size; (b) **WebP q80**: smallest files, supported by current browsers, but tool support beyond what `sips` showed and the visual quality were not checked; (c) **PNG**: lossless, largest, the better choice only for flat scanned line art. Recommendation: **(a) JPEG**, because a library cover is a thumbnail whose size is hardly a constraint at 40 to 70 KB, JPEG is the most widely supported of the three formats, and the EPUB covers are normally JPEG or PNG already.

## Consequences

- **M4 cover feature:** implements the pattern of `Render.cs` (page size, `ToImage` with `Width` or `Height` and `WithAspectRatio`, SkiaSharp encoder with an explicit quality) behind `GetPageCount > 0` and a catch for `PdfException` and `ArgumentOutOfRangeException`; scenarios for text, image-only and metadata-less PDFs, a damaged file, a password-protected file, a zero-page file and landscape and portrait pages.
- **Distribution (M5)** per platform: the binary plus `libe_sqlite3.*`, `vec0.*`, `libpdfium.*` and `libSkiaSharp.*` (confirms ADR 0004 and extends ADR 0008); the sum on `osx-arm64` is about 55 MB (30.6 + 1.7 + 0.2 + 7.2 + 15.8). The notices file needs PDFium's `licenses/` (ADR 0001 row 7) and SkiaSharp's.
- **Platform analyzer:** decide the `CA1416` handling (assembly-level `SupportedOSPlatform`, finding 2) before warnings become errors.
- **Parallel import:** use a single worker for cover rendering, or a child-process pool, if throughput matters; the 1.6 to 7.4 ms per page measured here does not make this urgent.
- **Maintainer risk** (ADR 0001 flag: a single maintainer) stays; the repository ships its own Native AOT smoke test (note under Research, S5), which lowers but does not remove the risk
- **`linux-x64` is not verified** (owner decision); `libpdfium.so` and the SkiaSharp Linux natives are untested. The M0 exit criterion stays open for this library on `linux-x64`.
- **Re-check triggers:** PDFtoImage 6.0 (preview exists), a new PDFium or SkiaSharp major, .NET 11, a need for rendering other pages or sizes, the first run on real books (scanned covers, huge pages).
- **Prototype:** `experiments/pdf-cover/` is kept until M0 closes together with the other experiments, then deleted with a note pointing to the last commit that contains it.

## Deviations from the plan

- The plan's `check.sh` text said K8 is run "through `experiments/combined/check.sh`"; it also compares pixel hashes of `proto-shelf cover` with `proto-cover` after that script, and the combined check script got its own cover row (also repeated from the copied directory).
- The zero-page PDF is created by `check.sh` as a hand-written minimal file; its error type (`ArgumentOutOfRangeException`) is not one of the PDFtoImage exceptions, so the experiment's `kind=io` mapping is a placeholder for it.
- The format recommendation ranges are computed from the four fixtures only.
