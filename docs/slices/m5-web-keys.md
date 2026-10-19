# Slice m5-web-keys: Keyboard Control

| | |
|---|---|
| **Slice ID** | `m5-web-keys` |
| **Branch** | `feat/m5-web-keys` (stacked on `feat/m5-web-views`) |
| **Milestone** | M5 – Polish |
| **Roadmap** | M5 slice 6 |
| **PRD references** | F-WEB-5 (`/` focuses the search, `j`/`k` navigates, `Enter` opens) |

## 1. Scope

`/` focuses (and selects) the search field of the header; `j` and `k` move through the items of the page; `Enter` opens the current item; `Escape` leaves a field. Pages with items: Library (grid and list), Search, Duplicates (file cards and the cards of probable pairs), Review.

**Out of scope**: the reader (PDF.js and foliate-js have their own keys; the handler is not blocked there but no item is marked), a help overlay for the keys, `g`-sequences, remapping.

## 2. Feature Files

`Features/Web/UiKeys.feature` (`@ui @F-WEB-5 @M5`, 8 scenarios) and one scenario in `UiDuplicates.feature`.

## 3. Design

- **One handler** (`src/keys.ts`) on the document, started from `App.svelte`. Keys with Ctrl, Meta or Alt are left alone; so is every key typed in an input, select, textarea or editable element (except `Escape`, which leaves the field).
- **Items are marked `data-nav`** (the `li` of a book or a hit, the card of a file, the entry of the review). The current item has the focus (`tabindex="-1"` is set when it gets it), so a screen reader announces it and the browser scrolls to it (`scrollIntoView({block: 'nearest'})`); there is no second notion of "selected". A visible outline (`[data-nav]:focus-visible`) marks it.
- **`j` from nowhere goes to the first item, `k` to the last**; at the ends the focus stays.
- **`Enter` acts only on a focused item**: it follows the first link inside. On a link or a button that has the focus, Enter does what it always does.
- The file cards of the duplicates view now link their title to the book (so Enter has something to open).

## 4. Result

BDD and unit tests green (2664 tests, 26 skipped for the missing e5 model), three runs in a row.

## 5. Open Questions

- The keys are not announced anywhere in the interface. A short list ("?") would help; not in the PRD.
- In the review view only a grouping entry has a link; Enter on another entry does nothing.
