#!/usr/bin/env bash
# Functional check of the web spike on osx-arm64 (ADR 0007, no timing gates).
# Usage: ./check.sh
# Steps: Svelte build with `npm ci && npm run build` (W2), AOT publish of Server (W1), JIT builds of Server and Client,
# test file, client against the AOT server (W3-W10) with Ui/dist moved away and an empty working directory, client
# against the JIT server and dump diff (W11). Output (also written to results/local/osx-arm64.txt): everything below.
set -euo pipefail

cd "$(dirname "$0")"
RID=osx-arm64
mkdir -p results/local
AOTOUT="out/Server-$RID"
JITOUT="out/Server-jit"
CLIENTOUT="out/Client"
AOTBIN="$AOTOUT/proto-web"
JITBIN="$JITOUT/proto-web"
WORK="$(mktemp -d)"
trap 'rm -rf "${WORK:?}"; [ -d Ui/dist.hidden ] && mv Ui/dist.hidden Ui/dist || true' EXIT

size_of() { if [ "$(uname -s)" = Darwin ]; then stat -f%z "$1"; else stat -c%s "$1"; fi; }
now_ms() { python3 -c 'import time; print(int(time.time()*1000))'; }

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
  echo "aspnetcore runtime: $(dotnet --list-runtimes | grep Microsoft.AspNetCore.App | tail -1)"
  echo "node: $(node --version) (.node-version: $(cat .node-version))"
  echo "npm: $(npm --version)"

  echo "== W2 Svelte build (npm ci && npm run build)"
  rm -rf Ui/dist Ui/dist.hidden
  start=$(now_ms)
  (cd Ui && npm ci --no-audit --no-fund 2>&1 | tail -2 && npm run build 2>&1 | tail -8)
  echo "observed X-1: svelte build (npm ci + build) $(( $(now_ms) - start )) ms"
  [ -f Ui/dist/index.html ] || { echo "FAIL W2: Ui/dist/index.html missing"; return 1; }
  ls Ui/dist/assets/*.js Ui/dist/assets/*.css > /dev/null
  echo "pass W2: dist: $(find Ui/dist -type f | wc -l | tr -d ' ') files, $(find Ui/dist -type f -exec stat -f%z {} + | paste -sd+ - | bc) B"
  (cd Ui && for p in svelte vite typescript @sveltejs/vite-plugin-svelte; do echo "  $p $(node -p "require('./node_modules/$p/package.json').version")"; done)

  echo "== W1 AOT publish (-c Release -r $RID -p:TrimmerSingleWarn=false)"
  rm -rf "${AOTOUT:?}"
  dotnet publish Server -c Release -r "$RID" -p:TrimmerSingleWarn=false -o "$AOTOUT" > "results/local/$RID.publish.log" 2>&1 || { tail -30 "results/local/$RID.publish.log"; echo "publish FAILED"; return 1; }
  if grep -E 'warning (IL|AOT)[0-9]+' "results/local/$RID.publish.log" | sed -E 's/ \[[^]]*\]$//' | sort -u | tee "results/local/$RID.warnings.txt" | grep -q .; then
    echo "FAIL W1: AOT/trim warnings found:"
    cat "results/local/$RID.warnings.txt"
    return 1
  fi
  echo "pass W1: AOT/trim warnings: 0 (all warnings: $(grep -c 'warning' "results/local/$RID.publish.log" || true))"
  for f in "$AOTOUT"/*; do echo "  $(size_of "$f") B $(basename "$f")"; done
  echo "observed X-1: size proto-web: $(size_of "$AOTBIN") B"

  echo "== JIT builds (server for W11, client)"
  rm -rf "${JITOUT:?}" "${CLIENTOUT:?}"
  dotnet publish Server -c Release -p:PublishAot=false -o "$JITOUT" > "results/local/jit-server.publish.log" 2>&1 || { tail -30 "results/local/jit-server.publish.log"; return 1; }
  dotnet publish Client -c Release -o "$CLIENTOUT" > "results/local/client.publish.log" 2>&1 || { tail -30 "results/local/client.publish.log"; return 1; }

  echo "== test file (1 MiB, byte i mod 251)"
  python3 -c "open('$WORK/f1.bin','wb').write(bytes(i % 251 for i in range(1048576)))"

  # Ui/dist is moved away so that nothing can be served from the file system (W3).
  mv Ui/dist Ui/dist.hidden
  echo "== client against the AOT server (W3-W10)"
  dotnet "$CLIENTOUT/proto-web-client.dll" "$WORK/aot-dump.json" Ui/dist.hidden "$WORK/f1.bin" "$AOTBIN"
  echo "== client against the JIT server"
  dotnet "$CLIENTOUT/proto-web-client.dll" "$WORK/jit-dump.json" Ui/dist.hidden "$WORK/f1.bin" "$JITBIN" > "$WORK/jit.out"
  tail -1 "$WORK/jit.out"
  mv Ui/dist.hidden Ui/dist

  echo "== W11 response dumps, AOT vs JIT"
  if diff -u "$WORK/jit-dump.json" "$WORK/aot-dump.json" > "$WORK/dump.diff"; then
    echo "pass W11: dumps identical ($(wc -l < "$WORK/aot-dump.json" | tr -d ' ') lines)"
  else
    echo "FAIL W11: dumps differ"
    head -40 "$WORK/dump.diff"
    return 1
  fi

  echo "== X-2 start to first successful response (informational, not gated)"
  mv Ui/dist Ui/dist.hidden
  start=$(now_ms)
  "$AOTBIN" --port 54321 > /dev/null 2>&1 &
  pid=$!
  until curl -s -o /dev/null http://127.0.0.1:54321/; do sleep 0.01; done
  echo "observed X-2: $(( $(now_ms) - start )) ms (AOT, includes process start and the polling loop)"
  kill "$pid" 2>/dev/null || true
  wait "$pid" 2>/dev/null || true
  mv Ui/dist.hidden Ui/dist
  echo "== check $RID: ok"
}

main 2>&1 | tee "results/local/$RID.txt"
exit "${PIPESTATUS[0]}"
