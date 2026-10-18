# Slice m3-vector-store: Vectors in the Index

| | |
|---|---|
| **Slice ID** | `m3-vector-store` |
| **Branch** | `feat/m3-semantic-core` (same branch as slices 1 and 2, separate commit) |
| **Milestone** | M3 – Semantics & MCP |
| **Roadmap** | M3 slice 3 |
| **PRD references** | F-SEM-4; 10.3 (`vec_works`, `vec_chunks`); 9.3 (WAL, parallel processes) |
| **Inputs** | [ADR 0004](../adr/0004-storage-sqlite-fts5-vector-search.md) (hand-off M3), the spike code in `70ad12d^:experiments/storage/` |

---

## 1. Scope

**In scope**

1. `SqliteVecExtension` (`Shelf.Index`): loads `vec0.dylib` / `vec0.so` from `AppContext.BaseDirectory` by absolute path; a missing or incompatible library is `false`, not an exception (ADR 0004 V5).
2. `tools/fetch-sqlite-vec.sh` and `tools/sqlite-vec.sha256`: sqlite-vec v0.1.9 from the official release, pinned SHA-256, into `vendor/sqlite-vec/<rid>/` (git-ignored). `Shelf.Index.csproj` copies the file next to every executable that references it, also on publish. CI fetches it before build and before the AOT publish.
3. `VectorStore` (`Shelf.Semantic`): `Put` (replaces), `Delete`, `Count`, `Nearest` (cosine distance, nearest first) for works (`vec_works`, key `works.seq`) and passages (`vec_chunks`, key `chunks.id`); `Reset`.
4. Two backends behind one API: sqlite-vec virtual tables (`vec0`, `distance_metric=cosine`) and, when the library is not there, plain tables with float32 BLOBs searched by a scan in C# (`TensorPrimitives.CosineSimilarity`, top-k with a heap).
5. `semantic_meta` records backend, model and dimension. `LibraryReindexer.Rebuild` removes the vector tables.

**Out of scope**

- Filling the tables (embedding pipeline): `m3-embedding-index`. Removing vectors of deleted passages after a scan: same slice.
- Quantization and the chunk-level budget at 1.5-2.5 million passages: decided in `m3-close` after measuring (ADR 0004).
- Re-embedding when the model changes (F-SEM-8): `m3-hybrid`; `Reset` and `IsCurrentModel` are its building blocks.

## 2. Feature Files

`Features/Semantic/VectorStore.feature` (`@F-SEM-4 @M3`, 13 scenarios and outlines, 26 cases over both backends): nearest vectors with exact cosine distances, works and passages kept apart, replacing, deleting, `k` larger than the store, empty store, wrong dimension (3 rows), reopening, plain fallback, an sqlite-vec index without the library, model bookkeeping (3 rows), `reindex` with both backends and without the library.

The sqlite-vec rows are skipped when `vec0` is not next to the test assembly (no `tools/fetch-sqlite-vec.sh` run).

## 3. Design

- **Why the tables are not in `IndexSchema`.** A virtual table of `vec0` cannot even be dropped without the module. `IndexSchema` is dropped and rebuilt on a version change; with the vector tables inside, a machine without `vec0` could not repair its own index. So `VectorStore` creates them (`CREATE ... IF NOT EXISTS`-style, on first open) and `IndexSchema.Version` stays 2: existing indexes keep working.
- **The backend is chosen once and remembered.** First open: sqlite-vec if it loads, else plain. After that the stored choice rules. Opening an sqlite-vec index without the library is an error that names the way out (`shelf reindex`), not a silent switch to an empty table.
- **`reindex` removes the vectors.** Rebuilding gives passages new ids, so the old vectors would point to other text. If the index has sqlite-vec tables and the library cannot be loaded, the index file is replaced (it is a cache, and the vectors would be dropped anyway). Consequence for the owner: a plain `shelf reindex` throws the embeddings away; they come back with `reindex --embeddings` (slice 4).
- **Replace is delete + insert** in one transaction: `vec0` has no upsert.
- **The distance is the cosine distance in both backends**, so scores do not change when the backend does. A unit test compares 300 random vectors, 5 queries, top 10, between the two.
- **Native AOT:** a probe published with `-r osx-arm64` loads `vec0.dylib`, stores two vectors and finds them (`SqliteVec 1:0.0000, 2:0.2929`); `vec0.dylib` is in the publish folder of `shelf`.

## 4. Unit Tests

`VectorStoreTests`: backends agree on 300 vectors; another model is refused until `Reset`; another dimension counts as another model; the codec writes little-endian float32.

## 5. Open Questions

None blocking. For the owner, later: `reindex` throwing the embeddings away is cheap to change only by keeping vectors in a file that survives the rebuild (for example keyed by the SHA-256 of the file and the chunk ordinal). Not done: a 6-hour re-embedding after every `reindex` would then be avoidable. Decide when the embedding pipeline exists.
