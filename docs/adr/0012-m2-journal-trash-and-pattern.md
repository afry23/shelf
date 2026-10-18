# ADR 0012: Journal, trash and path pattern of milestone M2

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-10-18 |
| **Scope** | PRD 6.4 (F-ORG-1 to F-ORG-8), 6.5, 6.8 (F-DUP-4), 9.3, 10.4, roadmap M2 |
| **Inputs** | [`docs/slices/m2-journal.md`](../slices/m2-journal.md), [`m2-pattern`](../slices/m2-pattern.md), [`m2-organize`](../slices/m2-organize.md), [`m2-tags`](../slices/m2-tags.md), [`m2-dupes`](../slices/m2-dupes.md), [`m2-related`](../slices/m2-related.md) |

The PRD leaves the journal format, the trash layout and several rules of the pattern open. This ADR fixes them.

## Decision

1. **One journal per run**, `.shelf/journal/<timestamp>.json` (`yyyyMMddTHHmmss.fffZ`, a `_2` suffix if taken). Fields: `schema` (1), `label` (`organize`, `tag add`, `dupes`, `merge`, ...), `undoable`, `started`, `state` (`running`, `finished`, `undone`) and `ops`. An op has `kind`, `from`, `to`, `sha256` and `status` (`pending`, `done`, `undone`). Paths are relative to the library root, with `/`. Nothing outside the root is touched (PRD 9.3).
2. **Four kinds of steps.** `mkdir` (a folder that the run created), `move`, `trash` (a move into the trash) and `create` (a new file; undo deletes it if its hash is unchanged). A step is written as `pending` before it runs and as `done` after, each time through a temporary file that is flushed to disk.
3. **A file is never overwritten.** A move to a taken target is reported (`Collision`) and not executed (F-ORG-7).
4. **Changing a file is `Replace`.** The old file is moved into `.shelf/journal/<timestamp>.backup/<path>` (numbered if the same file is replaced twice in a run) and the new content is created. Undo therefore restores the exact old bytes. Sidecars (`book.json`) and `review.json` change this way.
5. **Recovery.** The next run that begins a journal first closes every journal left `running`: a pending step whose effect is found on disk becomes `done`, one that never ran is dropped, a file that is gone from both places is reported. The result stays undoable.
6. **Undo** reverts the newest `undoable` journal that has a `done` step, in reverse order. It never overwrites (`target taken`), never moves a file whose hash changed (`changed since`), and keeps what it cannot revert (exit 1). Only `organize`, `tag *` and `dupes` runs are undoable. `merge`, `split` and the import also use the journal for crash safety (`merge`, `split`) but are not undoable: their sidecar changes are not recorded in it.
7. **Retention.** `Begin` keeps the newest 50 journals with their backup folders and deletes older ones (never a running one). Older runs cannot be undone any more. Files that such a run put in the trash stay there until `shelf trash empty`.
8. **Trash layout:** `.shelf/trash/<journal timestamp>/<original relative path>`; a taken name gets ` (2)`, ` (3)`. `shelf trash empty` is the only command that deletes for good, and it is a dry run unless `--yes` is given. After it, `undo` reports the deleted files as `missing`.
9. **Path pattern.** Placeholders as in F-ORG-2, default `{author_sort}/{title} ({year})/{title}.{ext}`. A missing value renders as nothing, brackets that this leaves empty are removed, an empty folder part is left out. The last part must end with `.{ext}`. A part has at most 120 UTF-8 bytes, NFC, no trailing dots or blanks. From four authors on, `et al` is appended to the folder name **without** its dot: F-ORG-3 (no trailing dots) wins over F-ORG-4 because Windows cannot name such a folder. `PathPattern` lives in `Shelf.Core`, because the config (`organize.pattern`) must validate it.
10. **Organizing is per work.** The folder of a work is the folder of its first file; the sidecar and the cover go along. If any target of a work is taken (on disk or by an earlier work of the same run) the whole work stays and the collision is reported, so a work is never split.
11. **Tags are journaled.** A tag run replaces each changed sidecar in one undoable journal, so `undo` reverts it. `tag rm` removes exactly the tag; `rename` and `merge` act on the tag and the tags below it.
12. **Related editions.** `book.json` gets an optional `related` list (F-GRP-5); `schema` stays 1.

## Consequences

- A crash at any step leaves a journal that explains what happened; `undo` after a recovery works (unit tests per step kind, `JournalTests`).
- The backups cost the size of the changed sidecars per run (about 2 KB per work); retention bounds them to 50 runs.
- A case-only rename on a case-insensitive file system counts as a taken target and is reported.
- `merge`, `split` and `edit` cannot be undone. Making them undoable means recording their sidecar changes with `Replace`.
- The import is not journaled: its source lies outside the library, and the copy-verify-delete order never loses the source.

## Performance of the M2 commands

`tests/perf/measure.py` on the generated reference library (10,000 works, 14,750 files imported, `osx-arm64`, Native AOT; PRD 9.1 has no targets for these commands):

| Command | Time |
|---|---|
| `organize` (dry run, whole library) | 0.47 s |
| `dupes` (list) | 0.29 s |
| `tag list` | 0.01 s |
| `trash empty` (dry run) | 0.01 s |

The M1 targets on the same run: cold start 4.8 ms (median, `--version`), scan without changes 0.80 s, search p95 32.8 ms for typical and 95.5 ms for very common words, initial index 0.92 min. They are lower than the numbers of [ADR 0011](0011-m1-performance.md) (30 ms, 98 ms); the run was on the same machine type but not under the same load, and the difference has not been investigated.
