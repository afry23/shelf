# Slice m5-enrich: Online Metadata

| | |
|---|---|
| **Slice ID** | `m5-enrich` |
| **Milestone** | M5 – Polish |
| **PRD references** | F-IMP-6, 7.1 (`shelf enrich`), 9.4 (network access) |

## 1. Scope

`shelf enrich <work> [--apply] [--overwrite] [--pick n] [--source openlibrary|google] [--no-cover] [--json]` loads the metadata and the cover of a work from Open Library and Google Books.

The work page of the web interface does the same: "Load details from the internet" shows the preview, candidates are chosen with a click, "Take over" writes.

**Out of scope**: an MCP tool, enriching many works at once, a Google Books API key, merging the answers of several sources, series data.

## 2. Feature Files

`Features/Import/Enrich.feature` (CLI), `Features/Web/WebEnrich.feature` (REST), `Features/Web/UiEnrich.feature` (browser); all `@F-IMP-6 @M5`.

## 3. Design

- `IMetadataSource` (`Shelf.Index/Enrichment`) is the seam, like `IRemoteFetcher`: `OpenLibrarySource` and `GoogleBooksSource` over `IHttpGetter`, the only place that opens a connection. `CliEnvironment.MetadataSources` holds them; scenarios replace them with fakes, so no scenario reaches the internet.
- `EnrichmentService.Preview` asks the sources in order. With an ISBN the first source that knows it wins. Without one the search result must match exactly after `TitleNormalizer` and share an author surname (`AuthorNames.Match`); one match is taken, several need `--pick`, none ends with "no clear match". A source that fails is a warning, the next one is asked.
- Fields: isbn, title, subtitle, authors, publisher, year, language, description. Empty fields are filled; a differing value is shown as kept (`=`) or, with `--overwrite`, replaced (`~`). A title that differs only in case or edition words is not shown.
- The sidecar is written with `LibraryEditor.Update`; the cover is saved as `cover.<ext>` and adopted by `LibraryCovers.FillWorks`, like one the user dropped into the folder. A work that has a cover keeps it.
- Web: `LibraryService.EnrichPreview/EnrichApply` (the rules of the CLI, one write gate), `POST /works/{id}/enrich` and `/enrich/apply` with `{source, pick, overwrite, cover}`. Several matches are a 200 with `candidates` for the preview and a 409 for apply; no match is a 409; an unusable field is a 400. The server holds the sources in `WebOptions.MetadataSources`; the UI sends the request again for every change of `overwrite` or `pick`, so the server keeps no state.
- JSON by `JsonDocument`, no reflection (AOT).
- Exit codes: 0 done; 1 nothing found, no clear match, several candidates, or a source failure without result; 2 unusable arguments (`--source`, `--pick`, `--overwrite` without `--apply`).

## 4. Result

BDD (27 CLI, 13 REST, 7 browser scenarios) and unit tests (parsers against recorded answers) green.
