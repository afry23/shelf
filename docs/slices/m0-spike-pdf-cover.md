# Slice m0-spike-pdf-cover: PDF cover rendering under Native AOT

| | |
|---|---|
| **Slice ID** | `m0-spike-pdf-cover` |
| **Branch** | `feat/m0-spike-pdf-cover` |
| **Milestone** | M0 – Setup & Spikes |
| **Roadmap** | M0 work package "PDF cover rendering spike" (roadmap line 57): PDFtoImage 5.4.0 + SkiaSharp + PDFium natives under Native AOT (ADR 0001 rows 6/7, risk row in ADR 0005); F-IMP-5 stays in M4. Also the "AOT spike" exit criterion (roadmap line 65) for this library, **on `osx-arm64` only** |
| **PRD references** | F-IMP-5 (cover for PDF: a rendering of the first page, stored as `cover.<ext>`; the format "is decided in `m0-spike-pdf-cover`"), 9.2 (Native AOT, ReadyToRun fallback), 10.2 (PDF cover rendering row: PDFium binding, e.g. PDFtoImage), G7 (single-file binary, native side files), risk table 14 |
| **Inputs** | ADR 0001 rows 6 (PDFtoImage 5.4.0, MIT, `IsAotCompatible=true` claimed, single maintainer) and 7 (PDFium via `bblanchon.PDFium.*` 152.0.7961); ADR 0005 risk row "PDF cover rendering" and option D (PDFium via P/Invoke); ADR 0008 and `experiments/combined/` (the combined binary this spike is added to); `tests/fixtures/` (four PDFs) |

---

## 1. Scope

**In scope**

1. Reproducible AOT experiment in `experiments/pdf-cover/` (not in `shelf.slnx`, not built by existing CI jobs): `proto-cover`, a Native AOT console app that renders **page 1** of a PDF with PDFtoImage 5.4.0 (SkiaSharp 4.150.1, PDFium 152.0.7961) on **`osx-arm64`**, scaled so that the **longest edge is 600 px** (owner decision), and writes PNG, JPEG and WebP.
2. Checks K1-K9 (3.4): AOT warnings, correctness of the rendering, the three formats, determinism and AOT-vs-JIT equality, robustness against broken and encrypted inputs, thread safety, native files, and the combination with `proto-shelf`.
3. A recommendation for the **format** of `cover.<ext>` for PDFs (F-IMP-5), derived from the K3 data; **the owner decides**.
4. A `cover` command in `experiments/combined/` (`proto-shelf cover <pdf> <out>`), so that the combined binary of ADR 0008 is checked with PDFtoImage in it (owner decision).
5. `docs/adr/0010-pdf-cover-rendering-aot.md`: criteria fixed before measuring, research, results, decision, hand-offs.
6. Update the roadmap (the "PDF cover rendering spike" row and the AOT spike line) and ADR 0001 rows 6 and 7 (claimed AOT value is measured).

**Requirements covered:** none implemented. The slice produces a decision (ADR 0010) and evidence for M0's exit criterion.

**Out of scope**

- **`linux-x64`.** Owner decision: only `osx-arm64`, no CI workflow. ADR 0010 and the roadmap state that PDF cover rendering is **not verified** on `linux-x64` (`libpdfium.so`, SkiaSharp Linux natives).
- **Performance gates.** Owner decision: render time per page, sizes and similar figures are observations only.
- Cover storage in the work folder, naming, the `cover_path` field, cover selection for EPUB, thumbnails of other sizes, caching, the web UI grid (all M1/M4).
- Rendering pages other than page 1, text extraction through PDFium (ADR 0005 option D beyond what the fallback needs), annotations, forms.
- OCR, detection of "blank first page" heuristics, choosing a better page than page 1.
- Any change to `src/`, `tests/`, `shelf.slnx`, root `Directory.*.props`, `global.json`, `build.yml`.

## 2. Feature Files

**None in this slice**, same reasoning as `m0-spike-mcp` section 2: no product behavior and no code under `src/` is added; the check rows (3.4) are the concrete examples. **Hand-off to M4:** the cover feature turns the K-rows into `Examples` of a feature tagged `@F-IMP-5 @M4`. Rule 6 still applies: `dotnet test -c Release` on the solution stays green (nothing in the solution changes).

## 3. Design

### 3.1 Files

