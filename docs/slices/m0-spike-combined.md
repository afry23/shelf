# Slice m0-spike-combined: AOT Spike Part 5, one binary with all building blocks

| | |
|---|---|
| **Slice ID** | `m0-spike-combined` |
| **Branch** | `feat/m0-spike-combined` |
| **Milestone** | M0 – Setup & Spikes |
| **Roadmap** | "AOT spike" work package: "One AOT binary that loads and uses: Microsoft.Data.Sqlite + FTS5, sqlite-vec, VersOne.Epub, PdfPig, ..., ModelContextProtocol SDK, ASP.NET Core Minimal APIs ..., TOML parser" (roadmap line 56); exit criterion "The AOT spike runs on both supported platforms, or every incompatible library has a documented fallback" (line 65), combined part, **on `osx-arm64` only** (see Scope) |
| **PRD references** | 9.1 (cold start < 150 ms; observation only here), 9.2 (single Native AOT binary, ReadyToRun fallback), 10.1 (one binary for CLI, MCP and web), 10.2 (technology table), G7 (single-file binary), risk table 14 ("Native AOT incompatible with libraries") |
| **Inputs** | ADR 0002 (System.CommandLine 2.0.12), ADR 0003 (CsToml), ADR 0004 (SQLite, sqlite-vec), ADR 0005 (EPUB/PDF parsers), ADR 0006 (MCP SDK), ADR 0007 (web backend, embedded Svelte); `experiments/*` of those spikes (clients and fixtures are reused); `tests/fixtures/` |

---

## 1. Scope

**In scope**

1. One Native AOT console app `proto-shelf` in `experiments/combined/` (not in `shelf.slnx`, not built by existing CI jobs) that references **all** libraries of the earlier spikes at their pinned versions and exposes one small command per building block through System.CommandLine:
   `config get`, `db check`, `extract`, `mcp`, `serve`, plus `--help` / `--version`.
2. The question this slice answers: do the libraries still publish with 0 AOT/trim warnings, still behave the same and still find their natives **when they are linked into one binary**, and what do the combined binary and its companion files look like?
3. The earlier spikes' check clients are reused unchanged: the MCP client (`experiments/mcp/Client`, rows M2-M9) against `proto-shelf mcp`, and the web client (`experiments/web/Client`, rows W3-W10) against `proto-shelf serve`.
4. `docs/adr/0008-combined-aot-binary.md`: criteria fixed before measuring, results, decision, hand-offs.
5. Update the roadmap AOT spike line.

**Requirements covered:** none implemented. The slice produces a decision (ADR 0008) and evidence for M0's exit criterion.

**Out of scope**

- **`linux-x64`.** Owner decision: only `osx-arm64`, no CI workflow. ADR 0008 and the roadmap state that the combined binary is **not verified** on `linux-x64`.
- **Re-running every row of the earlier spikes.** The combined app uses each library through a small command, not through the full check suites of ADR 0003-0007 (about 3,700 lines of storage and parser check code are not linked in). Coverage of rarely used code paths per library is therefore lower than in the single spikes; this is a stated limit, not a gap to close here.
- Product code: the real `src/Shelf.Cli`, `src/Shelf.Core`, any `Shelf.Web` / `Shelf.Mcp` library split. The combined app is one executable project with a `FrameworkReference` to `Microsoft.AspNetCore.App`; whether the product keeps web and MCP in separate class libraries is not tested here.
- ONNX Runtime and PDF cover rendering (PDFtoImage/PDFium): not part of the AOT spike set (owner decisions); the binary does not contain them.
- Performance gates. Cold start of `--version` / `--help` is recorded as an **observation only** (owner decision), together with binary size.
- A browser run of the UI, `mcp.readOnly`, real data (as in ADR 0006 and 0007).
- Any change to `src/`, `tests/`, `shelf.slnx`, root `Directory.*.props`, `global.json`, `build.yml`.

## 2. Feature Files

**None in this slice**, same reasoning as `m0-spike-mcp` section 2: no product behavior and no code under `src/` is added; the check rows (3.4) are the concrete examples. Rule 6 still applies: `dotnet test -c Release` on the solution stays green (nothing in the solution changes).

