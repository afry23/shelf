# Slice m4-ui-toolchain: The Svelte UI Build, Embedded and Served

| | |
|---|---|
| **Slice ID** | `m4-ui-toolchain` |
| **Branch** | `feat/m4-library-core` (stacked: same branch as slices 1 to 5, separate commit) |
| **Milestone** | M4 – Web |
| **Roadmap** | M4 slice 6; carried over from M0: "UI build in CI", "project setup for the executable" (web part), the `.gitignore` negation |
| **PRD references** | F-WEB-3 (the UI is embedded as static assets, no Node.js at runtime), F-WEB-4 (light and dark mode: the base), 10.1 (`Shelf.Web.Ui`), 10.2, Q1 |
| **Inputs** | [ADR 0009](../adr/0009-frontend-toolchain-licenses-and-maintenance.md) (the toolchain and what it left open), [ADR 0007](../adr/0007-web-backend-aot.md) (W3, W4: embedded files, SPA fallback), the spike in `f6089e6:experiments/web/Ui/` |

---

## 1. Scope

**In scope**

1. `src/Shelf.Web.Ui`: a Svelte 5 + Vite + TypeScript single-page application with a typed client of the API (`src/api.ts`), a first page that shows `/api/v1/status` (a missing token is explained), and the base styles for light and dark mode. Exact versions and a lock file; `npm run build`, `npm run check` (svelte-check) and `npm run dev` (hot reload, `/api` sent to `shelf serve`).
2. The build in MSBuild: building `Shelf.Web` runs `npm ci` (when the packages changed) and `npm run build` (when the sources changed) and embeds `dist/` under the logical folder `ui`. `-p:SkipUiBuild=true` leaves it out.
3. The server side: static files from the embedded UI (public), the SPA fallback for addresses without a file name, the JSON `404` for everything under `/api`, cache headers for the hashed assets, and the security headers.
4. CI: Node.js from `.node-version`, `npm ci` and `npm run check` in the `test` and `aot` jobs.
5. The repository: `.node-version` (26), the `.gitignore` negation, `.npmrc` (`ignore-scripts`), an addendum to ADR 0009.

**Out of scope**: the views (slice 7), the reader and its libraries (slice 8), the end-to-end check of the native binary without Node.js on the path (slice 9, `native.sh`), `THIRD-PARTY-NOTICES` (M5).

## 2. Feature Files

`Features/Web/WebUi.feature` (`@F-WEB-3 @M4`): `/` is the page of the build, byte for byte; its script and style (hashed names, read from the page) are served with their media types and the long-lived cache header, while the page itself is `no-cache`; an address without a file name (`/library/abc`) gives the page again; a file that does not exist (`/missing.js`) and anything under `/api` that is not an endpoint are problem documents, never the page; the page and the assets need no token while the API does; `HEAD` and a conditional request; the security headers; a method other than `GET` and `HEAD` on the page is `405` (and `POST` to an endpoint of the API still is, too).

`Features/Web/WebApi.feature`: the scenario "A path outside the API is a problem document until the UI is embedded" is replaced (the page is served now).

## 3. Design

