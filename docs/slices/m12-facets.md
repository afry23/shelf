# Slice m12-facets: Series and Type Facets

| | |
|---|---|
| **Slice ID** | `m12-facets` |
| **Branch** | `feat/facets-series-type` |
| **Milestone** | M12 – Facets and Reading Statistics |
| **PRD references** | F-WEB-7, F-SRC-3, 8.2 view Library |
| **Inputs** | `QueryParser`, `LibrarySearch.BuildFilter`, `LibraryBrowse.Facets`, `query.ts` |

---

## 1. Scope

**In scope**

1. `FieldKind.Series` and the filter `series:` (the whole name, ignoring case, `lower()` of SQLite; negation keeps works without a series).
2. `BrowseFacets.Series` (cut at `FacetLimit`, case variants counted as one, the spelling that sorts first shown) and `BrowseFacets.Types`.
3. The REST facets `series` and `types`; the rows "Series" and "Type" in `facetGroups` of the UI. The panel, the paging and the address keep working as they are.

**Not in scope**

- A new facet endpoint or disjunctive counts (the web API stays as M8 left it, only two more arrays).
- Case folding beyond ASCII (SQLite `lower()`), as for `venue:`.

## 2. Feature Files

`Search/SearchSeries.feature`, `Web/WebFacetsSeriesType.feature`, `Web/UiFacetsSeriesType.feature`.

## 3. Verification

Complete BDD suite green, `npm run check`.
