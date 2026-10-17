#!/usr/bin/env bash
# Functional check of the combined AOT binary on osx-arm64 (ADR 0008, no timing gates).
# Usage: ./check.sh
# Steps: Svelte build and the two reused clients, pinned sqlite-vec asset, AOT and JIT publish of proto-shelf (C1),
# command rows C2-C5, MCP client rows (C6), web client rows (C7), the publish directory copied to an empty location (C8),
# observations X-1 and X-2. Output (also written to results/local/osx-arm64.txt): everything below.
set -euo pipefail

cd "$(dirname "$0")"
RID=osx-arm64
mkdir -p results/local
AOT="$PWD/out/AOT"
JIT="$PWD/out/JIT"
WORK="$(mktemp -d)"
FIX="$PWD/../../tests/fixtures"
TOML="$PWD/../toml-parser"
trap 'rm -rf "${WORK:?}"; [ -d ../web/Ui/dist.hidden ] && mv ../web/Ui/dist.hidden ../web/Ui/dist || true' EXIT

size_of() { stat -f%z "$1"; }
now_ms() { python3 -c 'import time; print(int(time.time()*1000))'; }
FAILS=0
row() { # row <id> <ok:0|1> <detail>
  if [ "$2" = 0 ]; then echo "pass $1: $3"; else echo "FAIL $1: $3"; FAILS=$((FAILS + 1)); fi
}

