# Slice m0-spike-web: AOT Spike Part 4, ASP.NET Core Minimal APIs and embedded Svelte UI

| | |
|---|---|
| **Slice ID** | `m0-spike-web` |
| **Branch** | `feat/m0-spike-web` |
| **Milestone** | M0 – Setup & Spikes |
| **Roadmap** | "AOT spike" work package, web part only: ASP.NET Core Minimal APIs (JSON API, range requests, embedded Svelte build, access token) (roadmap line 56); exit criterion "The AOT spike runs on both supported platforms, or every incompatible library has a documented fallback" (line 65), web part, **on `osx-arm64` only** (see Scope) |
| **PRD references** | F-WEB-1 (`shelf serve`, default bind `127.0.0.1`), F-WEB-2 (access token mandatory on other interfaces), F-WEB-3 (UI embedded as static assets, no Node.js at runtime), 7.3 (REST/JSON under `/api/v1`, files via `/api/v1/files/{id}` with range requests, OpenAPI generated automatically), 9.2 (Native AOT, ReadyToRun fallback), 10.2 (Web backend, Web frontend rows), Q1 (Svelte, decided 2026-10-17), Q2, risk table 14 ("Native AOT incompatible with libraries") |
| **Inputs** | [ADR 0006](../adr/0006-mcp-sdk-aot.md) and `docs/slices/m0-spike-mcp.md` (spike/ADR structure, `experiments/mcp/` scaffolding, raw-client pattern); ADR 0001 row 22 (Svelte) and its 2026-10-17 note (Vite, TypeScript, Node.js not checked); `src/` logging slice `m0-logging` (stderr-only provider) |

---

## 1. Scope

**In scope**

1. Reproducible AOT experiment in `experiments/web/` (not in `shelf.slnx`, not built by existing CI jobs): an HTTP server as Native AOT binary on **`osx-arm64`**, run locally, with a small Svelte single-page UI, a JSON API under `/api/v1`, file delivery with range requests, an access token and an OpenAPI document.
2. The Svelte build: Svelte 5 + Vite + TypeScript, a minimal page that calls the API, built with `npm ci && npm run build`; the build output is **embedded into the binary** and served without any file on disk (F-WEB-3). The binary runs without Node.js.
3. HTTP check client (`Client/`, JIT console app on `HttpClient`) that runs the rows in 3.4 against the AOT server and against a JIT build of the same server and compares them.
4. `docs/adr/0007-web-backend-aot.md`: criteria fixed before measuring, research, results, decision, hand-offs.
5. Update the roadmap AOT spike line.

**Requirements covered:** none implemented. The slice produces a decision (ADR 0007) and evidence for M0's exit criterion.

**Out of scope**

- **`linux-x64`.** Owner decision: only `osx-arm64`, no CI workflow `spike-web.yml`. ADR 0007 and the roadmap state that the web part is **not verified** on `linux-x64`. The exit criterion "AOT spike runs on both platforms" stays open for it. Node.js for the UI build is a build-time tool and is not part of that question.
- **Executing the UI in a browser.** The spike verifies that the Svelte build output is built, embedded and served correctly (bytes, content types, fallback). It does not run the JavaScript in a browser and does not test Svelte behavior.
- Real views (Library, Search, Work details, Reader), PDF.js and foliate-js, htmx-free interactions, routing inside the SPA beyond one fallback check, dark mode, keyboard control (all M4).
- The **browser flow for the access token** (how a user enters or stores the token in the UI, cookie or header). The spike checks the server side only (3.4, W7) and records the open design question in the ADR.
- License and maintenance check of Vite, TypeScript, Node.js and the UI's npm dependency tree (ADR 0001 method). It is open (see PR #18, ADR 0001 row 22 note) and belongs to the first web slice; the spike records the versions it used.
- Performance gates (library view < 500 ms, idle RAM < 200 MB); the harness may print figures as observations.
- Real data model, real files, authentication beyond the single token, TLS, CORS, rate limiting.
- Any change to `src/`, `tests/`, `shelf.slnx`, root `Directory.*.props`, `global.json`, `build.yml`.