```
experiments/pdf-cover/                NEW  not in shelf.slnx
  Directory.Build.props               NEW  as experiments/combined (no root import, TreatWarningsAsErrors=false)
  Directory.Packages.props            NEW  PDFtoImage 5.4.0
  .gitignore                          NEW  bin/ obj/ out/ results/* !results/local/ results/local/*.log
  README.md                           NEW
  fixtures/encrypted.pdf              NEW  small encrypted PDF generated once with pypdf (3.3), committed with provenance
  fixtures/README.md                  NEW  how it was generated, license
  Cover/                              NEW  AOT console app `proto-cover`
    Cover.csproj                      NEW
    Program.cs                        NEW  commands (3.5)
    Render.cs                         NEW  PDFtoImage calls, scaling to the longest edge, encoders
    Pixels.cs                         NEW  non-blank statistic, pixel hash
  check.sh                            NEW  publish AOT + JIT, run K1-K7 and K9, write results
experiments/combined/Combined/CoverCommand.cs   NEW  `cover` command, same render code
experiments/combined/check.sh                   EDIT  K8 rows (cover on the three PDFs, equality with proto-cover)
docs/adr/0010-pdf-cover-rendering-aot.md        NEW
docs/adr/0001-...md, docs/roadmap.md            EDIT
```

### 3.2 Research (facts to verify; nothing below is verified)

Method as in ADR 0006: source at the release tag, `.nupkg` listings, README. Findings go into ADR 0010 with its own source index.

- R1: The PDFtoImage 5.4.0 API: functions that render a page to an `SKBitmap` or directly to PNG, JPEG, WebP streams; `RenderOptions` (target width/height, DPI, aspect ratio handling, anti-aliasing, background color, rotation); how the page size is obtained; how page numbers and passwords are passed; which exceptions it throws.
- R2: Where the natives land on `dotnet publish -r osx-arm64` and under Native AOT: `libpdfium.dylib` from `bblanchon.PDFium.macOS` and `libSkiaSharp.dylib` from `SkiaSharp.NativeAssets.macOS` (universal binary, ADR 0001), including how the native library is found by the managed binding (name, `NativeLibrary` resolution).
- R3: The thread-safety statement of the README (ADR 0001 S39): does PDFtoImage serialize PDFium calls itself, and does it apply across all entry points?
- R4: The encoders: which `SKEncodedImageFormat` values (PNG, JPEG, WebP) are supported by the SkiaSharp native binary of 4.150.1 and how quality is set.
- R5: macOS signing and quarantine behavior of `libpdfium.dylib` and `libSkiaSharp.dylib` (ad-hoc signed? hardened runtime?), as ADR 0004 R5 did for `vec0.dylib`.
- R6: How a protected PDF is reported (exception type or error code) and what happens for a PDF without pages or with a damaged cross-reference table.

### 3.3 Fixtures

The four fixture PDFs of `tests/fixtures/` (text layer; no metadata; image-only `scanned-page.pdf`; the real arXiv paper) plus:

- `fixtures/encrypted.pdf`: generated once with `uv run --with pypdf python` from one fixture PDF with a user password (`secret`), committed with its provenance (license GPL-3.0-or-later like the other derived fixtures). The generation is not part of `check.sh`.
- Broken inputs created by the check at run time: an empty file, random bytes with a `.pdf` name, a fixture truncated at 50 %, and a hand-written minimal PDF whose page tree has zero pages.

### 3.4 Checks (AOT binary, `osx-arm64`)

| Row | Check | Gate |
|---|---|---|
| K1 | `dotnet publish -c Release -r osx-arm64 -p:TrimmerSingleWarn=false` of `Cover`: 0 `IL*`/`AOT*` warnings | yes |
| K2 | page 1 of each of the four fixture PDFs rendered with the longest edge = 600 px: the output decodes with `sips`; the longest edge is exactly 600 px; the other edge is within 1 px of the page aspect ratio; the image is not blank (standard deviation of the luminance >= 5 on a 0-255 scale) | yes |
| K3 | from one rendering per fixture: PNG, JPEG (quality 85) and WebP (quality 80) all decode with `sips` and report the same pixel size; file sizes are recorded | yes |
| K4 | determinism: rendering the same page twice gives the same pixel hash; the AOT and the JIT build give the same pixel hash for every fixture | yes |
| K5 | robustness, one child process per input with a 10 s timeout: the empty file, the random bytes, the truncated file, the zero-page PDF and `encrypted.pdf` each end with a controlled error line (error kind, no crash, no hang, exit code 3) and a peak RSS below 512 MB; the error kind is printed per input | yes |
| K6 | thread safety: 8 threads render 5 pages each from two PDFs at the same time without any lock of ours; every result has the pixel hash of the sequential rendering; no exception | yes |
| K7 | the publish directory copied to an empty location, other working directory: K2 on one fixture passes; the list and sizes of the files next to the binary are recorded | yes |
| K8 | `experiments/combined`: `proto-shelf cover` on three fixtures gives the same pixel hashes as `proto-cover`; the AOT publish of the combined app still has 0 warnings and its rows C1-C8 still pass | yes |
| K9 | the normalized result dump of K2-K6 (pixel hashes, sizes, error kinds) is identical between the AOT and the JIT build | yes |
| X-1 | render time per page: first call and median of 20 calls per fixture; sizes of `proto-cover`, `libpdfium.dylib`, `libSkiaSharp.dylib`; sizes of the three formats per fixture | observation (not gated) |

