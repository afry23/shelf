#!/usr/bin/env bash
# Full parsers spike run on osx-arm64: AOT publish per variant (warnings), sizes incl. ReadyToRun and code pages,
# functional checks on the AOT binary (also under de_DE), robustness, JIT-vs-AOT differential, verification and
# throughput on the reference library, large PDFs, cold start, optional owner corpus, gate summary.
# Usage: ./measure.sh > results/measure.out 2>&1
# Requires macOS arm64 (stat -f, zsh, sw_vers, /usr/bin/time -l). Run on AC power.
# Needs artifacts/synthetic-library, artifacts/tiny and artifacts/large-pdf (generator commands are printed if missing).
set -euo pipefail

cd "$(dirname "$0")"
RID=osx-arm64
RUNS=50
WARMUP=5
ROOT=../..
FIX=$ROOT/tests/fixtures
GT=$ROOT/artifacts/synthetic-library/ground-truth.json
TINY=$ROOT/artifacts/tiny
LARGE=$ROOT/artifacts/large-pdf
OUT=out
BIN=$OUT/Parsers-all/proto-parsers-all
BASE=$OUT/Baseline/proto-parsers-baseline
R2R=$OUT/Parsers-r2r/proto-parsers-all
JITDLL=$OUT/Parsers-jit/proto-parsers-all.dll
MUT="${TMPDIR:-/tmp}/shelf-spike-parsers/measure-$$"
mkdir -p results "$OUT" "$MUT"
: > results/metrics.txt
trap 'rm -rf "${MUT:?}"' EXIT

metric() { echo "$1 $2" >> results/metrics.txt; }
getm() { awk -v k="$1" '$1 == k {v = $2} END {print v}' results/metrics.txt; }
le() { awk -v a="$1" -v b="$2" 'BEGIN {exit !(a <= b)}'; }
lt() { awk -v a="$1" -v b="$2" 'BEGIN {exit !(a < b)}'; }
verdict() { if "$@"; then echo pass; else echo FAIL; fi; }
eq() { [ "$1" = "$2" ]; }
# run <name> <cmd...>: output to results/<name>.txt and stdout, exit code recorded (never aborts the script).
run() {
  local name="$1"; shift
  local rc=0
  "$@" > "results/$name.txt" 2>&1 || rc=$?
  cat "results/$name.txt"
  echo "exit code ($name): $rc"
  metric "$name.rc" "$rc"
}
# rss_of <time -l output file>: peak resident set size in MiB
rss_mb() { awk '/maximum resident set size/ {printf "%.0f", $1 / 1048576}' "$1"; }

for D in "$GT" "$TINY/ground-truth.json" "$LARGE/ground-truth.json"; do
  if [ ! -f "$D" ]; then
    echo "missing $D; generate the data with:"
    echo "  dotnet run --project tests/Shelf.TestData -c Release -- generate --out artifacts/synthetic-library --preset reference"
    echo "  dotnet run --project tests/Shelf.TestData -c Release -- generate --out artifacts/tiny --preset tiny --seed 42"
    echo "  dotnet run --project tests/Shelf.TestData -c Release -- generate --out artifacts/large-pdf --seed 42 --works 5 --files 5 --words-min 100000 --words-max 100000"
    exit 1
  fi
done

if command -v hyperfine >/dev/null 2>&1; then TOOL=hyperfine; else TOOL="zsh EPOCHREALTIME loop"; fi
echo "== environment"
echo "date: $(date -u +%Y-%m-%dT%H:%M:%SZ)"
echo "os: $(sw_vers -productName) $(sw_vers -productVersion) ($(uname -m))"
echo "cpu: $(sysctl -n machdep.cpu.brand_string)"
echo "cores: $(sysctl -n hw.ncpu) logical, $(sysctl -n hw.perflevel0.logicalcpu 2>/dev/null || echo '?') performance + $(sysctl -n hw.perflevel1.logicalcpu 2>/dev/null || echo '?') efficiency"
echo "ram_bytes: $(sysctl -n hw.memsize)"
echo "dotnet: $(dotnet --version)"
echo "power: $(pmset -g batt | head -1)"
echo "timing tool: $TOOL ($WARMUP warmups, $RUNS runs, page cache warm; hyperfine is not used, zsh loop is the documented fallback)"

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