## 2. Feature Files

**None in this slice**, same reasoning as `m0-spike-mcp` section 2: no product behavior and no code under `src/` is added; the check rows (3.4) are the concrete examples. **Hand-off to M4:** the web slices turn the W-rows into `Examples` of features tagged `@F-WEB-1 @F-WEB-2 @F-WEB-3 @M4` and for the API and range requests `@M4` with the 7.3 references. Rule 6 still applies: `dotnet test -c Release` on the solution stays green (nothing in the solution changes).

## 3. Design

### 3.1 Files

```
experiments/web/                      NEW  not in shelf.slnx
  Directory.Build.props               NEW  as experiments/mcp
  Directory.Packages.props            NEW  pins (see R1, R4: Microsoft.AspNetCore.OpenApi only if R4 requires it)
  .gitignore                          NEW  bin/ obj/ out/ Ui/node_modules/ Ui/dist/ results/* !results/local/
                                           results/local/*.log
  README.md                           NEW  how to build the UI, run check.sh
  .node-version                       NEW  Node.js version used (recorded, not a policy; see 6)
  Ui/                                 NEW  Svelte 5 + Vite + TypeScript
    package.json, package-lock.json   NEW  exact versions, `npm ci`
    index.html, vite.config.ts, tsconfig.json
    src/main.ts, src/App.svelte       NEW  fetches /api/v1/books/B1 and renders the title
  Server/                             NEW  AOT console app `proto-web`
    Server.csproj                     NEW  EmbeddedResource for Ui/dist/**, PublishAot=true
    Program.cs                        NEW  options (--bind, --port, --token, --file), builder, middleware, endpoints
    Api.cs                            NEW  /api/v1/books/{id}, tags endpoint, JSON 404
    Files.cs                          NEW  /api/v1/files/{id} with range processing
    Auth.cs                           NEW  bearer token middleware, bind validation
    EmbeddedUi.cs                     NEW  static files from embedded resources, SPA fallback
    Json.cs                           NEW  source-generated JsonSerializerContext
  Client/                             NEW  JIT console app `proto-web-client` (HttpClient): rows W2-W11
  check.sh                            NEW  build UI, publish AOT + JIT, run client against both, diff
docs/adr/0007-web-backend-aot.md                      NEW
docs/roadmap.md                                       EDIT  AOT spike line
```

### 3.2 Research (facts to verify; nothing below is verified)

Method as in ADR 0006: source at the release tag, package files, docs. Findings go into ADR 0007 with its own source index.

- R1: Which builder is the right one for Native AOT: `WebApplication.CreateSlimBuilder`, `CreateEmptyBuilder` or the full `CreateBuilder` (what each registers, which are AOT-safe, size consequences)? Source-generated request delegates and `ConfigureHttpJsonOptions` with a `JsonSerializerContext`.
- R2: Range processing: `Results.File` / `Results.Stream` / `Results.PhysicalFile` with `enableRangeProcessing`; behavior for suffix ranges, ranges beyond the end (416), multiple ranges, `HEAD`; AOT status.
- R3: Embedded static files: `ManifestEmbeddedFileProvider` and the static files middleware under Native AOT, versus serving `GetManifestResourceStream` from own endpoints; content types, ETag, caching headers; how the SPA fallback is done (`MapFallback`).
- R4: OpenAPI in .NET 10: `Microsoft.AspNetCore.OpenApi` (runtime document generation) versus build-time generation (`Microsoft.Extensions.ApiDescription.Server`); which of them is documented as Native AOT compatible; what the document needs (API explorer, reflection, schema generation for source-generated types).
- R5: Kestrel logging through `ILogger`: how to route it to stderr only (the shelf stderr provider), and at which level Kestrel's default events are too noisy (compare ADR 0006 observation on the MCP SDK).
- R6: Binding: `--bind` handling (`UseUrls`, `ListenLocalhost`, `Listen`), how to detect that the configured address is not loopback, and what happens with `localhost`, `::1`, `0.0.0.0`.
- R7: Svelte 5 + Vite + TypeScript toolchain: current versions and their Node.js engine requirements, `npm ci` determinism with the lock file, hashed asset names and `base` setting, and the size of a minimal build.

