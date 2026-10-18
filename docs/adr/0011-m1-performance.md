# ADR 0011: Performance of the CLI in M1

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-10-18 |
| **Scope** | PRD 9.1 (search p95 < 100 ms, scan < 3 s, initial index < 15 min, cold start < 150 ms), roadmap M1 exit criteria |
| **Inputs** | [ADR 0004](0004-storage-sqlite-fts5-vector-search.md), [`docs/slices/m1-ci-perf.md`](../slices/m1-ci-perf.md) |

## Decision

1. **Measure with the real binary.** `tests/perf/measure.py` times whole processes (start included) of the Native AOT `shelf` on the generated reference library. `perf.yml` runs it on `linux-x64`.
2. **Two query classes.** Typical queries must have p95 < 100 ms; words that are in nearly every passage (stop words and the like) < 250 ms. The 100 ms target of PRD 9.1 is read as the typical case; the common-word case is reported, not hidden.
3. **Cheap starts for readers.**
   - A config file that holds only comments and blank lines is an empty document without starting the TOML library.
   - A reader connection does not set the journal pragmas (they persist in the file) and maps up to 1 GiB (`mmap_size`).
4. **Search ranks first, joins second.** Without filters or exclusions, FTS5 selects the best 500 passages and only these are joined.
5. **No `InvariantGlobalization`.** Tried for start-up time: no gain on `osx-arm64`, and it changes Unicode normalization on Linux, which title normalization and the search keys depend on.

## Consequences

- p95 of typical queries is 98 ms on the development machine, close to the limit: the 61 ms median is half process start (30 ms) and half index open and query. A shared CI runner is judged with factor 2.
- A query with a filter still joins every match before ranking (`year:>2010 data` 112 ms, `format:pdf network` 135 ms); acceptable for now, first candidate if the common-word class must come down.
- Stop-word handling and a smaller FTS5 `detail` are open for M5.
