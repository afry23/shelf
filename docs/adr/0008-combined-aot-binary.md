# ADR 0008: One Native AOT binary with all libraries

| | |
|---|---|
| **Status** | Proposed: criteria fixed, nothing measured yet |
| **Date** | 2026-10-17 |
| **Scope** | PRD 9.1 (cold start, observation only), 9.2 (single Native AOT binary, ReadyToRun fallback), 10.1 (one binary for CLI, MCP and web), 10.2, G7, risk table 14 |
| **Roadmap** | M0 "AOT spike": "One AOT binary that loads and uses ..." (roadmap line 56); exit criterion "every incompatible library has a documented fallback", on `osx-arm64` only |
| **Inputs** | [ADR 0002](0002-cli-framework.md) to [ADR 0007](0007-web-backend-aot.md), slice plan `docs/slices/m0-spike-combined.md` |

Order of work: this file is committed with status `Proposed` before `experiments/combined/` exists, so the criteria are provable from the repository history. Results, decision and consequences are added after the run.

## Context

The earlier spikes verified each library in its own AOT binary: System.CommandLine (ADR 0002), CsToml (ADR 0003), SQLite + FTS5 and sqlite-vec (ADR 0004), VersOne.Epub and PdfPig (ADR 0005), the MCP SDK (ADR 0006), ASP.NET Core Minimal APIs with the embedded Svelte build (ADR 0007). The product is one binary (PRD 10.1, G7), so the remaining risk is the combination: trim and AOT warnings that only appear when all libraries share one trimmed assembly set, native library resolution next to a binary that also references the ASP.NET Core shared framework, and the size of the result.

Owner decisions taken before the work:

- **Platform:** `osx-arm64` only, run locally, no CI workflow. `linux-x64` is not verified for the combined binary; the exit criterion "AOT spike runs on both supported platforms" stays open for it.
- **Location:** `experiments/combined/`, outside `src/`; the product code is not touched.
- **Cold start** (`--version`, `--help`) and binary size are observations only.
- ONNX Runtime is out of the AOT spike (owner decision); PDF cover rendering is a separate slice. The binary contains neither.
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

## Measurements

Not yet taken.

## Decision

Pending the measurement.

## Consequences

To be written after the measurement. Hand-offs already known:

- `linux-x64` is not verified for the combined binary.
- The product's project structure (separate `Shelf.Web` / `Shelf.Mcp` class libraries referenced by `Shelf.Cli`) is not tested here; the spike uses one executable project with a `FrameworkReference` to `Microsoft.AspNetCore.App`.
- Coverage per library is limited to the commands of the slice plan section 3.3; the full check suites remain in the single-spike experiments.