### 3.3 Dummy API, files and UI

| Endpoint | Behavior |
|---|---|
| `GET /api/v1/books/{id}` | JSON object (camelCase): `id`, `title`, `authors` (array), `formats` (array of objects with a nullable `pages`), `tags`; unknown id: 404 with a JSON body (`type`, `title`, `status`) |
| `PUT /api/v1/books/{id}/tags` | request body `{"tags":["a","b"]}` (JSON body binding), returns the updated tags; invalid body: 400 JSON |
| `GET /api/v1/files/{id}` | serves the file given by `--file` (a generated 1 MiB file of known bytes) with `Accept-Ranges: bytes` and range support; unknown id: 404 JSON |
| `GET /openapi/v1.json` (R4 decides the exact path) | OpenAPI document |
| `GET /` and assets | the embedded Svelte build; any other non-API path without a file extension returns `index.html` (SPA fallback) |

All data is fixed in memory; the file is generated by `check.sh` with known content (byte `i mod 251`). The token applies to `/api/v1/*` (see W8 and the open question in 6).

### 3.4 Checks (AOT server, JIT client, `osx-arm64`)

Each row runs against the AOT binary and, for W11, against the JIT build.

| Row | Check | Gate |
|---|---|---|
| W1 | `dotnet publish -c Release -r osx-arm64 -p:TrimmerSingleWarn=false` of the server: 0 `IL*`/`AOT*` warnings (the UI build output must already exist) | yes |
| W2 | `npm ci && npm run build` succeeds and produces `Ui/dist/index.html` plus hashed JS and CSS; the versions of Node.js, npm, Svelte, Vite and TypeScript are printed | yes |
| W3 | with the working directory moved elsewhere and `Ui/dist` renamed away, `GET /` returns 200 `text/html` with the bytes of `index.html`; the JS and CSS assets return 200 with `text/javascript` / `text/css` and the exact bytes of the build output | yes |
| W4 | SPA fallback: `GET /library/abc` returns `index.html` (200); `GET /missing.js` returns 404; `GET /api/v1/unknown` returns a **JSON** 404, not HTML | yes |
| W5 | `GET /api/v1/books/B1` equals the expected JSON (field names, nested array, `pages` omitted or null per the serializer options; the observed behavior is recorded); unknown id: JSON 404 with the expected `status`; `PUT .../tags` with a valid body updates and returns the tags, with an invalid body returns a JSON 400 | yes |
| W6 | range requests on `/api/v1/files/F1`: full body (200, `Accept-Ranges: bytes`, correct length); `bytes=0-99` (206, `Content-Range: bytes 0-99/1048576`, exact bytes); a middle range; a suffix range `bytes=-100`; `bytes=2000000-` returns 416 | yes |
| W7 | bind: without `--bind` the server listens on `127.0.0.1` only (the startup line on stderr names the address and a connection to the loopback address succeeds); `--bind 0.0.0.0` without `--token` exits with a non-zero code and a clear message and never listens; `--bind 0.0.0.0 --token T` starts | yes |
| W8 | token: with `--token T`, `/api/v1/*` without header returns 401 (JSON), with a wrong token 401, with `Authorization: Bearer T` 200; the static UI (`/`, assets) stays reachable without a token (open question 6); without `--token` on loopback no header is needed | yes |
| W9 | OpenAPI: the document is served (or produced at build time per R4) and is valid JSON with an `openapi` version, the paths `/api/v1/books/{id}`, `/api/v1/books/{id}/tags` and `/api/v1/files/{id}`, their methods and a `404` response for the book path, and a schema for the book type | yes |
| W10 | stdout and stderr: nothing but intended output on stdout (the server prints no protocol; the check records what it prints); Kestrel and application logs reach stderr only | yes |
| W11 | the JIT build of the server returns identical status codes, content types, bodies (and `Content-Range`, `Accept-Ranges`) for W3-W9; `Date`, `Server` and other volatile headers are not compared | yes |
| X-1 | binary size, publish directory size, `Ui/dist` size, Svelte build time | observation |
| X-2 | time from process start to the first successful response | observation (not gated) |

