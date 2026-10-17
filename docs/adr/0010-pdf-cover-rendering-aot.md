# ADR 0010: PDF cover rendering with PDFtoImage under Native AOT

| | |
|---|---|
| **Status** | Proposed: criteria fixed, nothing measured yet |
| **Date** | 2026-10-17 |
| **Scope** | PRD F-IMP-5 (cover for PDF: a rendering of the first page; the format is decided here), 9.2 (Native AOT, ReadyToRun fallback), 10.2 (PDF cover rendering row), G7, risk table 14 |
| **Roadmap** | M0 "PDF cover rendering spike" (roadmap line 57) and the "AOT spike" exit criterion for this library, on `osx-arm64` only |
| **Inputs** | [ADR 0001](0001-dependency-licenses-and-maintenance.md) rows 6 and 7, [ADR 0005](0005-epub-pdf-parsers-aot.md) (risk row, option D), [ADR 0008](0008-combined-aot-binary.md), slice plan `docs/slices/m0-spike-pdf-cover.md` |

Order of work: this file is committed with status `Proposed` before `experiments/pdf-cover/` exists, so the criteria are provable from the repository history. Results, decision and consequences are added after the run.

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

## Measurements

Not yet taken.

## Decision

Pending the measurement.

## Consequences

To be written after the measurement. Hand-offs already known:

- `linux-x64` is not verified for PDF cover rendering.
- The format of `cover.<ext>` for PDFs is decided by the owner from the K3 data.
- PDFtoImage serializes all PDFium calls (R3): the import cannot render covers in parallel inside one process.
