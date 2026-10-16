#!/usr/bin/env bash
# Full storage spike run on osx-arm64: AOT publish, warnings, functional checks on the AOT binary,
# data loading, FTS latency, WAL concurrency, vector search (sqlite-vec and brute force),
# process-level timing, gate summary.
# Usage: ./measure.sh [--large]      (--large adds the 1,000,000 x 384 information dataset)
# Requires macOS arm64 (stat -f, zsh, sw_vers, codesign, xattr). Run on AC power.
# Needs artifacts/synthetic-library/ground-truth.json (generator command is printed if missing).
# Output: ./measure.sh > results/measure.out 2>&1
set -euo pipefail

cd "$(dirname "$0")"
RID=osx-arm64
RUNS=50
WARMUP=5
LARGE=0
if [ "${1:-}" = "--large" ]; then LARGE=1; fi
GT=../../artifacts/synthetic-library/ground-truth.json
VOCAB=../../tests/Shelf.TestData/Vocabulary
OUT=out
BIN=$OUT/Storage/proto-storage
BASE=$OUT/Baseline/proto-storage-baseline
mkdir -p results data "$OUT"
: > results/metrics.txt

metric() { echo "$1 $2" >> results/metrics.txt; }
getm() { awk -v k="$1" '$1 == k {v = $2} END {print v}' results/metrics.txt; }
kv() { echo "$1" | tr ' ' '\n' | awk -F= -v k="$2" '$1 == k {print $2}'; }
lt() { awk -v a="$1" -v b="$2" 'BEGIN {exit !(a < b)}'; }
le() { awk -v a="$1" -v b="$2" 'BEGIN {exit !(a <= b)}'; }
verdict() { if "$@"; then echo pass; else echo FAIL; fi; }
eq() { [ "$1" = "$2" ]; }
all_eq() { local want="$1"; shift; local v; for v in "$@"; do [ "$v" = "$want" ] || return 1; done; }

if [ ! -f "$GT" ]; then
  echo "missing $GT; generate it with:"
  echo "  dotnet run --project tests/Shelf.TestData -c Release -- generate --out artifacts/synthetic-library --preset reference"
  exit 1
fi

if command -v hyperfine >/dev/null 2>&1; then TOOL=hyperfine; else TOOL="zsh EPOCHREALTIME loop"; fi
echo "== environment"
echo "date: $(date -u +%Y-%m-%dT%H:%M:%SZ)"
echo "os: $(sw_vers -productName) $(sw_vers -productVersion) ($(uname -m))"
echo "cpu: $(sysctl -n machdep.cpu.brand_string)"
echo "ram_bytes: $(sysctl -n hw.memsize)"
echo "dotnet: $(dotnet --version)"
echo "power: $(pmset -g batt | head -1)"
echo "timing tool: $TOOL ($WARMUP warmups, $RUNS runs, page cache warm; hyperfine is not used, zsh loop is the documented fallback)"
echo "large: $LARGE"

# tm <label> <binary> <args...>: process-level timing, prints and records median/p95/min/max in ms.
tm() {
  local label="$1"; shift
  local res
  res="$(zsh -c '
    zmodload zsh/datetime
    bin=$1; shift
    for i in {1..'"$WARMUP"'}; do $bin "$@" >/dev/null 2>&1; done
    for i in {1..'"$RUNS"'}; do
      s=$EPOCHREALTIME; $bin "$@" >/dev/null 2>&1; e=$EPOCHREALTIME
      printf "%.3f\n" $(( (e - s) * 1000 ))
    done | sort -n | awk "{a[NR]=\$1} END {n=NR; m=(n%2)?a[(n+1)/2]:(a[n/2]+a[n/2+1])/2; p=int(n*0.95); if (p<n*0.95) p++; printf \"%.2f %.2f %.2f %.2f\n\", m, a[p], a[1], a[n]}"
  ' _ "$@")"
  # shellcheck disable=SC2086
  set -- $res
  echo "timing $label median=$1 p95=$2 min=$3 max=$4 (ms)"
  metric "$label.median" "$1"
  metric "$label.p95" "$2"
}

