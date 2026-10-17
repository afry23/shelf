# ADR 0007: ASP.NET Core Minimal APIs and the embedded Svelte UI under Native AOT

| | |
|---|---|
| **Status** | Accepted for `osx-arm64`: ASP.NET Core Minimal APIs on Kestrel (`CreateSlimBuilder`), source-generated JSON, runtime OpenAPI generation and the embedded Svelte build pass W1-W11 under Native AOT and are adopted for M4; `linux-x64` not verified, see Decision |
| **Date** | 2026-10-17 |
| **Scope** | PRD F-WEB-1 (`shelf serve`, bind `127.0.0.1`), F-WEB-2 (token on other interfaces), F-WEB-3 (UI embedded, no Node.js at runtime), 7.3 (REST/JSON under `/api/v1`, files with range requests, OpenAPI generated automatically), 9.2 (Native AOT, ReadyToRun fallback), 10.2 (Web backend, Web frontend), Q1 (Svelte), risk table 14 |
| **Roadmap** | M0 "AOT spike", web part only: ASP.NET Core Minimal APIs (JSON API, range requests, embedded Svelte build, access token); exit criterion "every incompatible library has a documented fallback", on `osx-arm64` only |
| **Inputs** | [ADR 0001](0001-dependency-licenses-and-maintenance.md) row 22 and its 2026-10-17 note, [ADR 0006](0006-mcp-sdk-aot.md) (spike/ADR structure), slice plan `docs/slices/m0-spike-web.md` |

Order of work: Context to Research were written before any measurement and committed with status `Proposed` before `experiments/web/` existed (commit `873af0b`), so the criteria are provable from the repository history. Measurements, decision and consequences were added after the run.

## Context

`shelf serve` starts a web server inside the same Native AOT binary as the CLI (PRD 9.2). The UI is a Svelte single-page application (PRD Q1, decided 2026-10-17), built at build time and embedded into the binary (F-WEB-3). The server provides a JSON API under `/api/v1`, file delivery with range requests for large PDFs, an OpenAPI document and an access token for non-loopback binding. This ADR answers whether this works under Native AOT on `osx-arm64`, and which builder, static-file and OpenAPI approach to use.

Owner decisions taken before the work (they fix scope):

