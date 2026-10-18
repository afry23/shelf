# ADR 0014: Performance of semantic search and embedding in M3

| | |
|---|---|
| **Status** | Accepted (the reading of the latency target in decision 1 is for the owner to confirm) |
| **Date** | 2026-10-18 |
| **Scope** | PRD 9.1 (semantic p95: work < 300 ms, chunk < 800 ms; initial embedding goal < 6 h, resumable), roadmap M3 exit criteria |
| **Inputs** | [ADR 0004](0004-storage-sqlite-fts5-vector-search.md), [ADR 0013](0013-onnx-tokenizer-aot.md), [`docs/slices/m3-close.md`](../slices/m3-close.md), `tests/perf/measure.py` |

## Decision

1. **The latency targets apply to a process that stays up** (`shelf mcp`, later `shelf serve`). A one-shot `shelf semantic` reads and sets up the 470 MB model on every start, about 0.44 s whatever the query; `measure.py` reports that time and does not judge it. Graph optimization levels of ONNX Runtime (`ORT_DISABLE_ALL`, `ORT_ENABLE_BASIC`) were tried and change the start by less than 0.03 s, so nothing was changed.
2. **No quantization, no larger windows.** The initial embedding of the reference library is estimated at 2.1 h against the goal of 6 h. Quantized models (`qint8`) stay an option if a real library is much larger than the synthetic one; it would need a second catalog entry and a re-embed (F-SEM-8 already covers that path).
3. **`vec0` stays the chunk search.** A full scan at the size of the reference library is 170 ms. In-memory indexes and int8 vectors (ADR 0004 hand-off) are not needed.
4. **`reindex` keeps dropping the embeddings**; `reindex --embeddings` refills them. Embeddings are index data (ADR 0004), and keeping them across a rebuild would need a key that survives changed chunks.
5. **The work embedding stays title, description, authors, tags.** Adding the table of contents was considered and left out: AC 4 passes without it, and it would change `semantic_work_hash` for every book.

## Evidence (`osx-arm64`, 10 cores, Native AOT build, model `multilingual-e5-small`)

Library: `generate --works 300 --files 450` (17.9 MB; 442 files, 301 works after grouping), imported with `shelf import`.

| Measure | Result |
|---|---|
| Embedding, everything | 301 work texts + 12,385 passages in 227 s (about 500 % CPU): **56 per second** |
| Passages per file | 28 on average |
| Extrapolation to the reference library (15,000 files, 10,000 works) | about 420,000 passages + 10,000 works = 7,700 s = **2.1 h** (goal: < 6 h). Extrapolated, not run in full; a CI runner with fewer cores will take proportionally longer |
| Resume | Ctrl-C (SIGINT) after 30 s: exit 1, `interrupted: 384 files still wait for embeddings; run the same command again to continue`, 58 files done. The second run exits 0 and embeds the 384 remaining files (197 s). The file in progress at the interrupt is embedded again |
| Semantic p95 in a running `shelf mcp`, 12,385 chunks | work 7 ms, chunk 14 ms (40 requests each, the first request, which loads the model, left out) |
| `vec0` nearest neighbors, k = 50, 430,000 chunk vectors (random unit vectors, same size as the reference library), warm cache | 170 ms per query. With the query embedding (about 10 ms) the chunk level in a running server is about 190 ms, against 800 ms |
| Semantic p95, one-shot process | work 465 ms, chunk 487 ms (model load included) |
| Peak memory of a process with the model loaded | 1.3 GB |
| Other commands (unchanged by M3) | search p95 18 ms, scan 0.03 s, cold start 6 ms on this library |

## Consequences

- The exit criterion "semantic p95 work < 300 ms" is met by a running server and missed by a one-shot process. If the owner wants it for the one-shot CLI, the way is a small resident process or a model file that loads faster (a smaller or quantized model); neither is planned.
- `measure.py --semantic` needs a library with embeddings. `perf.yml` does not run it: the full embedding takes hours.
- The 1.3 GB resident size is mostly the model file; a long-running `shelf mcp` holds it from the first `semantic_search` on.
