# Slice m5-web-views: The Tags, Review and Organize Views

| | |
|---|---|
| **Slice ID** | `m5-web-views` |
| **Branch** | `feat/m5-web-views` (stacked on `feat/m5-reader-progress`) |
| **Milestone** | M5 – Polish |
| **Roadmap** | M5 slice 5 |
| **PRD references** | 8.2 views Tags (tag tree, rename/merge), Review (confirm or correct), Organize (diff, execute, undo); F-TAG-4, F-TAG-5, F-IMP-7, F-GRP-4, F-ORG-5 to F-ORG-7, 7.3 (parity with the CLI for these) |
| **Inputs** | slices `m4-ui-views`, `m4-api-files-dupes`; the CLI commands `tag`, `review`, `organize`, `undo` |

---

## 1. Scope

Three views and the endpoints behind them. The rules are those of the CLI; the endpoints call the same library code.

| View | Endpoints | Rules |
|---|---|---|
| Tags (`/tags`) | `POST /tags/rename`, `POST /tags/merge` (`GET /tags` existed) | rename needs an unused name (409), merge may go into an existing tag; both move the tags below; one undoable journal |
| Review (`/review`) | `GET /review`, `POST /review/{id}/accept` (`into` optional), `POST /review/{id}/dismiss` | accept only for grouping entries, merges into the best candidate or `into`; dismiss remembers grouping candidates as "not duplicates" |
| Organize (`/organize`) | `GET /organize[?pattern=]`, `POST /organize` (`pattern` optional), `POST /undo` | the preview is the dry run; apply is one undoable journal; collisions are left in place and listed |

**Out of scope**: editing the tags of one work (done in slice m4), accepting into several candidates at once, a badge for the review count, undo of `merge` and `split` (they are not undoable in the CLI either), keyboard control (slice `m5-web-keys`).

## 2. Feature Files

- `Features/Web/WebTags.feature`, `UiTags.feature` (`@F-TAG-4 @F-TAG-5`)
- `Features/Web/WebReview.feature`, `UiReview.feature` (`@F-IMP-7 @F-GRP-4`)
- `Features/Web/WebOrganize.feature`, `UiOrganize.feature` (`@F-ORG-5 @F-ORG-6 @F-ORG-7`)

## 3. Design

- **Same library code as the CLI.** `LibraryService` gets `RenameTag`, `MergeTag`, `ListReview`, `AcceptReview`, `DismissReview`, `PreviewOrganize`, `ApplyOrganize` and `UndoLast`; the last four partial files (`LibraryService.Review.cs`, `.Organize.cs`) keep the main file from growing. All writes run under the write gate of the service.
- **Errors by kind.** An unknown tag or entry is 404, a tag name in use 409, a bad name or pattern 400, "nothing to undo" 409.
- **Undo is the CLI's undo.** `POST /undo` reverts the last undoable run whatever it was (an organize, a tag change, a duplicates run). The Organize view therefore names it ("Undo organize", "Undo tag rename") from `nextUndo` in the preview, which comes from the new `JournalUndo.NextLabel`. A file that changed since the run is kept and reported (`skipped`).
- **Confirmation in the browser.** Move and merge ask twice, like the duplicates view: nothing moves before the second click; Escape or Cancel withdraws. A merge says that it cannot be undone.
- **The pattern that was previewed is applied.** The view sends the pattern of the preview with the apply, so a change of the config in between does not change what runs.
- **The preview is capped at 200 moves** (the limit of the MCP tool `propose_organization`); the view says so, and all moves are made.

## 4. Result

- BDD and unit tests green (2655 tests, 26 skipped for the missing e5 model); 36 new scenarios over HTTP and in the browser.
- Found while testing slice `m5-reader-progress`: two saves of a position could arrive in the wrong order, and a save queued behind another was lost when the page was reloaded. Saves now go one after the other, except for a page that is being hidden; the pages that show a position wait for the saves in flight.

## 5. Open Questions

- The review count has no badge in the header (the duplicates count has one); it would cost a request per page.
- The Organize view applies to the whole library. A selection of works (a folder, a tag) is not offered.
- `POST /undo` cannot name the run to revert; it is always the last one, like `shelf undo`.
