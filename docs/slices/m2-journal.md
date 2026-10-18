# Slice m2-journal: File-Operation Journal, Undo and Trash

| | |
|---|---|
| **Slice ID** | `m2-journal` |
| **Branch** | `feat/m2-journal` (from `main`) |
| **Milestone** | M2 – Order |
| **Roadmap** | M2 note: the journal is used for all file operations, including the trash |
| **PRD references** | F-ORG-6, F-ORG-7, F-DUP-4, 9.3 |
| **Inputs** | `LibraryImporter.Place`, `LibraryEditor.Merge`/`Split`, `FolderNames`, `SidecarStore` |

---

## 1. Scope

**In scope**

1. New project `src/Shelf.Organize` (PRD 10.1) and `tests/Shelf.Organize.Tests`.
2. `FileJournal`: one JSON file per run in `.shelf/journal/<timestamp>.json`, ops `move`, `trash`, `mkdir`, each with `from`, `to`, `sha256` and a status that is flushed to disk after every step.
3. `JournaledFileOps`: `Move` never overwrites; an existing target ends as `Skipped(Collision)`.
4. Crash recovery: an unfinished journal is rolled forward or back on the next run.
5. `Undo`: replays the last finished journal in reverse, verifies hashes, marks it `undone`.
6. Trash under `.shelf/trash/<timestamp>/<original relative path>`; `TrashEmptier` deletes for good.
7. `journal/` and `trash/` are created lazily (init does not create them).
8. `LibraryEditor.Merge` and `Split` move through `FileJournal` (`undoable: false`: their sidecar changes are not in the journal).

**Deviation from the roadmap plan:** `LibraryImporter.Place` stays as it is. Its source lies outside the library, the journal only accepts paths inside the root (PRD 9.3), and the copy-verify-delete order already never loses the source.

**Requirements covered:** F-ORG-6 (engine), F-ORG-7 (engine), F-DUP-4 (engine), PRD 9.3.

**Out of scope**

- CLI commands `organize`, `undo`, `trash empty` and their feature files: `m2-organize`, `m2-dupes`.
- Pattern engine: `m2-pattern`.

## 2. Feature Files

None in this slice. The journal has no command of its own yet, so the BDD scenarios for F-ORG-6 and F-ORG-7 (`Features/Organize/Undo.feature`, `Organize.feature`) land in `m2-organize`. Existing `Merge`, `Split` and `Import` features must stay green; they cover the refactored callers.

## 3. Design

- **Journal before action.** An op is written as `pending`, executed, then set to `done`. Recovery looks at the first non-`done` op and checks whether `from` or `to` exists.
- **No overwrite.** `Move` checks the target and returns a result instead of throwing. The caller reports and skips (F-ORG-7).
- **Hash check on undo.** The journal stores `sha256` of each moved file; undo refuses to move a file whose hash changed and reports it.
- **Sidecars.** Sidecar writes keep `SidecarStore.Write` (temp + move). The journal covers file moves only; the sidecar is reconciled by `LibraryScanner.Scan` as in M1 ("disk first, then reconcile").
- **Layer.** `Shelf.Organize` references `Shelf.Core` only; `Shelf.Index` references `Shelf.Organize`.
- Decision without ADR for now; the journal format and trash layout go into ADR 0012 at `m2-close`.

## 4. Unit Tests

`tests/Shelf.Organize.Tests/JournalTests.cs` (18 tests): move and mkdir journaling, collision keeps target bytes, paths outside the root refused, undo hash round trip, undo refuses changed or taken files, only undoable and only the last run, trash numbering and undo, `TrashEmptier`, recovery of a pending step (ran / never ran / gone), recovery on `Begin`, damaged journal, same-millisecond names.

## 4a. Implementation Notes

- An empty journal (all pending steps dropped by recovery) must not count as "last undoable run"; `JournalUndo` only picks journals with a `done` step.
- Undo removes folders it created (journaled `mkdir`) if they are empty, including `.shelf/trash` itself.
- Class names differ from the first draft: `FileJournal`, `JournalRecovery`, `JournalUndo`, `TrashEmptier`; there is no separate `JournaledFileOps`.

## 5. Open Questions

None for the owner.
