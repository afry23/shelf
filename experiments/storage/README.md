# experiments/storage

Throwaway spike for [ADR 0004](../../docs/adr/0004-storage-sqlite-fts5-vector-search.md): Microsoft.Data.Sqlite + FTS5,
sqlite-vec and a brute-force fallback in a Native AOT binary. Not part of `shelf.slnx`, not built by `build.yml`.
Deleted when M0 closes. Plan: [`docs/slices/m0-spike-storage.md`](../../docs/slices/m0-spike-storage.md).

## Run

```
# sqlite-vec v0.1.9 official loadable assets, pinned SHA-256 (sqlite-vec.sha256), into vendor/<rid>/ (git-ignored)
./fetch-sqlite-vec.sh [osx-arm64 osx-x64 linux-x64 linux-arm64]

# synthetic library (once, repository root; git-ignored artifacts/)
dotnet run --project tests/Shelf.TestData -c Release -- generate --out artifacts/synthetic-library --preset reference

# full osx-arm64 run on AC power; --large adds 1,000,000 x 384 vectors (about 7 GB in data/ in total, git-ignored)
./measure.sh --large > results/measure.out 2>&1

# functional check on the host RID (what the spike-storage workflow runs), writes results/ci/<rid>.txt
./check.sh <rid>
```

Prototypes: `Baseline` (prints `ok`) and `Storage` (assembly `proto-storage`; `info`, `fts-check`, `load`, `search`,
`fts-bench`, `wal-*`, `vec-gen`, `vec-load`, `vec-check`, `ext-try`, `knn`, `knn-bench`).
`vendor/`, `data/`, `out/` and everything in `results/` except `measure.out` and `ci/*.txt` are git-ignored.
