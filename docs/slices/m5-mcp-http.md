# Slice m5-mcp-http: MCP over Streamable HTTP

| | |
|---|---|
| **Slice ID** | `m5-mcp-http` |
| **Branch** | `feat/m5-mcp-http` (stacked on `feat/m5-web-keys`) |
| **Milestone** | M5 – Polish |
| **Roadmap** | M5 slice 7 |
| **PRD references** | 7.1 (`shelf mcp [--http]`), 7.2 (Streamable HTTP, `mcp.read_only`), 9.2 (Native AOT) |

## 1. Scope

`shelf mcp --http` serves the MCP server at `/mcp` over Streamable HTTP, with the tools and resources of the stdio server. Options `--port` (default 8421, 0 for any), `--bind` (default `web.bind`), `--token` (default `web.token`); they are errors without `--http`.

**Out of scope**: sessions and server-to-client messages (stateless mode), OAuth, TLS, a cookie or query token, running the MCP endpoint inside `shelf serve`.

## 2. Feature Files

`Features/Mcp/McpHttp.feature` (`@PRD-7.2 @M5`, 7 scenarios: introduction, same tools, a call, a resource, a write, `mcp.read_only`, error of a tool) and `Features/Mcp/McpHttpServer.feature` (rules: announced address, interrupt, `Host` check, bearer token in 6 forms, token from the config and its precedence, token never in a log, bind rules, port range, options without `--http`, port in use, missing library).

## 3. Design

- **AOT gate passed** with the SDK's own transport (see the addendum of [ADR 0006](../adr/0006-mcp-sdk-aot.md)); no hand-made route was needed.
- `LibraryMcpServer.Open` builds the `McpServerOptions` (tools, resources, instructions); `RunAsync` (stdio) and `McpHttpHost` (HTTP) share it. The embedder lives as long as the server.
- `McpHttpHost` is a second, small Kestrel host in `Shelf.Web` (slim builder, loopback `Host` check, bearer check, status pages as problem documents). `shelf serve` is unchanged; the two are separate processes with separate ports.
- Test steps: the web steps start `mcp --http` the same way they start `serve`; the SDK's HTTP client talks to it for the tool scenarios.

## 4. Result

- AOT publish: 0 warnings; `tests/e2e/native.sh` has a new section (tools over HTTP, 421, 401/200 with the token, clean stdout, orderly stop).
- The scenarios of `WebAuth.feature` that set the token in the config did nothing so far: the step `the global config contains the line` only fills the buffer of the settings steps. They use `the global config file contains the line` now (and the TOML needs single quotes), and pass.

## 5. Open Questions

- Stateless mode only. A client that insists on a session gets none; not tested with a real assistant.
- No latency or memory measurement of the HTTP transport (slice `m5-close`).
- `linux-x64` is not run yet.
