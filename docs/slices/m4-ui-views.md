# Slice m4-ui-views: Library, Search, Work Details and Duplicates

| | |
|---|---|
| **Slice ID** | `m4-ui-views` |
| **Branch** | `feat/m4-library-core` (stacked: same branch as slices 1 to 6, separate commit) |
| **Milestone** | M4 – Web |
| **Roadmap** | M4 slice 7 |
| **PRD references** | F-WEB-4 (desktop and tablet, light and dark), 8.2 (views Library, Search, Work details, Duplicates), F-DUP-5 (comparison in the web UI), F-SRC-5, F-SEM-5, F-TAG-5, F-LIB-6 |
| **Inputs** | slices `m4-api-read`, `m4-api-files-dupes`, `m4-covers`, `m4-ui-toolchain` (the API, the files and covers, the build) |

---

## 1. Scope

**In scope**

1. The shell: header with the name, the search field, the links Library and Duplicates (with the number of groups), and a small router (History API; no new npm package).
2. **Library**: grid (covers on a shelf line) or list, faceted filters (tags, authors, language, year, format, reading status) as buttons that add and remove filters of the query language, sorting, paging, empty states. The state is in the address (`/library?q=...&sort=...`), so reload and back work.
3. **Search**: the three modes (Full text, Meaning, Both), the level (Books, Passages) for the two with embeddings, hits with the snippet and a link into the reader at the location (`#page=N`, `#cfi=...`), the note for an incomplete index.
4. **Work details**: metadata, files with Read and Download, reading status, rating, tags, table of contents, and editing the details in a form.
5. **Duplicates**: the groups side by side with the suggestion, and the three actions with an inline confirmation.
6. Light and dark (the system setting), tablet layout (filters behind a button).
7. The browser tests: Playwright in Reqnroll (owner decision 2026-10-18), scenarios tagged `@ui`.
8. `passages[].fileId` in the search answer, so that a hit can link to its file.

**Out of scope**: the reader itself (slice 8; until then `/read/...` shows a page that says so and offers the download), keyboard control (F-WEB-5, M5), views Tags, Review and Organize (M5), similar books (M5), an undo from the web (M5).

## 2. Feature Files

All tagged `@ui` and run in Chromium against a real `shelf serve`.

- `Features/Web/UiLibrary.feature` (`@PRD-8.2 @M4`): the shelf of covers, the book without a cover, facet filters (add, combine, remove), the sort and its order, the list view, paging, the empty library, no match, reload keeps the state, opening a book.
- `Features/Web/UiWork.feature` (`@PRD-8.2 @F-LIB-6 @M4`): the details, Read and Download, status, rating, tags (add, remove, persist), editing (save, cancel, a refusal from the server), an unknown book.
- `Features/Web/UiSearch.feature` (`@F-SRC-5 @F-SEM-5 @PRD-8.2 @M4`): full text with a highlighted snippet and the deep link (EPUB and PDF), meaning, both, the level, the note, nothing found, an error from the server.
- `Features/Web/UiDuplicates.feature` (`@F-DUP-5 @F-DUP-4 @F-DUP-6 @M4`): the groups, keep, trash, not duplicates, the confirmation, the number in the header, no duplicates.
- `Features/Web/UiLayout.feature` (`@F-WEB-4 @F-WEB-2 @M4`): light and dark, tablet and desktop, no sideways scrolling, the access token in a real browser.
- `Features/Web/UiSmoke.feature` is replaced by these.

## 3. Design

**Subject and the one memorable thing.** shelf manages the books of one person; the job is to find a book and open it. The real content is covers, titles and passages of text. The memorable element is the **shelf line**: every row of covers rests on a thin shelf edge. It is the name of the product, it needs no decoration beyond one rule and a faint shadow, and everything else stays quiet.

**Tokens.** Cool paper instead of cream, bookcloth green instead of terracotta.

| Token | Light | Dark |
|---|---|---|
| paper (page) | `#EFF1EE` | `#131819` |
| surface | `#F8F9F7` | `#1B2223` |
| ink | `#1A2220` | `#E3E8E4` |
| muted | `#5B6965` | `#9AA7A2` |
| line | `#CBD2CE` | `#2C3638` |
| accent (bookcloth green) | `#2F5D50` | `#8DC2AF` |
| danger | `#8A2E2E` | `#E39A9A` |

**Type.** System fonts only (the server needs no network, PRD 9.4): a book serif stack (`Iowan Old Style`, `Palatino`, `Georgia`) for titles, authors and the text of snippets, `system-ui` for everything one operates. Sentence case, no capital-letter labels. Lines of text stay under 70 characters.

**A book without a cover** is drawn as a spine: the title set in the serif on a muted cloth color taken from the ID of the work (six colors), so that the shelf is not a row of grey boxes.

