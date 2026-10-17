# Slice m0-spike-mcp: AOT Spike Part 3, ModelContextProtocol SDK

| | |
|---|---|
| **Slice ID** | `m0-spike-mcp` |
| **Branch** | `feat/m0-spike-mcp` |
| **Milestone** | M0 – Setup & Spikes |
| **Roadmap** | "AOT spike" work package, MCP part only: ModelContextProtocol SDK (roadmap line 56); exit criterion "The AOT spike runs on both supported platforms, or every incompatible library has a documented fallback" (line 65), MCP part, **on `osx-arm64` only** (see Scope) |
| **PRD references** | 7.2 (tools, resources `shelf://book/{id}` and `shelf://book/{id}/toc`, stdio transport, destructive operations not offered), 9.2 (Native AOT, ReadyToRun fallback), 10.2 (MCP row), risk table 14 ("Native AOT incompatible with libraries") |
| **Inputs** | ADR 0001 row 13 (`ModelContextProtocol` 2.2.0, Apache-2.0, `IsAotCompatible=true` claimed, verdict `adopt`; sources S92-S96); ADR 0005 and `docs/slices/m0-spike-parsers.md` (spike/ADR structure); `experiments/parsers/` (props, `check.sh`, differential pattern); ADR 0002 (System.CommandLine, stderr logging slice `m0-logging`) |

---

## 1. Scope

**In scope**

1. Reproducible AOT experiment in `experiments/mcp/` (not in `shelf.slnx`, not built by existing CI jobs): a stdio MCP server as Native AOT binary on **`osx-arm64`**, run locally, with a few dummy tools and two resources modelled on PRD 7.2.
2. Protocol check over stdio: `initialize`, `tools/list`, `tools/call`, `resources/templates/list`, `resources/read`; responses compared with expected JSON.
3. Tool parameter and result serialization without reflection (source-generated `JsonSerializerContext`), including JSON schemas in `tools/list`.
4. Clean stdout: only protocol on stdout, logs on stderr.
5. `docs/adr/0006-mcp-sdk-aot.md`: criteria fixed before measuring, research, results, decision, fallback, hand-offs.
6. Update ADR 0001 row 13 (`claimed` becomes the measured result) and the roadmap (AOT spike line).

**Requirements covered:** none implemented. The slice produces a decision (ADR 0006) and evidence for M0's exit criterion.

**Out of scope**

- **`linux-x64`.** Owner decision: only `osx-arm64`. No CI workflow `spike-mcp.yml`. ADR 0006 and the roadmap state that MCP on `linux-x64` is **not verified**. The SDK has no native assets, so the risk is low, but the exit criterion stays open for MCP until the combined AOT binary runs there.
- **`mcp.readOnly`** (dynamic hiding of write tools). Owner decision: verified in M3. The spike only records in ADR 0006 what the research (R4) finds about filtering the tool list.
- Streamable HTTP transport and `ModelContextProtocol.AspNetCore` (Should, M5; part of the ASP.NET Core spike).
- The real tool implementations (`search_books`, `semantic_search`, ...), the real data model, authentication of any kind.
- Performance gates (cold start, throughput); the harness may print a wall-clock figure as an observation.
- Any change to `src/`, `tests/`, `shelf.slnx`, root `Directory.*.props`, `global.json`, `build.yml`.

## 2. Feature Files

**None in this slice**, same reasoning as `m0-spike-parsers` section 2: no product behavior and no code under `src/` is added. The check rows (3.4) are the concrete examples. **Hand-off to M3:** the MCP slice turns the M-rows into `Examples` of a feature tagged `@M3` with the PRD 7.2 / F-ID tags it implements. Rule 6 still applies: `dotnet test -c Release` on the solution stays green (nothing in the solution changes).

## 3. Design

### 3.1 Files

```
experiments/mcp/                      NEW  not in shelf.slnx
  Directory.Build.props               NEW  as experiments/parsers (PublishAot=true, IsAotCompatible=true,
                                           TreatWarningsAsErrors=false)
  Directory.Packages.props            NEW  ModelContextProtocol 2.2.0 (package choice per R1)
  .gitignore                          NEW  bin/ obj/ out/ results/* !results/local/
  README.md                           NEW  how to run check
  Server/                             NEW  AOT console app `proto-mcp`
    Program.cs                        NEW  stdio server, logging to stderr
    Tools.cs                          NEW  dummy tools (3.3)
    Resources.cs                      NEW  book resources
    McpJsonContext.cs                 NEW  [JsonSerializable] types for tool I/O
  Client/                             NEW  JIT console app `proto-mcp-client`: spawns the server binary,
                                           runs the M-rows, prints pass/fail
  check.sh                            NEW  publish AOT, run client against it, write results/local/osx-arm64.txt
docs/adr/0006-mcp-sdk-aot.md                          NEW
docs/adr/0001-dependency-licenses-and-maintenance.md  EDIT  row 13 verdict and AOT value
docs/roadmap.md                                       EDIT  AOT spike line
```

### 3.2 Research (facts to verify; nothing below is verified)

Method as in ADR 0001: source at the release tag, `.nupkg` listings, docs. Findings go into ADR 0006 with their own source index.