## 3. Design

### 3.1 Files

```
experiments/combined/                 NEW  not in shelf.slnx
  Directory.Build.props               NEW  as experiments/web (no root import, TreatWarningsAsErrors=false)
  Directory.Packages.props            NEW  the pins of the earlier spikes (3.2 table)
  .gitignore                          NEW  bin/ obj/ out/ vendor/ results/* !results/local/ results/local/*.log
  README.md                           NEW
  Combined/                           NEW  AOT console app `proto-shelf`
    Combined.csproj                   NEW  FrameworkReference Microsoft.AspNetCore.App; EmbeddedResource of
                                           ../../web/Ui/dist (LinkBase=ui); sqlite natives next to the binary
    Program.cs                        NEW  root command, options, exit codes
    ConfigCommand.cs                  NEW  CsToml read of one key, syntax error with line
    DbCommand.cs                      NEW  SQLite FTS5 and sqlite-vec check (3.3)
    ExtractCommand.cs                 NEW  VersOne.Epub / PdfPig metadata
    Mcp/                              NEW  the MCP server of experiments/mcp (namespace Proto.Mcp)
    Web/                              NEW  the web server of experiments/web (namespace Proto.Web)
  check.sh                            NEW  build UI and clients, publish AOT, run the rows, write results
docs/adr/0008-combined-aot-binary.md                  NEW
docs/roadmap.md                                       EDIT  AOT spike line
```

The MCP and web code is **copied** with a namespace prefix instead of linked, because the two spikes declare the same type names (`Book`, `FormatInfo`, `StderrLoggerProvider`) in namespace `Proto`. The copies are verbatim apart from the namespace and from turning the web server's top-level statements into a method.

### 3.2 Pins (from the earlier ADRs; nothing here is re-evaluated)

| Package | Version | From |
|---|---|---|
| System.CommandLine | 2.0.12 | ADR 0002 |
| CsToml | 1.8.6 | ADR 0003 |
| Microsoft.Data.Sqlite, SQLitePCLRaw.bundle_e_sqlite3 | 10.0.12, 2.1.12 | ADR 0004 |
| sqlite-vec (loadable asset, not a package) | v0.1.9, pinned SHA-256 | ADR 0004 |
| VersOne.Epub, PdfPig | 3.3.6, 0.1.16 | ADR 0005 |
| ModelContextProtocol.Core | 2.2.0 | ADR 0006 |
| Microsoft.AspNetCore.OpenApi, Microsoft.Extensions.FileProviders.Embedded | 10.0.12 | ADR 0007 |
| Microsoft.Extensions.Logging | 10.0.12 | repository root |

Research for this slice is limited to what combining adds: version conflicts between these packages' transitive dependencies (for example `Microsoft.Extensions.*` 10.0.10 in the MCP packages against 10.0.12 elsewhere) and where the SQLite natives end up in a binary that also references the ASP.NET Core shared framework. Findings go into ADR 0008.

### 3.3 Commands and fixtures

| Command | Behavior (fixed data only) |
|---|---|
| `proto-shelf --version`, `--help` | version string; help text; exit code 0 |
| `proto-shelf config get <file> <key>` | prints the value of a key of `experiments/toml-parser/sample/config.toml`; a syntax error prints the line number and exits 1 |
| `proto-shelf db check --vec <path-to-vec0>` | creates an in-memory SQLite database, an FTS5 table with fixed rows, runs a `MATCH` query ordered by `bm25`, loads `vec0` with `LoadExtension`, runs `vec_version()` and a KNN query over fixed vectors; prints a result line per step |
| `proto-shelf extract <file>` | EPUB: title, authors, language; PDF: title, author, page count; prints one `key=value` line each |
| `proto-shelf mcp` | the stdio MCP server of ADR 0006 |
| `proto-shelf serve [--bind --port --token --file]` | the web server of ADR 0007 with the embedded Svelte build |

Fixtures: `tests/fixtures/upstream/gutenberg/pg201-flatland.epub3.epub` and `tests/fixtures/derived/Event Streams in Practice.pdf` (expected values from `tests/fixtures/manifest.json` and ADR 0005); the sqlite-vec asset is fetched with the pinned script of `experiments/storage/fetch-sqlite-vec.sh`.

