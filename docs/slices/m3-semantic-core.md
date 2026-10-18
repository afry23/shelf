# Slice m3-semantic-core: Embedder, Tokenizer and the AOT Gate

| | |
|---|---|
| **Slice ID** | `m3-semantic-core` |
| **Branch** | `feat/m3-semantic-core` (stacked on `docs/m2-tagged`) |
| **Milestone** | M3 – Semantics & MCP |
| **Roadmap** | M3 slice 1; "Carried over from M0": ONNX Runtime and the XLM-R tokenizer under Native AOT |
| **PRD references** | F-SEM-1; 9.2; 10.2 |
| **Inputs** | [ADR 0001](../adr/0001-dependency-licenses-and-maintenance.md) rows 11, 12, 21 |

---

## 1. Scope

**In scope**

1. Project `src/Shelf.Semantic` and `tests/Shelf.Semantic.Tests`: `ModelFolder`, `IEmbedder`, `E5Tokenizer`, `E5Embedder`, `Pooling`.
2. The gate: token-ID parity with the reference tokenizer and a Native AOT publish on `osx-arm64`; result in [ADR 0013](../adr/0013-onnx-tokenizer-aot.md); ADR 0001 row 11.
3. Packages `Microsoft.ML.OnnxRuntime` 1.31.0 and `Microsoft.ML.Tokenizers` 2.0.0 in `Directory.Packages.props`.

**Out of scope**

- No spike for speed, quantization or model comparison (owner decision).
- `model install`, the download and the config: `m3-model`. Vector tables: `m3-vector-store`. A batch API: `m3-embedding-index`.
- Wiring into the CLI binary. The publish probe lives outside the repository.

## 2. Feature Files

`Features/Semantic/Tokenizer.feature` (`@F-SEM-1 @M3`): 7 reference texts, 3 truncation lengths. `Features/Semantic/Embedding.feature` (`@F-SEM-1 @M3`): dimension and unit length, prefixes, determinism, 3 cross-language queries, missing model (query and passage).

The real model is 470 MB and not in git. The steps read `SHELF_E5_DIR` and skip when it is not set. Without it, 20 scenarios are skipped; with it, all pass. `m3-close` provides the files in CI.

## 3. Design

- `E5Tokenizer` adds the one-off ID shift of the XLM-R layout (ADR 0013, 2).
- `E5Embedder` reads `session.InputMetadata` and adds `token_type_ids` only when the model has that input.
- `Pooling.MeanNormalize` is pure and takes the mask, so batching later reuses it.
- A missing file is `ModelNotInstalledException` with "model not installed: ...".

## 4. Unit Tests

`PoolingTests` (masked mean and unit length, all masked out, zero input), `ModelFolderTests` (empty folder, both files).

## 5. Open Questions

None for the owner. Reference IDs were produced with `transformers` in a throw-away venv; the feature file keeps the values.
