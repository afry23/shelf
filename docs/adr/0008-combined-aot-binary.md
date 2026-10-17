# ADR 0008: One Native AOT binary with all libraries

| | |
|---|---|
| **Status** | Accepted for `osx-arm64`: all libraries run in one Native AOT binary; C1-C8 pass; two project-setup findings (3.1, 3.2 below) must be carried into the product; `linux-x64` not verified |
| **Date** | 2026-10-17 |
| **Scope** | PRD 9.1 (cold start, observation only), 9.2 (single Native AOT binary, ReadyToRun fallback), 10.1 (one binary for CLI, MCP and web), 10.2, G7, risk table 14 |
| **Roadmap** | M0 "AOT spike": "One AOT binary that loads and uses ..." (roadmap line 56); exit criterion "every incompatible library has a documented fallback", on `osx-arm64` only |
| **Inputs** | [ADR 0002](0002-cli-framework.md) to [ADR 0007](0007-web-backend-aot.md), slice plan `docs/slices/m0-spike-combined.md` |

Order of work: the criteria were committed with status `Proposed` before `experiments/combined/` existed (commit `3da3c4e`), so they are provable from the repository history. Findings, measurements, decision and consequences were added after the run.

## Context

The earlier spikes verified each library in its own AOT binary: System.CommandLine (ADR 0002), CsToml (ADR 0003), SQLite + FTS5 and sqlite-vec (ADR 0004), VersOne.Epub and PdfPig (ADR 0005), the MCP SDK (ADR 0006), ASP.NET Core Minimal APIs with the embedded Svelte build (ADR 0007). The product is one binary (PRD 10.1, G7), so the remaining risk is the combination: trim and AOT warnings that only appear when all libraries share one trimmed assembly set, native library resolution next to a binary that also references the ASP.NET Core shared framework, and the size of the result.

Owner decisions taken before the work:

- **Platform:** `osx-arm64` only, run locally, no CI workflow. `linux-x64` is not verified for the combined binary; the exit criterion "AOT spike runs on both supported platforms" stays open for it.
- **Location:** `experiments/combined/`, outside `src/`; the product code is not touched.
- **Cold start** (`--version`, `--help`) and binary size are observations only.
- ONNX Runtime has no spike: the owner rejected the plan for it (`m0-spike-onnx`), and whether ONNX stays in M0 is open (ADR 0001 row 11 is still `pending`); PDF cover rendering is a separate slice. The binary contains neither.
- `osx-x64` and `linux-arm64` are not supported platforms (PRD 9.2).

## Options

- **A. One Native AOT executable** referencing all libraries (the plan under test).
- **B. ReadyToRun** for the whole binary (PRD 9.2 fallback; an AOT binary cannot host JIT-compiled code, so the fallback applies to everything).
- **C. Separate executables per function** (CLI, MCP, web). Contradicts PRD 10.1 and G7; named only as the last resort for the owner.

## Criteria (fixed before measuring)

### Gates (`osx-arm64`, AOT binary `proto-shelf`)

| ID | Gate |
|---|---|
| C1 | `dotnet publish -c Release -r osx-arm64 -p:TrimmerSingleWarn=false`: 0 `IL*`/`AOT*` warnings |
| C2 | `--version` and `--help` exit 0 with output; an unknown command and an unknown option exit non-zero with a message on stderr |
| C3 | `config get` returns the expected value for three keys of the sample file; the invalid duplicate-key file exits 1 with `line 3` in the message |
| C4 | `db check`: FTS5 `MATCH` returns the expected rows in `bm25` order; `vec0` loads from the binary's directory; `vec_version()` is `v0.1.9`; the KNN query returns the expected nearest id |
| C5 | `extract` on the EPUB prints title `Flatland: A Romance of Many Dimensions`, author `Edwin Abbott Abbott`, language `en`; on the PDF title `Event Streams in Practice`, author `Jördis Ålander`, 1 page |
| C6 | the MCP client of `experiments/mcp/Client` against `proto-shelf mcp`: rows M2-M9 pass; the AOT dump equals the dump against the JIT build of the same app |
| C7 | the web client of `experiments/web/Client` against `proto-shelf serve`, with `Ui/dist` moved away and an empty working directory: rows W3-W10 pass; the AOT dump equals the dump against the JIT build |
| C8 | the publish directory copied to an empty location: C3-C5 pass there; the files that the publish placed next to the binary are listed |

**Observations (recorded, not gated):** X-1 binary size, sizes of the files next to it, the sum of the five single-spike binaries; X-2 cold start of `--version` and `--help` (median and p95 of 30 runs after 5 warm-up runs, plus the first run).

### Decision rule

