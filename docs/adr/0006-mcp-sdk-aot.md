# ADR 0006: ModelContextProtocol SDK under Native AOT

| | |
|---|---|
| **Status** | Accepted for `osx-arm64`: `ModelContextProtocol.Core` 2.2.0 passes M1-M10 under Native AOT and is adopted for M3 (option A, explicit registration with a source-generated serializer context); `linux-x64` not verified, `mcp.readOnly` deferred to M3, see Decision |
| **Date** | 2026-10-17 |
| **Scope** | PRD 7.2 (tools, resources `shelf://book/{id}` and `shelf://book/{id}/toc`, stdio transport), 9.2 (Native AOT, ReadyToRun fallback), 10.2 (MCP row), risk table 14 ("Native AOT incompatible with libraries"). `mcp.readOnly` and Streamable HTTP are out of scope (see Context) |
| **Roadmap** | M0 "AOT spike", MCP part only; exit criterion "every incompatible library has a documented fallback", on `osx-arm64` only |
| **Inputs** | [ADR 0001](0001-dependency-licenses-and-maintenance.md) row 13, [ADR 0005](0005-epub-pdf-parsers-aot.md) (spike/ADR structure), slice plan `docs/slices/m0-spike-mcp.md` |

Order of work: Context to Research were written before any measurement and committed with status `Proposed` before `experiments/mcp/` existed (commit `b266311`), so the criteria are provable from the repository history. Measurements, decision and consequences were added after the run.

## Context

shelf exposes the library to AI assistants as an MCP server (PRD 7.2), started by the same Native AOT binary as the CLI. ADR 0001 adopted the official C# SDK `ModelContextProtocol` 2.2.0 (Apache-2.0) and recorded `IsAotCompatible=true` as **claimed**, never measured. This ADR answers whether a stdio MCP server with tools and resources works under Native AOT on `osx-arm64`.

Owner decisions taken before the work (they fix scope):

- **Platform:** `osx-arm64` only, run locally; no CI workflow. `linux-x64` is not verified (the SDK has no native assets, so the risk is judged low); the exit criterion "AOT spike runs on both supported platforms" stays open for MCP until the combined AOT binary runs on `linux-x64`.
- **`mcp.readOnly`** (hiding write tools at runtime) is verified in M3. The spike only records what the research finds (R4).
- Streamable HTTP and `ModelContextProtocol.AspNetCore` are not part of this spike (Should, M5; belongs to the ASP.NET Core spike).
- No performance gates.
- `osx-x64` and `linux-arm64` are not supported platforms (PRD 9.2).

## Options

- **A. `ModelContextProtocol.Core` 2.2.0 only**, server built directly on `McpServerOptions` / a transport, no generic host.
- **B. `ModelContextProtocol` 2.2.0** (adds the `IMcpServerBuilder` extensions such as `WithTools<T>` and `WithStdioServerTransport`; depends on `Microsoft.Extensions.Hosting.Abstractions` and `Microsoft.Extensions.Caching.Abstractions`).
- **C. ReadyToRun** instead of Native AOT for the whole binary (PRD 9.2 fallback; an AOT binary cannot host JIT-compiled code, so the fallback applies to everything).
- **D. Thin hand-written JSON-RPC layer** over stdio for the PRD 7.2 subset (fallback for A/B; estimated, not built).

## Criteria (fixed before measuring)

### Gates (`osx-arm64`, AOT server binary, JIT client)

