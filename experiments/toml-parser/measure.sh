#!/usr/bin/env bash
# Publishes the TOML parser prototypes as Native AOT (osx-arm64), counts AOT/trim warnings,
# runs the G5 checks and the conformance corpus on the AOT binary, assesses the write path,
# records binary size and load time, and re-runs the float check under de_DE.
# Usage: ./measure.sh [prototype ...]   (default: Baseline Tomlyn Tomlet CsToml Tommy)
# Requires macOS (stat -f, zsh). Uses hyperfine if installed, else a zsh EPOCHREALTIME loop.
set -euo pipefail

cd "$(dirname "$0")"
RID=osx-arm64
RUNS=50
WARMUP=5
PROTOS=("$@")
if [ ${#PROTOS[@]} -eq 0 ]; then
  PROTOS=(Baseline Tomlyn Tomlet CsToml Tommy)
fi
mkdir -p results out

bin_name() {
  case "$1" in
    Baseline) echo proto-toml-baseline ;;
    Tomlyn) echo proto-tomlyn ;;
    Tomlet) echo proto-tomlet ;;
    CsToml) echo proto-cstoml ;;
    Tommy) echo proto-tommy ;;
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

# key_sequence <file>: the `key =` names in file order, without the key added by the edit
key_sequence() {
  grep -E '^[[:space:]]*("[^"]*"|[A-Za-z0-9_.-]+)[[:space:]]*=' "$1" | sed -E 's/^[[:space:]]*//; s/[[:space:]]*=.*$//' | grep -v '^new_key$' || true
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

  if [ "$P" != Baseline ]; then
    # G5: every row of ConfigFile.feature (checks/) on the AOT binary; then the conformance corpus.
    echo "--- checks (G5)"
    "$BIN" corpus checks | tee "results/$P.checks.txt" | grep -E '^(fail|passed|lines)|WRONG' | sed 's/^/  /' || true
    echo "--- corpus"
    "$BIN" corpus corpus | tee "results/$P.corpus.txt" | grep -E '^(fail|passed|lines)|WRONG|none' | sed 's/^/  /' || true

    # Error report as a user would see it (first invalid check files).
    echo "--- error output"
    for F in missing-value duplicate-key unterminated-string; do
      "$BIN" get "checks/invalid/$F.toml" x 2>&1 >/dev/null | head -2 | sed "s/^/  [$F] /" || true
    done

    # Write path.
    echo "--- round-trip"
    rm -f "results/$P.same.toml" "results/$P.edit.toml"
    RT="$("$BIN" roundtrip sample/config.toml "results/$P" 2>&1)" || true
    echo "  result: $RT"
    if [ -f "results/$P.same.toml" ]; then
      if cmp -s sample/config.toml "results/$P.same.toml"; then echo "  unchanged round-trip byte-identical: yes"; else echo "  unchanged round-trip byte-identical: no"; fi
      echo "  comment lines in sample/unchanged: $(grep -c '#' sample/config.toml) / $(grep -c '#' "results/$P.same.toml")"
    fi
    if [ -f "results/$P.edit.toml" ]; then
      echo "  comment lines in edited: $(grep -c '#' "results/$P.edit.toml")"
      if [ "$(key_sequence sample/config.toml)" = "$(key_sequence "results/$P.edit.toml")" ]; then echo "  key order kept after edit: yes"; else echo "  key order kept after edit: no"; fi
      echo "  changed lines vs sample (diff, both sides): $(diff sample/config.toml "results/$P.edit.toml" | grep -c '^[<>]')"
      if grep -qE '(^|[{,][[:space:]]*)new_key = "x"' "results/$P.edit.toml"; then echo "  new key present: yes"; else echo "  new key present: no"; fi
      if grep -qE 'auto_merge = 0\.85' "results/$P.edit.toml"; then echo "  edited value present: yes"; else echo "  edited value present: no"; fi
    fi
  fi

  # Size and output directory.
  echo "size_bytes: $(stat -f%z "$BIN")"
  echo "output directory:"; ls -1 "out/$P" | sed 's/^/  /'

  # Locale run: float output must not depend on the culture.
  if [ "$P" != Baseline ]; then
    L="$(LANG=de_DE.UTF-8 LC_ALL=de_DE.UTF-8 "$BIN" get sample/config.toml grouping.auto_merge 2>&1)" || true
    echo "de_DE float output: $L"
  fi

  # First run, then timing.
  S=$(zsh -c 'zmodload zsh/datetime; s=$EPOCHREALTIME; '"$BIN"' get sample/config.toml grouping.auto_merge >/dev/null; printf "%.2f" $(( ($EPOCHREALTIME - s) * 1000 ))')
  echo "first_run_ms: $S"
  RESULTS_JSON="results/$P.get.json"
  echo "timing[get sample/config.toml grouping.auto_merge] median p95 min max (ms): $(timing_ms "$BIN" get sample/config.toml grouping.auto_merge)"
done
exit "$FAILS"