publish() {
  # publish <name> <project> <outdir> <msbuild args...>
  local name="$1" proj="$2" dir="$3"; shift 3
  rm -rf "${proj:?}/bin" "${proj:?}/obj" "${dir:?}"
  dotnet publish "$proj" -c Release -r "$RID" "$@" -o "$dir" > "results/$name.publish.log" 2>&1 || { echo "publish $name FAILED"; tail -20 "results/$name.publish.log"; return 1; }
  grep -E 'warning' "results/$name.publish.log" | sed -E 's/ \[[^]]*\]$//' | sort -u > "results/$name.warnings.txt" || true
  local n
  n=$(wc -l < "results/$name.warnings.txt" | tr -d ' ')
  echo "$name: warnings (distinct lines, all codes): $n"
  grep -oE 'warning (IL|AOT)[0-9]+' "results/$name.warnings.txt" | sort | uniq -c | sed 's/^/  /' || true
  sed 's/^/  /' "results/$name.warnings.txt"
  metric "warnings.$name" "$n"
}

echo "== step 1: AOT publish per variant (-c Release -r $RID -p:TrimmerSingleWarn=false), ReadyToRun and code pages variants"
publish baseline Baseline "$OUT/Baseline" -p:TrimmerSingleWarn=false
publish epub Parsers "$OUT/Parsers-epub" -p:Variant=epub -p:TrimmerSingleWarn=false
publish pdf Parsers "$OUT/Parsers-pdf" -p:Variant=pdf -p:TrimmerSingleWarn=false
publish all Parsers "$OUT/Parsers-all" -p:Variant=all -p:TrimmerSingleWarn=false
publish all-codepages Parsers "$OUT/Parsers-all-codepages" -p:Variant=all -p:CodePages=true -p:TrimmerSingleWarn=false
publish epub-codepages Parsers "$OUT/Parsers-epub-codepages" -p:Variant=epub -p:CodePages=true -p:TrimmerSingleWarn=false
echo "-- ReadyToRun single file (self-contained), same source (fallback cost)"
rm -rf Parsers/bin Parsers/obj "$OUT/Parsers-r2r"
dotnet publish Parsers -c Release -r "$RID" -p:Variant=all -p:PublishAot=false -p:PublishReadyToRun=true -p:SelfContained=true -p:PublishSingleFile=true -o "$OUT/Parsers-r2r" > results/r2r.publish.log 2>&1 || { echo "publish r2r FAILED"; tail -20 results/r2r.publish.log; exit 1; }
echo "r2r: warnings (distinct lines): $( (grep -E 'warning' results/r2r.publish.log || true) | sed -E 's/ \[[^]]*\]$//' | sort -u | wc -l | tr -d ' ')"

