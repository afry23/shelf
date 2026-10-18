# ADR 0017: Decisions and measurements of M4 (web)

| | |
|---|---|
| **Status** | Accepted (open points for the owner in "Open decisions") |
| **Date** | 2026-10-18 |
| **Scope** | PRD 7.3 (web API), 9.1 (library view < 500 ms, idle RAM of `serve` < 200 MB), 9.2 (Native AOT), F-WEB-1 to F-WEB-4, F-RDR-1 to F-RDR-3, F-IMP-5, roadmap M4 exit criteria |
| **Inputs** | [ADR 0007](0007-web-backend-aot.md), [ADR 0008](0008-combined-aot-binary.md), [ADR 0009](0009-frontend-toolchain-licenses-and-maintenance.md), [ADR 0010](0010-pdf-cover-rendering-aot.md), [ADR 0015](0015-ui-tests-with-playwright.md), [ADR 0016](0016-reader-libraries.md), [`docs/slices/m4-*.md`](../slices/m4-close.md), `tests/perf/measure.py` |

## Decision

1. **`Shelf.Library` holds the rules for MCP and web.** Search, browse, edit, tags, files, covers and duplicates are written once; the front ends only translate. Errors are `LibraryException` (invalid, not found, conflict) and each front end maps them to its format. Writes go through one gate lock.
2. **One error format for `/api/v1`:** `application/problem+json` with `status`, `title`, `detail`. The framework's own problem details are not used, because their extra fields are not ours to promise.
3. **Token flow:** bearer header or the cookie `shelf_token` (HttpOnly, SameSite=Strict). `?token=` sets the cookie and redirects without the parameter. A non-loopback bind needs a token; on loopback the `Host` header is checked (421 otherwise). The request log stays off so that the token never reaches a log.
4. **File IDs are `{workId}~{name}`.** They are stable across `reindex` and `scan`; they change when `organize` renames a file.
5. **Covers:** EPUB keeps the original image; PDF is rendered by PDFtoImage (page 1, 600 px on the longest side, JPEG q85). `shelf covers` fills what is missing; `import`, `scan` and `reindex` do it for the files they read. A cover update is targeted (`cover_path`, `updated_at`) and does not reconcile the index.
6. **UI toolchain:** Svelte 5, Vite 8, TypeScript 6.0.3 (7.x is not accepted by `svelte-check`), exact versions, no new runtime dependency. The build output is embedded in the binary (`ManifestEmbeddedFileProvider`). The page policy is a fixed `Content-Security-Policy` without third-party hosts.
7. **UI tests run in a real browser** (Playwright in Reqnroll, ADR 0015). Acceptance criterion 5 is a scenario: a click on a search hit opens the reader at the passage.
8. **Readers:** PDF.js and foliate-js ([ADR 0016](0016-reader-libraries.md)); deep links are `/read/{fileId}#page=N` and `#cfi=…`.
9. **Native AOT:** the plain SDK project needs `EnableRequestDelegateGenerator`, the `InterceptorsNamespaces` entry and the runtime switch `IsEnhancedModelMetadataSupported=false` in `Shelf.Cli.csproj`. 0 warnings on `osx-arm64`. `CA1416` is handled by a `#pragma` at the two PDFtoImage call sites.

## Evidence (`osx-arm64`, 10 cores, Native AOT build, reference library: 10,011 works, 14,750 files)

`tests/perf/measure.py --web`:

| Measure | Result | Target |
|---|---|---|
| `serve`: start to listening | 66.5 ms | – |
| `serve`: resident memory, idle (before any request) | **41.5 MB** | < 200 MB |
| `serve`: resident memory after the requests below | 87.9 MB | < 200 MB |
| Library view: page + script + style + first list (40 runs, p95) | **33.4 ms** | < 500 ms |
| First list alone, median | 30.8 ms | – |
| List sorted by author / by year, p95 | 40.2 / 36.2 ms | – |
| List filtered by language / by tag, p95 | 14.0 / 0.9 ms | – |
| Last page of 48, p95 | 36.2 ms | – |
| Search (full text) / very common word, p95 | 10.7 / 23.0 ms | – |
| Tags / work details / cover, p95 | 0.6 / 3.6 / 1.4 ms | – |
| Duplicates of the whole library, once | 115.6 ms | – |

The same view in a real browser (Chromium, fresh context without cache, 30 runs, `localhost`): DOM ready 13 ms median; first page of 48 books on the screen 97 ms median (p95 99 ms); all covers decoded 122 ms. That is the figure that matters for the goal, and it is far below 500 ms.

| Measure | Result |
|---|---|
| `shelf covers` on 8,336 missing covers (the rest, 1,675 works, have none: no image or an unreadable file) | 76 s; a second run creates 0 and takes 0.5 s |
| `shelf scan` afterwards | 0.92 s, 14,750 files unchanged |
| Import of the reference library with covers | about 4 min |
| Binary size | 29 MiB (M1) → 36 MiB (M3) → 42 MiB (M4, with the interface and PDFium glue) |
| Test suite | 2413 scenarios and tests, 0 failed, 26 skipped (scenarios that need the real model) |

## Consequences

- Both exit criteria of PRD 9.1 for the web are met with a wide margin; no cache, no paging tricks and no lazy list were needed.
- `perf.yml` runs `measure.py --web`, so a regression shows up in CI.
- A first-time `import` pays about a third of its time for covers. `--no-covers` was not added; the cost is small against extraction.

## Open decisions (owner)

- **Liberation fonts in the PDF.js data** (GPL-2 with the font exception): drop the four `LiberationSans-*.ttf` files or accept the exception (addendum in ADR 0016).
- **foliate-js** is flagged as lightly maintained (ADR 0016); the pinned version is 1.0.1.
- **Font de-obfuscation in the EPUB reader** needs a secure context; on plain HTTP in a LAN the affected books show the fallback font.
- **`web.read_only`** (like `mcp.read_only`) was not added; M5 question.
- **`organize` changes file IDs** of renamed files; a bookmark `/read/{fileId}` then gives 404.
- **`ignore` in the duplicate view** marks the whole group.
- **First CI run on `linux-x64`:** Chromium, the PDFium and SkiaSharp natives, ONNX Runtime and the web parts have not run there yet.