- **Platform:** `osx-arm64` only, run locally; no CI workflow. `linux-x64` is not verified; the exit criterion "AOT spike runs on both supported platforms" stays open for the web part.
- **Svelte, not RazorSlices** (PRD Q1, PR #18). The spike builds a minimal Svelte 5 + Vite + TypeScript page, embeds it and serves it. It does **not** run the UI in a browser.
- **OpenAPI is checked in the spike.**
- **Token:** the server side is checked (bearer header for `/api/v1/*`, static UI public). The browser flow for entering or storing the token is an M4 design question.
- No performance gates. License and maintenance of Vite, TypeScript and Node.js are not checked here (open, ADR 0001 note).
- `osx-x64` and `linux-arm64` are not supported platforms (PRD 9.2).

## Options

- **A. `WebApplication.CreateSlimBuilder`**, Minimal APIs, source-generated JSON, Kestrel on HTTP.
- **B. `WebApplication.CreateEmptyBuilder`** (omits all default features; more manual wiring).
- **C. Full `WebApplication.CreateBuilder`** (not the AOT-oriented builder; expected to produce more warnings and a larger binary).
- **D. ReadyToRun** instead of Native AOT for the whole binary (PRD 9.2 fallback; an AOT binary cannot host JIT-compiled code, so the fallback applies to everything).
- OpenAPI: **runtime generation** (`Microsoft.AspNetCore.OpenApi`, `AddOpenApi` / `MapOpenApi`) or **build-time generation** (`Microsoft.Extensions.ApiDescription.Server`, document served as a static asset).
- Static UI: **static file middleware over an embedded file provider**, or **own endpoints over `GetManifestResourceStream`**.

## Criteria (fixed before measuring)

### Gates (`osx-arm64`, AOT server binary, JIT HTTP client)

| ID | Gate |
|---|---|
| W1 | `dotnet publish -c Release -r osx-arm64 -p:TrimmerSingleWarn=false` of the server: 0 `IL*`/`AOT*` warnings |
| W2 | `npm ci && npm run build` succeeds and produces `Ui/dist/index.html` plus hashed JS and CSS; versions of Node.js, npm, Svelte, Vite and TypeScript are printed |
| W3 | with the working directory moved and `Ui/dist` renamed away, `GET /` returns 200 `text/html` with the bytes of `index.html`; JS and CSS assets return 200 with `text/javascript` / `text/css` and the exact bytes of the build output |
| W4 | SPA fallback: `GET /library/abc` returns `index.html`; `GET /missing.js` returns 404; `GET /api/v1/unknown` returns a **JSON** 404, not HTML |
| W5 | `GET /api/v1/books/B1` equals the expected JSON; unknown id returns a JSON 404; `PUT /api/v1/books/B1/tags` with a valid body updates and returns the tags, with an invalid body returns a JSON 400 |
| W6 | range requests on `/api/v1/files/F1`: full body (200, `Accept-Ranges: bytes`, correct length); `bytes=0-99` (206, `Content-Range: bytes 0-99/1048576`, exact bytes); a middle range; a suffix range `bytes=-100`; `bytes=2000000-` returns 416 |
| W7 | bind: without `--bind` the server listens on `127.0.0.1` only (startup line on stderr names the address; a loopback connection succeeds); `--bind 0.0.0.0` without `--token` exits non-zero with a clear message and never listens; `--bind 0.0.0.0 --token T` starts |
| W8 | token: with `--token T`, `/api/v1/*` without header returns 401 (JSON), with a wrong token 401, with `Authorization: Bearer T` 200; the static UI stays reachable without a token; without `--token` on loopback no header is needed |
| W9 | OpenAPI: the document is served (or produced at build time per the decision rule) and is valid JSON with an `openapi` version, the paths `/api/v1/books/{id}`, `/api/v1/books/{id}/tags` and `/api/v1/files/{id}`, their methods, a `404` response for the book path and a schema for the book type |
| W10 | stdout/stderr: the check records what the server prints on stdout; Kestrel and application logs reach stderr only |
| W11 | the JIT build of the server returns identical status codes, content types, bodies (and `Content-Range`, `Accept-Ranges`) for W3-W9; `Date`, `Server` and other volatile headers are not compared |

**Observations (recorded, not gated):** X-1 binary size, publish directory size, `Ui/dist` size, Svelte build time; X-2 time from process start to the first successful response.

### Decision rule

1. W1-W11 pass: ASP.NET Core Minimal APIs on Kestrel are adopted for M4 under Native AOT, with the builder and static-file approach that passed, and with Svelte embedded as in W3; the OpenAPI approach that passed W9 (runtime or build time) is recorded.
2. Only W9 fails with runtime generation: use build-time generation embedded as a static document; if that passes, adopt with it. If neither works, **escalate to the owner** with the options (hand-written OpenAPI document, drop the requirement for 1.0).
3. W1 warnings or a runtime failure of the framework path (W3-W8): record the exact warning or exception. Evaluate in order (a) a different builder, (b) own endpoints instead of the failing middleware, (c) ReadyToRun for the web part. **Escalate with the options; no decision alone.**
4. W2 fails (toolchain): record the cause; this does not decide against Svelte (PRD Q1) but is escalated to the owner.
5. `linux-x64` stays "not verified" in every outcome and is listed as a hand-off.

### Checks

`experiments/web/check.sh` builds the UI, publishes the AOT and JIT servers and the client, runs the client against both and diffs the normalized results. The dummy API, file and UI are specified in the slice plan section 3.3.

## Method

Research of R1-R7 (slice plan 3.2) from the ASP.NET Core documentation (`dotnet/AspNetCore.Docs`, branch `main`, retrieved 2026-10-17) and the npm registry; the ASP.NET Core source and package contents were **not** read in this phase, so most findings are documentation-level and are marked as such. No code was executed in this phase. Measurement follows in `experiments/web/`. Installed runtime: Microsoft.AspNetCore.App 10.0.12, .NET SDK 10.0.401.

## Research

Statuses: **documented** = stated in the cited documentation page; **unverified** = not looked up or not stated.

| ID | Finding | Status | Source |
|---|---|---|---|
| R1 | The Native AOT page recommends `WebApplication.CreateSlimBuilder` and Minimal APIs only (MVC is not supported). The feature table lists Minimal APIs as **partial** support. `CreateSlimBuilder` keeps configuration (appsettings, user secrets), console logging and logging configuration and omits hosting startup assemblies, EventLog/Debug/EventSource logging providers, `UseStaticWebAssets`, IIS integration, HTTPS endpoints, HTTP/3 and regex/alpha routing constraints. `CreateEmptyBuilder` omits all features. Every type in a request body or a returned value must be registered on a `JsonSerializerContext` (inserted into `ConfigureHttpJsonOptions` `TypeInfoResolverChain`); without it `System.Text.Json` falls back to reflection, which Native AOT does not support. What "partial" excludes for shelf's endpoints was not looked up | documented | S1 |
| R2 | Result helpers for files handle conditional and range request headers when `enableRangeProcessing` is set to `true` ("File result support for range requests", with `If-Range` handling). Behavior for suffix ranges, ranges beyond the end (416), multiple ranges and `HEAD`, and the Native AOT status of the file result path, were not looked up | partly documented | S2 |
| R3 | `StaticFiles` is listed as **supported** under Native AOT. `MapStaticAssets` serves only assets in the build-time static assets manifest and explicitly does not serve embedded resources or files served from disk; for embedded resources the page says to use the static file middleware with a file provider. Whether `ManifestEmbeddedFileProvider` (or an `EmbeddedFileProvider`) works under Native AOT, how content types and ETags are set for it and how a SPA fallback is done (`MapFallback`) were not looked up. The `Spa` entry of the table (SPA services) is not supported, which concerns the SpaServices package, not serving a built SPA | partly documented | S1, S3 |
| R4 | Built-in OpenAPI support (`Microsoft.AspNetCore.OpenApi`) is documented as "Compatible with native AoT", generates documents at runtime and supports OpenAPI 3.1 in .NET 10 (3.2 from .NET 11). **`MapOpenApi` is restricted to the `Development` environment by default** to avoid exposing information in production, so a shipped AOT binary needs another environment setting or the build-time route. Build-time generation uses `Microsoft.Extensions.ApiDescription.Server` with `OpenApiGenerateDocumentsOnBuild`; the page lists "served statically from the web server" as a use case. YAML at build time is not supported. Whether runtime generation works under AOT for source-generated shelf types and the exact path of the endpoint were not looked up | documented (claim), measurement open | S4, S5 |
| R5 | `CreateSlimBuilder` includes console logging (R1). Which stream the default console logger writes to was not looked up in this phase; the `QuickstartWeatherServer` sample of the MCP C# SDK (`samples/QuickstartWeatherServer/Program.cs` at tag `v2.2.0`, read while researching ADR 0006 but not cited there) sets `LogToStandardErrorThreshold = LogLevel.Trace` to move console logging to stderr. shelf has its own stderr `ILoggerProvider` (slice `m0-logging`) and the MCP spike showed that framework libraries log at `Information` level, so the web host's default levels should be checked | unverified | S1 |
| R6 | Kestrel binds to `http://localhost:5000` when nothing is configured. `localhost` binds both the IPv4 and the IPv6 loopback interface and fails if the port is taken on either. Anything that is not a valid IP address or `localhost` is treated as a **wildcard** (all IPv4 and IPv6 addresses). Endpoint APIs: `Listen`, `ListenLocalhost`, `ListenAnyIP`. So `--bind` must be parsed as an IP address by shelf itself and rejected otherwise, not passed through as a URL | documented | S6 |
| R7 | npm registry, 2026-10-17: `svelte` 5.57.2 (MIT, Node `>=18`), `vite` 8.3.4 (MIT, Node `^20.19.0 \|\| >=22.12.0`), `@sveltejs/vite-plugin-svelte` 7.3.1 (MIT, Node `^20.19 \|\| ^22.12 \|\| >=24`, peers `vite ^8.0.0`, `svelte ^5.46.4`), `typescript` 7.0.2 (Apache-2.0, Node `>=16.20.0`), `svelte-check` 4.7.7 (MIT). Locally installed: Node.js v26.10.0, npm 11.19.1, which satisfies the engine ranges. `npm ci` determinism, hashed asset naming, `base` setting and the size of a minimal build were not looked up. The license of the transitive npm dependencies was not checked | partly verified | S7 |

### Source index

| ID | Source |
|---|---|
| S1 | `aspnetcore/fundamentals/native-aot.md` in `dotnet/AspNetCore.Docs` (`main`, retrieved 2026-10-17): feature table, `CreateSlimBuilder` sections, source generators and `JsonSerializerContext` |
| S2 | `aspnetcore/fundamentals/minimal-apis/responses.md` (same repository and date): "File result support for range requests" |
| S3 | `aspnetcore/fundamentals/static-files.md` (same repository and date): `MapStaticAssets`, "Static assets manifest", embedded resources |
| S4 | `aspnetcore/fundamentals/openapi/aspnetcore-openapi.md` and `.../openapi/overview.md` (same repository and date) |
| S5 | `Microsoft.Extensions.ApiDescription.Server` as described in S4 (package page not opened) |
| S6 | `aspnetcore/fundamentals/servers/kestrel/endpoints.md` (same repository and date) |
| S7 | `npm view <package> version engines.node license` for svelte, vite, @sveltejs/vite-plugin-svelte, typescript, svelte-check; `node --version`, `npm --version` on the development machine (2026-10-17) |

## Research updates from the measurement

- **R1 (builder):** `WebApplication.CreateSlimBuilder` with Kestrel, Minimal APIs (`MapGroup`, `TypedResults`, `Results<...>` unions), a source-generated `JsonSerializerContext` in `ConfigureHttpJsonOptions` and `AddProblemDetails` publishes with 0 warnings and runs. Options B (`CreateEmptyBuilder`) and C (`CreateBuilder`) were not tried because A passed.
- **R2 (range requests):** `Results.File(path, contentType, enableRangeProcessing: true)` works under AOT: 200 with `Accept-Ranges: bytes`, 206 with the right `Content-Range` for first, middle, suffix and open-ended ranges, 416 for a start beyond the end. A request with **multiple ranges returns the full body with 200**, not a multipart response (observation; PDF.js uses single ranges). ETag behavior was not checked.
- **R3 (embedded UI):** `new ManifestEmbeddedFileProvider(assembly, "ui")` with `UseStaticFiles(new StaticFileOptions { FileProvider = ... })` and `MapFallbackToFile("index.html", staticFileOptions)` works under AOT. Project settings: `GenerateEmbeddedFilesManifest=true`, package `Microsoft.Extensions.FileProviders.Embedded` 10.0.12, `<EmbeddedResource Include="..\Ui\dist\**\*" LinkBase="ui" />`. Content types: `text/html`, `text/javascript`, `text/css`. A path with a file extension that does not exist is a 404; a path without one falls back to `index.html`. The publish writes a 53 B `proto-web.staticwebassets.endpoints.json` next to the binary; the binary copied **alone** into an empty directory passes all rows, so the file is not needed (`MapStaticAssets` is not used). `MapStaticAssets` was not tried.
- **R4 (OpenAPI):** runtime generation works under Native AOT: `AddOpenApi()` plus `MapOpenApi("/openapi/v1.json")` served an OpenAPI **3.1.1** document with the three paths, the methods and responses declared by `.Produces(...)` (the book path lists 200 and 404) and schemas `Book`, `ErrorBody`, `FormatInfo`, `TagsRequest`, `TagsResponse`. `MapOpenApi` was called unconditionally and the endpoint answered; the environment variable was not set, so the app ran in the default environment, which ASP.NET Core treats as Production (the environment name was not printed). The documentation's "restricted to Development" therefore describes the sample code, not a restriction of the API. The document's `servers` entry contains the base URL of the request. Build-time generation was not tried.
- **R5 (logging):** with `ClearProviders`, a stderr-only provider and `SetMinimumLevel(Warning)`, stdout stayed empty and stderr held exactly the startup line (W10). The default console logger was not examined.
- **R6 (binding):** `--bind` is parsed with `IPAddress.TryParse` and passed to `Kestrel.Listen(address, port)`; a non-IP value is rejected with exit code 2, a non-loopback address without `--token` is rejected with exit code 2 before anything listens. The default `127.0.0.1` was probed: a connection to the machine's LAN address on the same port was refused. IPv6 (`::1`, `::`) was not tested.
- **R7 (Svelte toolchain):** `npm ci` plus `vite build` took about 1 s in total (warm npm cache), 41 packages, output of 3 files and 30,607 B (`index.html`, one hashed JS and one hashed CSS asset under `assets/`). Versions used: Node.js v26.10.0, npm 11.19.1, Svelte 5.57.2, Vite 8.3.4, TypeScript 7.0.2, `@sveltejs/vite-plugin-svelte` 7.3.1. npm 11.19.1 prints "npm warn install-scripts" lines (some package has an install script that is not approved); which package and whether it matters was not investigated.

## Measurements

`experiments/web/check.sh`, `osx-arm64`, macOS 26.6.2 (Apple Silicon), .NET SDK 10.0.401, Microsoft.AspNetCore.App 10.0.12, `Microsoft.AspNetCore.OpenApi` 10.0.12. Raw output: `experiments/web/results/local/osx-arm64.txt`.

| Gate | Result | Value |
|---|---|---|
| W1 AOT publish, 0 warnings | **pass** | 0 `IL*`/`AOT*` warnings, 0 warnings of any kind in the publish log |
| W2 Svelte build | **pass** | `npm ci && npm run build`, 3 files, 30,607 B |
| W3 embedded assets | **pass** | with `Ui/dist` moved away and an empty working directory: `/` and both assets 200, exact bytes, content types as expected |
| W4 SPA fallback and 404s | **pass** | `/library/abc` returns `index.html`; `/missing.js` 404; `/api/v1/unknown` 404 as JSON |
| W5 JSON API | **pass** | book JSON as expected (`pages: null` kept, ASP.NET's serializer defaults); unknown id 404 JSON; valid tags update; invalid body and empty tag 400 JSON |
| W6 range requests | **pass** | 200, 206 (`bytes 0-99/1048576`, middle, suffix, open end), 416 for a start beyond the end |
| W7 bind | **pass** | default `127.0.0.1`; `0.0.0.0` without token exit 2; non-IP bind exit 2; `0.0.0.0` with token starts |
| W8 token | **pass** | 401 JSON without or with a wrong token (`WWW-Authenticate: Bearer`), 200 with the token; the file endpoint is protected too; the UI stays public |
| W9 OpenAPI | **pass** | OpenAPI 3.1.1, paths, methods, 404 response and `Book` schema present |
| W10 stdout/stderr | **pass** | stdout 0 bytes, stderr only the startup line |
| W11 AOT vs JIT | **pass** | normalized dumps identical (564 lines) |
| X-1 observation | | `proto-web` 15,901,896 B (about 15.2 MiB); `Ui/dist` 30,607 B; Svelte build about 1 s; the binary links only system libraries |
| X-2 observation | | about 90 ms from process start to the first successful response, including the polling loop; not gated |

### Observations and pitfalls found while running

1. **Two error formats.** Responses produced by the framework (an unparseable body, a missing static file) are `application/problem+json` with a volatile `traceId`; responses produced by the endpoints (`TypedResults.NotFound(ErrorBody)`, token 401) are `application/json` with the project's own `ErrorBody`. The product should pick one format.
2. **An invalid JSON body gave a 400 with an empty body** until `UseStatusCodePages()` was added together with `AddProblemDetails()`.
3. **Multiple ranges are not honored** (full 200 response, see R2).
4. **Volatile content in the dump.** `traceId` in problem documents and the OpenAPI `servers` URL differ per run; the W11 comparison removes exactly these two and nothing else.
5. **Binary size.** The web server alone is 15.2 MiB (the MCP server alone was 11.1 MiB); the size of a combined CLI + MCP + web binary is open (combined spike).
6. **The UI is not executed.** The Svelte bundle is served byte-exactly, but no browser ran it.

## Gate results

W1-W11 all pass on `osx-arm64`.

## Decision

**ASP.NET Core Minimal APIs on Kestrel are adopted for M4 under Native AOT (decision rule step 1), with this composition:**

- `WebApplication.CreateSlimBuilder`, Kestrel `Listen(IPAddress, port)` (no URL strings; `--bind` is validated as an IP address by shelf), Minimal APIs with `MapGroup("/api/v1")`, all request and response types on a source-generated `JsonSerializerContext` inserted first into the resolver chain;
- files via `Results.File(..., enableRangeProcessing: true)`;
- the Svelte build embedded as resources (`GenerateEmbeddedFilesManifest`, `LinkBase="ui"`) and served by `ManifestEmbeddedFileProvider` + `UseStaticFiles` + `MapFallbackToFile("index.html")`; an explicit `/api/v1/{**rest}` fallback returns JSON 404 before the SPA fallback;
- OpenAPI by **runtime generation** (`AddOpenApi`, `MapOpenApi`); build-time generation is not needed;
- a bearer-token middleware for `/api/v1/*` with a constant-time comparison; `--bind` to a non-loopback address is refused without `--token`.

No fallback (another builder, own endpoints, ReadyToRun) was needed. Options B, C and D were not evaluated.

## Consequences

- **M4 web slices** implement these patterns and add scenarios for: one error format for all `/api/v1` errors (observation 1), an empty or invalid body returning a JSON 400 (observation 2), range requests (full, first, middle, suffix, open end, beyond end), SPA fallback versus JSON 404, token handling and bind refusal.
- **Browser flow for the token** (cookie from a `?token=` link, or the UI asking for the token and sending a header) is an M4 design question. The spike keeps the static UI public and protects only `/api/v1/*`.
- **OpenAPI path:** the spike used `/openapi/v1.json`; whether it lives under `/api/v1` and whether it needs the token is decided in M4.
- **`linux-x64` is not verified for the web part**; to be covered in the combined AOT binary or in M4. The M0 exit criterion "AOT spike runs on both supported platforms" stays open for it.
- **CI and builds need Node.js** for the UI (`setup-node` with a pinned version, `npm ci`). The version policy (v26.10.0 is the local version, an LTS line may be preferred) and the license and maintenance check of Vite, TypeScript, Node.js and the npm dependency tree (including the npm install-script warning, R7) are open and belong to the first web slice (ADR 0001 note).
- **Re-check triggers:** .NET 11 (OpenAPI 3.2 becomes the default version), Vite or Svelte major versions, a requirement for HTTPS or HTTP/3 (not in `CreateSlimBuilder`), multi-range support, running the UI in a browser test.
- **Prototype:** `experiments/web/` is kept until M0 closes, then deleted with a note pointing to the last commit that contains it (like the other spikes).

## Deviations from the plan

- W4 passes with `/missing.js` returning a 404 with a `application/problem+json` body; the plan only required the status.
- W5's invalid-body row needed `UseStatusCodePages()` (observation 2); the first smoke run without it returned an empty 400 body. The gate was not weakened.
- The W11 comparison removes `traceId` from problem documents and `servers` from the OpenAPI document (observation 4); the plan only named `Date`, `Server` and other volatile headers.
- X-2 is measured with a polling loop in `check.sh` and includes the loop's own latency.

## M0 close: prototype removed (2026-10-17)

M0 is closed (see the roadmap). `experiments/web/` was deleted from the repository. The last commit that contains it is `f6089e6a490d8ada02cca8db79ab034f671bbad1` (`git show f6089e6:<path>` or `git checkout f6089e6`). The measurements and the decision in this ADR stand; the commands under "Reproduce" and the paths named above refer to that commit.
