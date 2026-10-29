# Slice m11-references: References and Cited By

| | |
|---|---|
| **Slice ID** | `m11-references` |
| **Branch** | `feat/references` |
| **Milestone** | M11 – Cite and References |
| **PRD references** | F-PAP-11, F-PAP-12, 7.2, 8.2 view Work details |
| **Inputs** | `CrossrefSource`, `IMetadataSource`, `AnnotationsStore`, `LibraryAnnotations`, `IndexSchema` |

---

## 1. Scope

**In scope**

1. `Reference`, `ReferencesSidecar`, `ReferencesFormat`, `ReferencesStore` in `Shelf.Core` (ADR 0019).
2. `IMetadataSource.FindReferences`, read from `message.reference[]` by `CrossrefSource`.
3. Index schema 10 with `work_refs`; `LibraryReferences` (fetch, save, describe, match, cited by); mirror in scan and reindex; organize, delete and merge carry the file.
4. `shelf refs list|fetch`; REST `GET /works/{id}/references`, `POST /works/{id}/references/fetch`; MCP `get_references`; the tab "References" on the work page.

**Not in scope**

- Matching of entries without a DOI by title and author.
- Loading the lists of all papers at once (one paper per command, to stay polite to Crossref).
- Other sources (arXiv, OpenAlex).

## 2. Feature Files

`Papers/PaperReferences.feature`, `Web/WebReferences.feature`, `Mcp/McpReferences.feature`, `Web/UiReferences.feature`; tool lists in the `Mcp` features. New steps: `the online source ... lists these references for the DOI`, `the online questions are forgotten`.

## 3. Verification

Complete BDD suite and unit tests green; `npm run check`; `shelf reindex` brings the cited-by relation back.
