# Slice m14-reading-list: Reading List

| | |
|---|---|
| **Slice ID** | `m14-reading-list` |
| **Branch** | `feat/reading-list` |
| **Milestone** | M14 – Reading List |
| **PRD references** | F-RLS-1 to F-RLS-7 |
| **Inputs** | `WorkSidecar`, `SidecarFormat`, `MetadataPatch`, `LibraryEditor` (`Update`, `Merge`), `LibraryService`, `IndexSchema`, `QueryParser`, `TagCommand`, `LibraryMcpServer`, `Api`, `Work.svelte` |

---

## 1. Scope

**In scope**

1. `WorkSidecar.ReadingListAt` (`book.json` key `readingList`, `schema` stays 1). `WithReadStatus` clears it when the status becomes `read`. A merge keeps the earliest time of the sources, and none if the merged work is read.
2. Index schema 13: `works.reading_list_at`; query filter `readinglist:yes|no`.
3. `LibraryService.AddToReadingList`, `RemoveFromReadingList`, `ReadingList`.
4. `shelf readlist add|rm|show`, REST `/reading-list`, MCP `get_reading_list`, `add_to_reading_list`, `remove_from_reading_list`, the view `/reading-list` and a button on the work page.

**Not in scope**

- A manual order (drag and drop); the order is the order of adding.
- Several lists, priorities, due dates.
- Putting a work on the list again when it is set back to `unread`.

## 2. Feature Files

`Commands/ReadingList.feature`, `Web/WebReadingList.feature`, `Mcp/McpReadingList.feature`, `Web/UiReadingList.feature`; tool lists in the `Mcp` features.

## 3. Verification

Complete BDD suite and unit tests green; `npm run check`; `shelf reindex` brings the list back.
