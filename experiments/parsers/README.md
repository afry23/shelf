# experiments/parsers

Throwaway spike for [ADR 0005](../../docs/adr/0005-epub-pdf-parsers-aot.md): VersOne.Epub 3.3.6 and PdfPig 0.1.16 in a
Native AOT binary. Not part of `shelf.slnx`, not built by `build.yml`. Deleted when M0 closes.
Plan: [`docs/slices/m0-spike-parsers.md`](../../docs/slices/m0-spike-parsers.md).

## Run

```
# data (once, repository root; git-ignored artifacts/)
dotnet run --project tests/Shelf.TestData -c Release -- generate --out artifacts/synthetic-library --preset reference
dotnet run --project tests/Shelf.TestData -c Release -- generate --out artifacts/tiny --preset tiny --seed 42
dotnet run --project tests/Shelf.TestData -c Release -- generate --out artifacts/large-pdf --seed 42 --works 5 --files 5 --words-min 100000 --words-max 100000

# full osx-arm64 run on AC power (about 15 minutes), the committed output is the unedited result
./measure.sh > results/measure.out 2>&1

# functional check on the host RID (what the spike-parsers workflow runs), writes results/ci/<rid>.txt
./check.sh <rid>

# optional: copy real e-books into corpus/ (git-ignored), then: out/Parsers-all/proto-parsers-all corpus corpus/
# (prints aggregates only; per-file lines carry the first 12 hex digits of the SHA-256, never names or text)
```

Prototypes: `Baseline` (prints `ok`) and `Parsers` (`-p:Variant=epub|pdf|all`, assembly `proto-parsers-<variant>`;
`-p:CodePages=true` adds `CodePagesEncodingProvider` for the size measurement). Commands: `info`, `selfcheck`,
`isbn-check`, `epub-check`, `pdf-check`, `mutate`, `robust`, `robust-one`, `meta`, `extract`, `dump`, `verify`,
`bench`, `corpus`. `bin/`, `obj/`, `out/`, `data/`, `corpus/` and everything in `results/` except `measure.out` and
`ci/*.txt` are git-ignored.
