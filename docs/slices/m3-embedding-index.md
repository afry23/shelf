# Slice m3-embedding-index: Building the Embedding Index

| | |
|---|---|
| **Slice ID** | `m3-embedding-index` |
| **Branch** | `feat/m3-semantic-core` (same branch as slices 1 to 3, separate commit) |
| **Milestone** | M3 – Semantics & MCP |
| **Roadmap** | M3 slice 4; exit criterion "initial embedding indexing < 6 h and resumable" (measured in `m3-close`) |
| **PRD references** | F-SEM-2, F-SEM-3, F-SEM-7; 7.1 (`reindex [--embeddings]`); 9.1 |
| **Inputs** | [ADR 0013](../adr/0013-onnx-tokenizer-aot.md), [`m3-model`](m3-model.md), [`m3-vector-store`](m3-vector-store.md) |

---

## 1. Scope

**In scope**

1. `shelf reindex --embeddings [--library] [--json]`: installs the model if it is missing (download, once), then makes the missing vectors. Nothing else in the index is touched. Combined with `--metadata-only` it is a usage error (exit 2).
2. `EmbeddingIndexer`: work vectors first, then the passages of every file whose `embed_status` is `pending` and whose `text_status` is `done`.
3. `PassageWindows`: a chunk of up to 600 words becomes overlapping windows of 280 words that start 240 words apart (40 words shared), at most 8 per chunk. The vector key is `chunkId * 8 + part`; the location of a window is the location of its chunk.
4. `WorkText`: title, subtitle, description and tags, one per line, embedded as a passage.
5. Resume: a file is `done` only when all its windows are stored; an interrupted run (Ctrl-C, or `CliEnvironment.Cancellation`) stops between two windows, prints how many files wait and exits with 1.
6. Progress on stderr (`embedding works 2/4`, `embedding files 3/5`, at most one line per second and the last one), counts on stdout or `--json` (`model`, `works`, `passages`, `files`, `remaining`, `interrupted`, `problems`).
7. Content that changes sets `embed_status` back to `pending` (`IndexWriter.SetContent`), so `scan` and `import` queue changed files.

**Out of scope**

- Changing the model (F-SEM-8) and hybrid search: `m3-hybrid`. A vector store of another model is refused with a message.
- The table of contents in the work text: the extraction does not provide it yet (needed for the MCP resource `shelf://book/{id}/toc` in `m3-mcp-core`). Adding it changes the text hash, so those works are embedded again then.
- Batching and threads: one text per call, ONNX Runtime uses all cores. Speed is measured in `m3-close`.

## 2. Feature Files

`Features/Semantic/EmbeddingIndex.feature` (`@F-SEM-2 @F-SEM-3 @F-SEM-7 @M3`, 21 cases): works and passages, JSON, the text of a work, passages never as queries, nothing to do on a second run, stopping and continuing (two scenarios), a changed file, a changed description, a removed work, 6 window sizes, rebuild and `--embeddings`, model download first, download failure, files without text, the usage error.

Scenarios use `FakeEmbedder` (words hash to directions, records every call, can cancel the run after n calls) and the fake model catalog of `m3-model`; no model is needed.

## 3. Design

- **Why windows and not one vector per chunk.** The chunks of the full-text index have 500 words (M1). The model reads 512 tokens, about 280 words of German or English; one vector per chunk would ignore the rest. Splitting at embedding time leaves the full-text chunks, their tests and their performance (ADR 0011) as they are.
- **Orphans are purged at the start of a run**, not when a file changes: `scan` runs without `vec0` and cannot delete vectors. A purge compares the keys of `vec_chunks` and `vec_works` with `chunks` and `works`. A new chunk can reuse the id of a deleted one, but its file is `pending`, so its windows replace the old vectors (`Put` replaces) before the file counts as `done`.
- **A scan during a run.** Before a file is marked `done`, its `sha256`, `size` and `mtime` are read again; if they differ, the file stays `pending` and is done in the next run.
- **Work vectors are skipped when unchanged:** `semantic_work_hash` holds the SHA-256 of the text each work vector was made from.
- **Errors:** an embedder error for a file marks it `failed` and the run goes on (exit 1 at the end, one warning per file). A database error aborts.
- **`reindex` versus `reindex --embeddings`.** The first rebuilds the metadata and removes the vectors ([`m3-vector-store`](m3-vector-store.md)); the second only fills in what is missing.
- **Interrupt.** `Program.cs` turns the first Ctrl-C into a cancel request; the second one ends the process. Exit code 1 because the PRD knows only 0, 1 and 2.

## 4. Unit Tests

`PassageWindowsTests` (window counts at the edges, 40 shared words, the end of the text, the cap, key round trip). `EmbeddingIndexerTests`: a file that changes while embedded stays pending, a failing file is marked and the next goes on, interrupting and finishing in a second run, purging vectors of removed chunks and works, a store of another model is refused.

## 5. First numbers (not the exit criterion)

Native AOT binary, `osx-arm64`, the 5 files of `tests/fixtures/upstream` (4 works, 573 windows), real model: 18.1 s wall, 84 s CPU on 4 to 5 cores, so **about 32 windows per second**. A second run with nothing to do takes 0.49 s (mostly loading the model). At that rate 2.5 million windows would take about 21 hours, against the goal of 6 hours. This is a single measurement on a small, unrepresentative sample; `m3-close` measures on the reference library and decides about int8 quantization, fewer or larger windows, or a smaller share of text (ADR 0004 names the same options for the search side).

## 6. Open Questions

For the owner, at the latest in `m3-close`: if the measurement confirms about 20 hours, which of the following is acceptable for the 6-hour goal? (a) the quantized model (about a quarter of the size, `model_qint8_avx512_vnni.onnx` is x86-only, so an ARM build would have to be made), (b) embedding only the first N windows of each book, (c) accepting a longer first run because it is resumable.