echo "== step 1: sqlite-vec release asset (pinned SHA-256)"
./fetch-sqlite-vec.sh osx-arm64
shasum -a 256 vendor/osx-arm64/archive/*.tar.gz vendor/osx-arm64/vec0.dylib
grep macos-aarch64 sqlite-vec.sha256

echo "== step 2: AOT publish (-c Release -r $RID -p:TrimmerSingleWarn=false)"
WARN_TOTAL=0
for P in Baseline Storage; do
  rm -rf "${P:?}/bin" "${P:?}/obj" "${OUT:?}/${P:?}"
  dotnet publish "$P" -c Release -r "$RID" -p:TrimmerSingleWarn=false -o "$OUT/$P" > "results/$P.publish.log" 2>&1 || { echo "publish $P FAILED"; tail -20 "results/$P.publish.log"; exit 1; }
  grep -E 'warning' "results/$P.publish.log" | sed -E 's/ \[[^]]*\]$//' | sort -u > "results/$P.warnings.txt" || true
  N=$(wc -l < "results/$P.warnings.txt" | tr -d ' ')
  echo "$P: warnings (distinct lines, all codes): $N"
  grep -oE 'warning (IL|AOT)[0-9]+' "results/$P.warnings.txt" | sort | uniq -c | sed 's/^/  /' || true
  sed 's/^/  /' "results/$P.warnings.txt"
  if [ "$P" = Storage ]; then WARN_TOTAL=$N; fi
done
metric warnings "$WARN_TOTAL"
cp vendor/osx-arm64/vec0.dylib "$OUT/Storage/vec0.dylib"

echo "== step 3: output directory and sizes"
ls -l "$OUT/Baseline" "$OUT/Storage"
BS=$(stat -f%z "$BASE")
SS=$(stat -f%z "$BIN")
LS=$(stat -f%z "$OUT/Storage/libe_sqlite3.dylib")
VS=$(stat -f%z "$OUT/Storage/vec0.dylib")
echo "size baseline binary: $BS B"
echo "size storage binary: $SS B (delta vs baseline: $((SS - BS)) B)"
echo "size libe_sqlite3.dylib: $LS B"
echo "size vec0.dylib: $VS B"
echo "archive payload (storage binary + libe_sqlite3 + vec0, without dSYM): $((SS + LS + VS)) B"
echo "-- signatures"
codesign -dv "$BIN" 2>&1 | grep -E 'Identifier|Format|CodeDirectory|Signature|TeamIdentifier' | sed 's/^/  proto-storage: /' || true
codesign -dv "$OUT/Storage/libe_sqlite3.dylib" 2>&1 | grep -E 'Signature|CodeDirectory|TeamIdentifier' | sed 's/^/  libe_sqlite3.dylib: /' || true
codesign -dv "$OUT/Storage/vec0.dylib" 2>&1 | grep -E 'Signature|CodeDirectory|TeamIdentifier' | sed 's/^/  vec0.dylib: /' || true
echo "-- xattr of the curl-downloaded vec0.dylib:"; xattr -l vendor/osx-arm64/vec0.dylib | sed 's/^/  /'; echo "  (end)"
echo "-- exported init symbols of vec0.dylib:"; nm -gU vendor/osx-arm64/vec0.dylib | grep -E 'sqlite3_vec.*init' | sed 's/^/  /'
echo "-- libraries linked by vec0.dylib:"; otool -L vendor/osx-arm64/vec0.dylib | sed 's/^/  /'

echo "== step 4: info, fts-check, vec-check on the AOT binary"
"$BIN" info
FTS_RC=0; "$BIN" fts-check > results/fts-check.txt 2>&1 || FTS_RC=$?
cat results/fts-check.txt
echo "fts-check exit code: $FTS_RC"
VEC_RC=0; "$BIN" vec-check --ext "$PWD/vendor/osx-arm64/vec0.dylib" > results/vec-check.txt 2>&1 || VEC_RC=$?
cat results/vec-check.txt
echo "vec-check exit code: $VEC_RC"
echo "-- culture: same checks under LANG=de_DE.UTF-8 LC_ALL=de_DE.UTF-8"
LANG=de_DE.UTF-8 LC_ALL=de_DE.UTF-8 "$BIN" fts-check > results/fts-check.de.txt 2>&1 || true
LANG=de_DE.UTF-8 LC_ALL=de_DE.UTF-8 "$BIN" vec-check --ext "$PWD/vendor/osx-arm64/vec0.dylib" > results/vec-check.de.txt 2>&1 || true
if cmp -s results/fts-check.txt results/fts-check.de.txt && cmp -s results/vec-check.txt results/vec-check.de.txt; then
  echo "de_DE output identical: yes"
else
  echo "de_DE output identical: no"
  diff results/fts-check.txt results/fts-check.de.txt || true
  diff results/vec-check.txt results/vec-check.de.txt || true
fi
metric fts_rc "$FTS_RC"; metric vec_rc "$VEC_RC"

echo "-- V3d: bare name with DYLD_LIBRARY_PATH (binary copied to a directory without vec0.dylib)"
D3=$(mktemp -d)
cp "$BIN" "$OUT/Storage/libe_sqlite3.dylib" "$D3/"
VENDOR_DIR="$PWD/vendor/osx-arm64"
(cd "$D3" && DYLD_LIBRARY_PATH="$VENDOR_DIR" ./proto-storage ext-try vec0) | sed 's/^/  V3d: /' || true
(cd "$D3" && ./proto-storage ext-try vec0) | sed 's/^/  V3d control without DYLD_LIBRARY_PATH: /' || true
rm -rf "${D3:?}"
echo "-- V9: quarantine attribute on a copy of vec0.dylib"
D9=$(mktemp -d)
cp vendor/osx-arm64/vec0.dylib "$D9/vec0.dylib"
xattr -w com.apple.quarantine "0081;00000000;Safari;" "$D9/vec0.dylib"
xattr -l "$D9/vec0.dylib" | sed 's/^/  xattr: /'
"$BIN" ext-try "$D9/vec0.dylib" | sed 's/^/  V9 (absolute path, quarantined copy): /' || true
rm -rf "${D9:?}"

echo "== step 5: data (10,000 works, 100,000 chunks; 384 and 1024 dimensions)"
rm -f data/vec-*.db data/vec-*.db-wal data/vec-*.db-shm data/vec-*.f32
"$BIN" load "$GT" data/lib.db --vocab "$VOCAB" | tee results/load.txt
for DIM in 384 1024; do
  "$BIN" vec-gen data/vec-works-$DIM.db --table works --count 10000 --dim $DIM --seed 42 --flat data/vec-works-$DIM.f32
  "$BIN" vec-load data/vec-works-$DIM.db --table works --dim $DIM
  "$BIN" vec-gen data/vec-chunks-$DIM.db --table chunks --count 100000 --dim $DIM --seed 43 --flat data/vec-chunks-$DIM.f32
  "$BIN" vec-load data/vec-chunks-$DIM.db --table chunks --dim $DIM
done
if [ "$LARGE" = 1 ]; then
  "$BIN" vec-gen data/vec-chunks-384-1m.db --table chunks --count 1000000 --dim 384 --seed 44 --flat data/vec-chunks-384-1m.f32
  "$BIN" vec-load data/vec-chunks-384-1m.db --table chunks --dim 384
fi
ls -l data
echo "data directory total: $(du -sh data | cut -f1)"

echo "== step 6: FTS latency in process (A4, target)"
"$BIN" fts-bench data/lib.db queries/fts-fixed.txt data/lib.db.queries.txt > results/fts-bench.works.txt
cat results/fts-bench.works.txt
metric a4_p95 "$(kv "$(grep overall results/fts-bench.works.txt)" p95)"
"$BIN" fts-bench data/lib.db data/lib.db.chunks.queries.txt --content > results/fts-bench.chunks.txt
cat results/fts-bench.chunks.txt
metric content_p95 "$(kv "$(grep overall results/fts-bench.chunks.txt)" p95)"

echo "== step 7: process-level timing (A5, B4, C3)"
tm baseline "$BASE"
tm search_works "$BIN" search data/lib.db "patterns data"
tm search_chunks "$BIN" search data/lib.db "data system" --content
for SPEC in "works 384 vec-works-384" "works 1024 vec-works-1024" "chunks 384 vec-chunks-384" "chunks 1024 vec-chunks-1024"; do
  # shellcheck disable=SC2086
  set -- $SPEC
  T=$1; D=$2; F=$3
  tm "knn_vec_${T}_${D}" "$BIN" knn "data/$F.db" --mode vec --table "$T" --dim "$D" --query-seed 1
  tm "knn_bfsql_${T}_${D}_t1" "$BIN" knn "data/$F.db" --mode bf-sql --table "$T" --dim "$D" --query-seed 1 --threads 1
  tm "knn_bfsql_${T}_${D}_tall" "$BIN" knn "data/$F.db" --mode bf-sql --table "$T" --dim "$D" --query-seed 1 --threads all
  tm "knn_bfmmap_${T}_${D}_t1" "$BIN" knn "data/$F.db" --mode bf-mmap --table "$T" --dim "$D" --query-seed 1 --flat "data/$F.f32" --threads 1
  tm "knn_bfmmap_${T}_${D}_tall" "$BIN" knn "data/$F.db" --mode bf-mmap --table "$T" --dim "$D" --query-seed 1 --flat "data/$F.f32" --threads all
done
echo "-- phase breakdown of one run per mode (chunks 100000 x 384; vec and bf-sql three times each)"
for REP in 1 2 3; do for M in vec bf-sql; do "$BIN" knn data/vec-chunks-384.db --mode $M --table chunks --dim 384 --query-seed 1 --threads 1 | grep '^knn '; done; done
"$BIN" knn data/vec-chunks-384.db --mode bf-mmap --table chunks --dim 384 --query-seed 1 --flat data/vec-chunks-384.f32 | grep '^knn '
"$BIN" knn data/vec-chunks-384.db --mode bf-sql --table chunks --dim 384 --query-seed 1 --threads 1 --dot manual | grep '^knn '

if [ "$LARGE" = 1 ]; then
  tm knn_vec_chunks_384_1m "$BIN" knn data/vec-chunks-384-1m.db --mode vec --table chunks --dim 384 --query-seed 1
  tm knn_bfsql_chunks_384_1m_t1 "$BIN" knn data/vec-chunks-384-1m.db --mode bf-sql --table chunks --dim 384 --query-seed 1 --threads 1
  tm knn_bfsql_chunks_384_1m_tall "$BIN" knn data/vec-chunks-384-1m.db --mode bf-sql --table chunks --dim 384 --query-seed 1 --threads all
  tm knn_bfmmap_chunks_384_1m_t1 "$BIN" knn data/vec-chunks-384-1m.db --mode bf-mmap --table chunks --dim 384 --query-seed 1 --flat data/vec-chunks-384-1m.f32 --threads 1
fi
echo "== step 8: knn-bench in process (B2, C2: results verified against the scalar double reference)"
BENCH_FAIL=0
for SPEC in "works 384 vec-works-384" "works 1024 vec-works-1024" "chunks 384 vec-chunks-384" "chunks 1024 vec-chunks-1024"; do
  # shellcheck disable=SC2086
  set -- $SPEC
  T=$1; D=$2; F=$3
  for ARGS in "--mode vec" "--mode bf-sql --threads 1" "--mode bf-sql --threads all" "--mode bf-sql --threads 1 --dot manual" "--mode bf-mmap --threads 1 --flat data/$F.f32" "--mode bf-mmap --threads all --flat data/$F.f32"; do
    LINE=""
    # shellcheck disable=SC2086
    LINE=$("$BIN" knn-bench "data/$F.db" $ARGS --table "$T" --dim "$D" --queries 100 | grep -E '^(knn-bench|fail)') || BENCH_FAIL=$((BENCH_FAIL + 1))
    echo "$LINE"
    case "$ARGS" in
      "--mode vec") metric "bench_vec_${T}_${D}_verified" "$(kv "$LINE" verified)" ;;
      "--mode bf-sql --threads 1") metric "bench_bf_${T}_${D}_verified" "$(kv "$LINE" verified)" ;;
    esac
  done
done
if [ "$LARGE" = 1 ]; then
  for ARGS in "--mode vec" "--mode bf-sql --threads 1" "--mode bf-sql --threads all"; do
    # shellcheck disable=SC2086
    "$BIN" knn-bench data/vec-chunks-384-1m.db $ARGS --table chunks --dim 384 --queries 20 | grep -E '^(knn-bench|fail)' || BENCH_FAIL=$((BENCH_FAIL + 1))
  done
fi
metric bench_fail "$BENCH_FAIL"

echo "== step 9: WAL concurrency (A3), 30 s per run, copies of data/lib.db"
run_wal() {
  local name="$1" writers="$2" think="${3:-0}"
  cp data/lib.db "data/$name.db"
  rm -f "data/$name.db-wal" "data/$name.db-shm"
  local pids=()
  local i
  for i in $(seq 1 "$writers"); do
    "$BIN" wal-writer "data/$name.db" --seconds 30 --batch 50 --id "$i" > "results/$name.w$i.txt" 2>&1 &
    pids+=($!)
  done
  for i in 1 2 3 4; do
    "$BIN" wal-reader "data/$name.db" --seconds 30 --sleep-ms "$think" --queries data/lib.db.queries.txt > "results/$name.r$i.txt" 2>&1 &
    pids+=($!)
  done
  local p
  for p in "${pids[@]}"; do wait "$p" || true; done
  cat results/"$name".w*.txt results/"$name".r*.txt | sed "s/^/  $name: /"
  local failed_total=0 maxp95=0 f line fl pv
  for f in results/"$name".w*.txt results/"$name".r*.txt; do
    line=$(grep -E '^wal-(writer|reader) ' "$f" || true)
    fl=$(kv "$line" failed)
    failed_total=$((failed_total + ${fl:-1000}))
  done
  for f in results/"$name".r*.txt; do
    pv=$(kv "$(grep -E '^wal-reader ' "$f" || true)" p95_ms)
    maxp95=$(awk -v a="$maxp95" -v b="${pv:-9999}" 'BEGIN {print (b + 0 > a + 0) ? b : a}')
  done
  echo "$name: failed operations total=$failed_total, reader p95 (max over readers)=$maxp95 ms, WAL file after run: $(stat -f%z "data/$name.db-wal" 2>/dev/null || echo 0) B"
  metric "${name}_failed" "$failed_total"
  metric "${name}_reader_p95" "$maxp95"
}
run_wal w1 1
run_wal w2 2
echo "-- W1t (observation): as W1, readers sleep 20 ms between queries (WAL growth with realistic readers)"
run_wal w1t 1 20
echo "-- W3 (observation): writer A holds a transaction for 6 s, writer B tries with a 5 s timeout"
cp data/lib.db data/w3.db
rm -f data/w3.db-wal data/w3.db-shm
"$BIN" wal-hold data/w3.db --seconds 6 > results/w3.hold.txt 2>&1 &
HP=$!
until grep -q holding results/w3.hold.txt 2>/dev/null; do sleep 0.1; done
"$BIN" wal-try data/w3.db | sed 's/^/  /'
wait "$HP" || true
sed 's/^/  /' results/w3.hold.txt
rm -f data/w1t.db data/w1t.db-wal data/w1t.db-shm data/w1.db data/w1.db-wal data/w1.db-shm data/w2.db data/w2.db-wal data/w2.db-shm data/w3.db data/w3.db-wal data/w3.db-shm

echo "== summary (osx-arm64, this run)"
printf '%-4s %-8s %s\n' ID RESULT MEASURED
printf '%-4s %-8s %s\n' A1 "$(verdict eq "$(getm warnings)" 0)" "warnings=$(getm warnings)"
printf '%-4s %-8s %s\n' A2 "$(verdict eq "$(getm fts_rc)" 0)" "fts-check exit=$(getm fts_rc) (four-RID part: check.sh in CI)"
printf '%-4s %-8s %s\n' A3 "$( (all_eq 0 "$(getm w1_failed)" "$(getm w2_failed)" && lt "$(getm w1_reader_p95)" 100) && echo pass || echo FAIL)" "W1 failed=$(getm w1_failed) reader p95=$(getm w1_reader_p95) ms; W2 failed=$(getm w2_failed) reader p95=$(getm w2_reader_p95) ms"
printf '%-4s %-8s %s\n' A4 "$(verdict lt "$(getm a4_p95)" 100)" "works FTS in-process p95=$(getm a4_p95) ms"
printf '%-4s %-8s %s\n' A5 "$(verdict lt "$(getm search_works.p95)" 150)" "search process p95=$(getm search_works.p95) ms (baseline p95=$(getm baseline.p95) ms)"
printf '%-4s %-8s %s\n' T1 info "content FTS in-process p95=$(getm content_p95) ms (target < 100); search --content process p95=$(getm search_chunks.p95) ms"
printf '%-4s %-8s %s\n' B1 "$(verdict eq "$(getm vec_rc)" 0)" "vec-check exit=$(getm vec_rc) (V3b gated; four-RID part: check.sh in CI)"
printf '%-4s %-8s %s\n' B2 "$(verdict all_eq 100/100 "$(getm bench_vec_works_384_verified)" "$(getm bench_vec_works_1024_verified)" "$(getm bench_vec_chunks_384_verified)" "$(getm bench_vec_chunks_1024_verified)")" "knn-bench vec verified works384=$(getm bench_vec_works_384_verified) works1024=$(getm bench_vec_works_1024_verified) chunks384=$(getm bench_vec_chunks_384_verified) chunks1024=$(getm bench_vec_chunks_1024_verified)"
printf '%-4s %-8s %s\n' B3 "$(verdict eq "$(getm warnings)" 0)" "same binary as A1"
printf '%-4s %-8s %s\n' B4 "$( (le "$(getm knn_vec_works_384.p95)" 100 && le "$(getm knn_vec_chunks_384.p95)" 400) && echo pass || echo FAIL)" "vec process p95: works 10k x 384=$(getm knn_vec_works_384.p95) ms (<=100), chunks 100k x 384=$(getm knn_vec_chunks_384.p95) ms (<=400)"
printf '%-4s %-8s %s\n' B5 see-ADR "license research"
printf '%-4s %-8s %s\n' C1 "$(verdict eq "$(getm warnings)" 0)" "same binary as A1"
printf '%-4s %-8s %s\n' C2 "$( (all_eq 100/100 "$(getm bench_bf_works_384_verified)" "$(getm bench_bf_works_1024_verified)" "$(getm bench_bf_chunks_384_verified)" "$(getm bench_bf_chunks_1024_verified)" && eq "$(getm bench_fail)" 0) && echo pass || echo FAIL)" "knn-bench bf-sql verified works384=$(getm bench_bf_works_384_verified) works1024=$(getm bench_bf_works_1024_verified) chunks384=$(getm bench_bf_chunks_384_verified) chunks1024=$(getm bench_bf_chunks_1024_verified); knn-bench failures=$(getm bench_fail)"
printf '%-4s %-8s %s\n' C3 "$( (le "$(getm knn_bfsql_works_384_t1.p95)" 100 && le "$(getm knn_bfsql_chunks_384_t1.p95)" 400) && echo pass || echo FAIL)" "bf-sql process p95 (threads=1, gated): works 10k x 384=$(getm knn_bfsql_works_384_t1.p95) ms, chunks 100k x 384=$(getm knn_bfsql_chunks_384_t1.p95) ms; threads=all: $(getm knn_bfsql_works_384_tall.p95) / $(getm knn_bfsql_chunks_384_tall.p95) ms"
echo "-- fallback threshold input: bf-sql process p95 (threads=1 / all) and vec p95 per size"
for SPEC in "works 384 10000" "works 1024 10000" "chunks 384 100000" "chunks 1024 100000"; do
  # shellcheck disable=SC2086
  set -- $SPEC
  echo "  $3 x $2: $(getm "knn_bfsql_$1_$2_t1.p95") / $(getm "knn_bfsql_$1_$2_tall.p95") ms; vec: $(getm "knn_vec_$1_$2.p95") ms"
done
if [ "$LARGE" = 1 ]; then
  echo "  1000000 x 384: $(getm knn_bfsql_chunks_384_1m_t1.p95) / $(getm knn_bfsql_chunks_384_1m_tall.p95) ms; vec: $(getm knn_vec_chunks_384_1m.p95) ms"
fi
echo "done"