# commands_rows <binary-dir> <label>: C2-C5 against the binary in <binary-dir>
commands_rows() {
  local BIN="$1/proto-shelf" L="$2" out rc
  out="$("$BIN" --version 2>&1)"; rc=$?
  local v_ok=1; [ $rc = 0 ] && [ "$out" = "proto-shelf 0.0.1" ] && v_ok=0
  out="$("$BIN" --help 2>&1)"; rc=$?
  local h_ok=1; [ $rc = 0 ] && echo "$out" | grep -q "serve" && echo "$out" | grep -q "mcp" && h_ok=0
  set +e; "$BIN" bogus >/dev/null 2>"$WORK/e1"; rc1=$?; "$BIN" config --nope >/dev/null 2>"$WORK/e2"; rc2=$?; set -e
  local u_ok=1; [ $rc1 = 2 ] && [ $rc2 = 2 ] && [ -s "$WORK/e1" ] && [ -s "$WORK/e2" ] && u_ok=0
  row "C2$L" $((v_ok + h_ok + u_ok)) "version=$v_ok help=$h_ok unknownCommand=$rc1 unknownOption=$rc2 (0 means ok for version/help)"

  local a b c d
  a="$("$BIN" config get "$TOML/sample/config.toml" grouping.auto_merge)"
  b="$("$BIN" config get "$TOML/sample/config.toml" title)"
  c="$("$BIN" config get "$TOML/sample/config.toml" search.limit)"
  set +e; d="$("$BIN" config get "$TOML/checks/invalid/duplicate-key.toml" x 2>&1)"; local drc=$?; set -e
  local c_ok=1
  [ "$a" = "float:0.9" ] && [ "$b" = "string:My Library" ] && [ "$c" = "integer:20" ] && [ $drc = 1 ] && echo "$d" | grep -q "line 3" && c_ok=0
  row "C3$L" $c_ok "grouping.auto_merge=$a title=$b search.limit=$c duplicate-key: exit=$drc '$d'"

  local db; db="$("$BIN" db check 2>&1)"
  local d_ok=1
  echo "$db" | grep -q "^fts5 match=streams order=1,2$" && echo "$db" | grep -q "^vec_version=v0.1.9$" && echo "$db" | grep -q "^knn nearest=1$" && d_ok=0
  row "C4$L" $d_ok "$(echo "$db" | tr '\n' ' ')"

  local ep pp
  ep="$("$BIN" extract "$FIX/upstream/gutenberg/pg201-flatland.epub3.epub")"
  pp="$("$BIN" extract "$FIX/derived/Event Streams in Practice.pdf")"
  local x_ok=1
  echo "$ep" | grep -q "^title=Flatland: A Romance of Many Dimensions$" && echo "$ep" | grep -q "^authors=Edwin Abbott Abbott$" && echo "$ep" | grep -q "^language=en$" \
    && echo "$pp" | grep -q "^title=Event Streams in Practice$" && echo "$pp" | grep -q "^author=Jördis Ålander$" && echo "$pp" | grep -q "^pages=1$" && x_ok=0
  row "C5$L" $x_ok "epub: $(echo "$ep" | tr '\n' ' ') pdf: $(echo "$pp" | tr '\n' ' ')"
}

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
  echo "node: $(node --version), npm: $(npm --version)"

  echo "== prerequisites (Svelte build, clients, sqlite-vec asset)"
  (cd ../web/Ui && npm ci --no-audit --no-fund > /dev/null 2>&1 && npm run build > /dev/null 2>&1)
  [ -f ../web/Ui/dist/index.html ] || { echo "FAIL: ../web/Ui/dist missing"; return 1; }
  rm -rf out/McpClient out/WebClient
  dotnet publish ../mcp/Client -c Release -o out/McpClient > "results/local/mcp-client.publish.log" 2>&1 || { tail -20 results/local/mcp-client.publish.log; return 1; }
  dotnet publish ../web/Client -c Release -o out/WebClient > "results/local/web-client.publish.log" 2>&1 || { tail -20 results/local/web-client.publish.log; return 1; }
  ../storage/fetch-sqlite-vec.sh "$RID" | tail -1
  VEC="$PWD/../storage/vendor/$RID/vec0.dylib"

  echo "== C1 AOT publish (-c Release -r $RID -p:TrimmerSingleWarn=false)"
  rm -rf "$AOT" "$JIT"
  dotnet publish Combined -c Release -r "$RID" -p:TrimmerSingleWarn=false -o "$AOT" > "results/local/$RID.publish.log" 2>&1 || { tail -30 "results/local/$RID.publish.log"; echo "publish FAILED"; return 1; }
  if grep -E 'warning (IL|AOT)[0-9]+' "results/local/$RID.publish.log" | sed -E 's/ \[[^]]*\]$//' | sort -u | tee "results/local/$RID.warnings.txt" | grep -q .; then
    echo "FAIL C1: AOT/trim warnings found:"; cat "results/local/$RID.warnings.txt"; return 1
  fi
  echo "pass C1: AOT/trim warnings: 0 (all warnings of any kind: $(grep -c 'warning' "results/local/$RID.publish.log" || true))"
  dotnet publish Combined -c Release -p:PublishAot=false -o "$JIT" > "results/local/jit.publish.log" 2>&1 || { tail -30 results/local/jit.publish.log; return 1; }
  cp "$VEC" "$AOT/vec0.dylib"
  cp "$VEC" "$JIT/vec0.dylib"
  echo "publish directory (AOT):"; (cd "$AOT" && ls -l | awk 'NR>1 {print "  " $5 " B " $9}')

  echo "== C2-C5 command rows (AOT)"
  commands_rows "$AOT" ""

  echo "== C6 MCP client against proto-shelf mcp"
  dotnet out/McpClient/proto-mcp-client.dll "$WORK/mcp-aot.json" "$AOT/proto-shelf" mcp > "$WORK/mcp-aot.out" || true
  grep -E "^(pass|FAIL|passed)" "$WORK/mcp-aot.out" | cut -c1-110
  grep -q "^passed 8/8" "$WORK/mcp-aot.out" || { echo "FAIL C6: AOT run did not pass 8/8"; FAILS=$((FAILS + 1)); }
  dotnet out/McpClient/proto-mcp-client.dll "$WORK/mcp-jit.json" "$JIT/proto-shelf" mcp > "$WORK/mcp-jit.out" || true
  tail -1 "$WORK/mcp-jit.out"
  if diff -q "$WORK/mcp-jit.json" "$WORK/mcp-aot.json" > /dev/null; then row C6-dump 0 "AOT and JIT response dumps identical ($(wc -l < "$WORK/mcp-aot.json" | tr -d ' ') lines)"; else row C6-dump 1 "dumps differ"; diff -u "$WORK/mcp-jit.json" "$WORK/mcp-aot.json" | head -20; fi
  grep -q "^passed 8/8" "$WORK/mcp-jit.out" || FAILS=$((FAILS + 1))

  echo "== C7 web client against proto-shelf serve (Ui/dist moved away, empty working directory)"
  python3 -c "open('$WORK/f1.bin','wb').write(bytes(i % 251 for i in range(1048576)))"
  mv ../web/Ui/dist ../web/Ui/dist.hidden
  dotnet out/WebClient/proto-web-client.dll "$WORK/web-aot.json" ../web/Ui/dist.hidden "$WORK/f1.bin" "$AOT/proto-shelf" serve > "$WORK/web-aot.out" || true
  grep -E "^(pass|FAIL|passed)" "$WORK/web-aot.out" | cut -c1-110
  grep -q "^passed 8/8" "$WORK/web-aot.out" || { echo "FAIL C7: AOT run did not pass 8/8"; FAILS=$((FAILS + 1)); }
  dotnet out/WebClient/proto-web-client.dll "$WORK/web-jit.json" ../web/Ui/dist.hidden "$WORK/f1.bin" "$JIT/proto-shelf" serve > "$WORK/web-jit.out" || true
  tail -1 "$WORK/web-jit.out"
  mv ../web/Ui/dist.hidden ../web/Ui/dist
  if diff -q "$WORK/web-jit.json" "$WORK/web-aot.json" > /dev/null; then row C7-dump 0 "AOT and JIT response dumps identical ($(wc -l < "$WORK/web-aot.json" | tr -d ' ') lines)"; else row C7-dump 1 "dumps differ"; diff -u "$WORK/web-jit.json" "$WORK/web-aot.json" | head -20; fi
  grep -q "^passed 8/8" "$WORK/web-jit.out" || FAILS=$((FAILS + 1))

  echo "== C8 publish directory copied to an empty location, other working directory"
  rm -rf "$WORK/copy"; cp -R "$AOT" "$WORK/copy"
  (cd "$WORK" && commands_rows "$WORK/copy" "-copy")
  echo "files next to the binary: $(ls "$AOT" | grep -v '\.dSYM$' | tr '\n' ' ')"

  echo "== X-1 sizes"
  echo "observed X-1: proto-shelf $(size_of "$AOT/proto-shelf") B; libe_sqlite3.dylib $(size_of "$AOT/libe_sqlite3.dylib" 2>/dev/null || echo n/a) B; vec0.dylib $(size_of "$AOT/vec0.dylib") B"
  echo "observed X-1 (single spikes, osx-arm64, from their results): $(grep -h 'size proto-mcp' ../mcp/results/local/osx-arm64.txt | head -1); $(grep -h 'size proto-web' ../web/results/local/osx-arm64.txt | head -1)"
  echo "observed X-1: ui embedded: $(find ../web/Ui/dist -type f -exec stat -f%z {} + | paste -sd+ - | bc) B"

  echo "== X-2 cold start (30 runs after 5 warm-up runs; informational, not gated)"
  for ARG in --version --help; do
    python3 - "$AOT/proto-shelf" "$ARG" <<'PY'
import subprocess, sys, time
binp, arg = sys.argv[1], sys.argv[2]
def run():
    s = time.perf_counter(); subprocess.run([binp, arg], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL); return (time.perf_counter() - s) * 1000
first = run()
for _ in range(5): run()
t = sorted(run() for _ in range(30))
print("observed X-2: %s first=%.1f ms median=%.1f ms p95=%.1f ms min=%.1f ms" % (arg, first, t[14] * 0.5 + t[15] * 0.5, t[int(0.95 * 30) - 1], t[0]))
PY
  done

  if [ "$FAILS" -ne 0 ]; then echo "== check $RID: $FAILS failing rows"; return 1; fi
  echo "== check $RID: ok"
}

main 2>&1 | tee "results/local/$RID.txt"
exit "${PIPESTATUS[0]}"
