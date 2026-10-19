# Slice m5-search-fuzzy-stem: Fuzzy Search and Stemming

| | |
|---|---|
| **Slice ID** | `m5-search-fuzzy-stem` |
| **Branch** | `feat/m5-search-fuzzy-stem` |
| **Milestone** | M5 – Polish |
| **Roadmap** | M5 slice 1 |
| **PRD references** | F-SRC-4 (fuzzy search for typos in title and author via trigrams), F-SRC-6 (stemming German and English) |
| **Inputs** | [ADR 0004](../adr/0004-storage-sqlite-fts5-vector-search.md) (FTS5, weights), [ADR 0011](../adr/0011-m1-performance.md) (search p95 98 ms: the budget) |

---

## 1. Scope

**In scope**

1. **Stemming of the metadata** (title, subtitle, authors, description, tags) by the language of the work: English (Porter2) and German (Snowball). A work in another language, or without a language, is indexed unstemmed.
2. **Fuzzy fallback**: if the exact search finds no work, the words of the query are compared by trigrams with the words in title and authors; the works that have a close word for **every** query word are returned.
3. A notice on standard error ("No exact match, showing similar titles and authors") and `"fuzzy": true` in `--json`.

**Out of scope**: stemming of the book text (`chunks_fts`; decided: not now, it would double the index and put the 100 ms target at risk), fuzzy search in the text, other languages, phonetic matching.

## 2. Decisions (owner, 2026-10-19)

- Stemming acts on **metadata only**.
- Fuzzy search is an **automatic fallback** when the exact search finds nothing; there is no operator.
- Quoted phrases, prefix terms (`word*`) and field filters stay exact.
- Query words are stemmed with the stemmer of each work's language (so one query term has up to two forms: English and German).

## 3. Feature Files

- `Features/Search/SearchFuzzyStem.feature` (`@F-SRC-4 @F-SRC-6 @M5`).

## 4. Design

- `Shelf.Core/Search/Stemmer`: Porter2 (English) and Snowball German, plain managed code. `Stemmer.For(language)` maps `en`, `en-GB`, `de-AT`, ... to a stemmer; other languages are not stemmed.
- `IndexSchema.Version` 2 → 3: `works_fts` gets the columns `stems_en` and `stems_de`. They hold the stems of title, subtitle, description and tags of a work (not the authors) in the column of its language. The index keeps the stem **and the stem of the stem**: Snowball German turns "systeme" into "system" but "system" into "syst", and a search for either must find both. Existing indexes need `shelf reindex`.
- `LibrarySearch.MetadataTerm`: a plain word of three or more letters becomes `(exact OR stems_en : stem OR stems_de : stem)`; a quoted phrase, a prefix and any other word stay exact. Terms are joined with an explicit `AND` (FTS5 does not take a parenthesized group after an implicit AND). Exclusions and the `author:` / `title:` / `tag:` filters query the exact columns only. The content index (`chunks_fts`) is unchanged.
- `LibrarySearch.SearchFuzzy`: no extra table. When the exact search finds nothing, the words of title, subtitle and authors of the works that pass the filters are compared with each query word by the Jaccard overlap of their trigrams (accents folded, threshold 0.4); every query word needs a close word in the same work. A word of fewer than four letters must be the same word. A query with a quoted phrase or a prefix has no fuzzy results. The `search` command calls it as the fallback, writes "No exact match, showing similar titles and authors." to standard error, and adds `"fuzzy": true` to `--json`.
- The fallback is only in the `search` command. MCP, the web API and hybrid search are unchanged (they get stemming through `LibrarySearch.Search`, no fuzzy).

## 5. Result

- BDD and unit tests green (2479 tests); 18 new unit tests for the stemmers; `SearchFuzzyStem.feature`.
- Performance on `osx-arm64`, reference library (10,011 works): search p95 for typical queries 47.7 ms (median 21.7 ms), very common words 103 ms, cold start 9 ms, scan 0.9 s. A query that finds nothing and runs the fuzzy scan takes about 40 ms as a whole process.
- Known limit: "system" and "systems" (English) also find a German "Systeme", because a query word is stemmed for both languages (the language of the work is unknown to the query).

## 6. Open Questions

- The numbers above come from a different run than ADR 0011 (98 ms); the comparison with the baseline is made again in `m5-close` on one machine.