echo "== step 2: sizes"
sz() { stat -f%z "$1"; }
BS=$(sz "$BASE"); ES=$(sz "$OUT/Parsers-epub/proto-parsers-epub"); PS=$(sz "$OUT/Parsers-pdf/proto-parsers-pdf"); AS=$(sz "$BIN")
CS=$(sz "$OUT/Parsers-all-codepages/proto-parsers-all"); ECS=$(sz "$OUT/Parsers-epub-codepages/proto-parsers-epub"); RS=$(sz "$R2R")
echo "size baseline: $BS B"
echo "size epub (VersOne.Epub): $ES B (delta vs baseline: $((ES - BS)) B)"
echo "size pdf (PdfPig): $PS B (delta vs baseline: $((PS - BS)) B)"
echo "size all: $AS B (delta vs baseline: $((AS - BS)) B)"
echo "size all + CodePagesEncodingProvider: $CS B (delta vs all: $((CS - AS)) B)"
echo "size epub + CodePagesEncodingProvider: $ECS B (delta vs epub: $((ECS - ES)) B)"
echo "size ReadyToRun single file, self-contained: $RS B"
for F in "$OUT"/Parsers-r2r/*; do echo "  $(sz "$F") B $(basename "$F")"; done
metric size.all "$AS"

echo "== step 3: info, isbn-check, epub-check, pdf-check on the AOT binary (and under de_DE)"
run info "$BIN" info
run selfcheck "$BIN" selfcheck "$FIX"
run isbn-check "$BIN" isbn-check
run epub-check "$BIN" epub-check "$FIX"
run pdf-check "$BIN" pdf-check "$FIX"
echo "-- culture: same checks under LANG=de_DE.UTF-8 LC_ALL=de_DE.UTF-8"
DE_SAME=yes
for C in isbn-check epub-check pdf-check; do
  LANG=de_DE.UTF-8 LC_ALL=de_DE.UTF-8 "$BIN" "$C" "$FIX" > "results/$C.de.txt" 2>&1 || true
  if ! cmp -s <(grep -v 'X-E9' "results/$C.txt") <(grep -v 'X-E9' "results/$C.de.txt"); then
    DE_SAME=no
    diff <(grep -v 'X-E9' "results/$C.txt") <(grep -v 'X-E9' "results/$C.de.txt") || true
  fi
done
echo "de_DE output identical (timing rows X-E9 excluded): $DE_SAME"
metric de_same "$DE_SAME"
for V in epub pdf; do
  echo "-- variant $V alone"
  "$OUT/Parsers-$V/proto-parsers-$V" info
  "$OUT/Parsers-$V/proto-parsers-$V" "$V-check" "$FIX" | tail -1
done

echo "== step 4: mutate and robust (E4, P4)"
"$BIN" mutate "$FIX" "$MUT/m1" | tee results/mutate.txt
"$BIN" mutate "$FIX" "$MUT/m2" > "$MUT/m2.txt"
if cmp -s results/mutate.txt "$MUT/m2.txt"; then echo "mutate deterministic (two runs identical): yes"; else echo "mutate deterministic: NO"; fi
run robust "$BIN" robust "$MUT/m1"
for ROW in R-E9.epub R-E13.epub R-E12.epub R-P8.pdf R-P6.pdf; do
  rc=0
  /usr/bin/time -l "$BIN" robust-one "$MUT/m1/$ROW" > "results/time-$ROW.txt" 2>&1 || rc=$?
  echo "robust-one $ROW (/usr/bin/time -l): exit=$rc peak RSS $(rss_mb "results/time-$ROW.txt") MiB; $(grep -E '^(ok|error)' "results/time-$ROW.txt" | cut -c1-200)"
done
echo "-- R-E11 code pages (observation): default build, then the CodePages build with --codepages"
"$BIN" robust-one "$MUT/m1/R-E11.epub" || true
"$OUT/Parsers-all-codepages/proto-parsers-all" robust-one "$MUT/m1/R-E11.epub" --codepages || true
echo "-- R-E11 in the epub-only build with code pages (nothing else registers the provider)"
"$OUT/Parsers-epub-codepages/proto-parsers-epub" robust-one "$MUT/m1/R-E11.epub" --codepages || true
"$OUT/Parsers-epub/proto-parsers-epub" robust-one "$MUT/m1/R-E11.epub" || true

echo "== step 5: differential, AOT vs JIT (dump)"
rm -rf Parsers/bin Parsers/obj "$OUT/Parsers-jit"
dotnet build Parsers -c Release -p:PublishAot=false -o "$OUT/Parsers-jit" > results/jit.build.log 2>&1 || { echo "JIT build FAILED"; tail -20 results/jit.build.log; exit 1; }
jit() { dotnet "$JITDLL" "$@"; }
for SET in fixtures tiny mutations; do
  case $SET in
    fixtures) ARG=("$FIX") ;;
    tiny) ARG=("$TINY") ;;
    mutations) ARG=("$MUT/m1" --isolate) ;;
  esac
  "$BIN" dump "${ARG[@]}" > "results/dump.aot.$SET.txt"
  jit dump "${ARG[@]}" > "results/dump.jit.$SET.txt"
  if cmp -s "results/dump.aot.$SET.txt" "results/dump.jit.$SET.txt"; then
    echo "dump $SET: identical ($(wc -l < "results/dump.aot.$SET.txt" | tr -d ' ') lines, $(wc -c < "results/dump.aot.$SET.txt" | tr -d ' ') B)"
    metric "dump.$SET" identical
  else
    echo "dump $SET: DIFFERENT"
    diff "results/dump.aot.$SET.txt" "results/dump.jit.$SET.txt" | cut -c1-300 | head -20
    metric "dump.$SET" different
  fi
done
# per format (E3: EPUB lines, P3: PDF lines)
E3=pass; P3=pass
for SET in fixtures tiny mutations; do
  cmp -s <(grep -E '\.epub	' "results/dump.aot.$SET.txt") <(grep -E '\.epub	' "results/dump.jit.$SET.txt") || E3=FAIL
  cmp -s <(grep -E '\.pdf	' "results/dump.aot.$SET.txt") <(grep -E '\.pdf	' "results/dump.jit.$SET.txt") || P3=FAIL
done
metric E3 "$E3"; metric P3 "$P3"
echo "dump under de_DE (AOT, fixtures) identical to invariant: $(if LANG=de_DE.UTF-8 LC_ALL=de_DE.UTF-8 "$BIN" dump "$FIX" | cmp -s - results/dump.aot.fixtures.txt; then echo yes; else echo no; fi)"

echo "== step 7: verify on the reference library (E5, P5)"
/usr/bin/time -l "$BIN" verify "$GT" > results/verify.txt 2> results/verify.time.txt || true
cat results/verify.txt
echo "verify wall/user: $(grep -E 'real' results/verify.time.txt | head -1 | sed 's/^ *//')"
E5=pass; P5=pass
awk '$1 == "verify" && $2 ~ /^epub\./ && $2 !~ /files/ {split($3, a, "/"); if (a[1] != a[2]) bad = 1} END {exit bad}' results/verify.txt || E5=FAIL
awk '$1 == "verify" && $2 ~ /^pdf\./ && $2 !~ /files/ {split($3, a, "/"); if (a[1] != a[2]) bad = 1} END {exit bad}' results/verify.txt || P5=FAIL
metric E5 "$E5"; metric P5 "$P5"
echo "-- tiny library verify"
"$BIN" verify "$TINY/ground-truth.json" | tail -1
echo "-- large PDFs verify (page count against ground truth)"
"$BIN" verify "$LARGE/ground-truth.json" | grep -E 'pdf.pages|result'

