#!/usr/bin/env bash
# Portable functional check of the parsers spike on one RID (no timing gates).
# Usage: ./check.sh <rid>      (rid: osx-arm64 | osx-x64 | linux-x64 | linux-arm64; must match the host)
# Steps: AOT publish of Parsers (Variant=all, fail on any IL*/AOT warning), info / isbn-check / epub-check / pdf-check,
# mutate + robust, tiny and 200-work/300-file libraries from the generator, verify, short bench (informational),
# differential dump AOT vs JIT. Output (also written to results/ci/<rid>.txt): everything below.
# Linux needs clang and zlib1g-dev.
set -euo pipefail

cd "$(dirname "$0")"
RID="${1:?usage: check.sh <rid>}"
case "$RID" in
  osx-arm64|osx-x64|linux-x64|linux-arm64) ;;
  *) echo "unknown rid: $RID" >&2; exit 2 ;;
esac
mkdir -p results/ci
OUTDIR="out/Parsers-$RID"
BIN="$OUTDIR/proto-parsers-all"
JITOUT="out/Parsers-jit-$RID"
FIX=../../tests/fixtures

size_of() { if [ "$(uname -s)" = Darwin ]; then stat -f%z "$1"; else stat -c%s "$1"; fi; }

main() {
  echo "== environment"
  echo "date: $(date -u +%Y-%m-%dT%H:%M:%SZ)"
  echo "rid: $RID"
  echo "uname: $(uname -srm)"
  echo "dotnet: $(dotnet --version)"
  if [ "$(uname -s)" = Linux ]; then
    echo "glibc (runner): $(ldd --version 2>&1 | head -1)"
    echo "memory: $(grep MemTotal /proc/meminfo)"
  else
    echo "macOS: $(sw_vers -productVersion)"
    echo "memory bytes: $(sysctl -n hw.memsize)"
  fi
  echo "cpus: $(getconf _NPROCESSORS_ONLN)"

  echo "== AOT publish (-c Release -r $RID -p:Variant=all -p:TrimmerSingleWarn=false)"
  rm -rf "${OUTDIR:?}" Parsers/bin Parsers/obj
  dotnet publish Parsers -c Release -r "$RID" -p:Variant=all -p:TrimmerSingleWarn=false -o "$OUTDIR" > "results/ci/$RID.publish.log" 2>&1 || { tail -30 "results/ci/$RID.publish.log"; echo "publish FAILED"; return 1; }
  if grep -E 'warning (IL|AOT)[0-9]+' "results/ci/$RID.publish.log" | sed -E 's/ \[[^]]*\]$//' | sort -u | tee "results/ci/$RID.warnings.txt" | grep -q .; then
    echo "AOT/trim warnings found:"
    cat "results/ci/$RID.warnings.txt"
    return 1
  fi
  echo "AOT/trim warnings: 0"
  echo "size proto-parsers-all: $(size_of "$BIN") B"

  echo "== info"
  "$BIN" info
  echo "== selfcheck"
  "$BIN" selfcheck "$FIX"
  echo "== isbn-check"
  "$BIN" isbn-check
  echo "== epub-check"
  "$BIN" epub-check "$FIX" | grep -v '^observed X-E9'
  echo "== pdf-check"
  "$BIN" pdf-check "$FIX"

  WORK="$(mktemp -d)"
  trap 'rm -rf "${WORK:?}"' EXIT
  echo "== mutate and robust (R-rows, child process per input, 10 s timeout)"
  "$BIN" mutate "$FIX" "$WORK/mut" | awk '{print $1, $2}'
  "$BIN" robust "$WORK/mut"

  echo "== generator data (tiny preset and 200 works / 300 files)"
  dotnet run --project ../../tests/Shelf.TestData -c Release -- generate --out "$WORK/tiny" --preset tiny --seed 42 > /dev/null
  dotnet run --project ../../tests/Shelf.TestData -c Release -- generate --out "$WORK/lib200" --works 200 --files 300 --seed 42 > /dev/null
  echo "-- verify tiny"
  "$BIN" verify "$WORK/tiny/ground-truth.json"
  echo "-- verify 200 works / 300 files"
  "$BIN" verify "$WORK/lib200/ground-truth.json"
  echo "-- bench --threads 1 --limit 300 (informational)"
  "$BIN" bench "$WORK/lib200/ground-truth.json" --format all --threads 1 --limit 300 | grep -E '^bench '

  echo "== differential dump, AOT vs JIT"
  rm -rf Parsers/bin Parsers/obj "${JITOUT:?}"
  dotnet build Parsers -c Release -p:Variant=all -p:PublishAot=false -o "$JITOUT" > "results/ci/$RID.jit.log" 2>&1 || { tail -20 "results/ci/$RID.jit.log"; echo "JIT build FAILED"; return 1; }
  for SET in fixtures tiny mutations; do
    case $SET in
      fixtures) DIR="$FIX"; EXTRA="" ;;
      tiny) DIR="$WORK/tiny"; EXTRA="" ;;
      mutations) DIR="$WORK/mut"; EXTRA="--isolate" ;;
    esac
    # shellcheck disable=SC2086
    "$BIN" dump "$DIR" $EXTRA > "$WORK/aot.$SET.txt"
    # shellcheck disable=SC2086
    dotnet "$JITOUT/proto-parsers-all.dll" dump "$DIR" $EXTRA > "$WORK/jit.$SET.txt"
    if cmp -s "$WORK/aot.$SET.txt" "$WORK/jit.$SET.txt"; then
      echo "dump $SET: identical ($(wc -l < "$WORK/aot.$SET.txt" | tr -d ' ') lines)"
    else
      echo "dump $SET: DIFFERENT"
      diff "$WORK/aot.$SET.txt" "$WORK/jit.$SET.txt" | cut -c1-300 | head -20
      return 1
    fi
  done
  echo "== check $RID: ok"
}

main 2>&1 | tee "results/ci/$RID.txt"