### 3.5 `check.sh`

Build the UI (`npm ci && npm run build`, W2), publish the AOT server and the JIT server and client (W1), generate the 1 MiB test file, run the client against the AOT server for every row, run it against the JIT server (W11) and diff the normalized result dumps, write `results/local/osx-arm64.txt`. No timing loops.

### 3.6 AOT and tooling risks

- Kestrel and the endpoint pipeline pull in a lot of code; a trim or AOT warning in a framework path may only be fixable by another builder or ReadyToRun (R1).
- Embedded file serving may rely on reflection or the file provider abstractions (R3); the fallback is own endpoints over `GetManifestResourceStream`.
- OpenAPI runtime generation may not be supported under Native AOT (R4); the fallback is build-time generation embedded as a static document.
- The Node.js toolchain adds a second build system; `npm ci` can fail without network access (R7).
- The experiment's UI is never executed in a browser, so a broken bundle that is served correctly would not be noticed.

### 3.7 Decision criteria (write into ADR 0007 with status `Proposed` and commit before measuring)

1. W1-W11 pass: ASP.NET Core Minimal APIs on Kestrel are adopted for M4 under Native AOT, with the builder from R1, and with Svelte embedded as in W3; the OpenAPI approach that passed W9 (runtime or build time) is recorded.
2. Only W9 fails with runtime generation (R4): use build-time generation embedded as a static document; if that passes, adopt with it. If neither works, **escalate to the owner** with the options (hand-written OpenAPI document, drop the requirement for 1.0).
3. W1 warnings or a runtime failure of the framework path (W3-W8): record the exact warning or exception. Evaluate in order (a) a different builder (R1), (b) own endpoints instead of the failing middleware (R3), (c) ReadyToRun for the web part. **Escalate with the options; no decision alone.**
4. W2 fails (toolchain): record the cause; this does not decide against Svelte (PRD Q1) but is escalated to the owner.
5. `linux-x64` stays "not verified" in every outcome and is listed as a hand-off.

## 4. Unit Tests

None added to the solution (no code under `src/`). The experiment carries its own W-rows.

## 5. Step Order

1. Branch `feat/m0-spike-web`; commit this plan.
2. Research R1-R7; write ADR 0007 as `Proposed` with the criteria (3.7) and commit **before measuring**.
3. `Ui/` (Svelte build, W2), `Server/`, `Client/`, `check.sh`; run on `osx-arm64`, JIT server first, then AOT.
4. Fill ADR 0007 with the results; set the status per the decision rule.
5. Update the roadmap AOT spike line (osx-arm64 result; `linux-x64` not verified).
6. Full `dotnet test -c Release` on the solution (must stay green); PR.

## 6. Open Questions

- **Browser flow for the token.** A browser navigation cannot send an `Authorization` header. The spike covers the server side (W8: bearer header for `/api/v1/*`, static UI public) and records the design question for M4 (cookie set from a `?token=` link, or the UI asking for the token and sending a header). Default for the spike: static UI public, API protected. Is that acceptable?
- **Node.js version.** The local Node.js is v26.10.0 (`node --version`). The spike records it in `.node-version`; whether to pin an LTS line is decided with the license/maintenance check of the toolchain (out of scope here).
- If another ADR takes the number 0007 first, renumber on merge.
