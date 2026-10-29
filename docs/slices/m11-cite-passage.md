# Slice m11-cite-passage: Cite a Passage

| | |
|---|---|
| **Slice ID** | `m11-cite-passage` |
| **Branch** | `feat/cite-passage` |
| **Milestone** | M11 – Cite and References |
| **PRD references** | F-ANN-8, F-PAP-4, F-PAP-5, 7.2, 8.3 |
| **Inputs** | `CitationFormats`, `Locators`, `LibraryService`, `SelectionBar`, the readers |

---

## 1. Scope

**In scope**

1. `QuoteFormat` in `Shelf.Core`: a Markdown block quote, then a Pandoc citation `[@key, p. 12]` (`sec. 3` for an EPUB section). Whitespace in the quote is collapsed per line.
2. `LibraryService.QuotePassage` (by file) and `QuotePassageOfWork` (by work, with `file` when a place is given); the result holds the text, the place for people and the BibTeX entry.
3. REST `POST /files/{id}/quote` (a read, the passage is in the body because it can be long), CLI `shelf cite`, MCP `cite_passage` (read-only).
4. Reader: "Copy as quote" in the selection bar of the PDF and the EPUB reader, with a status "Quote copied". The Clipboard API needs a secure context; over plain HTTP a hidden field and `execCommand('copy')` serve.

**Not in scope**

- Other citation styles (APA, Chicago): the Pandoc citation is resolved by the writer's tool from the bibliography (`shelf export`).
- Copying the quote with a highlight at the same time.

## 2. Feature Files

`Annotations/QuoteCommand.feature`, `Web/WebQuote.feature`, `Mcp/McpQuote.feature`, `Web/UiQuote.feature`; tool lists in the `Mcp` features. New steps: `the clipboard holds`; the browser context gets the clipboard permissions.

## 3. Verification

Complete BDD suite and unit tests green; `npm run check`.
