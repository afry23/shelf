# ADR 0006: ModelContextProtocol SDK under Native AOT

| | |
|---|---|
| **Status** | Proposed: criteria fixed, nothing measured yet |
| **Date** | 2026-10-17 |
| **Scope** | PRD 7.2 (tools, resources `shelf://book/{id}` and `shelf://book/{id}/toc`, stdio transport), 9.2 (Native AOT, ReadyToRun fallback), 10.2 (MCP row), risk table 14 ("Native AOT incompatible with libraries"). `mcp.readOnly` and Streamable HTTP are out of scope (see Context) |
| **Roadmap** | M0 "AOT spike", MCP part only; exit criterion "every incompatible library has a documented fallback", on `osx-arm64` only |
| **Inputs** | [ADR 0001](0001-dependency-licenses-and-maintenance.md) row 13, [ADR 0005](0005-epub-pdf-parsers-aot.md) (spike/ADR structure), slice plan `docs/slices/m0-spike-mcp.md` |

Order of work: Context to Method and the research were written before any measurement. This file is committed with status `Proposed` before `experiments/mcp/` exists, so the criteria are provable from the repository history. Results, decision and consequences are added after the measurement.

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

## Measurements

Not yet taken.

## Decision

Pending the measurement. Package choice (option A or B) and the registration pattern are decided by the first passing variant, preferring the smaller package (A) per the slice plan.

## Consequences

To be written after the measurement. Hand-offs already known:

- `mcp.readOnly` (tool hiding, R4) is verified in the M3 MCP slice.
- `linux-x64` is not verified for MCP; to be covered in the combined AOT binary or in M3.
- Streamable HTTP / `ModelContextProtocol.AspNetCore` belong to the ASP.NET Core spike and M5.