| ID | Gate |
|---|---|
| M1 | `dotnet publish -c Release -r osx-arm64 -p:TrimmerSingleWarn=false` of the server: 0 `IL*`/`AOT*` warnings |
| M2 | `initialize` succeeds; the server reports name, version and the tools and resources capabilities |
| M3 | `tools/list` returns the four dummy tools; the JSON schemas contain the declared parameter names, types, required flags and the `maxChars` default |
| M4 | `tools/call get_book` returns the expected nested object (field-by-field compare) |
| M5 | `tools/call read_passage` with and without `maxChars`; `maxChars` above 20,000 returns a tool error, not a crash |
| M6 | `tools/call list_tags` returns the expected recursive tree; `add_tags` with an array parameter returns the expected object |
| M7 | unknown tool and invalid argument type return protocol errors; the server keeps running and answers the next request |
| M8 | `resources/templates/list` lists both templates; `resources/read` of `shelf://book/{id}` and `.../toc` return the expected JSON; an unknown id returns a protocol error |
| M9 | stdout contains only valid JSON-RPC messages; a log line written by a tool call appears on stderr |
| M10 | the same checks against a JIT build of the server give identical response JSON (modulo server-generated ids/timestamps) |

**Observations (recorded, not gated):** X-1 binary size, publish directory size, SDK version string; X-2 wall time of one `initialize` + `tools/call` round trip.

### Decision rule

1. M1-M10 pass: `ModelContextProtocol` 2.2.0 is adopted for M3 under Native AOT, with the package choice (A or B) from R1 and the registration pattern that passed.
2. M1 reports warnings, or tools are missing at runtime because discovery depends on reflection: try the explicit, source-generated registration path (R2). If that passes, adopt with the documented pattern. If it needs significant glue code, estimate it and **escalate to the owner**; no decision alone.
3. The SDK fails under AOT with no workaround: evaluate in order (a) ReadyToRun for the MCP part, (b) option D (estimate the effort for stdio and the PRD 7.2 tool/resource subset). **Escalate with the options; no decision alone.**
4. `linux-x64` stays "not verified" in every outcome and is listed as a hand-off.

### Checks

`experiments/mcp/check.sh` publishes the AOT server and the JIT client, runs the client against the AOT binary (M1-M9), runs it again against a JIT server build (M10) and writes `results/local/osx-arm64.txt`. The dummy tools and resources are specified in the slice plan section 3.3.

## Method

Research of R1-R6 (slice plan 3.2) from source at the release tag `v2.2.0`, `.nuspec` files from the NuGet flat container and the GitHub release notes; no code of the SDK was executed in this phase. Measurement follows in `experiments/mcp/`.

## Research

Source index: S1 ... below. Statuses: **verified** = read in the cited source; **unverified** = not looked up.