1. C1-C8 pass: option A is adopted as the architecture for M1-M4; the list of companion files from C8 is the input for the distribution work.
2. C1 reports warnings only in the combination (not in the single spikes): record each warning; evaluate in order (a) remove a package-version conflict by pinning, (b) a code change on our side, (c) ReadyToRun for the whole binary. **Escalate with the options; no decision alone.**
3. A runtime failure in exactly one library only in the combined binary (C3-C7): record the exact exception; use the single-spike configuration of that library as the reference to isolate the cause. **Escalate; no decision alone.**
4. `linux-x64` stays "not verified" in every outcome and is listed as a hand-off.

### Checks

`experiments/combined/check.sh` builds the Svelte UI and the two reused clients, publishes the AOT and the JIT build of `proto-shelf`, fetches the pinned sqlite-vec asset, runs C1-C8 and writes `results/local/osx-arm64.txt`. Commands and fixtures are specified in the slice plan section 3.3.

## Method

The question is empirical; no further research is planned beyond what combining adds (slice plan 3.2): version conflicts between transitive dependencies and the placement of the SQLite natives. Findings are added to this file after the run.

## Findings while building the combination

1. **A plain SDK project with a `FrameworkReference` to `Microsoft.AspNetCore.App` does not get the implicit usings of `Microsoft.NET.Sdk.Web`.** The copied web server then failed to compile, and the Minimal API request delegate generator produced invalid code (`typeof()` and `JsonTypeInfo<?>` in `GeneratedRouteBuilderExtensions.g.cs`, about 90 compile errors) as a follow-up of the missing types. The explicit `<Using>` items for `Microsoft.AspNetCore.Builder`, `.Hosting`, `.Http`, `.Routing` and `Microsoft.Extensions.Configuration`, `.DependencyInjection`, `.Hosting`, `.Logging` fix it.
2. **The first AOT publish reported two warnings** (`IL3050` and `IL2026`) in `Microsoft.AspNetCore.Mvc.ModelBinding.ModelMetadata.ModelMetadata(ModelMetadataIdentity)` ("Using member `InitializeDynamicTypeInformation()` which has `RequiresDynamicCodeAttribute` / `RequiresUnreferencedCodeAttribute`"). Bisected: publishing the same project with `Microsoft.NET.Sdk.Web` gave 0 warnings. The Web SDK defaults `MvcEnhancedModelMetadataSupport=false` (`Microsoft.NET.Sdk.Web.ProjectSystem.targets`) and turns it into the runtime feature switch `Microsoft.AspNetCore.Mvc.ApiExplorer.IsEnhancedModelMetadataSupported` (`Sdk.Server.targets`, `Trim="true"`), which lets the trimmer drop that code. Setting the property and adding the `RuntimeHostConfigurationOption` item by hand in the plain SDK project also gives 0 warnings. Not tested: whether the warnings appear in a plain SDK project that references only the web code and none of the other libraries (the cause is the missing switch, not the other libraries, but this was not isolated further).
3. An explicit `PackageReference` to `Microsoft.Extensions.Logging` is unnecessary in a project with the ASP.NET Core framework reference (warning `NU1510`, the package comes with the shared framework); it was removed.

## Measurements

`experiments/combined/check.sh`, `osx-arm64`, macOS 26.6.2 (Apple Silicon), .NET SDK 10.0.401, Node.js v26.10.0. Raw output: `experiments/combined/results/local/osx-arm64.txt`.

| Gate | Result | Value |
|---|---|---|
| C1 AOT publish, 0 warnings | **pass** (after findings 1 and 2) | 0 `IL*`/`AOT*` warnings, 0 warnings of any kind |
| C2 `--version`, `--help`, usage errors | **pass** | version `proto-shelf 0.0.1`; help lists the commands; unknown command and unknown option exit 2 with a message on stderr |
| C3 `config get` | **pass** | `float:0.9`, `string:My Library`, `integer:20`; duplicate-key file exits 1 with `line 3` |
| C4 `db check` | **pass** | SQLite 3.53.3; FTS5 `bm25` order `1,2`; `vec_version=v0.1.9`; KNN nearest `1` |
| C5 `extract` | **pass** | EPUB title, author, language as expected; PDF title `Event Streams in Practice`, author `Jördis Ålander`, 1 page |
| C6 MCP | **pass** | rows M2-M9 pass against `proto-shelf mcp`; AOT and JIT dumps identical (439 lines) |
| C7 web | **pass** | rows W3-W10 pass against `proto-shelf serve` with `Ui/dist` moved away and an empty working directory; AOT and JIT dumps identical (564 lines) |
| C8 copied publish directory | **pass** | C2-C5 pass from an empty location and another working directory |