- R1: Which package is the minimum for a stdio server (`ModelContextProtocol.Core` or `ModelContextProtocol` with hosting)? Does the hosting variant pull in `Microsoft.Extensions.Hosting` and thereby heavier AOT surface than the project wants (the CLI uses System.CommandLine and a custom stderr `ILoggerProvider`)?
- R2: AOT-safe registration of tools and resources. Attribute-based discovery (`WithToolsFromAssembly`, `[McpServerTool]`) normally relies on reflection; is there a source-generated or explicit path (`WithTools<T>`, `McpServerTool.Create` with `JsonSerializerOptions`)? How are JSON schemas for parameters produced under AOT (`JsonSerializerContext`, `AIFunctionFactory`)?
- R3: Resource templates (`shelf://book/{id}`): how are URI template arguments bound, and does that path use reflection?
- R4: Can the server filter or hide tools at runtime (needed for `mcp.readOnly` in M3)? Findings are recorded only.
- R5: stdio transport: where does the SDK write logs by default, and can it be forced to stderr only?
- R6: The `2.x` API stability: breaking changes between 2.0.0 and 2.2.0 (ADR 0001 risk).

### 3.3 Dummy tools and resources

| Name | Shape (modelled on PRD 7.2) |
|---|---|
| `get_book` | parameter `id` (string); result object with nested `formats` array and `tags` array |
| `read_passage` | parameters `id`, `location`, optional `maxChars` (int, default 4,000, hard maximum 20,000); result string |
| `list_tags` | no parameters; result recursive tree with counts |
| `add_tags` | parameters `id` and `tags` (string array); result object (a write tool, only to prove a mutating signature works) |
| resource `shelf://book/{id}` | JSON metadata for a fixed in-memory book |
| resource `shelf://book/{id}/toc` | JSON table of contents |

All data is fixed in memory; no files, no database.

### 3.4 Checks (AOT server, JIT client, `osx-arm64`)

| Row | Check | Gate |
|---|---|---|
| M1 | `dotnet publish -c Release -r osx-arm64 -p:TrimmerSingleWarn=false` of the server: 0 `IL*`/`AOT*` warnings | yes |
| M2 | `initialize` succeeds; server reports name, version and the tools and resources capabilities | yes |
| M3 | `tools/list` returns the four tools; the JSON schemas contain the declared parameter names, types, required flags and the `maxChars` default | yes |
| M4 | `tools/call get_book` returns the expected nested object (field-by-field compare) | yes |
| M5 | `tools/call read_passage` with and without `maxChars`; `maxChars` above 20,000 returns a tool error, not a crash | yes |
| M6 | `tools/call list_tags` returns the expected recursive tree; `add_tags` with an array parameter returns the expected object | yes |
| M7 | `tools/call` with an unknown tool and with an invalid argument type return protocol errors, the server keeps running and answers the next request | yes |
| M8 | `resources/templates/list` lists both templates; `resources/read` of `shelf://book/{id}` and `.../toc` return the expected JSON; an unknown id returns a protocol error | yes |
| M9 | stdout contains only valid JSON-RPC messages (client parses every line); a log line written by a tool call appears on stderr | yes |
| M10 | the same checks against a JIT build of the server give identical response JSON (modulo the server's own timestamps/ids) | yes |
| X-1 | binary size, publish directory size, SDK version string | observation |
| X-2 | wall time of one `initialize` + `tools/call` round trip | observation (not gated) |

### 3.5 `check.sh`

Publish the AOT server and the JIT client, run the client against the AOT binary (M1-M9), run it again against a JIT server build (M10), write `results/local/osx-arm64.txt`. No timing loops.

### 3.6 AOT risks

- Reflection-based tool discovery and schema generation (R2) is the most likely failure point.
- JSON serialization of result types without a source-generated context.
- Hosting packages pulling in AOT-unfriendly parts (R1).
- Anything on stdout other than protocol breaks the client (M9).

### 3.7 Decision criteria (write into ADR 0006 with status `Proposed` and commit before measuring)

1. M1-M10 pass: `ModelContextProtocol` 2.2.0 is adopted for M3 under Native AOT, with the package choice from R1.
2. M1 reports warnings or tools are missing at runtime because discovery depends on reflection: try the explicit, source-generated registration path (R2). If that passes, adopt with the documented registration pattern. If it needs significant glue code, estimate it and **escalate to the owner**.
3. The SDK fails under AOT with no workaround: evaluate in order (a) ReadyToRun for the MCP part, (b) a thin hand-written JSON-RPC stdio layer (estimate the effort, stdio and the tool/resource subset of PRD 7.2 only). **Escalate with the options; no decision alone.**
4. `linux-x64` stays "not verified" in every outcome and is listed as a hand-off.

## 4. Unit Tests

None added to the solution (no code under `src/`). The experiment carries its own M-rows.

## 5. Step Order

1. Branch `feat/m0-spike-mcp`; commit this plan.
2. Research R1-R6; write ADR 0006 as `Proposed` with the criteria (3.7) and commit **before measuring**.
3. `Server/` and `Client/`, `check.sh`; run on `osx-arm64`, JIT server first, then AOT.
4. Fill ADR 0006 with the results; set the status per the decision rule.
5. Update ADR 0001 row 13 and the roadmap AOT spike line (osx-arm64 result; `linux-x64` not verified; `mcp.readOnly` deferred to M3).
6. Full `dotnet test -c Release` on the solution (must stay green); PR.

## 6. Open Questions

- Does the spike server use `Microsoft.Extensions.Hosting` or only `ModelContextProtocol.Core` (R1)? Default: the smaller option that passes M1-M10, because the CLI has its own startup and logging.
- The ADR number 0006 is shared with other spike plans; if another ADR takes 0006 first, renumber on merge.
