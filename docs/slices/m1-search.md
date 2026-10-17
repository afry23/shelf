# Slice m1-search: Full-Text Search

| | |
|---|---|
| **Slice ID** | `m1-search` |
| **Branch** | `feat/m1-search` (stacked on `feat/m1-import-scan`) |
| **Milestone** | M1 – Foundation |
| **Roadmap** | M1 area "Search"; CLI command `search`; exit criterion "search p95 < 100 ms" |
| **PRD references** | F-SRC-1, F-SRC-2, F-SRC-3, F-SRC-5; 7.1 (`shelf search <query> [--limit] [--json]`); 9.1 |
| **Inputs** | [ADR 0004](../adr/0004-storage-sqlite-fts5-vector-search.md) (tokenizer, bm25 weights, findings F10-F19), `works_fts` / `chunks_fts` from `m1-index-db`, chunks from `m1-import-scan` |

---

## 1. Scope

**In scope**

1. The query language (F-SRC-3), parsed in `Shelf.Core.Search` so that CLI, web UI and MCP use one language:
   - free words, `"phrases"`, `prefix*`, `-negation` (words, phrases, filters);
   - filters `author:` `title:` `tag:` `lang:` `year:` `format:` `status:` (field names in any case; a value in quotes may contain spaces: `author:"Martin Kleppmann"`);
   - `year:2015`, `year:>2015`, `year:>=2015`, `year:<2000`, `year:<=2000`, `year:2010..2015` (stored as an inclusive range);
   - anything else, including words with a colon that is no field (`http://x`), is plain text.
2. `LibrarySearch` (Shelf.Index): FTS5 over metadata and content, ranking, filters, exclusions, snippets and locations (F-SRC-1, F-SRC-2, F-SRC-5).
3. `shelf search <query>... [--limit N] [--library] [--json]`: several words may be given without quotes; a text result per work with its best passages, "No results." for none; `--limit` 1-1000 (default 20).
4. `Locators.Describe`: "page 212" for a PDF page, "section 3" for an EPUB spine position.

**Requirements covered:** F-SRC-1, F-SRC-2, F-SRC-3, F-SRC-5.

**Out of scope**

- Fuzzy search (F-SRC-4) and stemming (F-SRC-6): M5. Umlaut expansion (`mueller`): owner decision, not in M1.
- Semantic and hybrid search, the MCP tool `search_books`: M3.
- Benchmarks in CI against the 10,000-work library: `m1-ci-perf`.

## 2. Feature Files

`Features/Search/QueryParsing.feature` (`@F-SRC-3`: 28 query forms, 7 empty forms, 8 invalid values) and `Features/Search/Search.feature` (`@F-SRC-1 @F-SRC-2 @F-SRC-3 @F-SRC-5`): ranking, folding of case and accents, content in EPUB/PDF/German, phrases, prefixes, 21 filter rows, combinations, negation, snippets with page and section, JSON, limit, no match, metadata without content, plain-text handling of index syntax, usage errors, missing index. The library of the scenarios is built from `SampleBooks` with known chapters and pages.

## 3. Design

- **A work matches** if all free words are in its metadata (title, subtitle, authors, description, tags) **or** all are together in one passage of its text. One word in the title and another only in the text does not match. Costs one extra join to lift later (M3 hybrid ranking); the rule is stated in the class comment.
- **Ranking:** score = −bm25(metadata) − bm25(best passage). The metadata columns carry the weights of ADR 0004 (title 10, authors 8, subtitle 5, tags 3, description 1), so a title hit beats a text hit; a work with both ranks first. Filter-only searches are sorted by author and title.
- **Filters and exclusions act on works.** `-sidecar` removes every work that has the word in its metadata or anywhere in its text (a per-source `NOT` would keep a work whose title lacks the word). `title:` covers the subtitle. `tag:cloud` matches `cloud/azure` (the tokenizer splits at `/`); `tag:cloud/azure` is the phrase. `lang:en` also matches `en-GB`. A work without a year matches no year filter and is not removed by a negated one.
- **No character is index syntax.** Every term goes to FTS5 as a quoted phrase (`"` doubled), column filters are built by shelf, values are parameters. `( ) : ^ NEAR OR AND` in a query are words.
- **Snippets are cut out last**, for the passages that are shown: computing `snippet()` for every match made the word "data" take 240 ms; now 10-30 ms warm. Candidates are capped at 500 per source (far more than anyone reads).
- **Empty query:** no word, filter or exclusion left after parsing is a usage error (exit 2), as are invalid filter values and `--limit` outside 1-1000. No hits is exit 0.
- **"Section" instead of "chapter":** the CFI step counts spine items, and a cover page is one; the output says "section N" and the JSON carries the exact locator.
- No ADR: FTS5 and the weights were decided in ADR 0004.

## 4. Unit Tests

`QueryParserTests` (terms, quoted filters, year edges, malformed years, plain text with colon, index characters, `Describe`), `LocatorsTests.Describe`, `LibrarySearchTests` (weights: title over description, author over tag, title over text; both sources; words in different sources; hits per work, snippet, location; negation by text; only negations; negated year and unknown year; language variants; status; 17 hostile inputs; limit; details), `ArgumentSplitterTests` (escaped quotes for the phrase scenarios).

## 5. Performance (first measurement)

1475 files / 18,440 chunks, Native AOT binary, `osx-arm64`: whole process incl. start, warm: 10-30 ms for common words ("data", "a", "the"), under 10 ms for rare ones and filters; the first run after a rebuild of the binary 240 ms (cold file cache). Target p95 < 100 ms at 10,000 works / 15,000 files: to be measured in `m1-ci-perf`; the late snippet and the 500-candidate cap are what keep this flat.

## 6. Open Questions

None for the owner.
