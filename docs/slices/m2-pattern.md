# Slice m2-pattern: Pattern Engine and `organize.pattern`

| | |
|---|---|
| **Slice ID** | `m2-pattern` |
| **Branch** | `feat/m2-pattern` (stacked on `feat/m2-journal`) |
| **Milestone** | M2 – Order |
| **PRD references** | F-ORG-1, F-ORG-2, F-ORG-3, F-ORG-4, 9.2 |
| **Inputs** | `FolderNames.Sanitize`, `AuthorNames`, `ShelfSettings`, [m2-journal](m2-journal.md) |

---

## 1. Scope

**In scope**

1. `PathPattern` (parse, validate, render) and `PathSanitizer` in `Shelf.Core/Organization`.
2. Placeholders of F-ORG-2; default `{author_sort}/{title} ({year})/{title}.{ext}` (F-ORG-1).
3. Sanitizing of F-ORG-3 including Unicode NFC; `FolderNames.Sanitize` now delegates to it.
4. Author rule of F-ORG-4.
5. Config key `organize.pattern` (global and per library), validated with the key and file in the message; `ConfigTemplate` documents it.

**Out of scope**

- `organize`, `undo` and the `--pattern` option: `m2-organize`. The rendering scenarios (F-ORG-1..4 through `organize` output) land there, because the pattern has no command of its own.

## 2. Feature Files

`Features/Configuration/OrganizeSettings.feature` (`@F-ORG-1 @F-ORG-2 @PRD-9.2 @M2`): default, layering global/library, and eight invalid patterns.

## 3. Design

- **Location.** `PathPattern` lives in `Shelf.Core`, not `Shelf.Organize` as PRD 10.1 suggests: `ShelfSettings` (Core) must validate the pattern, and Core cannot reference Organize. The journal stays in `Shelf.Organize`.
- **Open points of the PRD, decided here:**
  - A placeholder without a value renders as nothing; brackets it leaves empty are removed (`Title ()` becomes `Title`).
  - A folder part that stays empty is left out; an empty file name becomes `_`.
  - The last part must end with `.{ext}`, so a long name is cut before the extension.
  - `{series_index}` has no trailing zeros (`1`, `2.5`).
  - `{first_tag}` with a hierarchy becomes one name (`architektur/ddd` becomes `architektur-ddd`).
  - Missing author: `Unknown Author`.
- **F-ORG-3 against F-ORG-4.** "No trailing dots" wins over "et al.": from four authors on the folder is `Fowler, Martin et al` (no final dot), because Windows cannot name such a folder. Decision without ADR.
- **Length.** The limit is 120 UTF-8 bytes per part (stricter than 120 characters, matches the M1 code); the cut never splits a surrogate pair.

## 4. Unit Tests

`PathPatternTests` (default rendering, missing year, author rules, empty parts, all placeholders, reserved characters, NFC, long titles keep the extension, surrogate pairs, invalid patterns, sanitizer cases); `ConfigTemplateTests` now also checks the pattern.

## 5. Open Questions

None for the owner.