echo "== step 8: bench (T1, T2)"
NCPU=$(sysctl -n hw.ncpu)
/usr/bin/time -l "$BIN" bench "$GT" --format all --threads "$NCPU" > results/bench.all.txt 2> results/bench.all.time.txt || true
cat results/bench.all.txt
T1_WALL=$(awk '$1 == "bench" {for (i = 1; i <= NF; i++) if ($i ~ /^wall_s=/) {sub("wall_s=", "", $i); print $i}}' results/bench.all.txt)
T1_ERR=$(awk '$1 == "bench" {for (i = 1; i <= NF; i++) if ($i ~ /^errors=/) {sub("errors=", "", $i); print $i}}' results/bench.all.txt)
T2_RSS=$(rss_mb results/bench.all.time.txt)
echo "T1 wall: ${T1_WALL} s (threads=$NCPU, errors=$T1_ERR); peak RSS (/usr/bin/time -l): ${T2_RSS} MiB"
metric T1.wall "$T1_WALL"; metric T1.errors "$T1_ERR"; metric T2.rss "$T2_RSS"
echo "-- thread scaling (observation, first 3000 files of the format): wall seconds"
for F in epub pdf; do
  for T in 1 2 4 "$NCPU"; do
    echo "scaling $F threads=$T: $("$BIN" bench "$GT" --format "$F" --threads "$T" --limit 3000 | head -1 | cut -d' ' -f1-8)"
  done