### 3.4 Checks (AOT binary, `osx-arm64`)

| Row | Check | Gate |
|---|---|---|
| C1 | `dotnet publish -c Release -r osx-arm64 -p:TrimmerSingleWarn=false`: 0 `IL*`/`AOT*` warnings | yes |
| C2 | `--version` and `--help` exit 0 with output; an unknown command and an unknown option exit with a non-zero code and a message on stderr | yes |
| C3 | `config get` returns the expected value for three keys of the sample file; the invalid file `experiments/toml-parser/checks/invalid/duplicate-key.toml` exits 1 with `line 3` in the message | yes |
| C4 | `db check`: FTS5 `MATCH` returns the expected rows in `bm25` order; `vec0` loads from the binary's directory; `vec_version()` is `v0.1.9`; the KNN query returns the expected nearest id | yes |
| C5 | `extract` on the EPUB prints title `Flatland: A Romance of Many Dimensions`, author `Edwin Abbott Abbott`, language `en`; on the PDF prints title `Event Streams in Practice`, author `Jördis Ålander`, 1 page | yes |
| C6 | the MCP client of `experiments/mcp/Client` against `proto-shelf mcp`: rows M2-M9 pass; the AOT dump equals the dump against the JIT build of the same combined app (M10 as in ADR 0006) | yes |
| C7 | the web client of `experiments/web/Client` against `proto-shelf serve`, with `Ui/dist` moved away and an empty working directory: rows W3-W10 pass; the AOT dump equals the dump against the JIT build (W11 as in ADR 0007) | yes |
| C8 | the publish directory is copied to an empty location and C3-C5 are repeated there: the binary and the files that the publish placed next to it (natives) are sufficient; the list of those files is recorded | yes |
| X-1 | binary size and sizes of the files next to it; the sum of the five single-spike binaries for comparison | observation |
| X-2 | cold start of `--version` and `--help`: median and p95 of 30 runs after 5 warm-up runs, plus the first run | observation (not gated) |

### 3.5 AOT and combination risks

- Trim and AOT warnings that only appear when libraries share one trimmed assembly set (shared dependencies in different versions).
- The SQLitePCLRaw native resolution (`libe_sqlite3`) behaving differently when the binary also references the ASP.NET Core shared framework.
- Reflection-based registration in one library being trimmed because another library's roots changed.
- System.CommandLine's help and parse-error output (ADR 0002) conflicting with commands that own stdout (`mcp`): the root command must not print anything to stdout before the server starts.

### 3.6 Decision criteria (write into ADR 0008 with status `Proposed` and commit before measuring)

1. C1-C8 pass: the combination is adopted as the architecture for M1-M4: one Native AOT binary containing all libraries; the list of companion files from C8 is the input for the distribution work.
2. C1 reports warnings only in the combination (not in the single spikes): record each warning; evaluate in order (a) remove a package-version conflict (pin), (b) a code change on our side, (c) ReadyToRun for the whole binary. **Escalate with the options; no decision alone.**
3. A runtime failure in exactly one library only in the combined binary (C3-C7): record the exact exception; try the single-spike configuration of that library as the reference to isolate the cause. **Escalate; no decision alone.**
4. `linux-x64` stays "not verified" and is listed as a hand-off.

## 4. Unit Tests

None added to the solution. The experiment carries its own rows.

## 5. Step Order

1. Branch `feat/m0-spike-combined`; commit this plan.
2. Write ADR 0008 as `Proposed` with the criteria (3.6) and commit **before measuring**.
3. `Combined/` app, `check.sh`; run on `osx-arm64`, JIT first, then AOT.
4. Fill ADR 0008 with the results; set the status per the decision rule.
5. Update the roadmap AOT spike line.
6. Full `dotnet test -c Release` on the solution (must stay green); PR.

## 6. Open Questions

- If a result shows a problem that the single spikes did not, the owner decides between the options in criterion 2 or 3.
- The root `.gitignore` ignores `package.json` and `package-lock.json`; the web UI's files are tracked through a negation in `experiments/web/.gitignore`. The combined experiment reuses that UI and adds no `package.json` of its own.
