#!/usr/bin/env bash
# Portable functional check of the storage spike on one RID (no timing gates).
# Usage: ./check.sh <rid>      (rid: osx-arm64 | osx-x64 | linux-x64 | linux-arm64; must match the host)
# Steps: fetch the pinned sqlite-vec asset, AOT publish (fail on any IL*/AOT warning), put vec0.* next to
# the binary, run info / fts-check / vec-check, smoke data (200 works), vec-gen/vec-load/knn, short WAL run.
# Output (also written to results/ci/<rid>.txt): everything below. Linux needs clang and zlib1g-dev.
set -euo pipefail

cd "$(dirname "$0")"
RID="${1:?usage: check.sh <rid>}"
case "$RID" in
  osx-arm64|osx-x64) EXT=dylib ;;
  linux-x64|linux-arm64) EXT=so ;;
  *) echo "unknown rid: $RID" >&2; exit 2 ;;
esac
mkdir -p results/ci
OUTDIR="out/Storage-$RID"
BIN="$OUTDIR/proto-storage"

size_of() { if [ "$(uname -s)" = Darwin ]; then stat -f%z "$1"; else stat -c%s "$1"; fi; }
kv() { echo "$1" | tr ' ' '\n' | awk -F= -v k="$2" '$1 == k {print $2}'; }

main() {
  echo "== environment"
  echo "date: $(date -u +%Y-%m-%dT%H:%M:%SZ)"
  echo "rid: $RID"
  echo "uname: $(uname -srm)"
  echo "dotnet: $(dotnet --version)"
  if [ "$(uname -s)" = Linux ]; then
    echo "glibc (runner): $(ldd --version 2>&1 | head -1)"
  else
    echo "macOS: $(sw_vers -productVersion)"
  fi

  echo "== sqlite-vec asset (pinned SHA-256)"
  ./fetch-sqlite-vec.sh "$RID"
  LIB="vendor/$RID/vec0.$EXT"
  echo "size of vec0.$EXT: $(size_of "$LIB") B"
  if [ "$(uname -s)" = Darwin ]; then
    codesign -dv "$LIB" 2>&1 | grep -E 'Signature|CodeDirectory|TeamIdentifier|not signed' || true
    nm -gU "$LIB" | grep -E 'sqlite3_vec_init' || true
    otool -L "$LIB" || true
  else
    echo "highest GLIBC symbol version required by vec0.so:"
    objdump -T "$LIB" | grep -oE 'GLIBC_[0-9.]+' | sort -V | uniq | tail -1 || true
    echo "NEEDED entries:"
    readelf -d "$LIB" | grep NEEDED || true
    nm -D --defined-only "$LIB" | grep -E 'sqlite3_vec_init' || true
  fi

  echo "== AOT publish (-c Release -r $RID -p:TrimmerSingleWarn=false)"
  rm -rf "${OUTDIR:?}"
  dotnet publish Storage -c Release -r "$RID" -p:TrimmerSingleWarn=false -o "$OUTDIR" > "results/ci/$RID.publish.log" 2>&1 || { tail -30 "results/ci/$RID.publish.log"; echo "publish FAILED"; return 1; }
  if grep -E 'warning (IL|AOT)[0-9]+' "results/ci/$RID.publish.log" | sed -E 's/ \[[^]]*\]$//' | sort -u | tee "results/ci/$RID.warnings.txt" | grep -q .; then
    echo "AOT/trim warnings found:"
    cat "results/ci/$RID.warnings.txt"
    return 1
  fi
  echo "AOT/trim warnings: 0"
  for f in "$OUTDIR"/*; do echo "  $(size_of "$f") B $(basename "$f")"; done
  echo "size proto-storage: $(size_of "$BIN") B"
  echo "size libe_sqlite3.$EXT: $(size_of "$OUTDIR/libe_sqlite3.$EXT") B"
  cp "$LIB" "$OUTDIR/vec0.$EXT"

  echo "== info"
  "$BIN" info
  echo "== fts-check"
  "$BIN" fts-check
  echo "== vec-check"
  "$BIN" vec-check --ext "$PWD/$LIB"

  echo "== smoke data"
  WORK="$(mktemp -d)"
  trap 'rm -rf "${WORK:?}"' EXIT
  dotnet run --project ../../tests/Shelf.TestData -c Release -- generate --out "$WORK/lib" --works 200 --files 300 > /dev/null
  "$BIN" load "$WORK/lib/ground-truth.json" "$WORK/lib.db" --vocab ../../tests/Shelf.TestData/Vocabulary --chunks 2000
  "$BIN" search "$WORK/lib.db" "patterns" | tail -3
  "$BIN" vec-gen "$WORK/vec.db" --table chunks --count 10000 --dim 384 --seed 43 --flat "$WORK/vec.f32"
  "$BIN" vec-load "$WORK/vec.db" --table chunks --dim 384
  "$BIN" knn "$WORK/vec.db" --mode vec --table chunks --dim 384 --query-seed 1 | grep '^knn '
  "$BIN" knn "$WORK/vec.db" --mode bf-sql --table chunks --dim 384 --query-seed 1 | grep '^knn '
  "$BIN" knn-bench "$WORK/vec.db" --mode vec --table chunks --dim 384 --queries 100 | grep '^knn-bench'
  "$BIN" vec-gen "$WORK/vec1024.db" --table chunks --count 1500 --dim 1024 --seed 44
  "$BIN" vec-load "$WORK/vec1024.db" --table chunks --dim 1024
  "$BIN" knn-bench "$WORK/vec1024.db" --mode vec --table chunks --dim 1024 --queries 100 | grep '^knn-bench'

  echo "== short WAL run (W1, 10 s; timings are informational)"
  "$BIN" fts-bench "$WORK/lib.db" queries/fts-fixed.txt "$WORK/lib.db.queries.txt" | grep overall
  "$BIN" wal-writer "$WORK/lib.db" --seconds 10 --batch 50 --id 1 > "$WORK/w1.txt" 2>&1 &
  WP=$!
  RPS=""
  for i in 1 2 3 4; do
    "$BIN" wal-reader "$WORK/lib.db" --seconds 10 --queries "$WORK/lib.db.queries.txt" > "$WORK/r$i.txt" 2>&1 &
    RPS="$RPS $!"
  done
  wait "$WP" || true
  for p in $RPS; do wait "$p" || true; done
  cat "$WORK"/w1.txt "$WORK"/r*.txt
  FAILED=0
  for f in "$WORK"/w1.txt "$WORK"/r*.txt; do
    n="$(kv "$(grep -E '^wal-(writer|reader) ' "$f" || true)" failed)"
    FAILED=$((FAILED + ${n:-1000}))
  done
  echo "failed operations total: $FAILED"
  if [ "$FAILED" -ne 0 ]; then return 1; fi
  echo "== check $RID: ok"
}

main 2>&1 | tee "results/ci/$RID.txt"
