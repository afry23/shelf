# Slice m4-web-host: `shelf serve`, Authentication and the API Frame

| | |
|---|---|
| **Slice ID** | `m4-web-host` |
| **Branch** | `feat/m4-library-core` (stacked: same branch as slice 1, separate commits) |
| **Milestone** | M4 – Web |
| **Roadmap** | M4 slice 2 |
| **PRD references** | F-WEB-1, F-WEB-2, 7.3 (`/api/v1`, OpenAPI), 9.2 (Native AOT), 9.4 (no telemetry), 10.1 (`Shelf.Web`) |
| **Inputs** | [ADR 0007](../adr/0007-web-backend-aot.md), [ADR 0008](../adr/0008-combined-aot-binary.md) (project-setup findings 1 and 2), the spike code in `f6089e6:experiments/web/Server/` |

---

## 1. Scope

**In scope**

1. `src/Shelf.Web` (class library, `FrameworkReference` to `Microsoft.AspNetCore.App`) and `shelf serve [--library] [--port 8420] [--bind 127.0.0.1] [--token]`.
2. Kestrel on `CreateSlimBuilder`, `MapGroup("/api/v1")`, a source-generated JSON context, one logger factory that writes to standard error.
3. Bind rules (F-WEB-1, F-WEB-2): default `127.0.0.1`; `--bind` must be an IP address; a non-loopback address needs a token.
4. Authentication: a bearer token, or a cookie that a `?token=` link sets (owner decision 2026-10-18). Without a token on loopback, no authentication.
5. One error format for every response without a success body: `application/problem+json` with `status`, `title` and `detail`.
6. `GET /api/v1/status` (version and counts) as the first real endpoint, and the OpenAPI document at `/api/v1/openapi.json`.
7. Config keys `web.port`, `web.bind`, `web.token` (command line wins).
8. The executable setup of ADR 0008 in `Shelf.Cli`, and the native AOT gate.
9. `Features/Web/*.feature` and the HTTP steps; the spec runs `serve` in the test process.

**Out of scope**: library, search, details, tags and file endpoints (slices 3 and 4), covers (5), the embedded UI (6, until then `/` answers with the JSON 404), `web.read_only` (see 6), HTTPS, CORS, rate limiting.

## 2. Feature Files

`Features/Web/WebServer.feature` (`@F-WEB-1 @M4`): the startup line on standard error and nothing on standard output, a free port with `--port 0`, a port in use, a missing library, the stop on cancellation (exit 0), the `Host` check (DNS rebinding: `127.0.0.1` and `localhost` accepted, a foreign name refused with 421).

`Features/Web/WebAuth.feature` (`@F-WEB-2 @M4`): refusal of a non-loopback address without a token (3 addresses), a bind value that is no IP address, a start on `0.0.0.0` with a token, the header check (5 requests), the cookie flow (`?token=` sets an `HttpOnly`, `SameSite=Strict` cookie and redirects to the address without the token; a wrong token sets nothing), the token from the config and from the command line, an empty token counts as none, the token never reaches a log line.

`Features/Web/WebApi.feature` (`@PRD-7.3 @M4`): `GET /api/v1/status` (version and counts), an unknown path and a wrong method as problem documents (`404`, `405` with `Allow`), a body that is no JSON as `400`, the OpenAPI document (valid, lists the status path, needs the token when there is one).

## 3. Design