done
for F in epub pdf; do
  "$BIN" bench "$GT" --format "$F" --threads 1 > "results/bench.$F.t1.txt"
  cat "results/bench.$F.t1.txt"
  metric "bench.$F.t1.total_p95" "$(awk '$1 == "bench-phase" && $4 == "phase=total" {sub("p95=", "", $6); print $6}' "results/bench.$F.t1.txt")"
done

echo "== step 9: large PDFs (T3)"
T3_OK=pass
for P in "$LARGE"/input/*/*.pdf "$LARGE"/input/*/*/*.pdf; do
  [ -f "$P" ] || continue
  /usr/bin/time -l "$BIN" extract "$P" --text > results/large.txt 2> results/large.time.txt || true
  cat results/large.txt
  REAL=$(awk '/ real / {print $1}' results/large.time.txt)
  RSS=$(rss_mb results/large.time.txt)
  echo "  large PDF: wall ${REAL} s, peak RSS ${RSS} MiB (process incl. start-up)"
  le "$REAL" 10 && le "$RSS" 500 || T3_OK=FAIL
done
metric T3 "$T3_OK"

echo "== step 10: cold start (process, $WARMUP warmups, $RUNS runs)"
tm baseline "$BASE"
for F in "$FIX"/derived/*.epub "$FIX"/derived/*.pdf "$FIX"/upstream/*/*; do
  N=$(basename "$F" | tr ' ' '_')
  tm "meta.aot.$N" "$BIN" meta "$F"
  tm "meta.r2r.$N" "$R2R" meta "$F"
done

echo "== step 11: owner corpus (optional)"
if [ -d corpus ] && [ -n "$(ls -A corpus 2>/dev/null)" ]; then
  "$BIN" corpus corpus/
else
  echo "corpus/ missing or empty: not run"
fi

echo "== summary (osx-arm64, this run)"
COLD_MAX=$(awk '$1 ~ /^meta\.aot\..*\.p95$/ {if ($2 > m) m = $2} END {print m}' results/metrics.txt)
R2R_MAX=$(awk '$1 ~ /^meta\.r2r\..*\.p95$/ {if ($2 > m) m = $2} END {print m}' results/metrics.txt)
count() { { grep -E "$1" "$2" || true; } | wc -l | tr -d ' '; }
FAIL_E=$(count '^fail (X-E|C-E|L-E|I-E|M-)' results/epub-check.txt)
FAIL_P=$(count '^fail (X-P|L-P|I-P|M-)' results/pdf-check.txt)
ROB_E=$(count '^fail R-E' results/robust.txt)
ROB_P=$(count '^fail R-P' results/robust.txt)
{
printf '%-4s %-5s %s\n' E1 "$(verdict eq "$(( $(getm warnings.epub) + $(getm warnings.all) ))" 0)" "warnings: epub=$(getm warnings.epub), all=$(getm warnings.all)"
printf '%-4s %-5s %s\n' E2 "$(verdict eq "$FAIL_E$(getm isbn-check.rc)$(getm epub-check.rc)" 000)" "failed EPUB rows=$FAIL_E, epub-check exit=$(getm epub-check.rc), isbn-check exit=$(getm isbn-check.rc) (four-RID part: check.sh in CI)"
printf '%-4s %-5s %s\n' E3 "$E3" "dump AOT == JIT for EPUB lines (fixtures, tiny, mutations)"
printf '%-4s %-5s %s\n' E4 "$(verdict eq "$ROB_E$(getm robust.rc)" 00)" "failed R-E gate rows=$ROB_E, robust exit=$(getm robust.rc)"
printf '%-4s %-5s %s\n' E5 "$E5" "verify on 8,300 EPUBs"
printf '%-4s %-5s %s\n' P1 "$(verdict eq "$(( $(getm warnings.pdf) + $(getm warnings.all) ))" 0)" "warnings: pdf=$(getm warnings.pdf), all=$(getm warnings.all)"
printf '%-4s %-5s %s\n' P2 "$(verdict eq "$FAIL_P$(getm pdf-check.rc)" 00)" "failed PDF rows=$FAIL_P, pdf-check exit=$(getm pdf-check.rc) (four-RID part: check.sh in CI)"
printf '%-4s %-5s %s\n' P3 "$P3" "dump AOT == JIT for PDF lines (fixtures, tiny, mutations)"
printf '%-4s %-5s %s\n' P4 "$(verdict eq "$ROB_P$(getm robust.rc)" 00)" "failed R-P gate rows=$ROB_P, robust exit=$(getm robust.rc)"
printf '%-4s %-5s %s\n' P5 "$P5" "verify on 6,700 PDFs"
printf '%-4s %-5s %s\n' T1 "$( (le "$T1_WALL" 300 && eq "$T1_ERR" 0) && echo pass || echo FAIL)" "15,000 files, threads=$NCPU: wall ${T1_WALL} s (limit 300 s), errors=$T1_ERR"
printf '%-4s %-5s %s\n' T2 "$(verdict le "$T2_RSS" 1024)" "peak RSS of the T1 run ${T2_RSS} MiB (limit 1024 MiB)"
printf '%-4s %-5s %s\n' T3 "$T3_OK" "large PDFs: see step 9 (limits 10 s, 500 MiB)"
printf '%-4s %-5s %s\n' CS "$(verdict lt "$COLD_MAX" 150)" "target: max over fixtures of meta p95 AOT=${COLD_MAX} ms (R2R=${R2R_MAX} ms), baseline process p95=$(getm baseline.p95) ms (< 150 ms)"
echo "-- sizes: baseline $BS, epub +$((ES - BS)), pdf +$((PS - BS)), all +$((AS - BS)), codepages +$((CS - AS)) (epub-only +$((ECS - ES))), ReadyToRun single file $RS"
} | tee results/summary.txt

