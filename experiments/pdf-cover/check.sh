#!/usr/bin/env bash
# Functional check of the PDF cover spike on osx-arm64 (ADR 0010, no timing gates).
# Usage: ./check.sh
# Steps: AOT and JIT publish of proto-cover (K1), rendering and formats (K2, K3), determinism and AOT vs JIT (K4, K9),
# robustness with broken inputs (K5), thread safety (K6), copied publish directory (K7), the combined binary (K8),
# observations X-1. Output (also written to results/local/osx-arm64.txt): everything below.
set -euo pipefail

cd "$(dirname "$0")"
RID=osx-arm64
mkdir -p results/local
AOT="$PWD/out/AOT"
JIT="$PWD/out/JIT"
WORK="$(mktemp -d)"
FIX="$PWD/../../tests/fixtures"
trap 'rm -rf "${WORK:?}"' EXIT

size_of() { stat -f%z "$1"; }
FAILS=0
row() { # row <id> <ok:0|1> <detail>
  if [ "$2" = 0 ]; then echo "pass $1: $3"; else echo "FAIL $1: $3"; FAILS=$((FAILS + 1)); fi
}

NAMES=(text nometa scanned arxiv)
PDFS=("$FIX/derived/Event Streams in Practice.pdf" "$FIX/derived/Jane Doe - Quiet Systems (2004).pdf" "$FIX/derived/scanned-page.pdf" "$FIX/upstream/pdf/arxiv-2403.02174v1.pdf")

# run_child <bin> <timeout-s> <args...>: prints "<exit> <peakRssMB> <stdout line>" (exit 124 = timeout)
run_child() {
  python3 - "$@" <<'PY'
import resource, subprocess, sys
binp, timeout, args = sys.argv[1], float(sys.argv[2]), sys.argv[3:]
try:
    p = subprocess.run([binp, *args], capture_output=True, text=True, timeout=timeout)
    rss = resource.getrusage(resource.RUSAGE_CHILDREN).ru_maxrss / (1024 * 1024)  # bytes on macOS
    out = (p.stdout.strip().splitlines() or [""])[0]
    print(p.returncode, "%.0f" % rss, out)
except subprocess.TimeoutExpired:
    print(124, 0, "timeout")
PY
}

field() { echo "$1" | tr ' ' '\n' | awk -F= -v k="$2" '$1 == k {print $2}'; }

