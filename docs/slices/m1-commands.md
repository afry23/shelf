# Slice m1-commands: show, edit, merge, split, review, open, stats

| | |
|---|---|
| **Slice ID** | `m1-commands` |
| **Branch** | `feat/m1-commands` (stacked on `feat/m1-search`) |
| **Milestone** | M1 – Foundation |
| **Roadmap** | M1 area "CLI"; carried over from `m1-spec-infra`: color output and the `NO_COLOR` scenarios |
| **PRD references** | F-GRP-4, F-IMP-7, F-LIB-5, 7.1 (commands, `--json`, exit codes, colored tabular output, `NO_COLOR`) |
| **Inputs** | `WorkSidecar`/`SidecarStore` (`m1-model-sidecar`), `LibraryScanner` (`m1-import-scan`), `ReviewQueue` (`m1-grouping`), `CliEnvironment` (`m1-spec-infra`) |

---

## 1. Scope

**In scope**

1. `shelf show <work>`: all fields of the sidecar, the folder, the files with size and reading position; `--json`.
2. `shelf edit <work> [--title --subtitle --author* --year --language --isbn --publisher --series --series-index --edition --description --tag* --add-tag* --remove-tag* --rating --status]`: writes the sidecar; an empty value (`""`) removes an optional field.
3. `shelf merge <work-a> <work-b>` (F-GRP-4): the files and the missing metadata of b go into a; b is removed.
4. `shelf split <work> <file>` (F-GRP-4): a file becomes a work of its own, described by its own metadata.
5. `shelf review` (F-IMP-7): lists the queue; `review accept <entry> [--into <work>]` merges the work of a grouping entry into a candidate; `review dismiss <entry>` settles an entry (a grouping entry is remembered as "not duplicates").
6. `shelf open <work> [--format pdf]`: hands the file to the system's application.
7. `shelf stats`: works, files, size, authors, tags, formats, read status, languages, review queue.
8. Color output (bold headings, dim labels and IDs) on a terminal without `NO_COLOR`; never for JSON; `search` uses it too and now shows the work ID of each result, so that the other commands can be given one.

**Requirements covered:** F-GRP-4, F-IMP-7 (the `review` command), F-LIB-5 (editing), PRD 7.1.

**Out of scope**

- Renaming or moving the work folder when title or author change (M2, with the file-operation journal).
- Using "not duplicates" marks between works in the grouping cascade: they are recorded in both sidecars (`notDuplicates.works`); the cascade still looks only at file marks. Left to the duplicates work of M2.
- Interactive review (prompts). The commands work without a terminal; an interactive front end can sit on top.

## 2. Feature Files

`Features/Commands/`: `Show.feature`, `Edit.feature`, `Merge.feature`, `Split.feature`, `Review.feature`, `Open.feature`, `Stats.feature` (empty library, missing index), `StatsNumbers.feature` (three works, four files, two review entries), `Color.feature` (terminal, `NO_COLOR` set and empty, no terminal, JSON). One scenario in `Search.feature` for the ID in the result line.

## 3. Design

- **A reference to a work** is the ULID or the start of it (case-insensitive, must be unique) or the path of the work folder or of a file in it, relative to the working folder or to the library root; `.` inside a work folder is that work. Resolved through the index (`WorkLookup`).
- **Disk first, then reconcile.** Every change is made on disk (files, then sidecars, the source of truth) and the index is brought in line by the scan that already exists (`LibraryScanner`), plus a refresh of the review mirror. No second code path writes work rows. The order of steps leaves a state a scan understands if the process stops in between: files move before the sidecars name them, a sidecar is removed last.
- **Merge rules.** The target keeps its own fields; empty fields (subtitle, language, year, ISBNs, publisher, series, edition, description, rating) come from the source; tags are united (sorted); the further read status wins; the earlier "added" wins; files keep their reading position; a name that is taken gets `name (1).ext`. The source sidecar is deleted, its folder (and empty parents) too, unless it holds files shelf does not know (warning, folder kept). Review entries are followed: IDs and file paths are re-pointed; a grouping entry about the pair itself is settled.
- **Split rules.** The new work gets the metadata that the file itself carries (`BookExtractor.ReadMetadata`, file name as the fallback), a folder `Author/Title (Year)` (numbered if taken), the reading position of the file, no tags. Both sidecars record the other work in `notDuplicates.works`. A work with one file cannot be split.
- **Review.** Entries are named by position or by the start of their ID. `accept` needs a grouping entry with a work; the target is `--into` or the best candidate. `dismiss` on a grouping entry marks the work and each candidate as not duplicates in both sidecars. The queue file stays the source of truth; the list needs no index (titles are looked up if there is one).
- **Edit validation** happens before the library is opened and is a usage error (exit 2): year 1-9999, rating 1-5, status unread/reading/read, ISBN with a valid check digit (ISBN-10 also sets the ISBN-13), no empty title, author or added tag, at least one option. Unknown work, ambiguous reference, nothing to merge: exit 1.
- **Open** goes through `IFileOpener` in `CliEnvironment` (`open`, `xdg-open`, the shell on Windows); the specs install a recording opener, so no scenario starts an application.
- **Color.** `Palette` (bold, dim) is on when `CliEnvironment.UseColor` (a terminal and no non-empty `NO_COLOR`) and the output is not JSON. Padding is computed on the plain text, so the text is identical with and without color (a scenario strips the sequences and compares).
- No ADR: nothing here reverses or adds an architectural decision.

## 4. Unit Tests

`LibraryEditorTests` (lookup by ID start, paths, `.`, labels; merge: renames, unions, kept folder, itself, review queue; split: sidecars, path form, rejections; mark; update; review removal; statistics), `TextFormatTests` (sizes, plural, `Palette`), `ArgumentSplitterTests` (single-quoted arguments, which the specs use to pass arguments with spaces and empty ones).

## 5. Test Infrastructure

- `ArgumentSplitter`: `'...'` at the start of an argument groups text (and gives an empty argument as `''`), because the step's own string cannot contain double quotes.
- `CliSteps`: a failed exit code check prints standard error and output; a failed pattern check prints the output; `RecordingFileOpener`.

## 6. Open Questions

None for the owner.
