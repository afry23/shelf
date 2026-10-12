#!/usr/bin/env bash
# Publishes the three prototypes as Native AOT (osx-arm64), counts AOT/trim warnings,
# runs the functional checks on the AOT binary, records size and cold start.
# Usage: ./measure.sh [prototype ...]   (default: Baseline SystemCommandLine SpectreCli)
# Requires macOS (stat -f, zsh) and python3 (pty check; reported as NOT RUN if no pty is available). Uses hyperfine if installed, else a zsh EPOCHREALTIME loop.
set -euo pipefail

cd "$(dirname "$0")"
RID=osx-arm64
RUNS=50
WARMUP=5
PROTOS=("$@")
if [ ${#PROTOS[@]} -eq 0 ]; then
  PROTOS=(Baseline SystemCommandLine SpectreCli)
fi
mkdir -p results out

bin_name() {
  case "$1" in
    Baseline) echo proto-baseline ;;
    SystemCommandLine) echo proto-scl ;;
    SpectreCli) echo proto-spectre ;;
  esac
}

if command -v hyperfine >/dev/null 2>&1; then TOOL=hyperfine; else TOOL="zsh EPOCHREALTIME loop"; fi
{
  echo "date: $(date -u +%Y-%m-%dT%H:%M:%SZ)"
  echo "os: $(sw_vers -productName) $(sw_vers -productVersion) ($(uname -m))"
  echo "cpu: $(sysctl -n machdep.cpu.brand_string)"
  echo "ram_bytes: $(sysctl -n hw.memsize)"
  echo "dotnet: $(dotnet --version)"
  echo "power: $(pmset -g batt | head -1)"
  echo "timing tool: $TOOL"
} | tee results/environment.txt