main() {
  if [ "$(uname -s)" != Darwin ] || [ "$(uname -m)" != arm64 ]; then
    echo "this spike is verified on osx-arm64 only (host: $(uname -sm))" >&2
    return 2
  fi
  echo "== environment"
  echo "date: $(date -u +%Y-%m-%dT%H:%M:%SZ)"
  echo "rid: $RID"
  echo "uname: $(uname -srm)"
  echo "macOS: $(sw_vers -productVersion)"
  echo "dotnet: $(dotnet --version)"

  echo "== K1 AOT publish (-c Release -r $RID -p:TrimmerSingleWarn=false)"
  rm -rf "$AOT" "$JIT"
  dotnet publish Cover -c Release -r "$RID" -p:TrimmerSingleWarn=false -o "$AOT" > "results/local/$RID.publish.log" 2>&1 || { tail -30 "results/local/$RID.publish.log"; echo "publish FAILED"; return 1; }
  if grep -E 'warning (IL|AOT)[0-9]+' "results/local/$RID.publish.log" | sed -E 's/ \[[^]]*\]$//' | sort -u | tee "results/local/$RID.warnings.txt" | grep -q .; then
    echo "FAIL K1: AOT/trim warnings found:"; cat "results/local/$RID.warnings.txt"; return 1
  fi
  echo "pass K1: AOT/trim warnings: 0 (other warnings: $(grep -E 'warning [A-Z]+[0-9]+' "results/local/$RID.publish.log" | sed -E 's/.*warning ([A-Z]+[0-9]+).*/\1/' | sort | uniq -c | tr '\n' ' ' | sed 's/  */ /g'))"
  dotnet publish Cover -c Release -p:PublishAot=false -o "$JIT" > results/local/jit.publish.log 2>&1 || { tail -30 results/local/jit.publish.log; return 1; }
  echo "publish directory (AOT):"; (cd "$AOT" && ls -l | awk 'NR>1 {print "  " $5 " B " $9}')
  echo "-- info"; "$AOT/proto-cover" info | grep -v "^file "

  echo "== K2 / K3 rendering and formats (AOT)"
  : > "$WORK/aot-render.txt"; : > "$WORK/jit-render.txt"
  for i in 0 1 2 3; do
    N="${NAMES[$i]}"; P="${PDFS[$i]}"
    LINE="$("$AOT/proto-cover" render "$P" "$WORK/out-$N")"
    echo "$N $LINE" >> "$WORK/aot-render.txt"
    echo "$N $("$JIT/proto-cover" render "$P" "$WORK/jit-out-$N")" >> "$WORK/jit-render.txt"
    SIZE="$(field "$LINE" size)"; PAGE="$(field "$LINE" page)"; STD="$(field "$LINE" stddev)"
    W="${SIZE%x*}"; H="${SIZE#*x}"; PW="${PAGE%%x*}"; PH="${PAGE#*x}"; PH="${PH%pt}"; PW="${PW}"
    OK=0
    python3 - "$W" "$H" "$PW" "$PH" "$STD" <<'PY' || OK=1
import sys
w, h, pw, ph, std = int(sys.argv[1]), int(sys.argv[2]), float(sys.argv[3]), float(sys.argv[4]), float(sys.argv[5])
long_edge, short_edge = max(w, h), min(w, h)
expected_short = 600 * min(pw, ph) / max(pw, ph)
ok = long_edge == 600 and abs(short_edge - expected_short) <= 1 and std >= 5 and ((w >= h) == (pw >= ph))
sys.exit(0 if ok else 1)
PY
    DEC=0
    for ext in png jpg webp; do
      D="$(sips -g pixelWidth -g pixelHeight "$WORK/out-$N/cover.$ext" 2>/dev/null | awk '/pixel(Width|Height)/ {printf "%s ", $2}')"
      [ "$D" = "$W $H " ] || DEC=1
    done
    row "K2-$N" $OK "page=$PAGE size=$SIZE stddev=$STD"
    row "K3-$N" $DEC "all three formats decode with sips at $W x $H; png=$(field "$LINE" png) jpg=$(field "$LINE" jpg) webp=$(field "$LINE" webp) bytes"
  done

  echo "== K4 determinism; AOT vs JIT pixel hashes"
  DET=0
  for i in 0 1 2 3; do
    A="$("$AOT/proto-cover" render "${PDFS[$i]}" "$WORK/again" | awk '{for (k=1;k<=NF;k++) if ($k ~ /^pixelhash=/) print $k}')"
    B="$(grep "^${NAMES[$i]} " "$WORK/aot-render.txt" | tr ' ' '\n' | grep '^pixelhash=')"
    [ "$A" = "$B" ] || DET=1
  done
  if diff -q "$WORK/aot-render.txt" "$WORK/jit-render.txt" > /dev/null; then JD=0; else JD=1; fi
  row K4 $((DET + JD)) "repeat renders equal: $([ $DET = 0 ] && echo yes || echo NO); AOT and JIT render lines identical: $([ $JD = 0 ] && echo yes || echo NO)"

  echo "== K5 robustness (one child process per input, 10 s timeout)"
  : > "$WORK/aot-robust.txt"; : > "$WORK/jit-robust.txt"
  : > "$WORK/empty.pdf"
  python3 - "$WORK" "${PDFS[0]}" <<'PY'
import random, sys
work, src = sys.argv[1], sys.argv[2]
random.seed(7)
open(f"{work}/random.pdf", "wb").write(bytes(random.randrange(256) for _ in range(2048)))
data = open(src, "rb").read()
open(f"{work}/truncated.pdf", "wb").write(data[: len(data) // 2])
objs = [b"<< /Type /Catalog /Pages 2 0 R >>", b"<< /Type /Pages /Kids [] /Count 0 >>"]
out = b"%PDF-1.4\n"
offsets = []
for i, o in enumerate(objs, 1):
    offsets.append(len(out))
    out += b"%d 0 obj\n" % i + o + b"\nendobj\n"
xref = len(out)
out += b"xref\n0 3\n0000000000 65535 f \n" + b"".join(b"%010d 00000 n \n" % x for x in offsets)
out += b"trailer\n<< /Root 1 0 R /Size 3 >>\nstartxref\n%d\n%%%%EOF\n" % xref
open(f"{work}/zeropage.pdf", "wb").write(out)
PY
  cp fixtures/encrypted.pdf "$WORK/encrypted.pdf"
  ROB=0
  for f in empty random truncated zeropage encrypted; do
    R="$(run_child "$AOT/proto-cover" 10 robust "$WORK/$f.pdf")"
    RJ="$(run_child "$JIT/proto-cover" 10 robust "$WORK/$f.pdf")"
    EXIT="${R%% *}"; REST="${R#* }"; RSS="${REST%% *}"; LINEOUT="${REST#* }"
    echo "$f $LINEOUT" >> "$WORK/aot-robust.txt"; echo "$f ${RJ#* * }" >> "$WORK/jit-robust.txt"
    OK=1; [ "$EXIT" = 3 ] && [ "$RSS" -lt 512 ] && echo "$LINEOUT" | grep -q "^error kind=" && OK=0
    [ $OK = 0 ] || ROB=1
    echo "  $f: exit=$EXIT peakRssMB=$RSS $LINEOUT"
  done
  row K5 $ROB "five broken or protected inputs end with a controlled error (exit 3), no crash, no timeout, RSS < 512 MB"

  echo "== K6 thread safety (no lock of ours)"
  ST="$("$AOT/proto-cover" stress "${PDFS[0]}" "${PDFS[3]}")"; STRC=$?
  echo "$ST" | sed 's/^/  /'
  echo "$ST" | grep -q "mismatches=0 exceptions=0" && K6=0 || K6=1
  row K6 $K6 "$(echo "$ST" | head -1)"
  echo "$ST" | head -1 > "$WORK/aot-stress.txt"
  "$JIT/proto-cover" stress "${PDFS[0]}" "${PDFS[3]}" | head -1 > "$WORK/jit-stress.txt"

  echo "== K9 normalized AOT vs JIT dump (K2-K6)"
  cat "$WORK/aot-render.txt" "$WORK/aot-robust.txt" "$WORK/aot-stress.txt" > "$WORK/aot-dump.txt"
  cat "$WORK/jit-render.txt" "$WORK/jit-robust.txt" "$WORK/jit-stress.txt" > "$WORK/jit-dump.txt"
  if diff -u "$WORK/jit-dump.txt" "$WORK/aot-dump.txt" > "$WORK/dump.diff"; then row K9 0 "identical ($(wc -l < "$WORK/aot-dump.txt" | tr -d ' ') lines)"; else row K9 1 "dumps differ"; head -20 "$WORK/dump.diff"; fi

  echo "== K7 publish directory copied to an empty location, other working directory"
  rm -rf "$WORK/copy"; cp -R "$AOT" "$WORK/copy"
  C="$(cd "$WORK" && "$WORK/copy/proto-cover" render "${PDFS[0]}" "$WORK/copy-out")"
  EXPECT="$(grep '^text ' "$WORK/aot-render.txt" | sed 's/^text //')"
  [ "$C" = "$EXPECT" ] && K7=0 || K7=1
  row K7 $K7 "render from the copy equals the original; files next to the binary: $(cd "$AOT" && ls | grep -v '\.dSYM$' | tr '\n' ' ')"

  echo "== X-1 observations"
  for i in 0 1 2 3; do "$AOT/proto-cover" bench "${PDFS[$i]}" 20 | sed 's/^/observed X-1: /'; done
  echo "observed X-1: proto-cover $(size_of "$AOT/proto-cover") B; $(cd "$AOT" && for f in lib*; do printf '%s %s B; ' "$f" "$(size_of "$f")"; done)"
  for N in "${NAMES[@]}"; do echo "observed X-1: formats $N $(grep "^$N " "$WORK/aot-render.txt" | tr ' ' '\n' | grep -E '^(size|png|jpg|webp)=' | tr '\n' ' ')"; done
  echo "observed X-1: stress $(echo "$ST" | grep stress-wall)"

  echo "== K8 combined binary (experiments/combined/check.sh, then pixel hashes against proto-cover)"
  if ../combined/check.sh > "$WORK/combined.out" 2>&1; then
    tail -1 "$WORK/combined.out"
    grep -E "^(pass|FAIL) (C1|K8)" "$WORK/combined.out" | cut -c1-160
    K8=0
    for i in 0 2 3; do
      H="$(../combined/out/AOT/proto-shelf cover "${PDFS[$i]}" "$WORK/shelf-out-$i" | tr ' ' '\n' | grep '^pixelhash=')"
      E="$(grep "^${NAMES[$i]} " "$WORK/aot-render.txt" | tr ' ' '\n' | grep '^pixelhash=')"
      [ "$H" = "$E" ] || K8=1
    done
    row K8 $K8 "proto-shelf cover gives the pixel hashes of proto-cover on three fixtures; combined C1-C8 and its cover rows passed"
  else
    tail -30 "$WORK/combined.out"; row K8 1 "experiments/combined/check.sh failed"
  fi

  if [ "$FAILS" -ne 0 ]; then echo "== check $RID: $FAILS failing rows"; return 1; fi
  echo "== check $RID: ok"
}

main 2>&1 | tee "results/local/$RID.txt"
exit "${PIPESTATUS[0]}"
