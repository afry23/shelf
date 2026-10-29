# ADR 0019: References of papers

| | |
|---|---|
| **Status** | Accepted: `references.json` per work; the index schema version is 10 |
| **Date** | 2026-10-29 |
| **Scope** | PRD F-PAP-11, F-PAP-12, 10.3 (table `work_refs`) |
| **Roadmap** | M11 |
| **Inputs** | [ADR 0018](0018-annotations.md), the Crossref REST API (`message.reference[]`) |

## Context

A paper cites other works, and a person who builds a library of papers wants to see which of the cited works they own and who cites a paper. Crossref publishes the reference lists of papers by DOI. Papers already have a DOI (F-PAP-1) and enrich asks Crossref only on a command (F-PAP-3).

## Decisions

1. **Storage.** `references.json` next to `book.json`, written atomically, with a `schema` field and unknown fields kept, like `annotations.json` (ADR 0018). A paper can cite hundreds of works; `book.json` stays small and is not rewritten for it. The index holds a copy, `work_refs(work_id, position, doi, title, year, author, unstructured)`, with an index on `doi`. The index schema version rises to 10; `shelf reindex` rebuilds the copy.
2. **Entry.** `doi` (lower case in the index), `title`, `year`, `author`, `unstructured` (the text of the citation). At least one of `doi`, `title` and `unstructured`.
3. **Matching.** A reference points to a work of the library if the DOIs are equal after `PaperIds.Normalize`. "Cited by" is the reverse: the works whose list holds the DOI of the paper. A work does not cite itself. Entries without a DOI are not matched (a title match would guess).
4. **Only on command.** The list is loaded by `shelf refs fetch`, the REST `POST .../references/fetch` and one click on the work page. Without `--apply` / `apply` nothing is written. Import, scan, reindex, the reading of the list and MCP never ask. MCP gets no tool that fetches.
5. **Replace.** A new list replaces the old one. A list of zero entries removes the file.
6. **With the work.** The file moves with the work on organize, goes to the trash with the work (undo brings it back), and on merge goes to the target if the target has no list, else it is dropped (it can be loaded again). Split gives the new work none.
7. **No journal.** Like annotations, a change is not undoable through the journal.

## Risks

- Crossref lists are incomplete for many publishers (some publishers close their references). The preview says how many entries a list has before it is stored.
- A work cited under another DOI (a preprint and a paper) is not matched.
