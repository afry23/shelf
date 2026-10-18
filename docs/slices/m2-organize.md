# Slice m2-organize: `shelf organize` and `shelf undo`

| | |
|---|---|
| **Slice ID** | `m2-organize` |
| **Branch** | `feat/m2-organize` (stacked on `feat/m2-pattern`) |
| **Milestone** | M2 – Order |
| **PRD references** | F-ORG-1 to F-ORG-8, 7.1, 9.3, acceptance criterion 2 |
| **Inputs** | [m2-journal](m2-journal.md), [m2-pattern](m2-pattern.md), `LibraryEditor`, `LibraryScanner` |

---

## 1. Scope

**In scope**

1. `shelf organize [--apply] [--pattern <p>] [--library]`: dry run with a diff `old -> new` by default; `--apply` moves (F-ORG-5).
2. `shelf undo`: reverts the last organize run (F-ORG-6); a file that changed since is kept and reported (exit 1).
3. Collisions are reported and skipped, never overwritten (F-ORG-7); the whole work stays where it is.
4. Empty folders are removed after moving (F-ORG-8).
5. `Create` and `Replace` in `FileJournal`, so that changed sidecars and `review.json` are part of the undo (exact bytes come back).
6. All with `--json`, exit codes 0/1/2, `NO_COLOR` through `Palette`.

**Requirements covered:** F-ORG-1 to F-ORG-8 (rendering, dry run, journal, collisions, cleanup), PRD 7.1.

**Out of scope**

- Making `merge`, `split` and `import` undoable: they stay `undoable: false` (their sidecar changes are not journaled).
- Case-only renames on case-insensitive file systems: the target counts as taken and is reported.

## 2. Feature Files

- `Features/Organize/Organize.feature` (`@F-ORG-1 @F-ORG-5 @F-ORG-6 @F-ORG-7 @F-ORG-8 @M2`): dry run, apply, index follows, journal written, empty folders, second run, `--pattern`, invalid pattern (exit 2), taken target, two works to one place, JSON.
- `Features/Organize/Undo.feature` (`@F-ORG-6 @M2`): exact restore by hash (acceptance criterion 2), index follows, only the last run, nothing to undo, dry run and merge leave nothing, changed file kept, JSON.
- `Features/Organize/Pattern.feature` (`@F-ORG-1..4 @M2`): default pattern, missing year, reserved characters, first author only, `et al` from four authors.

## 3. Design

- **Plan, then apply.** `LibraryOrganizer` first plans every work (index lists the works, sidecars are the truth), claims all targets, and only then applies. Dry run and apply share the plan, so the diff is what happens.
- **One folder per work.** The folder of a work is the folder of its first file; `book.json` and the cover go along. A work whose target is taken, on disk or by an earlier work of the same run (works are taken in folder order), is skipped as a whole, so a work is never split across two folders.
- **Sidecar in the journal.** Renamed files change `files[].name` and `notDuplicates.files`. The sidecar is replaced through `FileJournal.Replace` (old bytes kept in `.shelf/journal/<timestamp>.backup/`), `review.json` the same way if a review entry names a moved file. Undo therefore restores every byte (acceptance criterion 2).
- **Index.** After apply and after undo `LibraryEditor.Reconcile` (scan plus review mirror) brings the index in line.
- **Empty folders** are not journaled; undo creates the parent folders of the files it moves back.
- **Output.** `old -> new` lines, a summary (`Would move 3 files in 2 works. Run with --apply to do it.`), collisions as `warning: skipped <from> -> <to>: <reason>` on stderr, exit 0. `undo` keeps files with `warning: kept <path>: <reason>` and exits 1.
- Decision without ADR; journal format and trash layout go into ADR 0012 at `m2-close`.

## 4. Unit Tests

`JournalTests` (+6): `Create` refuses a taken path, `Replace` and undo give back the exact bytes, `Replace` of a missing file, undo order after replace and move, a created file that changed is kept, recovery of pending creates.

## 5. Open Questions

None for the owner.

## 6. Implementation Notes

- A work at the library root has the directory `"."` in the index (`IndexWriter.JoinRelative`); the organizer never removes that folder.
- Undoing a replaced sidecar that was edited since reports two skips (the edited file and the backup that cannot go back).
