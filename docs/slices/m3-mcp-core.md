# Slice m3-mcp-core: `shelf mcp` with the Read Tools

| | |
|---|---|
| **Slice ID** | `m3-mcp-core` |
| **Branch** | `feat/m3-semantic-core` (same branch as slices 1 to 6, separate commit) |
| **Milestone** | M3 – Semantics & MCP |
| **Roadmap** | M3 slice 7; acceptance criterion 6 (without `semantic_search`, which is slice 8) |
| **PRD references** | 7.2 (tools, resources, stdio), 9.4, 10.1 (`Shelf.Mcp`), AC 6 |
| **Inputs** | [ADR 0006](../adr/0006-mcp-sdk-aot.md) (registration pattern and pitfalls), the spike code in `70ad12d^:experiments/mcp/` |

---

## 1. Scope

**In scope**

1. `src/Shelf.Mcp` and `shelf mcp [--library]`: the MCP server on standard input and output with `ModelContextProtocol.Core` 2.2.0, hand-registered tools and resources, a source-generated JSON context (option A of ADR 0006).
2. Six read-only tools, all marked `readOnlyHint`, none destructive:
   `search_books` (the query language of the CLI; places in the text as `location`), `get_book` (metadata, files with size and pages, tags, table of contents), `read_passage` (text at a location; `maxChars` default 4000 and hard maximum 20000; `offset` to read on; optional `file`), `list_tags` (tree with the number of works below each tag), `find_duplicates`, `propose_organization` (a dry run, capped at 200 moves).
3. Resources `shelf://book/{id}` (metadata) and `shelf://book/{id}/toc` (table of contents).
4. `TocExtractor` (`Shelf.Extraction`): the navigation of an EPUB or the outline of a PDF, with locations that `read_passage` accepts; read from the file when asked.
5. Config key `mcp.max_chars` (hard maximum of `read_passage`, 1 to 1,000,000, default 20,000).
6. `CliEnvironment.WithMcpStreams`, so that specs run the server on pipes.

**Out of scope** (slice 8): `semantic_search`, `add_tags` and `remove_tags`, `mcp.read_only`. Streamable HTTP is M5.

## 2. Feature Files

`Features/Mcp/McpServer.feature` (`@PRD-7.2 @AC-6`, 25 cases): the server's name and instructions, exactly the six tools and all of them read-only with descriptions, no tool that deletes or moves, required arguments, a description for every argument, the two resource templates, every required property of an output schema present in a real result (7 tools and inputs), an unknown tool (protocol error -32602, the server goes on), a wrong argument type, a missing argument, the sorted list of names.

`Features/Mcp/McpTools.feature` (`@PRD-7.2`, 38 cases): `search_books` (5 queries, passages with locations, limit, 4 error cases), `get_book` (all fields, nulls as null, PDF pages, table of contents, an ID that names several works, unknown work), `read_passage` (text and place, a page number, the table of contents' location, cutting and `offset` in 4 rows, 7 refusals, the configured hard maximum), `list_tags`, `find_duplicates` (and that it changes nothing, checked by hashes), `propose_organization` (and that it moves nothing), two invalid patterns.

`Features/Mcp/McpResources.feature` (4 cases): both resources, an unknown work is protocol error -32602 and the server goes on.

`Features/Mcp/McpProcess.feature` (`@AC-6`, 3 cases): `dotnet shelf.dll mcp` is started for real; every line on standard output is a JSON-RPC message, a tool error is a successful response with `isError`, log lines (`SHELF_LOG_LEVEL=Debug`) arrive on standard error, a missing library is reported on standard error with exit 1 and nothing on standard output.

## 3. Design

- **Two kinds of errors, as in ADR 0006.** Anything the assistant can fix (bad query, unknown work, no text at a location, `maxChars` out of range) is an `McpException`: a tool error with the message, the server stays up. Resources use `McpProtocolException` with `InvalidParams`, because only that carries an error code.
- **Optional values are written as `null`, never left out.** The SDK derives an output schema in which every member is required; `[JsonIgnore(Condition = Never)]` on the nullable members makes the result match it (ADR 0006, pitfall 2). A spec checks, for each tool, that every required property of the schema is in a real result.
- **Parameter descriptions** come from `[Description]` attributes; a spec checks that every argument of the four tools that take any has one, so the assistant knows what to pass.
- **`read_passage` reads from the index**, not from the file: the chunks hold the text per location, so a page of a PDF is a lookup. A location that has several chunks (a long chapter) is joined, then cut by `offset` and `maxChars`; `truncated` says whether more follows.
- **A page number alone is accepted** (`"12"` means `page=12`), since models often send it that way.
- **`propose_organization` calls `LibraryOrganizer` with `apply: false`**; a spec compares the folder by hashes before and after.
- **The table of contents is not stored.** It is read from the file on each `get_book` or resource read (milliseconds). The sample EPUB generator writes a root entry "Contents" with the chapters below it, so its TOC has a first entry without a location; real EPUBs usually do not.
- **Streams are injected** (`CliEnvironment.McpInput`/`McpOutput`, default the process streams) so that specs run the server in-process against the SDK client; the process scenarios cover the real streams.
- **Logging:** the logger factory writes to standard error only (`StderrLoggerProvider`), as everywhere in shelf.
- **PRD wording:** the PRD writes `mcp.readOnly: true`; the config uses snake case like every other key (`mcp.read_only`, slice 8, and `mcp.max_chars` here).

## 4. Native AOT

`dotnet publish -r osx-arm64 -p:PublishAot=true` of `shelf` has no warnings. A session against the native binary on the fixture library: `initialize`, `tools/list` (six tools with descriptions, required arguments, `readOnlyHint`, output schemas), `search_books`, `get_book`, and `resources/read` of the table of contents all work; the instructions text arrives intact. `linux-x64` is not verified (CI `aot` job, `m3-close`).

## 5. Unit Tests

`TocExtractorTests` (6): EPUB navigation with chapter locations, a PDF outline with pages (the arXiv fixture), a PDF without an outline, a missing file, an unknown extension, a file that is no EPUB.

## 6. Open Questions

None. Two small things noticed on the way, not changed here: `Program.cs` throws when the home directory is empty (`HOME` pointing at a folder that does not exist); and the work embedding (slice 4) does not yet include the table of contents of F-SEM-3, because it would mean opening every file during embedding. `TocExtractor` makes that possible later if the owner wants it.
