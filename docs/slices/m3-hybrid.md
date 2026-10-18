# Slice m3-hybrid: Hybrid Search and Switching the Model

| | |
|---|---|
| **Slice ID** | `m3-hybrid` |
| **Branch** | `feat/m3-semantic-core` (same branch as slices 1 to 5, separate commit) |
| **Milestone** | M3 – Semantics & MCP |
| **Roadmap** | M3 slice 6 (Should items) |
| **PRD references** | F-SEM-5, F-SEM-8; 7.1; Q3 (model stays replaceable) |
| **Inputs** | [`m3-semantic-search`](m3-semantic-search.md), [`m3-embedding-index`](m3-embedding-index.md) |

---

## 1. Scope

**In scope**

1. `shelf semantic <words> --hybrid [--chunks]`: full-text results (`LibrarySearch`) and vector results fused with Reciprocal Rank Fusion, `k = 60`.
2. `Rrf.Fuse` and `HybridSearch` (`Shelf.Semantic`); the MCP tool `semantic_search` can offer the same.
3. JSON gets `"mode": "hybrid"` or `"semantic"`.
4. F-SEM-8: `reindex --embeddings` notices that the index holds vectors of another model or dimension (`semantic.model` was changed), says so, queues every file again, starts the vector store over and embeds everything with the new model. Stopping in the middle and running again continues with the new model.
5. `E5Embedder.Dimensions` is read from the model (384 for small, 768 for base, 1024 for large), so a larger model of the e5 family needs only a catalog entry.
6. `semantic` with vectors of another model names both models and the command that makes them again.

**Out of scope**

- A default for `--hybrid` in the config: the PRD asks for a toggle in the web UI (M4), not for a CLI default.
- A second real model in the catalog. bge-m3 (PRD Q3) needs CLS pooling and no prefixes; e5-base would work with the existing embedder but its license, revision and checksums are not checked (ADR 0001 covers only small and bge-m3). The mechanism is tested with two fake models.
- Filters in hybrid queries (see Design).

## 2. Feature Files

`Features/Semantic/HybridSearch.feature` (`@F-SEM-5 @M3`, 12 cases): a prefix that only full text understands (and the counter case: semantic search alone does not rank it first), a work found by both comes first with score 1, scores fall and stay in 0..1, passages fused by place, three kinds of rejected filter syntax, no embeddings (full text alone, with the note), model missing, `"mode"` in JSON.

`Features/Semantic/ModelSwitch.feature` (`@F-SEM-8 @M3`, 7 cases): another model embeds everything again (and the warning, and 24 instead of 16 dimensions), a search after the switch, a second run with nothing to do and no warning, a stopped switch that continues, going back to the first model, a search before the switch refuses to mix, an unknown model is named.

## 3. Design

- **Works are fused by id, passages by place (file and locator).** Full text cuts a file into pages and chapters, the vectors into windows of chunks; only the place is common. The full-text snippet (with the matches in square brackets) wins over the vector snippet.
- **Scores are scaled to 0..1.** A hit at the top of every list that has entries scores 1. An empty list (a natural-language question finds nothing in full text, where all words must be in one passage) does not lower the others: a German question gives the same scores with or without `--hybrid`.
- **Filters stay in `shelf search`.** The vector side cannot apply `author:` or `year:`; mixing hits that were filtered with hits that were not would be wrong. `--hybrid` with a filter or `-word` is a usage error (exit 2).
- **Candidates:** the larger of 50 and three times the limit from each side before fusing.
- **Without embeddings** hybrid is full-text search with the usual note; the model is not loaded.
- **Switching is automatic, not interactive.** The warning goes to stderr and the run starts. The command is the explicit act (`reindex --embeddings` after the config change); a prompt would break scripts and the long run is resumable. `RequeueAll` runs before `VectorStore.Reset`: a crash in between leaves the old model in the store and every file `pending`, so the next run does both again.

## 4. Unit Tests

`RrfTests`: a key at the top of every list scores 1, `1/(k+rank)` sums, two lists beat one first place, ties keep first-seen order, an empty list does not change the others, no lists.

## 5. First numbers

Native AOT, `osx-arm64`, real model, 5 fixture files: `shelf semantic "Flatland dimensions" --hybrid --chunks` finds *Flatland* first with score 0.99 and the matches in the snippet; the German question "Wesen in einer flachen Welt" with `--hybrid` ranks *Flatland* first (full text had nothing to add).

## 6. Open Questions

None. For later (M5 backlog, PRD Q3): the catalog entry and pooling for bge-m3.
