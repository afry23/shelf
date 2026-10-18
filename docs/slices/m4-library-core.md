# Slice m4-library-core: Shared Library Service

| | |
|---|---|
| **Slice ID** | `m4-library-core` |
| **Branch** | `feat/m4-library-core` |
| **Milestone** | M4 – Web |
| **Roadmap** | M4 slice 1 |
| **PRD references** | 7.2, 7.3 (feature parity of CLI, MCP and web), 10.1 |
| **Inputs** | `src/Shelf.Mcp/LibraryTools.cs`, `McpTypes.cs`, `SemanticBackend.cs`; [ADR 0006](../adr/0006-mcp-sdk-aot.md) |

---

## 1. Scope

**In scope**

1. New project `src/Shelf.Library`: `LibraryService` (the logic of the MCP tools and resources over one library), the result records (`BookDetails`, `SearchBooksResult`, ...) and `SemanticBackend`, moved out of `Shelf.Mcp`.
2. `LibraryException` replaces `McpException` inside the service: an error the caller can act on (bad query, unknown work, no text at a location). The service no longer references the MCP SDK.
3. `Shelf.Mcp` keeps only the protocol: `LibraryTools` becomes a thin shell (`[Description]` texts, `LibraryException` to `McpException`), `LibraryMcpServer` and `McpJsonContext` stay.
4. Unit tests for the service (`tests/Shelf.Library.Tests`): argument checks that do not depend on the MCP protocol.

**Out of scope**: any new behavior, the web host (slice 2), new endpoints.

## 2. Feature Files

No new feature file. This slice changes no behavior: `Features/Mcp/Mcp*.feature` (`@PRD-7.2 @AC-6`) are the specification and must stay green without a change. A diff in a `Mcp*.feature` file in this slice is a defect.

## 3. Design

- **One service, two shells.** `LibraryService(root, settings, SemanticBackend)` holds every rule (limits, error texts, the write gate). MCP and, from slice 2 on, the web API call it and only translate `LibraryException` to their error format.
- **Error texts stay byte for byte** (the Mcp specs compare them).
- **Records stay public and keep their `JsonIgnore(Never)` members**; the source-generated `McpJsonContext` stays in `Shelf.Mcp` because the null-handling is a rule of the MCP output schema (ADR 0006, pitfall 2). The web gets its own context in slice 2.
- **`SemanticBackend` moves as is** (public), so that `serve` can keep one loaded model for its whole life.
- **Write gate**: stays inside the service (one lock per service instance). Cross-process writes (CLI while `serve` runs) are slice 3's question.

## 4. Native AOT

`dotnet publish -r osx-arm64 -p:PublishAot=true` of `shelf` has no new warnings; `tests/e2e/native.sh` unchanged and green.

## 5. Unit Tests

`LibraryServiceTests`: limit out of range, empty query, bad level for semantic search, negative offset, `maxChars` above the hard maximum, empty tag list, each as `LibraryException`.

## 6. Open Questions

None.