- **Where the UI lives.** `src/Shelf.Web.Ui` is a folder with a `package.json`, not a .NET project (PRD 10.1 names it a build-time project). `Shelf.Web` builds it, so everything that references `Shelf.Web` (the CLI, the specs) gets the UI built and embedded by an ordinary `dotnet build`.
- **The MSBuild target** `BuildUi` has the package files, the config and `src/**` as inputs and `dist/index.html` as output, so a build without UI changes does nothing. `InstallUi` runs `npm ci` when `package-lock.json` is newer than `node_modules/.package-lock.json`. Both fail with a plain message when `node` is not on the path ("Node.js 26 is needed to build the UI; see .node-version, or build without it with -p:SkipUiBuild=true"). The items for `dist/**` are added by a target that runs before the resources are collected, because a static item would be evaluated before `dist` exists.
- **Without the UI.** With `SkipUiBuild`, or an empty `dist`, the assembly carries no UI. The server then starts, says so on standard error ("the UI is not embedded") and answers the pages with the problem document.
- **Embedded files, as in ADR 0007 W3.** `GenerateEmbeddedFilesManifest` and `ManifestEmbeddedFileProvider` over the assembly, logical folder `ui`; `Microsoft.Extensions.FileProviders.Embedded` 10.0.12 (the spike's pin, the same as the runtime).
- **The SPA fallback is an endpoint, not the framework's `MapFallbackToFile`.** That one answers `/api/v1/nothing` with the page, and `CreateSlimBuilder` has no regex constraint to keep `/api` out. The fallback is `GET` and `HEAD` only, so that a `POST` to an endpoint of the API still gets `405` with `Allow` (a fallback for every method would be a valid candidate and turn that into `404`). Its handler gives `404` as a problem document for `/api/...` and for an address with a file name, and the page otherwise.
- **Three MSBuild details found while building it.** (1) `LinkBase="ui"` is not applied to items that a target adds, so the manifest listed the files flat; the target builds the `Link` itself, from a qualified metadata reference (`%(_UiFile.RecursiveDir)`; the unqualified form evaluates to empty inside a target), with forward slashes (`ui/assets/...`; a backslash became part of a folder name). (2) The manifest is calculated as a step of `PrepareResourceNames`, so the target that adds the files runs before `_CalculateEmbeddedFilesManifestInputs`. (3) A project that has no `dist` still builds: `SkipUiBuild`.
- **`MapFallback` adds the constraint `nonfile` to every pattern** (the routing log shows `{*path:nonfile}` for `{**path}`), so an address with a dot never reaches the fallback. The address of the reader ends in the name of a book (`/read/01J9...~kp.epub`), and reloading it must give the page. A small middleware sends a `GET` or `HEAD` that asks for `text/html` and has a dot in its path (outside `/api`) to `/`, where the fallback answers; a script or a style asks for anything else and gets its 404. The middleware must run before an endpoint is chosen, so `UseRouting` is called by hand after it (otherwise the framework puts it first).
- **Cache.** Vite writes content hashes into the names under `/assets/`; those get `Cache-Control: public, max-age=31536000, immutable`. The page (`index.html`) gets `no-cache`, so a new build is seen at once; its `ETag` makes the check cheap.
- **Headers on every answer:** `X-Content-Type-Options: nosniff`, `X-Frame-Options: SAMEORIGIN` (the reader may frame its own pages, nobody else may frame shelf) and `Referrer-Policy: same-origin`. No `Content-Security-Policy` yet: PDF.js and foliate-js need workers and blob frames, and the policy is written in slice 8 when their needs are known.
- **TypeScript 6.0.3, not 7.0.2 (ADR 0009 T5).** `svelte-check` 4.7.7, the only type checker for `.svelte` files, takes TypeScript 5 or 6 as a peer; `npm ci` with 7.0.2 stops with `ERESOLVE`. That is the "TypeScript compiler requirement from Svelte tooling" that ADR 0009 named as a re-check trigger. TypeScript 6.0.3 is Apache-2.0 and from the same organization; the addendum to the ADR records the change. `vite build` does not use the compiler, so the bundle is the same.
- **`ignore-scripts=true`** in `.npmrc` is the decision on fsevents (ADR 0009 T9): the build is identical without the install script, and a build tool has no reason to run code at install time. It also keeps npm 11's warning about uncovered install scripts away.
- **Node.js 26** (owner decision, ADR 0009) in `.node-version`; `engines` says `>=26`.
- **CI** runs `actions/setup-node` with `node-version-file: .node-version` and the npm cache keyed on the lock file, then `npm ci` and `npm run check` once in `src/Shelf.Web.Ui`; the build step of MSBuild finds the packages installed and only runs `vite build`.

## 4. Native AOT

**Result (`osx-arm64`):** 0 warnings, 36 MiB. The binary was copied to an empty folder and started with `PATH=/usr/bin:/bin` (no `node`): `GET /` is the page (`no-cache`, the three security headers), the script (`text/javascript`, `public,max-age=31536000,immutable`) and the style (`text/css`) are byte for byte the files of `dist/` (`cmp`), `/library/abc` and the reader address with `Accept: text/html` give the page, `/missing.js` and `/api/v1/nothing` are problem documents, `/api/v1/status` answers. `linux-x64` is checked by the `aot` job.

## 5. Unit Tests

None for the UI itself: this slice ships a page and the build, not logic. `npm run check` (0 errors) is the check of the TypeScript and the components; it runs in CI. The server side is covered by the specs.

## 6. Open Questions

- **Tests of the UI.** Component tests (Vitest) and browser tests (Playwright) are not set up. Slice 7 decides when the first view has behavior worth testing; slice 8 decides how AC 5 (a click opens the reader at the location) is tested.
- **CSP** in slice 8 (see 3).
