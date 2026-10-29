# Slice m12-reading-stats: Reading Statistics

| | |
|---|---|
| **Slice ID** | `m12-reading-stats` |
| **Branch** | `feat/reading-stats` |
| **Milestone** | M12 – Facets and Reading Statistics |
| **PRD references** | F-STA-1, F-STA-2, F-STA-3, F-RDR-4 |
| **Inputs** | `WorkSidecar`, `SidecarFormat`, `MetadataPatch`, `EditCommand`, `LibraryEditor.Merge`, `LibraryStatistics`, `reading_progress` |

---

## 1. Scope

**In scope**

1. `WorkSidecar.FinishedAt` (`book.json` key `finishedAt`, `schema` stays 1) and `WithReadStatus`, used by the two places that set the status. A merge keeps the later date, and none if the merged work is not read.
2. Index schema 11: `works.finished_at` (only for works that are read).
3. `LibraryStatistics`: read without a date, finished by month, being read, read lately. The last move of a reader is the newest `reading_progress.updated_at` of any file of the work.
4. `shelf stats` (text and JSON), `GET /api/v1/stats`, MCP `get_stats`, the view `/stats`.

**Not in scope**

- A history of reads (a book read twice has one date, the last time it was set to read).
- Estimating dates for works read before this field: they stay "read without a date". A person dates them by setting them to Read again.
- A "last opened" event: opening a book writes nothing (m5-reader-progress); "read lately" is the last move in a file.
- Percent read, goals, charts beyond the monthly bars.

## 2. Feature Files

`Commands/StatsReading.feature`, `Web/WebStats.feature`, `Mcp/McpStats.feature`, `Web/UiStats.feature`, `Web/UiStatsEmpty.feature`; tool lists in the `Mcp` features.

## 3. Verification

Complete BDD suite and unit tests green; `npm run check`; `shelf reindex` brings the dates back.
