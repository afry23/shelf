# Slice m3-mcp-semantic-write: `semantic_search`, the Tag Tools and `mcp.read_only`

| | |
|---|---|
| **Slice ID** | `m3-mcp-semantic-write` |
| **Branch** | `feat/m3-semantic-core` (same branch as slices 1 to 7, separate commit) |
| **Milestone** | M3 – Semantics & MCP |
| **Roadmap** | M3 slice 8; acceptance criteria 4 and 6 |
| **PRD references** | 7.2 (`semantic_search`, `add_tags`, `remove_tags`, `mcp.readOnly`, "destructive operations are not offered"), AC 4, AC 6 |
| **Inputs** | [`m3-mcp-core`](m3-mcp-core.md), [`m3-hybrid`](m3-hybrid.md), [ADR 0006](../adr/0006-mcp-sdk-aot.md) (R4: hiding tools) |

---

## 1. Scope

**In scope**

1. `semantic_search(query, level = "work" | "chunk", hybrid = false, limit = 10)`: the same functions as `shelf semantic`. A hit has `id`, `title`, `authors`, `year`, `language`, `formats`, `score` and, at passage level, a `passage` with `file`, `format`, `location`, `where` and `snippet` (the `location` is what `read_passage` takes). The result also carries `pendingFiles`, `worksWithoutEmbedding` and a `note` (`no embeddings yet; run: shelf reindex --embeddings`, ...), so an assistant learns that the answer may be incomplete.
2. `add_tags(id, tags[])` and `remove_tags(id, tags[])`: names are checked like on the command line (`TagNames.TryNormalize`), all before any is applied; each tag is one journal run, so `shelf undo` reverts it. The result is the work's tags after the change and the number of changes.
3. `mcp.read_only` (global or library config, default false): the two tag tools are not registered, so they are neither listed nor callable (`-32602`); the instructions say the server is read-only.
4. The embedding model is opened on the first `semantic_search` and kept for the session (`SemanticBackend`).
5. `SemanticCoverage.Note` is shared by the CLI and MCP, so both say the same.

**Out of scope**: Streamable HTTP (M5); `similar_books` (M5, F-SEM-6); an option to confirm changes.

## 2. Feature Files

`Features/Mcp/McpSemantic.feature` (`@PRD-7.2 @F-SEM-1 @F-SEM-5`, 22 cases, fake embedder): works by title, description and tags, passages with places, a location read with `read_passage`, hybrid, the query embedded as a query, limits, six refusals, no embeddings (with the note), waiting files, a complete index without a note, no model, the model opened once for two searches, no download.

`Features/Mcp/McpWrite.feature` (`@PRD-7.2`, 15 cases): adding with the work's tags returned, the index knows the tag at once, adding an existing tag changes nothing, names are cleaned, removing exactly the tag named, `shelf undo` reverts a change made through MCP, six refusals, a refused tag leaves the library as it was (by hashes), nothing but tags changes, the annotations of the two tools.

`Features/Mcp/McpReadOnly.feature` (`@PRD-7.2`, 9 cases): offered by default, not offered with `mcp.read_only = true` in the library or the global config, not callable, unchanged library, read tools keep working, `false` offers them.

`Features/Mcp/McpAcceptance.feature` (`@AC-6`, 1 case, **real model**, skipped without `SHELF_E5_DIR`): a real `shelf mcp` process is sent `search_books`, `semantic_search` with the German question "Bücher über Messaging zwischen Microservices" and `read_passage`; the German question returns the English *Enterprise Integration Patterns* as its only hit, the passage text is read back, standard error stays empty and the exit code is 0.

`McpServer.feature` now expects nine tools, only `add_tags` and `remove_tags` not read-only, no tool marked destructive, no tool name that starts with `delete`, `move`, `trash` or `apply`. `Settings.feature` has rows for `mcp.read_only`.

## 3. Design

- **Hiding by not registering (ADR 0006, R4).** The SDK has tool filters, but a tool that was never added is the simplest thing that cannot be called by accident. The cost is that a change of the setting needs a restart of the server, which is how a config file works anyway.
- **The tag tools are annotated `readOnlyHint = false`, `destructiveHint = false`, `idempotentHint = true`.** Removing a tag is reversible with `shelf undo`; the PRD forbids "delete, move", not tagging.
- **All names are checked before any change**, so `["fine", "a//b"]` changes nothing.
- **Writes are serialized** with a lock in the server: the SDK handles requests concurrently, and a native session in which `add_tags` and `remove_tags` were sent back to back answered in the order 6, 5. Two changes of one sidecar must not overlap.
- **A note, not an error, for an incomplete index.** The assistant gets results and the warning; an error would make it give up an answer it could give.
- **An error for what cannot work:** no model installed, vectors of another model, an unknown level, filters with `hybrid`.
- **Level is a string** (`work`/`chunk`) rather than a boolean, since it reads better in the tool description and leaves room for more.

## 4. Native AOT

`dotnet publish -r osx-arm64 -p:PublishAot=true`: no warnings. A session against the native binary on the fixture library lists the nine tools with the right annotations, `semantic_search` at passage level finds *Flatland* for the German phrase "Wesen in einer flachen Welt", hybrid works, `add_tags` and `remove_tags` change and restore the tag. `linux-x64` is not verified (`m3-close`).

## 5. Open Questions

None.
