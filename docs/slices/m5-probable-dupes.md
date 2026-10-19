# Slice m5-probable-dupes: Probable Duplicates

| | |
|---|---|
| **Slice ID** | `m5-probable-dupes` |
| **Branch** | `feat/m5-probable-dupes` (stacked on `feat/m5-similar`) |
| **Milestone** | M5 – Polish |
| **Roadmap** | M5 slice 3 |
| **PRD references** | F-DUP-3 (probable duplicates across works), F-DUP-6 ("not a duplicate" also here), F-GRP-6 (thresholds in `config.toml`), 7.2, 8.2 |
| **Inputs** | slices `m1-grouping`, `m2-dupes`, `m5-similar`; [ADR 0014](../adr/0014-m3-semantic-performance.md) (e5 scores) |

---

## 1. Scope

**In scope**

1. `shelf dupes --probable [--interactive] [--json]`: pairs of **different works** that probably are one book, for manual review.
2. Two ways to be a pair: the SimHash Hamming distance of a file of each work is at most `grouping.probable_hamming` (default 3), or the cosine similarity of the work vectors is at least `grouping.probable_cosine` (default 0.95).
3. "Not a duplicate" (F-DUP-6) for a pair: written to both sidecars as the ID of the other work, like the marks of the groups.
4. The MCP tool `find_probable_duplicates` (read-only, offered with `mcp.read_only`), `GET /api/v1/duplicates/probable`, `POST /api/v1/duplicates/probable/ignore` and a "Probable duplicates" section in the duplicates view.

**Out of scope**: moving files to the trash on a guess (the interactive mode offers `n`, `s`, `q` only), merging works, chunk-level similarity, pairs inside one work (those are `same-format` groups already).

## 2. Feature Files

- `Features/Duplicates/ProbableDuplicates.feature` (`@F-DUP-3 @F-DUP-6 @F-GRP-6 @M5`): same text, identical files are not repeated, distance setting (boundary at 3), JSON, marking, skipping, only `n`/`s`/`q`.
- `Features/Duplicates/ProbableDuplicatesSemantic.feature`: the vectors (fake embedder), threshold as setting (0.05 / 0.8 / 0.95 / 1.0), no embeddings → note, marking by vectors.
- `Features/Mcp/McpProbableDuplicates.feature`, `Features/Web/WebProbableDuplicates.feature`, `Features/Web/UiProbableDuplicates.feature` (`@ui`).
- `Settings.feature` knows the two new keys; `McpServer.feature` and `McpReadOnly.feature` list the new tool.

## 3. Design

- `LibraryDuplicates.FindProbable` (`Shelf.Index`): fingerprints of all files against each other, the smallest distance per pair of works wins. Pairs whose works share a file with the same SHA-256 are left out (`Find` lists those as identical), and so are pairs that are marked as not duplicates.
- Pairs of fingerprints are found by **blocks**: two fingerprints that differ in at most k bits agree on one of k + 1 blocks of bits, so only fingerprints that share a block are compared (for a setting above 7 every pair is compared). This took the reference library from 23 s (all pairs, debug build) to 0.15 s.
- `SemanticSearch.SimilarPairs` (`Shelf.Semantic`): all work vectors are read once, normalised and compared in memory (`TensorPrimitives.Dot`); the cost grows with the square of the number of works (10,000 works: about 50 million dot products of 384 floats). It does not need the model.
- `ProbableDuplicates.Find` joins both and adds the coverage note ("no embeddings yet; run: shelf reindex --embeddings"). Without embeddings only the texts decide.
- A distance above the setting is not reported for a pair that was found by its vectors: "text distance 31" would say the opposite of what the pair is.
- The marking reuses `DuplicateSession.MarkNotDuplicates`; the web action looks the pair up again under the write lock (409 when it is no probable pair any more).
- Text, JSON (`pairs[]` with `hamming`, `cosine`, `a`, `b`), MCP and web carry the same facts.

## 4. Calibration of the SimHash distance

Measured with the test library generator: a 1000-word text with n words replaced by another word.

| words replaced | 1 | 3 | 5 | 10 | 20 | 50 | 100 | 200 | 400 |
|---|---|---|---|---|---|---|---|---|---|
| Hamming distance | 0 | 2 | 1 | 3 | 9 | 10 | 20 | 31 | 31 |

So the PRD's start value 3 finds a second edition with about 1 % of the words changed and nothing that differs more. Wider settings find more, with more false pairs; 31 is what two unrelated texts reach.

## 5. Result

- BDD and unit tests green (2567 tests; 26 of them need `SHELF_E5_DIR` and are skipped, as before).
- Reference library (10,011 works), vector part not run (no embeddings): `shelf dupes --probable` 0.15 s, 5 pairs, all distance 3.
- The 5 pairs are the near duplicates that the generator plants.

## 6. Open Questions

- **The cosine threshold is not calibrated.** 0.95 is the PRD's start value; it needs the real model (`SHELF_E5_DIR`, `tools/fetch-e5-model.sh`) on a library with embeddings: the fake embedder in the scenarios only shows the mechanism. To be done with the full embedding run in `m5-close`; the result goes into ADR 0018.
- The cost of `SimilarPairs` on 10,000 works with real 384-dimension vectors is estimated, not measured (same run).
- `find_probable_duplicates` is a tool beyond the nine of PRD 7.2 (`find_duplicates` keeps its meaning); the PRD table should list it when M5 is closed.
