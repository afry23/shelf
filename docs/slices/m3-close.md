# Slice m3-close: Closing M3

| | |
|---|---|
| **Slice ID** | `m3-close` |
| **Branch** | `feat/m3-semantic-core` (same branch as slices 1 to 8, separate commit) |
| **Milestone** | M3 – Semantics & MCP |
| **Roadmap** | M3 exit criteria |
| **Inputs** | [ADR 0013](../adr/0013-onnx-tokenizer-aot.md), [ADR 0014](../adr/0014-m3-semantic-performance.md), `.github/workflows/build.yml`, `tests/perf/measure.py` |

---

## 1. Scope

**In scope**

1. `tests/e2e/native.sh`: an M3 part. The MCP handshake and `tools/list` (nine tools), `search_books`, and clean stdout (every line is one JSON-RPC message, standard error empty) always run. With `SHELF_E5_DIR` it also runs `model install <folder>`, `reindex --embeddings`, `semantic` with a German query for the English book, `--chunks --hybrid --json` with a locator, and `semantic_search` through `shelf mcp`. The script keeps standard input open for 6 s, because the server ends when its input ends.
2. CI: `tools/fetch-e5-model.sh` downloads the two model files at the pinned revision and checks the SHA-256 (same values as `ModelCatalog`). The `test` and `aot` jobs cache the folder and set `SHELF_E5_DIR`, so the real-model scenarios run on `linux-x64` and `osx-arm64` instead of being skipped. The `aot` job publishes the real binary with MCP and ONNX on `linux-x64`.
3. `tests/perf/measure.py --semantic`: semantic p95 (work, chunk) for a one-shot process and for a running `shelf mcp`.
4. Measurements and [ADR 0014](../adr/0014-m3-semantic-performance.md); roadmap status and result of M3.

**Out of scope**

- The tag `v0.3.0`: set by the owner after the merge.
- A full embedding run of the reference library (about 2 h): extrapolated from a sample, see ADR 0014.
- `perf.yml` does not run `--semantic`.

## 2. Feature Files

None. The end-to-end script checks the same behavior as the features through the native binary.

## 3. Design

- The native script is the only place that starts `shelf mcp` from a shell; the feature `McpAcceptance` does the same through the test host.
- Model files are never committed; the fetch script is the only second place that knows the URLs and hashes of the catalog.

## 4. Unit Tests

None new.

## 5. Open Questions for the Owner

1. Is the 300 ms target for a running process (as assumed in ADR 0014), or also for the one-shot CLI?
2. `shelf` with an empty `HOME` throws an `ArgumentException` in `Program.cs` (found by a process test; not fixed here).
