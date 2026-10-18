# Slice m3-semantic-search: `shelf semantic`

| | |
|---|---|
| **Slice ID** | `m3-semantic-search` |
| **Branch** | `feat/m3-semantic-core` (same branch as slices 1 to 4, separate commit) |
| **Milestone** | M3 – Semantics & MCP |
| **Roadmap** | M3 slice 5; acceptance criterion 4; exit criterion "semantic search p95: work level < 300 ms, chunk level < 800 ms" (measured in `m3-close`) |
| **PRD references** | F-SEM-1, F-SEM-2, F-SEM-3; 7.1 (`semantic <text> [--chunks] [--json]`); AC 4 |
| **Inputs** | [`m3-embedding-index`](m3-embedding-index.md), [`m3-vector-store`](m3-vector-store.md) |

---

## 1. Scope

**In scope**

1. `shelf semantic <text>... [--chunks] [--limit 1-1000] [--library] [--json]`: works by default, passages with `--chunks`. Several words need no quotes.
2. `SemanticSearch` (`Shelf.Semantic`): `SearchWorks`, `SearchPassages`, `Coverage`. The MCP tool `semantic_search` (slice 8) calls the same functions.
3. Text output in the format of `search` (`N. Title - Authors (year) [formats]  id  score`), with a second line per passage: `place  file: start of the text`.
4. JSON: `query`, `level` (`work` or `chunk`), `total`, `pendingFiles`, `worksWithoutEmbedding`, `results[]` with `id`, `title`, `authors`, `year`, `language`, `formats`, `score`; passages add `file`, `format`, `locator`, `where`, `snippet`.
5. Notes on stderr when the index is incomplete: `no embeddings yet; run: shelf reindex --embeddings`, `1 file still waits for its embeddings; ...`, `N works have no embedding yet; ...`. Results and exit code (0) are not affected.
6. `VectorStore.OpenExisting`: reads an index as it is (model and dimension from `semantic_meta`) on a `query_only` connection; null when there are no vectors.

**Out of scope**

- Hybrid ranking with full-text results and the `--hybrid` switch: `m3-hybrid`.
- Filters (`author:`, `tag:`) in semantic queries.

## 2. Feature Files

`Features/Semantic/SemanticSearch.feature` (`@F-SEM-1 @F-SEM-2 @F-SEM-3 @M3`, 22 cases, fake embedder): works by the words of title, description and tags, scores between 0 and 1 and best first, the query embedded as a query, passages with page or section, JSON of passages, limits (5 + 2 invalid), empty query, no embeddings, files waiting for embeddings, no download during a search, model missing, vectors of another model.

`Features/Semantic/SemanticAcceptance.feature` (`@AC-4 @M3`, 5 cases, **real model**, skipped without `SHELF_E5_DIR`): four German questions find the English book they are about as the first hit, in a library of four English books ("Bücher über Messaging zwischen Microservices" gives *Enterprise Integration Patterns*); a German question about asynchronous messages finds the right passage with its file. The real model is linked into the library with a symbolic link, not copied.

## 3. Design

- **A search needs no network and does not install.** A missing model is an error with the command that installs it; downloading happens only in `model install` and `reindex --embeddings`.
- **No model, no embeddings: no error.** If the index holds no vectors, `semantic` prints `No results.` and the note without loading the model.
- **Passages of files that wait are left out.** The vectors of a file that a scan replaced may still belong to the old text; only files with `embed_status = 'done'` are shown. `Coverage` reports how many wait.
- **One hit per chunk.** The best of its windows wins; the snippet is the start of that window (240 characters, cut at a word). The place comes from the chunk (`page=2` is "page 2", a CFI is "section N").
- **Score = 1 − cosine distance.** e5 scores are compressed (unrelated texts 0.7 to 0.8, see ADR 0013), so the score orders hits but says little as a number; there is no threshold.
- **Vectors of another model** (set `semantic.model` after embedding) are an error that names both models.

## 4. Unit Tests

`VectorStoreTests`: no store to open in an index without vectors; an existing store is read with its own model on a `query_only` connection.

## 5. First numbers (not the exit criterion)

Native AOT binary, `osx-arm64`, the 5 files of `tests/fixtures/upstream`, real model: work-level `shelf semantic` takes **0.48 s** for the whole process (0.65 s for the first run after a rebuild), passage level 0.50 s. Almost all of it is loading the 450 MB model into ONNX Runtime, so a one-shot command cannot meet "work level p95 < 300 ms including query embedding" on this path. A long-running process (`shelf mcp`, later `serve`) loads the model once. `m3-close` measures both and decides: keep the target for the long-running case and state a cold-start figure for the CLI, or speed up the load (memory-mapped weights, a quantized model).

## 6. Open Questions

For the owner, in `m3-close`: is "p95 < 300 ms" meant for the one-shot CLI or for the server processes? The answer decides whether the model load must shrink.
