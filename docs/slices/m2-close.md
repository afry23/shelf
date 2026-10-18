# Slice m2-close: Closing M2

| | |
|---|---|
| **Slice ID** | `m2-close` |
| **Branch** | `feat/m2-close` (stacked on `feat/m2-related`) |
| **Milestone** | M2 – Order |
| **Roadmap** | M2 exit criteria, cross-cutting rules (performance, data safety, `--json`) |
| **Inputs** | [ADR 0011](../adr/0011-m1-performance.md), `.github/workflows/build.yml`, `tests/perf/measure.py` |

---

## 1. Scope

**In scope**

1. `tests/e2e/native.sh`: the end-to-end check of the native binary, now shared by the CI `aot` job and local runs. It covers M1 (import, search, edit, reindex, scan) and M2 (organize and undo by hash, tags, dupes, the trash).
2. The Native AOT binary built locally on `osx-arm64` and checked with the script; the CI runs it on `linux-x64` and `osx-arm64`.
3. `tests/perf/measure.py` times the M2 commands (informative, no target).
4. `JournalRetention`: the newest 50 journals and their backups are kept.
5. [ADR 0012](../adr/0012-m2-journal-trash-and-pattern.md), the roadmap status of M2, and the PRD (command list, `related`).

**Out of scope**

- The tag `v0.2.0`: it is set by the owner after the merge.
- The `linux-x64` run of the perf job: `perf.yml` does it on `main`.

## 2. Feature Files

None. The end-to-end script checks the same behavior as the features through the native binary.

## 3. Design

- The script uses `shasum -a 256` (macOS and Linux) and compares the library before and after with files and folders, without the index and the journal.
- `grep -q` after a pipe is safe here: the outputs are short.
- Retention runs in `FileJournal.Begin`, after recovery and before the new journal exists.

## 4. Unit Tests

`JournalRetentionTests`: only the newest journals and their backups are kept, the last runs can still be undone, a running journal is never deleted, no journal folder.

## 5. Open Questions

None for the owner.
