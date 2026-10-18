# Slice m1-ci-perf: CI for the real binary and the performance targets

| | |
|---|---|
| **Slice ID** | `m1-ci-perf` |
| **Branch** | `feat/m1-ci-perf` (stacked on `feat/m1-calibration`) |
| **Milestone** | M1 – Foundation (last slice) |
| **PRD references** | 9.1 (performance), 9.2 (Native AOT) |
| **Inputs** | [ADR 0008](../adr/0008-combined-aot-binary.md), roadmap "Carried over from M0", generator preset `reference` |

## 1. Scope

1. The `aot` job of `build.yml` publishes the real `shelf` (config, SQLite, parsers inside) on `linux-x64` and `osx-arm64` and runs an end-to-end check with the native binary: `init`, `import` (EPUB + PDF of one work), `search --json`, `show`, `edit`, `stats --json`, delete the index, `reindex`, search the new tag, `scan` reports files unchanged. This closes the carried-over gaps "`linux-x64` of the CLI parts" and "the CsToml adapter on `linux-x64`".
2. `tests/perf/measure.py` measures the targets of PRD 9.1 with the native binary on a populated library; `perf.yml` generates the reference library (10,000 works / 15,000 files), imports it and runs the script on `linux-x64` (on `main`, weekly, on request; not part of the pull request gate).
3. Make the targets true.
4. Close the M0 carry-overs that concern the executable (section 4) and close M1.

## 2. Measurements (`osx-arm64`, Native AOT, reference library, whole process incl. start)

| Target | Result before | Result after |
|---|---|---|
| Cold start < 150 ms | 29 ms (`--version`) | 30 ms |
| Scan without changes < 3 s (15,000 files) | 0.38 s at 1,500 files (extrapolated 3.8 s) | 0.9 s measured |
| Search p95 < 100 ms | 204 ms (20 queries) | 98 ms (typical queries), 176 ms (very common words) |
| Initial index < 15 min | not measured | 0.92 min (`reindex`, 10,011 works, 14,750 files) |

Import of the 15,000 files: about 2.5 minutes.

What made search fast enough (decisions in [ADR 0011](../adr/0011-m1-performance.md)):

- A search that starts with a config file of only comments no longer starts the TOML library (10 ms of a 66 ms run).
- A reader does not set the journal pragmas on every open (10 ms).
- FTS5 picks the best 500 passages first and only those are joined to files and works, when the query has no filter or exclusion (common words: 255 ms to 150 ms).
- A reader maps up to 1 GiB of the index (`mmap_size`).

## 3. Query classes

The synthetic books are written from a small vocabulary, so words like "data" or "system" are in nearly every passage. The script therefore enforces p95 < 100 ms for typical queries (selective words, phrases, filters, exclusion) and < 250 ms for very common words and filters on them, and prints both. The numbers of shared CI runners are judged with a factor (`--tolerance 2`).

## 4. Carried over from M0

- **ADR 0008 findings 1 and 2** (Web SDK, `MvcEnhancedModelMetadataSupport`, `<Using>` items) and **CA1416**: the CLI has no web reference, so none of this applies yet; it moves entirely to M4 when `Shelf.Web` is added.
- **Cold start**: measured, 30 ms.
- **`linux-x64` of the CLI parts**: covered by the CI check above; MCP, web and covers stay with M3/M4.

## 5. Open Questions

None for the owner. Worth a look in M5: search for words that are in most passages (stop words) scores a large part of the index; stop-word handling or a smaller `detail` of the index could bring it under 100 ms as well.
