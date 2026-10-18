# Slice m2-dupes: Duplicates and the Trash

| | |
|---|---|
| **Slice ID** | `m2-dupes` |
| **Branch** | `feat/m2-dupes` (stacked on `feat/m2-tags`) |
| **Milestone** | M2 – Order |
| **PRD references** | F-DUP-1, F-DUP-2, F-DUP-4, F-DUP-5 (CLI), F-DUP-6, 7.1, 9.3 |
| **Inputs** | [m2-journal](m2-journal.md), [m2-organize](m2-organize.md), `dupe_ignores`, `LibraryEditor.MarkNotDuplicates` |

---

## 1. Scope

**In scope**

1. `shelf dupes`: lists groups of duplicates, as text or `--json` (F-DUP-1, F-DUP-2).
2. `shelf dupes --interactive`: for each group the user picks the file that stays, skips, marks the group as "not duplicates" or quits (F-DUP-5, CLI part).
3. Deleting always means the trash (F-DUP-4); `shelf trash empty` deletes it for good.
4. "Not a duplicate" marks for file pairs within a work and for pairs of works (F-DUP-6).
5. `CliEnvironment.StandardInput` for the answers.

**Requirements covered:** F-DUP-1, F-DUP-2, F-DUP-4, F-DUP-5 (CLI), F-DUP-6.

**Out of scope**

- Probable duplicates by embedding similarity (F-DUP-3, M5) and the comparison view in the web UI (M4).
- Merging tags or ratings of a work that ends without files into the work that stays.

## 2. Feature Files

`Features/Duplicates/Dupes.feature` (`@F-DUP-1 @F-DUP-2 @M2`), `DupesInteractive.feature` (`@F-DUP-4 @F-DUP-5 @F-DUP-6 @M2`), `Trash.feature` (`@F-DUP-4 @M2`): identical files in one work and across works, same format with different content, the suggestion, JSON, nothing changes when listing, Enter/number/skip/quit/end of input/invalid answer, "not duplicates" within and across works, the work left without files, the whole session undone, `trash empty` as dry run, with `--yes`, and the limit of undo.

## 3. Design

- **Two kinds of groups.** *Identical*: the same SHA-256 anywhere in the library. *Same format*: in one work, different content, same format; files with equal content count as one candidate there, since they form a group of their own. Groups are connected components of the files that are not marked as "not a duplicate", so a new copy of a marked pair is still found.
- **Suggestion.** Identical: a name without a trailing ` (1)`, then the path order. Same format: most complete metadata (of eight fields read from the file), then more pages, then larger size, then path order.
- **Session.** `DuplicateSession` opens one undoable journal (label `dupes`) with the first change and finishes it at the end, so `shelf undo` reverts the whole interactive session. Each step reconciles the index so that the next search of groups sees the new state. Skipping all changes nothing and writes no journal.
- **Sidecars.** Trashed files leave `files` and `notDuplicates.files` of their work. A work that has no file left goes to the trash with its `book.json` (and cover), and the folder is removed if empty. Its tags and rating are in the trash, not merged. Sidecars change through `FileJournal.Replace`; the same file replaced twice in a run gets numbered backups.
- **Marks.** Within a work: a file pair in that sidecar. Across works: the ID of the other work in both sidecars. Both are mirrored by the scan into `dupe_ignores`.
- **Trash empty is a dry run unless `--yes`.** The roadmap names the command; the `--yes` is a decision for irreversible deletion, in line with `organize --apply`. After it, `undo` reports the deleted files as missing and exits 1.
- **Interactive input.** Prompts go to standard output, answers come from `CliEnvironment.StandardInput`; the end of the input counts as `q`. `--interactive` with `--json` is a usage error.

## 4. Unit Tests

`JournalTests`: the same file replaced twice and undone. The rest is covered by the features.

## 5. Open Questions

None for the owner.