bf() { awk -v k="$2" '$1 == "bench" {for (i = 1; i <= NF; i++) if ($i ~ "^" k "=") {sub(k "=", "", $i); print $i}}' "$1"; }
echo "-- extrapolation (M1 risk, not a gate): realistic library of 8,300 EPUBs + 6,700 PDFs with 100,000 words each (PDFs of about 300 pages)"
E_RATE=$(bf results/bench.epub.t1.txt words_per_s); P_RATE=$(bf results/bench.pdf.t1.txt words_per_s)
E_WALL=$(bf results/bench.epub.t1.txt wall_s); P_WALL=$(bf results/bench.pdf.t1.txt wall_s)
SPEEDUP=$(awk -v a="$E_WALL" -v b="$P_WALL" -v t="$T1_WALL" 'BEGIN {printf "%.2f", (a + b) / t}')
echo "single-thread words/s: EPUB ${E_RATE}, PDF ${P_RATE}; speedup of the ${NCPU}-thread run over one thread: ${SPEEDUP}x (reference library: single-thread ${E_WALL} s + ${P_WALL} s, ${NCPU} threads ${T1_WALL} s)"
awk -v er="$E_RATE" -v pr="$P_RATE" -v sp="$SPEEDUP" 'BEGIN {
  e = 8300 * 100000 / er; p = 6700 * 100000 / pr;
  printf "estimated single thread: EPUB %.0f s + PDF %.0f s = %.0f s; with the measured speedup: %.0f s (%.1f min) against the 900 s end-to-end budget (extraction share gate 300 s)\n", e, p, e + p, (e + p) / sp, (e + p) / sp / 60
}'
echo "caveat: synthetic files are small, uncompressed, Helvetica-only, 6-16 PDF pages; real books have embedded fonts, Flate streams and more complex layout."
echo "done: $(date -u +%Y-%m-%dT%H:%M:%SZ)"
if grep -qE '^(E[1-5]|P[1-5]|T[1-3]) +FAIL' results/summary.txt; then
  echo "GATES FAILED (see summary)"
  exit 1
fi
