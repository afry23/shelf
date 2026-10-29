# Slice m10-tag-suggestions: Tag Suggestions

| | |
|---|---|
| **Slice ID** | `m10-tag-suggestions` |
| **Branch** | `feat/tag-suggestions` |
| **Milestone** | M10 – Bookmarks and Tag Suggestions |
| **PRD references** | F-TAG-6, F-SEM-6, 7.2, 8.2 view Work details |
| **Inputs** | `SemanticSearch.SimilarWorks`, `LibraryTags.SetTags`, `LibraryService.SimilarBooks` |

---

## 1. Scope

**In scope**

1. `SemanticSearch.SuggestTags`: the 10 nearest works vote with their similarity; a tag scores the sum of the similarity of the works that carry it, as a share of the similarity of all 10. The tags the work has are left out. The stored vector is the query, so no model is loaded.
2. `LibraryService.SuggestTags` (limit 1 to 100, default 10) with the same note about missing or incomplete embeddings as `SimilarBooks`.
3. CLI `shelf tag suggest <work> [--limit] [--apply]`; `--apply` sets all suggested tags in one journal (`shelf undo` takes them back).
4. REST `GET /works/{id}/tag-suggestions?limit=`; MCP `suggest_tags` (read-only).
5. Work page: "Suggested tags" under the tag field, one click adds a tag.

**Not in scope**

- Suggesting tags of a tag hierarchy level (only tags as they are on the similar works).
- A minimum score: `works` and `score` are in the result, so that a caller can decide.

## 2. Feature Files

`Semantic/TagSuggestions.feature`, `Web/WebTagSuggestions.feature`, `Mcp/McpTagSuggestions.feature`, `Web/UiTagSuggestions.feature`; tool lists in the `Mcp` features.

## 3. Verification

Complete BDD suite green, `npm run check`; the fake embedder gives a stable order for the sample library.