**Layout.** Desktop: filters in a column on the left (about 15 rem), the shelf on the right. Tablet (under 960 px): the filters sit behind a "Filters" button. The header is one row: name, search field, two links.

```
 shelf   [ Search your library        ]   Library   Duplicates (3)
 ─────────────────────────────────────────────────────────────────
 Library · 3 books                      Sort by [Title ▾] ↑   [Grid|List]
 Filters          ┌────┐ ┌────┐ ┌────┐
  Tags            │    │ │    │ │    │
  ...             │    │ │    │ │    │
                  └────┘ └────┘ └────┘
                  ═══════════════════════   <- the shelf line
                  Title   Title   Title
```

**Accessibility as the contract for the tests.** The tests find things the way a person using a screen reader would: by role and name. So the names are part of this plan: links named by the title, facet buttons `Format: pdf (1)` with `aria-pressed`, `Remove filter Format: pdf`, the radio-like buttons `Unread`, `Reading`, `Read` and `1 star` to `5 stars` with `aria-pressed`, `Add a tag`, `Edit details`, `Keep this file`. Focus is visible, motion is only a reaction to an action, `prefers-reduced-motion` is respected.

**State in the address.** `/library?q=<filters>&sort=<field>&order=<asc|desc>&offset=<n>&limit=<n>&view=<grid|list>`; `/search?q=<text>&mode=<fulltext|semantic|hybrid>&level=<work|chunk>`. A facet button adds a filter to `q` (`tag:"cloud/azure"`, `lang:en`, `year:2019`, `format:pdf`, `status:read`, `author:"Lukas Weiß"`); the chips above the shelf parse `q` back and remove one filter each. The view mode is also kept in `localStorage` as a convenience.

**No dialogs of the browser.** Confirmations are inline (the button turns into "Confirm", `Escape` or a click elsewhere cancels). That keeps the scenarios free of `window.confirm`, and a dialog can't be missed by a screen reader.

**Deep links.** A passage links to `/read/{fileId}#page=N` (PDF) or `/read/{fileId}#cfi=epubcfi(...)` (EPUB). The file ID is `passages[].fileId` of the search answer (new in this slice: `workId~name`).

**The reader page** exists as a stub so that the links lead somewhere: it says the reader comes with the next slice and offers the download of the file.

- **One result per book at the level of passages.** The service answers a search of passages with one hit per passage, so a book with two good passages came twice; the interface stopped on the duplicate key (`each_key_duplicate`). The API now groups: one result per book, in the order of its best passage, with all of its passages (`total` counts books). Every client gets the same shape at both levels.
- **A 401 has its own words.** The client turns any `401` into "This server needs an access token. Open the address that shelf serve was started for with ?token=… at the end, once; the browser keeps the token after that." The plain text of the server ("a valid access token is required") is true but tells nobody what to do.
- **Reading status "Finished", not "Read".** On the work page the button for the status and the action to open the book were both "Read". The value in the sidecar is still `read`.

## 4. Native AOT

**Result (`osx-arm64`):** 0 warnings, 36 MiB. The server changes of this slice are the `fileId` of a passage and the grouping; a native run answers the full-text search with `passages[].fileId` (`01M5...~Event Streams in Practice.pdf`), the search of passages without a model (`level chunk`, `total 0`, the note), the page, a reader address with `Accept: text/html`, and the script of the build contains the new label. The scenarios run against the JIT server in the test process, as in the earlier slices; the native binary serves the same files and the same API.

## 5. Tests (result)

73 browser scenarios (`@ui`: library 22, work 24, search 14, duplicates 11, layout 12) run in about a minute, in Chromium, headless. Two complete runs of the suite in a row were green (2366 tests). Findings of the work, all from the failure messages of the scenarios (which carry the page text, the API calls, the server's error output, the browser console, the traffic and the cookies):

- a list key that was not unique (above),
- a browser context that was started anew for every "open", so the cookie of the access token was gone on the second page (a flaw of the step, not of the interface; the step now keeps the browser and starts a new one only when the window or the scheme changes),
- the clean-up: a page that stays open keeps asking the server, and the server keeps writing into the index folder while the working directory is deleted ("Directory not empty"); the browser and the server are now stopped by hooks that run before the other clean-up.

## 6. Open Questions

- **Screenshots** (`SHELF_SCREENSHOTS=<folder>`) are how the design was reviewed; there is no visual-regression test and none is planned.

- **Cost of the browser tests in CI.** Chromium (about 150 MB) is downloaded once per runner and cached; the `ui` scenarios add about a minute. If that becomes a problem the tag allows running them in a separate job.
- **License and maintenance of Playwright** (Apache-2.0, Microsoft) are recorded in ADR 0015 together with the decision.
