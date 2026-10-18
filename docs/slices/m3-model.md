# Slice m3-model: Installing the Embedding Model

| | |
|---|---|
| **Slice ID** | `m3-model` |
| **Branch** | `feat/m3-semantic-core` (same branch as slice 1, separate commit) |
| **Milestone** | M3 – Semantics & MCP |
| **Roadmap** | M3 slice 2; "Model handling" row |
| **PRD references** | F-SEM-1; 9.2 (download once, `shelf model install <path>`); 9.4 (no network at runtime) |
| **Inputs** | [ADR 0013](../adr/0013-onnx-tokenizer-aot.md), [ADR 0001](../adr/0001-dependency-licenses-and-maintenance.md) row 21 |

---

## 1. Scope

**In scope**

1. `ModelCatalog` with `multilingual-e5-small` at the pinned revision `614241f6`: URL, size and SHA-256 of `model.onnx` (470,268,510 bytes) and `sentencepiece.bpe.model` (5,069,051 bytes).
2. `ModelInstaller`: install from a folder (the files directly in it, or in its `onnx/` subfolder) and by download. Every file is checked against the catalog while it is copied.
3. `shelf model install [<folder>] [--library] [--json]`.
4. Config key `semantic.model` (global and per library), default `multilingual-e5-small`.
5. `IModelDownloader` as the seam; `HttpModelDownloader` is the only code that uses the network.

**Out of scope**

- The download "on first need" from `semantic` and `reindex --embeddings`: those commands call `ModelInstaller.Download` in `m3-embedding-index` and `m3-semantic-search`.
- Models other than the one in the catalog (F-SEM-8 is slice 6). Another name in the config is an error that lists the known models.
- Progress with percentages; a line per file goes to stderr.

## 2. Feature Files

`Features/Semantic/Model.feature` (`@F-SEM-1 @PRD-9.2 @M3`): install from 3 folder layouts, 2 missing files, a path that is no folder, 2 tampered files, installing twice, download, failed download, 2 tampered downloads, unknown model in the config, JSON. `Features/Configuration/Settings.feature`: 3 rows for `semantic.model`, 2 invalid values.

Scenarios use a catalog of tiny files with correct checksums and a fake downloader (`FakeModels` in `ScenarioEnvironment.cs`), so no scenario needs the network or the real model.

## 3. Design

- **Staging.** Files are written to `.shelf/models/<name>.part/`, checked, and the folder is renamed at the end. A failed run deletes the staging folder; a model folder is complete or absent.
- **Hash while copying**, 1 MiB buffer: the real model (448 MiB) installs from a folder in 0.45 s.
- **"Already installed"** compares file sizes only. The hash was checked at install; hashing 470 MB on every start would cost seconds.
- **Offline hint.** A download error ends with "To install without a network, run: shelf model install <folder>".
- **`CliEnvironment.WithModels`** carries catalog and downloader, like `WithFileOpener`; the process never reads the network on its own.
- `Shelf.Cli` now references `Shelf.Semantic`, so the publish output carries `libonnxruntime` (43 MB, side file). The AOT binary is 19 MB; `--version` still takes under 10 ms.

## 4. Unit Tests

`ModelInstallerTests`: subfolder layout without a staging folder left, replacing a broken folder, same-size wrong hash, failed download leaves nothing, no second download, default catalog (file names, hash format, pinned URL).

## 5. Open Questions

None for the owner. The real download from huggingface.co was not run in a test (no network in the suite); the URLs are the ones that fetched the files for slice 1.
