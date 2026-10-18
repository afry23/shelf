# ADR 0015: Browser tests of the web interface with Playwright in Reqnroll

| | |
|---|---|
| **Status** | Accepted: decided by the project owner on 2026-10-18; verified on `osx-arm64`, the `linux-x64` run is the first CI run |
| **Date** | 2026-10-18 |
| **Scope** | How the behavior of the web interface (PRD 8.2 views, F-WEB-4, and from slice 8 the reader, acceptance criterion 5) is tested; the license and maintenance of the test tool |
| **Roadmap** | M4 slice `m4-ui-views`; [ADR 0009](0009-frontend-toolchain-licenses-and-maintenance.md) left the question of UI tests open |
| **Inputs** | `CLAUDE.md` (BDD with Reqnroll, every scenario green), slice plan `docs/slices/m4-ui-views.md` |

## Context

`CLAUDE.md` asks for Reqnroll scenarios that run and are green. The web interface has behavior (filters that change a shelf, a tag that is added, a hit that leads to a page of a book) that no API test can show: the API gives the data, the interface turns it into what a person sees and clicks. Acceptance criterion 5 ("clicking a search hit in the web UI opens the reader at the location") cannot be checked without a browser at all.

## Options

- **A. Playwright for .NET in Reqnroll** (`Microsoft.Playwright`): feature files drive a real Chromium against a real `shelf serve`.
- **B. Vitest with Testing Library** (jsdom): component tests with the API mocked; fast, no browser, but no real server and no real layout (the shelf, the tablet layout, light and dark are layout).
- **C. `svelte-check` and manual acceptance only**: fastest, but against `CLAUDE.md`, and criterion 5 stays unproven.

## Decision

**A, chosen by the project owner on 2026-10-18.** The scenarios of the views are tagged `@ui` and live next to the others in `tests/Shelf.Specs` (`Features/Web/Ui*.feature`); `BrowserSteps` starts one headless Chromium per test run and gives every scenario a context of its own (cookies, window size, color scheme). The server is the one the API scenarios use (in the test process, port 0, the fake embedder), so a scenario is a person's path through the real thing.

How the tests find things matters more than the tool: **by role and accessible name**, the way a screen reader user finds them (a link named by the title, `Format: pdf (1)` buttons with `aria-pressed`, `Remove tag to-read`). A test that passes therefore also says something about the accessibility of the page, and a renamed button is a visible change of the contract. The names are listed in the slice plan.

## License and maintenance (ADR 0001 method)

| Item | License | Notes |
|---|---|---|
| `Microsoft.Playwright` 1.63.0 (NuGet) | Apache-2.0 | Microsoft, active (a release every few weeks); a test-only dependency, it is not part of `shelf`. Compatible with GPL-3.0-or-later (ADR 0001 S1). |
| Chromium and the other browser builds | BSD-3-Clause and the licenses of its parts | Downloaded by Playwright at test time into the user's cache (`~/Library/Caches/ms-playwright`, `~/.cache/ms-playwright`), never shipped, never in the repository. |
| Playwright driver (Node.js based, inside the NuGet package) | Apache-2.0 | Runs only in the test process; unrelated to the Node.js 26 of the UI build. |

Not assessed: the licenses of the bundled parts of the Playwright driver and of Chromium's third-party code (as for Node.js in ADR 0009); nothing of it is shipped.

## Consequences

- **First run:** `BrowserSteps.InstallBrowser` (`[BeforeTestRun]`) installs Chromium through Playwright's own installer; it is quick when the browser is cached. On a Linux CI runner (`CI=true`) it also installs the system libraries (`--with-deps`, needs `sudo`, which the GitHub runners have). The CI `test` job caches the browser directory per platform.
- **Time:** about 75 browser scenarios take about a minute; the browser start is paid once.
- **Screenshots for the design:** with `SHELF_SCREENSHOTS=<folder>` every scenario saves its last page there (`BrowserSteps.SaveScreenshot`); nothing is written otherwise.
- **Failure messages** carry what a person debugging needs: the text of the page, the API calls it made, the server's standard error, the errors of the browser console, the traffic and the cookies. That is how three bugs of this slice were found in minutes (a duplicate key in a list, a browser context that dropped its cookie, a request that never came back).
- **Slice 8** uses the same steps for the reader; AC 5 becomes a scenario: search, click a hit, the reader shows that page.
- **Re-check triggers:** a Playwright major version, a Chromium change that breaks headless mode on a runner, the test time passing three minutes (then run `@ui` in a job of its own), a need for Firefox or WebKit.