- **`Shelf.Web` is a library, the setup is in `Shelf.Cli`.** ADR 0008 finding 2: the runtime feature switch (`MvcEnhancedModelMetadataSupport=false` plus the `RuntimeHostConfigurationOption` item) belongs to the executable. `Shelf.Web` carries the `<Using>` items of finding 1. The split is untested: the AOT publish with 0 warnings is a gate of this slice (section 4).
- **Entry point**: `WebHost.RunAsync(WebOptions, ILoggerProvider, LogLevel, TextWriter stderr, CancellationToken)`; `WebOptions` holds root, settings, version, bind address, port and token. The `LibraryService` of slice 1 is created by the first endpoint that needs it (slice 3), so that every endpoint uses the rules MCP uses.
- **The address is announced on standard error** (`Listening on http://127.0.0.1:8420`), read from the server's real addresses after start, so `--port 0` works and the specs learn the port from that line. Standard output stays empty.
- **`--bind` must be an IP address in its written-out form** (ADR 0007: anything else is read as a wildcard by Kestrel's URL parsing). `IPAddress.TryParse` also reads `0`, `127.1` and `2130706433`; those are refused (found by `BindRulesTests`). A non-IP value is a usage error (exit 2), like a missing token on a non-loopback address.
- **Token compare in constant time** (SHA-256 of both sides, `CryptographicOperations.FixedTimeEquals`). An empty `--token` or config value counts as none.
- **Cookie flow**: a request with `?token=` and the right value gets `shelf_token` (`HttpOnly`, `SameSite=Strict`, `Path=/`, no `Secure` because the host speaks HTTP only) and a `303` to the same address without the `token` parameter, so the token does not stay in the address bar. A wrong value is `401` and sets nothing. The API accepts `Authorization: Bearer` or the cookie. `SameSite=Strict` keeps another site from using the cookie for a write.
- **Static files stay public, `/api/v1/*` is protected** (the spike default, now decided). The UI itself holds no data.
- **Host check on loopback**: when the bind address is loopback, a `Host` that is not `127.0.0.1`, `localhost` or `[::1]` gets `421`. That closes DNS rebinding for a server without a token. Not applied on other addresses, where the token protects.
- **Error format**: `UseStatusCodePages` and an own writer, so a status without a body (404, 405, an unreadable body) and the errors of the endpoints share one shape. The framework's `AddProblemDetails` is not used for the body, because its fields (`type`, `traceId`) are not ours to promise.
- **Logging**: the logger factory of `shelf mcp` (`StderrLoggerProvider`) is passed in; `Microsoft.AspNetCore` and `Microsoft.Hosting` categories are held at `Warning` unless `SHELF_LOG_LEVEL` asks for more (ADR 0006 observed that framework libraries are noisy at `Information`).
- **Stopping**: the process turns Ctrl-C and `SIGTERM` (service manager, container) into the cancellation token (`Program.cs`); the web host brings its own lifetime to a no-op so that it registers no signal handlers of its own. Found in the native run: without the `SIGTERM` handler the server died at once (exit 143).
- **Lazy `LibraryService`**: `serve` starts without opening the model or the index (idle RAM target, M4 exit criteria); the first endpoint that needs the service creates it (slice 3).
- **The request log is never on**: the `Microsoft.AspNetCore.Hosting.Diagnostics` category stays at `Warning`, because it prints the query string and a link with `?token=` must not put the token into a log.

## 4. Native AOT

**Result (`osx-arm64`):** 0 warnings. The plain SDK project needed two more settings than ADR 0008 names, both in `Shelf.Web.csproj`: `EnableRequestDelegateGenerator` and the `InterceptorsNamespaces` entry for `Microsoft.AspNetCore.Http.Generated`; without them `MapGet` binds to the reflection-based overload (IL2026, IL3050). The runtime switch of ADR 0008 finding 2 is in `Shelf.Cli.csproj`. The binary grew from 29 to 34 MiB. A native run answers the status, the problem documents (404, 405 with `Allow`, 421), the OpenAPI document, the token and cookie flow, a refused bind (exit 2) and `SIGTERM` (exit 0, nothing on standard output).

Gate: `dotnet publish -r osx-arm64 -p:PublishAot=true` of `shelf` with 0 warnings, then `shelf serve --port 0` from the native binary answers `GET /api/v1/status` and the OpenAPI document. If the library split trips the warnings of ADR 0008 finding 2, the fix goes into `Shelf.Cli.csproj` and is recorded in ADR 0017. `linux-x64` is checked by the `aot` job (slice 9).

## 5. Unit Tests

`BindRulesTests` (address parsing: loopback, wildcard, IPv6, a name), `TokenComparerTests` (equal, different length, empty, prefix), `HostCheckTests` (accepted and refused host names with ports), `ProblemWriterTests` (shape of the document).

## 6. Open Questions

- **`web.read_only`** (like `mcp.read_only`): should `serve` be able to run without write endpoints? Not needed for the Must items; decide when slice 3 adds the first write endpoint.
- **Port 8420** as the default is from the PRD; no check whether another well-known service uses it.
