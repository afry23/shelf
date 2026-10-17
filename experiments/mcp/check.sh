#!/usr/bin/env bash
# Functional check of the MCP spike on osx-arm64 (ADR 0006, no timing gates).
# Usage: ./check.sh
# Steps: AOT publish of Server (fail on any IL*/AOT warning, M1), JIT build of Server, JIT build of the raw
# JSON-RPC client, client against the AOT server (M2-M9) and against the JIT server (M10: identical response dumps).
# Output (also written to results/local/osx-arm64.txt): everything below.
set -euo pipefail

cd "$(dirname "$0")"
RID=osx-arm64
mkdir -p results/local
AOTOUT="out/Server-$RID"
JITOUT="out/Server-jit"
CLIENTOUT="out/Client"
AOTBIN="$AOTOUT/proto-mcp"
JITBIN="$JITOUT/proto-mcp"
WORK="$(mktemp -d)"
trap 'rm -rf "${WORK:?}"' EXIT

size_of() { if [ "$(uname -s)" = Darwin ]; then stat -f%z "$1"; else stat -c%s "$1"; fi; }

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

  echo "== M1 AOT publish (-c Release -r $RID -p:TrimmerSingleWarn=false)"
  rm -rf "${AOTOUT:?}"
  dotnet publish Server -c Release -r "$RID" -p:TrimmerSingleWarn=false -o "$AOTOUT" > "results/local/$RID.publish.log" 2>&1 || { tail -30 "results/local/$RID.publish.log"; echo "publish FAILED"; return 1; }
  if grep -E 'warning (IL|AOT)[0-9]+' "results/local/$RID.publish.log" | sed -E 's/ \[[^]]*\]$//' | sort -u | tee "results/local/$RID.warnings.txt" | grep -q .; then
    echo "FAIL M1: AOT/trim warnings found:"
    cat "results/local/$RID.warnings.txt"
    return 1
  fi
  echo "pass M1: AOT/trim warnings: 0"
  for f in "$AOTOUT"/*; do echo "  $(size_of "$f") B $(basename "$f")"; done
  echo "observed X-1: size proto-mcp: $(size_of "$AOTBIN") B"

  echo "== JIT builds (server for M10, client)"
  rm -rf "${JITOUT:?}" "${CLIENTOUT:?}"
  dotnet publish Server -c Release -p:PublishAot=false -o "$JITOUT" > "results/local/jit-server.publish.log" 2>&1 || { tail -30 "results/local/jit-server.publish.log"; return 1; }
  dotnet publish Client -c Release -o "$CLIENTOUT" > "results/local/client.publish.log" 2>&1 || { tail -30 "results/local/client.publish.log"; return 1; }

  echo "== client against the AOT server (M2-M9)"
  dotnet "$CLIENTOUT/proto-mcp-client.dll" "$WORK/aot-dump.json" "$AOTBIN"
  echo "== client against the JIT server"
  dotnet "$CLIENTOUT/proto-mcp-client.dll" "$WORK/jit-dump.json" "$JITBIN" > "$WORK/jit.out"
  tail -1 "$WORK/jit.out"

  echo "== M10 response dumps, AOT vs JIT"
  if diff -u "$WORK/jit-dump.json" "$WORK/aot-dump.json" > "$WORK/dump.diff"; then
    echo "pass M10: dumps identical ($(wc -l < "$WORK/aot-dump.json" | tr -d ' ') lines)"
  else
    echo "FAIL M10: dumps differ"
    head -40 "$WORK/dump.diff"
    return 1
  fi

  echo "== X-2 wall time of one client run against the AOT server (informational, not gated)"
  start=$(date +%s)
  dotnet "$CLIENTOUT/proto-mcp-client.dll" "$WORK/t.json" "$AOTBIN" > /dev/null
  echo "observed X-2: $(( $(date +%s) - start )) s (whole client run, includes client JIT start)"
  echo "== check $RID: ok"
}

main 2>&1 | tee "results/local/$RID.txt"
exit "${PIPESTATUS[0]}"