### 3.5 Commands (`proto-cover`)

- `render <pdf> <out-dir> [--max-edge 600]`: writes `cover.png`, `cover.jpg`, `cover.webp`; prints `size=WxH png=<bytes> jpg=<bytes> webp=<bytes> pixelhash=<sha256 of the RGBA pixels> stddev=<luminance stddev>`.
- `robust <pdf>`: renders page 1 and prints either the `render` line or `error kind=<kind> type=<exception type> message=<first line>` with exit code 3.
- `stress <pdf1> <pdf2>`: K6 (threads, comparison with the sequential hashes).
- `bench <pdf> [--runs 20]`: X-1 timing.
- `info`: library versions and the native library file names that were loaded.

### 3.6 `check.sh`

Publish AOT and JIT, run K2-K7 and K9 with `sips` as independent decoder (`sips -g pixelWidth -g pixelHeight`), then the K8 rows through `experiments/combined/check.sh`, write `results/local/osx-arm64.txt`. macOS only.

### 3.7 AOT and native risks

- PDFtoImage and SkiaSharp may use reflection or marshalling that Native AOT trims (claimed `IsAotCompatible=true`, never measured).
- Two native libraries next to the binary; the managed binding may look them up by name or relative path.
- PDFium is not thread-safe; an unprotected parallel import could crash.
- Quarantine or signing of the downloaded natives on macOS.
- The combined binary gets two more large natives; its size and warnings may change (K8).

### 3.8 Decision criteria (write into ADR 0010 with status `Proposed` and commit before measuring)

1. K1-K9 pass: PDFtoImage 5.4.0 with SkiaSharp and PDFium is adopted for F-IMP-5 (M4) under Native AOT; the format recommendation is derived from the K3 data, the owner decides.
2. K1 warnings or a runtime failure of PDFtoImage: record the exact warning or exception. Evaluate in order (a) another PDFtoImage entry point, (b) PDFium through P/Invoke without PDFtoImage (still needs an image encoder), (c) `PdfPig.Rendering.Skia`, (d) ReadyToRun for the whole binary. **Escalate with the options; no decision alone.**
3. K5 or K6 fails (crash, hang, memory, wrong pixels under parallel calls): record the failure mode; the mitigation is a process-wide lock around rendering (and, for crashes on hostile input, running the render in a child process). **Escalate if the mitigation changes the architecture.**
4. K8 fails (the combined binary): record the warnings or failure; isolate with the single-spike configuration; escalate.
5. `linux-x64` stays "not verified" in every outcome and is listed as a hand-off.

## 4. Unit Tests

None added to the solution (no code under `src/`). The experiment carries its own rows.

## 5. Step Order

1. Branch `feat/m0-spike-pdf-cover`; commit this plan.
2. Research R1-R6; write ADR 0010 as `Proposed` with the criteria (3.8) and commit **before measuring**.
3. `fixtures/encrypted.pdf`; `Cover/` app and `check.sh`; run on `osx-arm64`, JIT first, then AOT.
4. `cover` command in `experiments/combined/` and the K8 rows.
5. Fill ADR 0010 with the results and the format recommendation; set the status per the decision rule.
6. Update the roadmap and ADR 0001 rows 6/7.
7. Full `dotnet test -c Release` on the solution (must stay green); PR.

## 6. Open Questions

- The format decision (PNG, JPEG or WebP for `cover.<ext>` of PDFs) is the owner's after the K3 data; the spike only recommends.
- If PDFtoImage needs a lock that the product cannot place cleanly (K6), the owner decides between a process-wide lock and a child-process renderer.
