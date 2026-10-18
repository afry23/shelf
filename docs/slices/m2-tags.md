# Slice m2-tags: Tags

| | |
|---|---|
| **Slice ID** | `m2-tags` |
| **Branch** | `feat/m2-tags` (stacked on `feat/m2-organize`, for the journal's `Replace` and `undo`) |
| **Milestone** | M2 – Order |
| **PRD references** | F-TAG-1 to F-TAG-5, 7.1, 9.3 |
| **Inputs** | [m2-organize](m2-organize.md), `LibrarySearch`, `QueryParser`, `edit --tag*` |

---

## 1. Scope

**In scope**

1. `shelf tag add <tag> [<work>...] [--query "<q>"]` and `tag rm`: tag the works named and/or found by a search (F-TAG-3).
2. `shelf tag list`: every tag with the number of works that carry it (F-TAG-5).
3. `shelf tag rename <old> <new>` and `tag merge <from> <into>`: library-wide, including the tags below (F-TAG-4).
4. Tag syntax of F-TAG-1 (`TagNames`); sidecar is the truth and the index mirrors it (F-TAG-2).
5. All with `--json`, exit codes 0/1/2; `undo` reverts a tag run too.

**Requirements covered:** F-TAG-1 to F-TAG-5.

**Out of scope**

- Tag suggestions (F-TAG-6, Could).
- Validation of tags written by `edit --tag`; it keeps accepting what it accepted in M1.

## 2. Feature Files

`Features/Tags/Tags.feature` (`@F-TAG-1 @F-TAG-2 @F-TAG-5 @M2`), `TagBulk.feature` (`@F-TAG-3 @M2`), `TagRenameMerge.feature` (`@F-TAG-4 @M2`): overview (text, JSON, empty), add and remove by work and by search, hierarchy, blank trimming, empty parts, usage errors, no match, rename of subtrees and not of look-alike names, merge of two tags a work has, rename onto a used name, undo, reindex keeps the tags.

## 3. Design

- **Journaled sidecar changes.** A tag run replaces each changed sidecar through `FileJournal.Replace` in one undoable journal (label `tag add`, `tag rm`, `tag rename`, `tag merge`), then reconciles the index once. PRD 9.3 and the roadmap rule "every file operation goes through the journal" hold; `shelf undo` therefore reverts the last organize **or** tag run.
- **Hierarchy.** `rename` and `merge` act on the tag and every tag below it (`cloud` covers `cloud/azure`, not `cloudy`). `rm` removes exactly the tag, because removing a whole subtree silently is the more dangerous surprise. `rename` refuses a name that is in use (also as a parent) and points to `merge`.
- **Selection.** `add`/`rm` take works (ID, ID start or path) and/or `--query` (the search language of `shelf search`, reusing `QueryParser` and `LibrarySearch`); the union is used. No selection is a usage error; a query that finds nothing (and no work named) is exit 1.
- **Case.** Tags are case-sensitive and kept as written, as `edit --tag` does. Decision without ADR.
- **Unused backups.** The sidecar backups of a run stay in `.shelf/journal/<timestamp>.backup/`; there is no cleanup yet (see `m2-close`).

## 4. Unit Tests

`TagNamesTests`: normalization, errors, `IsSameOrBelow`, `Move`.

## 5. Open Questions

None for the owner.