| ID | Finding | Status | Source |
|---|---|---|---|
| R1 | `ModelContextProtocol.Core` 2.2.0, `net10.0` group, depends only on `Microsoft.Extensions.AI.Abstractions` 10.8.3 and `Microsoft.Extensions.Logging.Abstractions` 10.0.10. `ModelContextProtocol` 2.2.0, `net10.0` group, adds `Microsoft.Extensions.Hosting.Abstractions` 10.0.10 and `Microsoft.Extensions.Caching.Abstractions` 10.0.10 (abstractions only, not the full generic host). Both packages set `IsAotCompatible=true` for every target framework except `netstandard2.0`. Whether a server can be created from `ModelContextProtocol.Core` alone (without the builder extensions) was **not** checked beyond `McpServerOptions` exposing `ToolCollection`, `ResourceCollection`, `Handlers` and `Filters` | verified (dependencies, flag); creation path unverified | S1, S2, S3 |
| R2 | Generic `WithTools<TToolType>()`, `WithPrompts<T>()` and `WithResources<T>()` carry `[DynamicallyAccessedMembers(PublicMethods \| NonPublicMethods \| PublicConstructors)]` on the type parameter and take `JsonSerializerOptions? serializerOptions`. The non-generic `WithTools(IEnumerable<Type>)`, `WithToolsFromAssembly`, the equivalent prompt and resource methods are marked `[RequiresUnreferencedCode]` ("require dynamic lookup of method metadata and might not work in Native AOT. Use the generic WithTools method instead"). `McpServerTool.Create` has overloads for `Delegate`, `MethodInfo` (+ target or target factory) and `AIFunction`, all accepting `McpServerToolCreateOptions` with `SerializerOptions`. Whether the JSON schema generation under Native AOT works with a source-generated `JsonSerializerContext` passed in `SerializerOptions` is **not** verified; this is the main measurement | partly verified | S4, S5 |
| R3 | `McpServerResource.Create` has four overloads; a resource is templated when its `UriTemplate` contains `{`. How URI template arguments are bound to method parameters (reflection or generated) was not read. The generic `WithResources<T>()` is annotated like `WithTools<T>()`; the non-generic ones are `[RequiresUnreferencedCode]` | partly verified | S4, S6 |
| R4 | Recorded only (`mcp.readOnly` is M3): `McpServerOptions` has `ToolCollection` (a `McpServerPrimitiveCollection<McpServerTool>`), `Handlers.ListToolsHandler` / `CallToolHandler` and `Filters` (`WithRequestFilters`, `WithListToolsHandler`). The documentation states that tools from `ToolCollection` and from `ListToolsHandler` are merged. Whether a tool can be removed or hidden from a running server (e.g. by removing it from `ToolCollection`, or a list filter) was not checked | unverified | S7 |
| R5 | `StdioServerTransport` reads from `Console.OpenStandardInput()` and writes to `Console.OpenStandardOutput()` (buffered); it accepts an `ILoggerFactory`. So protocol output uses stdout and the logger decides where log lines go: the server must be given a logger factory that writes to stderr (shelf's own stderr `ILoggerProvider`, slice `m0-logging`). The default logging of a generic host console logger was not looked up | verified (transport); default logging unverified | S8 |
| R6 | Version 2.0.0 (2026-07-28) is a major release with 10 listed breaking changes: default stateless HTTP, Roots/Sampling/Logging APIs deprecated (`MCP9005` warnings), Tasks moved to `ModelContextProtocol.Extensions.Tasks`, OAuth changes, non-object structured tool results emitted directly, `Tool.inputSchema` required on deserialization. None concerns a stdio server with tools and resources. The notes of 2.1.0 and 2.2.0 were not read. The SDK's own Logging API is deprecated (MCP9005); shelf logs through its own provider, so the deprecation does not apply | partly verified | S9 |

### Source index

| ID | Source |
|---|---|
| S1 | nuspec, `ModelContextProtocol.Core` 2.2.0 (`https://api.nuget.org/v3-flatcontainer/modelcontextprotocol.core/2.2.0/modelcontextprotocol.core.nuspec`) |
| S2 | nuspec, `ModelContextProtocol` 2.2.0 (`.../modelcontextprotocol/2.2.0/modelcontextprotocol.nuspec`) |
| S3 | `src/ModelContextProtocol.Core/ModelContextProtocol.Core.csproj`, `src/ModelContextProtocol/ModelContextProtocol.csproj` at tag `v2.2.0` (`IsAotCompatible`), `src/ModelContextProtocol.Core/Server/McpServerOptions.cs` |
| S4 | `src/ModelContextProtocol/McpServerBuilderExtensions.cs` at tag `v2.2.0` |
| S5 | `src/ModelContextProtocol.Core/Server/McpServerTool.cs` at tag `v2.2.0` |
| S6 | `src/ModelContextProtocol.Core/Server/McpServerResource.cs` at tag `v2.2.0` |
| S7 | `src/ModelContextProtocol.Core/Server/McpServerOptions.cs` at tag `v2.2.0` (doc comments of `ToolCollection`, `Handlers`, `Filters`) |
| S8 | `src/ModelContextProtocol.Core/Server/StdioServerTransport.cs` at tag `v2.2.0` |
| S9 | GitHub release notes `v2.0.0` of `modelcontextprotocol/csharp-sdk` |

Repository: https://github.com/modelcontextprotocol/csharp-sdk, tag `v2.2.0` (2026-08-13).

## Research updates from the measurement

- **R1 (creation path):** a server is created from `ModelContextProtocol.Core` alone with `McpServer.Create(ITransport, McpServerOptions, ILoggerFactory?, IServiceProvider?)`, `StdioServerTransport` and `McpServerOptions.ToolCollection` / `ResourceCollection`. No generic host and no `ModelContextProtocol` (builder) package was needed. Option A works; option B was not built.
- **R2 / R3 (registration, schemas):** tools and resource templates were created with `McpServerTool.Create(Delegate, ...)` and `McpServerResource.Create(Delegate, ...)` and added to the collections explicitly. The attribute-based paths (`WithTools<T>()`, `WithToolsFromAssembly`) were **not** tried. `SerializerOptions` carries a source-generated `JsonSerializerContext` combined with `McpJsonUtilities.DefaultOptions`; input and output JSON schemas (nested objects, arrays, a recursive type, a nullable integer, an `int` default value) are produced under AOT without warnings. Without the context in the options, schema creation throws `NotSupportedException` ("JsonTypeInfo metadata for type ... was not provided by TypeInfoResolver"), so the context is required, not optional.
- **R5 (logging):** the transport takes the `ILoggerFactory` given to `McpServer.Create`; with a stderr-only provider nothing but JSON-RPC reaches stdout (M9). At `Information` level the SDK logs every request and transport event (about 16 KB of stderr for one check run), so the product should log the SDK at `Warning` or above by default.
- **R4 (hiding tools):** not tested. `McpServerOptions.ToolCollection` is a plain collection that was filled before the server started.

## Measurements

`experiments/mcp/check.sh`, `osx-arm64`, macOS (Apple Silicon), .NET SDK 10.0.401, `ModelContextProtocol.Core` 2.2.0, `Microsoft.Extensions.Logging` 10.0.12. Raw output: `experiments/mcp/results/local/osx-arm64.txt`.

| Gate | Result | Value |
|---|---|---|
| M1 AOT publish, 0 warnings | **pass** | 0 `IL*`/`AOT*` warnings, 0 warnings of any kind in the publish log |
| M2 `initialize` | **pass** | `serverInfo` proto-mcp 0.0.1; capabilities `tools`, `resources`, `logging`; negotiated protocol version `2025-06-18` |
| M3 `tools/list` schemas | **pass** | four tools; `read_passage.required=[id,location]`, `maxChars` integer with `default: 4000`; `add_tags.tags` array of string |
| M4 `get_book` | **pass** | nested object equals the expected `structuredContent` |
| M5 `read_passage` | **pass** | default 4,000 chars, `maxChars=100` gives 100, `maxChars=30000` returns `isError: true` with the exception text and the server stays up |
| M6 `list_tags`, `add_tags` | **pass** | recursive tree and array parameter round trip |
| M7 errors | **pass** | unknown tool: JSON-RPC error `-32602`; invalid argument type (`"abc"` for an int): `result.isError: true`; next request answered |
| M8 resources | **pass** | both templates listed, both resources read; unknown id gives JSON-RPC error `-32602` (via `McpProtocolException` with `InvalidParams`) |
| M9 stdout clean | **pass** | 15 stdout lines, 0 not JSON-RPC; the tool-side `Console.Error` line arrived on stderr |
| M10 AOT vs JIT | **pass** | normalized response dumps identical (439 lines) |
| X-1 observation | | `proto-mcp` 11,622,648 B (about 11.1 MiB); `otool -L` lists system libraries and frameworks only (first eight entries inspected) |
| X-2 observation | | not meaningful (whole client run under 1 s); not gated |

### Observations and pitfalls found while running

1. **List order is not stable.** The order of `resources/templates/list` changed between runs of the same build, both JIT and AOT (6 runs each: JIT 2 of 6 swapped, AOT 1 of 6). The order of `tools/list` was not checked separately; the M10 comparison sorts both lists by name to be safe; everything else is compared as sent. The product must not depend on the SDK's list order (sort explicitly if the order matters for clients).
2. **Null properties are omitted in `structuredContent`.** For `pages: null` the SDK's default options omit the property, while the generated output schema lists `pages` in `required` (`[kind,sizeBytes,pages]`). A strict MCP client that validates results against the output schema could reject this. Mitigation for M3: declare such properties as optional in the schema (e.g. not `required`), or avoid nullable members in tool results. Not investigated further.
3. **Static initialization order.** Building the shared `JsonSerializerOptions` in a static property initializer of the same `JsonSerializerContext` class silently dropped the generated context (`Default` was still null; `JsonTypeInfoResolver.Combine` ignores nulls). The options must be created lazily. The symptom was the `NotSupportedException` described under R2.
4. Resource errors: `McpException` is mapped to a tool error for tools; for resources the protocol error code needs `McpProtocolException` (`McpException` has no error-code constructor).

## Gate results

M1-M10 all pass on `osx-arm64`.

## Decision

**`ModelContextProtocol.Core` 2.2.0 is adopted for M3 under Native AOT (decision rule step 1), option A (Core only, no generic host).**

Registration pattern for the product:

- create tools and resources with `McpServerTool.Create` / `McpServerResource.Create` from delegates or static methods and add them to `McpServerOptions.ToolCollection` / `ResourceCollection`;
- pass `SerializerOptions` with a source-generated `JsonSerializerContext` (all tool parameter and result types annotated with `[JsonSerializable]`) combined with `McpJsonUtilities.DefaultOptions`, built lazily;
- run the server with `McpServer.Create(new StdioServerTransport(...), options, loggerFactory)` and a logger factory that writes to stderr;
- do not use the non-generic `WithTools*` / `WithToolsFromAssembly` paths (`[RequiresUnreferencedCode]`).

The attribute-based `WithTools<T>()` path was not measured and stays unverified; it is not needed.

No fallback (ReadyToRun, hand-written JSON-RPC layer) is required. The estimate for option D was not made because it was not triggered.

## Consequences

- **M3 MCP slice:** implements the PRD 7.2 tools with the registration pattern above. The slice must add scenarios for: unknown tool and invalid argument as errors, list order independence, optional (nullable) result properties in the output schema (pitfall 2).
- **`mcp.readOnly`** (hiding write tools, R4) is verified in the M3 MCP slice; this ADR only records that `ToolCollection`, `Handlers.ListToolsHandler` and `Filters` exist.
- **`linux-x64` is not verified for MCP.** The SDK has no native assets, so the risk is judged low; the check belongs to the combined AOT binary or to M3. The M0 exit criterion "AOT spike runs on both supported platforms" stays open for MCP until then.
- **Streamable HTTP** and `ModelContextProtocol.AspNetCore` belong to the ASP.NET Core spike and M5.
- **Packages:** `ModelContextProtocol.Core` brings `Microsoft.Extensions.AI.Abstractions` 10.8.3 and `Microsoft.Extensions.Logging.Abstractions`; notices for them belong in the notices file (ADR 0001, release pipeline).
- **Re-check triggers:** `ModelContextProtocol` 2.3 / 3.0, a changed MCP protocol version, .NET 11, a requirement for the attribute-based registration.
- **Prototype:** `experiments/mcp/` is kept until M0 closes, then deleted with a note pointing to the last commit that contains it (like the other spikes).

## Deviations from the plan

- The plan's M10 said "identical response JSON (modulo server-generated ids/timestamps)". The dumps are identical after sorting the `tools` and `resourceTemplates` lists by name, because the order of the template list is not stable between runs (observation 1) and the tool list was not checked. No other normalization was applied.
- The expected `get_book` result in M4 omits `pages` for the EPUB (observation 2), after a first run showed the SDK omits null properties. The expectation was adapted to the measured SDK behavior and the discrepancy with the output schema is documented instead.
- Decision rule step 2 (explicit registration path) was used from the start; the attribute-based path was never tried, so no fallback to it was needed.
