# ADR 0013: ONNX Runtime and the XLM-R tokenizer under Native AOT

| | |
|---|---|
| **Status** | Accepted for `osx-arm64`; `linux-x64` not verified |
| **Date** | 2026-10-18 |
| **Scope** | PRD F-SEM-1, 9.2, 10.2 (embeddings row), risk "Native AOT incompatible with libraries"; roadmap "Carried over from M0" |
| **Inputs** | [`docs/slices/m3-semantic-core.md`](../slices/m3-semantic-core.md), [ADR 0001](0001-dependency-licenses-and-maintenance.md) rows 11, 12, 21 |

M0 did not spike ONNX Runtime. The owner ruled out a separate spike for M3; the question is answered by the first real slice, as a gate.

## Decision

1. **`Microsoft.ML.OnnxRuntime` 1.31.0 and `Microsoft.ML.Tokenizers` 2.0.0 are adopted** for `Shelf.Semantic` (ADR 0001 rows 11 and 12 move from `pending` to `adopt`).
2. **The tokenizer is `SentencePieceTokenizer` on `sentencepiece.bpe.model`, with the ID layout of the reference implementation applied by shelf.** The library returns raw SentencePiece IDs (`<unk>`=0). The model expects `<s>`=0, `<pad>`=1, `</s>`=2, `<unk>`=3 and every other piece shifted by one. `E5Tokenizer` adds one to every ID (0 becomes 3) and wraps the sequence in `<s>` / `</s>`; the text is cut to 512 tokens including both markers.
3. **Pooling is the masked mean of `last_hidden_state`, scaled to unit length**, and the prefixes are `query: ` and `passage: ` (model card). The model file is `onnx/model.onnx` of `multilingual-e5-small` at revision `614241f6`, stored as `model.onnx` next to `sentencepiece.bpe.model`.
4. **The model files are not in the repository** (the budget for fixtures is 5 MiB). Scenarios that need them read the folder from `SHELF_E5_DIR` and are skipped when it is not set. CI provides the files since `m3-close` (`tools/fetch-e5-model.sh`, cached).

## Evidence (`osx-arm64`, macOS, .NET 10)

| Gate | Result |
|---|---|
| Token-ID parity with the reference tokenizer (`transformers`, same `sentencepiece.bpe.model`, SHA-256 `cfc8146a...db2865`) | 7 of 7 texts identical: German with umlauts, English, `ß`, Japanese, punctuation and digits, an empty body (`Tokenizer.feature`) |
| Truncation | 100 words give 102 tokens, 510 give 512, 2000 give 512, last ID is 2 |
| Embedding scenarios with the real model | 9 of 9 pass (`Embedding.feature`): 384 dimensions, unit length, deterministic, prefixes, DE query finds the EN passage (3 queries), missing model is a clear error |
| Native AOT publish of a probe that loads the tokenizer and the model | no trim or AOT warnings; binary 5.2 MB plus `libonnxruntime.dylib` (43 MB) next to it; runs, same token IDs, vector of 384 floats |
| Cosine of the query "Bücher über Messaging zwischen Microservices" | 0.84 to the matching passage, 0.72 to an unrelated one. Unrelated text is not near 0: the threshold for probable duplicates (F-DUP-3) must be calibrated, as the PRD says |
| Wall clock of the probe, load plus three embeddings | 0.98 s |

Speed and quantization were not measured; that is `m3-close`.

## Consequences

- The native `libonnxruntime` is a side file, as `vec0` is (ADR 0004); the publish and the release must carry it per RID.
- `linux-x64` is not verified yet. Since `m3-close` the `aot` job runs `tests/e2e/native.sh` with the real model (`SHELF_E5_DIR`, fetched with a pinned checksum by `tools/fetch-e5-model.sh`) on `linux-x64` and `osx-arm64`; the result is in the first CI run (`libonnxruntime.so` ships in the package).
- ReadyToRun as the fallback was not needed.
- `E5Embedder.EmbedRaw` exists so that tests can prove the prefixes.
