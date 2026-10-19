# Slice m5-similar: Similar Books

| | |
|---|---|
| **Slice ID** | `m5-similar` |
| **Branch** | `feat/m5-similar` (stacked on `feat/m5-search-fuzzy-stem`) |
| **Milestone** | M5 – Polish |
| **Roadmap** | M5 slice 2 |
| **PRD references** | F-SEM-6 ("similar books" for a given work), 7.2 (`similar_books`), 8.2 (work view) |
| **Inputs** | [ADR 0004](../adr/0004-storage-sqlite-fts5-vector-search.md) (`vec_works`), slices `m3-vector-store`, `m3-embedding-index` |

---

## 1. Scope

**In scope**

1. `shelf similar <work> [--limit N] [--json]`: the works nearest to the work, best first, without the work itself, with a cosine score.
2. The MCP tool `similar_books(id, limit)`, read-only, also offered with `mcp.read_only`.
3. `GET /api/v1/works/{id}/similar?limit=N` and a "Similar books" section in the work view (shown only when there are results).

**Out of scope**: similarity of passages, a minimum score (the list is a ranking, not a verdict; F-DUP-3 is where thresholds matter), grouping by series, a settings key for the number of works.

## 2. Feature Files

- `Features/Semantic/SimilarBooks.feature` (`@F-SEM-6 @M5`): order and self-exclusion, no model call, JSON, `--limit`, ID prefix and folder reference, unknown and ambiguous references, a work without vector, a library without embeddings.
- `Features/Mcp/McpSimilar.feature` (`@PRD-7.2 @F-SEM-6 @M5`); the tool lists in `McpServer.feature` and `McpReadOnly.feature` now include `similar_books`.
- `Features/Web/WebSimilar.feature`, `Features/Web/UiSimilar.feature` (`@ui`).

## 3. Design

- **No model.** The vector of the work is read from `vec_works` (`VectorStore.Get`) and used as the query, so `similar` works offline and never loads ONNX Runtime (a one-shot `shelf similar` stays near the cold start of `search`).
- `SemanticSearch.SimilarWorks` asks for `limit + 1` neighbours and drops the work itself (by `seq`), so a work with an exact twin still shows the twin. It returns `null` when the work has no vector; `SemanticSearch.SimilarNote` turns that and an incomplete index into the same kind of note as `semantic` ("no embeddings yet", "this work has no embedding yet", "N files still wait ...").
- One service method, `LibraryService.SimilarBooks`, used by MCP and the web API (limit 1 to 100); the CLI reads the index directly like `semantic` does (limit 1 to 1000).
- Work level only. The work vector is made from title, subtitle, description and tags (`WorkText`), so "similar" means "described alike", not "written alike".

## 4. Result

- BDD and unit tests green (2509 tests plus the new `VectorStore.Get` test).
- Not measured on the reference library yet; the cost is one `Nearest` over `vec_works` (ADR 0014: 7 ms at work level in a running process).

## 5. Open Questions

- Whether works of the same series or the same group of editions should be shown first or left out. Not decided; the list shows what the vectors say.
