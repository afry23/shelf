#!/usr/bin/env bash
# Portable functional check of the CsToml prototype on one RID (no timing gates), see ADR 0003.
# Usage: ./check.sh <rid>      (rid: osx-arm64 | linux-x64; must match the host)
# Steps: AOT publish of CsToml (fail on any IL*/AOT warning, G4), the G5 checks and the conformance corpus on the AOT
# binary (fail on any failing row), an error report, the round-trip of sample/config.toml, a float check under de_DE
# (informational). Output (also written to results/ci/<rid>.txt): everything below. Linux needs clang and zlib1g-dev.
set -euo pipefail

cd "$(dirname "$0")"
RID="${1:?usage: check.sh <rid>}"
case "$RID" in
  osx-arm64|linux-x64) ;;
  *) echo "unsupported rid: $RID" >&2; exit 2 ;;
esac
mkdir -p results/ci
OUTDIR="out/CsToml-$RID"
BIN="$OUTDIR/proto-cstoml"

size_of() { if [ "$(uname -s)" = Darwin ]; then stat -f%z "$1"; else stat -c%s "$1"; fi; }

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

  echo "== AOT publish (-c Release -r $RID -p:TrimmerSingleWarn=false)"
  rm -rf "${OUTDIR:?}"
  dotnet publish CsToml -c Release -r "$RID" -p:TrimmerSingleWarn=false -o "$OUTDIR" > "results/ci/$RID.publish.log" 2>&1 || { tail -30 "results/ci/$RID.publish.log"; echo "publish FAILED"; return 1; }
  if grep -E 'warning (IL|AOT)[0-9]+' "results/ci/$RID.publish.log" | sed -E 's/ \[[^]]*\]$//' | sort -u | tee "results/ci/$RID.warnings.txt" | grep -q .; then
    echo "AOT/trim warnings found:"
    cat "results/ci/$RID.warnings.txt"
    return 1
  fi
  echo "AOT/trim warnings: 0"
  for f in "$OUTDIR"/*; do echo "  $(size_of "$f") B $(basename "$f")"; done
  echo "size proto-cstoml: $(size_of "$BIN") B"

  echo "== checks (G5)"
  "$BIN" corpus checks | tee "results/ci/$RID.checks.txt" | grep -E '^(fail|passed|lines)|WRONG'
  if grep -qE '^fail|WRONG' "results/ci/$RID.checks.txt"; then echo "checks FAILED"; return 1; fi
  echo "== corpus"
  "$BIN" corpus corpus | tee "results/ci/$RID.corpus.txt" | grep -E '^(fail|passed|lines)|WRONG'
  if grep -qE '^fail|WRONG' "results/ci/$RID.corpus.txt"; then echo "corpus FAILED"; return 1; fi

  echo "== error output (first lines)"
  for F in missing-value duplicate-key unterminated-string; do
    "$BIN" get "checks/invalid/$F.toml" x 2>&1 >/dev/null | head -2 | sed "s/^/  [$F] /" || true
  done

  echo "== round-trip"
  rm -f "results/ci/$RID.same.toml" "results/ci/$RID.edit.toml"
  RT="$("$BIN" roundtrip sample/config.toml "results/ci/$RID" 2>&1)" || { echo "round-trip FAILED: $RT"; return 1; }
  echo "  result: $RT"
  cmp -s sample/config.toml "results/ci/$RID.same.toml" && echo "  unchanged round-trip byte-identical: yes" || echo "  unchanged round-trip byte-identical: no"
  grep -qE 'auto_merge = 0\.85' "results/ci/$RID.edit.toml" && echo "  edited value present: yes" || { echo "  edited value present: no"; return 1; }

  echo "== get under de_DE (float output must not depend on the culture; informational)"
  echo "  $(LANG=de_DE.UTF-8 LC_ALL=de_DE.UTF-8 "$BIN" get sample/config.toml grouping.auto_merge 2>&1)"
  echo "== check $RID: ok"
}

main 2>&1 | tee "results/ci/$RID.txt"
exit "${PIPESTATUS[0]}"