# timing_ms <binary> <args...>: prints "median p95 min max" in milliseconds
timing_ms() {
  local bin="$1"; shift
  if [ "$TOOL" = hyperfine ]; then
    local json="$RESULTS_JSON"
    hyperfine -N --warmup "$WARMUP" --runs "$RUNS" --export-json "$json" "$bin $*" >/dev/null
    python3 -I - "$json" <<'PY'
import json, sys
t = sorted(json.load(open(sys.argv[1]))["results"][0]["times"])
n = len(t)
print("%.2f %.2f %.2f %.2f" % (t[n // 2 - 1] * 500 + t[n // 2] * 500 if n % 2 == 0 else t[n // 2] * 1000,
                                t[int(-(-0.95 * n // 1)) - 1] * 1000, t[0] * 1000, t[-1] * 1000))
PY
  else
    zsh -c '
      zmodload zsh/datetime
      bin=$1; shift
      for i in {1..'"$WARMUP"'}; do $bin "$@" >/dev/null 2>&1; done
      for i in {1..'"$RUNS"'}; do
        s=$EPOCHREALTIME; $bin "$@" >/dev/null 2>&1; e=$EPOCHREALTIME
        printf "%.3f\n" $(( (e - s) * 1000 ))
      done | sort -n | awk "{a[NR]=\$1} END {n=NR; m=(n%2)?a[(n+1)/2]:(a[n/2]+a[n/2+1])/2; p=int(n*0.95); if (p<n*0.95) p++; printf \"%.2f %.2f %.2f %.2f\n\", m, a[p], a[1], a[n]}"
    ' _ "$bin" "$@"
  fi
}

FAILS=0
for P in "${PROTOS[@]}"; do
  echo "=== $P"
  NAME="$(bin_name "$P")"
  LOG="results/$P.publish.log"
  rm -rf "$P/bin" "$P/obj" "out/$P"
  dotnet publish "$P" -c Release -r "$RID" -p:TrimmerSingleWarn=false -o "out/$P" 2>&1 | tee "$LOG" >/dev/null || true
  BIN="out/$P/$NAME"

  # Warnings: distinct lines (project path suffix stripped), grouped by code.
  grep -E 'warning (IL[0-9]+|AOT[0-9]+)' "$LOG" | sed -E 's/ \[[^]]*\]$//' | sort -u > "results/$P.warnings.txt" || true
  WCOUNT=$(wc -l < "results/$P.warnings.txt" | tr -d ' ')
  echo "warnings (distinct lines): $WCOUNT"
  grep -oE 'warning (IL|AOT)[0-9]+' "results/$P.warnings.txt" | sort | uniq -c | sed 's/^/  /' || true

  if [ ! -x "$BIN" ]; then
    echo "publish FAILED: $BIN missing"; FAILS=$((FAILS + 1)); continue
  fi

  # Functional checks on the AOT binary.
  PASS=1
  check() { # name expected_exit stdout_mode stdout_expect args...
    local name="$1" want="$2" mode="$3" expect="$4"; shift 4
    local out err code
    out="$("$BIN" "$@" 2>"results/.stderr")" && code=0 || code=$?
    err="$(cat results/.stderr)"
    local ok=1
    [ "$code" -eq "$want" ] || ok=0
    case "$mode" in
      exact) [ "$out" = "$expect" ] || ok=0 ;;
      empty) [ -z "$out" ] || ok=0 ;;
      help) { printf '%s' "$out" | grep -q greet && printf '%s' "$out" | grep -q tag && printf '%s' "$out" | grep -q -- --json; } || ok=0 ;;
    esac
    if [ "$want" -eq 0 ]; then [ -z "$err" ] || ok=0; else [ -n "$err" ] || ok=0; fi
    if [ "$ok" -eq 1 ]; then echo "  ok    $name"; else echo "  FAIL  $name (exit $code, stdout '$out', stderr '$err')"; PASS=0; fi
  }
  check "--version" 0 exact "proto 1.0.0" --version
  check "--help" 0 help "" --help
  check "greet World --times 2" 0 exact "$(printf 'Hello, World\nHello, World')" greet World --times 2
  check "--json greet World" 0 exact '{"greeting":"Hello, World"}' --json greet World
  check "greet World --json" 0 exact '{"greeting":"Hello, World"}' greet World --json
  check "tag add fiction" 0 exact "added fiction" tag add fiction
  check "(no args)" 2 empty "" 
  check "greet (missing argument)" 2 empty "" greet
  check "greet World --times abc" 2 empty "" greet World --times abc
  check "frobnicate" 2 empty "" frobnicate
  # NO_COLOR on a real pseudo-terminal (pty-run.py; `script` does not work in every environment).
  # The .NET runtime itself emits keypad mode switches (ESC[?1h, ESC=) when a terminal is attached;
  # those are not color codes and also appear for the baseline, so they are removed before the check.
  # Result: "NOT RUN" (never ok) when no pty could be allocated (wrapper exit 125); FAIL when the
  # help command fails or prints nothing; ok only when output was captured and has no stray ESC.
  PTY_OUT="$(NO_COLOR=1 python3 -I ./pty-run.py "$BIN" --help 2>results/.pty-stderr)" && PTY_CODE=0 || PTY_CODE=$?
  PTY_STATE=""
  if [ "$PTY_CODE" -eq 125 ]; then
    echo "  NOT RUN  --help NO_COLOR=1 on a pty (no pty available: $(cat results/.pty-stderr))"
    PTY_STATE=" (pty check NOT RUN)"
  elif [ "$PTY_CODE" -ne 0 ] || [ -z "$PTY_OUT" ]; then
    echo "  FAIL  --help NO_COLOR=1 on a pty (exit $PTY_CODE, output bytes ${#PTY_OUT})"; PASS=0
  else
    NC="$(printf '%s' "$PTY_OUT" | LC_ALL=C sed -e $'s/\033\\[?1h//g' -e $'s/\033=//g' | LC_ALL=C grep -c $'\033' || true)"
    if [ "$NC" = 0 ]; then echo "  ok    --help NO_COLOR=1 on a pty (${#PTY_OUT} bytes captured) has no ESC byte (runtime keypad mode ignored)"
    else echo "  FAIL  ESC byte under NO_COLOR=1 on a pty ($NC lines)"; PASS=0; fi
  fi
  if [ "$PASS" -eq 1 ]; then echo "functional: PASS$PTY_STATE"; else echo "functional: FAIL"; FAILS=$((FAILS + 1)); fi

  # Size and output directory.
  echo "size_bytes: $(stat -f%z "$BIN")"
  echo "output directory:"; ls -1 "out/$P" | sed 's/^/  /'

  # First run, then timing.
  if ! "$BIN" --version >/dev/null 2>&1; then
    echo "first_run_ms: n/a (binary fails)"; echo "timing: n/a (binary fails --version)"; continue
  fi
  S=$(zsh -c 'zmodload zsh/datetime; s=$EPOCHREALTIME; '"$BIN"' --version >/dev/null; printf "%.2f" $(( ($EPOCHREALTIME - s) * 1000 ))')
  echo "first_run_ms: $S"
  for CASE in "--version" "greet World --times 2" "--help"; do
    RESULTS_JSON="results/$P.$(echo "$CASE" | tr ' ' '_' | tr -d '-').json"
    # shellcheck disable=SC2086
    echo "timing[$CASE] median p95 min max (ms): $(timing_ms "$BIN" $CASE)"
  done
done
exit "$FAILS"