**Companion files:** the publish placed `libe_sqlite3.dylib` (1,696,624 B) next to the binary; `vec0.dylib` (161,896 B) is not a package asset and was copied by the script (as in ADR 0004). The directory holds `proto-shelf`, `libe_sqlite3.dylib`, `vec0.dylib` (and the `.dSYM` folder); no `*.staticwebassets.endpoints.json` was produced with the plain SDK.

| Observation | Value |
|---|---|
| X-1 binary | `proto-shelf` 30,404,136 B (about 29.0 MiB); the single spikes alone: MCP server 11,622,648 B, web server 15,901,896 B; the embedded UI is 30,607 B of it |
| X-2 cold start | `--version`: first run 8.1 ms, median 8.0 ms, p95 8.6 ms; `--help`: first 9.7 ms, median 8.1 ms, p95 8.4 ms (30 runs after 5 warm-up runs, process start included, started from a Python `subprocess`; power state not controlled) |

## Gate results

C1-C8 all pass on `osx-arm64`.

## Decision

**Option A is adopted as the architecture for M1-M4: one Native AOT executable that contains all libraries (decision rule step 1).** Every library behaves as in its own spike inside the combined binary, the MCP and web response dumps are identical between AOT and JIT, and the binary works from an empty directory with `libe_sqlite3` and `vec0` next to it. Decision rule steps 2 and 3 (escalation) were not needed: the two findings are project-setup issues with a known fix, not library incompatibilities. ReadyToRun and separate executables (options B, C) were not evaluated.

## Consequences

- **Product executable (`Shelf.Cli`).** Whichever project builds the final binary must either use `Microsoft.NET.Sdk.Web` or, as a plain SDK project, set `MvcEnhancedModelMetadataSupport=false` together with the `RuntimeHostConfigurationOption` item of finding 2, and carry the `<Using>` items of finding 1 in the projects that contain Minimal API code. The `RuntimeHostConfigurationOption` belongs to the application, not to a class library; if the web server moves into a class library (`Shelf.Web`, PRD 10.1), the setting must be in the executable project. That split was not tested.
- **Distribution (M5)** per platform: `shelf`, `libe_sqlite3.*` and `vec0.*` together (confirms ADR 0004), plus the licence notices; the embedded UI needs nothing extra.
- **Size.** 29.0 MiB for the AOT binary is far above the single spikes (CLI alone about 3 MB). The PRD has no size target (G7 asks for a single-file binary); trimming options are not pursued. If a limit appears, the largest contributors are not analysed here (not measured).
- **Cold start** of `--version` / `--help` is about 8 ms, well below the PRD target of 150 ms (9.1); the target applies to the commands that matter (search), which this spike did not measure.
- **`linux-x64` is not verified for the combined binary** (owner decision); it is covered when a `linux-x64` check of MCP and web exists, or in the first CI job that publishes the real binary. The M0 exit criterion "AOT spike runs on both supported platforms" stays open for that.
- **Coverage per library** is limited to the commands of the slice plan section 3.3; the full check suites of ADR 0003-0007 remain in the single-spike experiments and were not re-run against the combined binary except through the MCP and web clients.
- **Re-check triggers:** a new library in the binary (PDF cover rendering, ONNX), .NET 11, a change of the ASP.NET Core version, the product's `Shelf.Web` class-library split.
- **Prototype:** `experiments/combined/` is kept until M0 closes together with the other experiments, then deleted with a note pointing to the last commit that contains it.

## Update 2026-10-17: PDF cover rendering

[ADR 0010](0010-pdf-cover-rendering-aot.md) added PDFtoImage 5.4.0 (SkiaSharp, PDFium) and a `cover` command to `proto-shelf`. C1 stayed at 0 AOT/trim warnings (2 `CA1416` platform-analyzer warnings), C2-C8 pass, the binary grew from 30,404,136 B to 30,571,192 B, and the new side files `libpdfium.dylib` (7.2 MB) and `libSkiaSharp.dylib` (15.8 MB) join `libe_sqlite3.dylib` and `vec0.dylib` in the distribution list. The cold start of `--version` stayed at about 8 ms.

## Deviations from the plan

- The plan listed an explicit `Microsoft.Extensions.Logging` package; it is not referenced (finding 3).
- The plan did not foresee findings 1 and 2; both were found by building and publishing and are resolved in `Combined.csproj`.
- `check.sh` counts the AOT runs of the two reused clients as gates (`passed 8/8`) and the JIT runs as well; the first version of the script printed the AOT client output without counting a failure, which was fixed before the recorded run.
- X-2 is reported for `--version` and `--help` only, as agreed; no search command was measured.
