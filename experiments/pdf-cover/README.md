# experiments/pdf-cover

Spike for [ADR 0010](../../docs/adr/0010-pdf-cover-rendering-aot.md): rendering page 1 of a PDF as a cover (longest edge 600 px) with
PDFtoImage 5.4.0, SkiaSharp and PDFium under Native AOT on `osx-arm64`. Not part of `shelf.slnx`, not built by CI.
Plan: [`docs/slices/m0-spike-pdf-cover.md`](../../docs/slices/m0-spike-pdf-cover.md).

- `Cover/`: AOT app `proto-cover` with the commands `render`, `robust`, `stress`, `bench` and `info`.
- `fixtures/encrypted.pdf`: password-protected PDF for the robustness row (provenance in `fixtures/README.md`).
- `check.sh`: publishes the AOT and JIT app, runs K1-K7 and K9, then calls `experiments/combined/check.sh` for K8 and compares the pixel
  hashes of `proto-shelf cover` with `proto-cover`. Writes `results/local/osx-arm64.txt`. Apple Silicon Mac only (uses `sips`).

```
./check.sh
```
