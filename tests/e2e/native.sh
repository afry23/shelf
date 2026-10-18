#!/usr/bin/env bash
# End-to-end check of the native shelf binary: import, search, edit and reindex (M1), then organize, undo, tags,
# duplicates and the trash (M2), then the MCP server and, when a model folder is given, semantic search (M3), then the web
# server (M4): the API, files with range requests, covers, the interface without Node.js, the access token and the cookie.
# Usage: native.sh <path to shelf> <empty work directory>
# SHELF_E5_DIR (optional): folder with model.onnx and sentencepiece.bpe.model of multilingual-e5-small.
set -euo pipefail

BIN="$1"
WORK="$2"
FIXTURES="$(cd "$(dirname "$0")/../fixtures/derived" && pwd)"

mkdir -p "$WORK/incoming"
cp "$FIXTURES/epub3-isbn-series.epub" "$FIXTURES/Event Streams in Practice.pdf" "$WORK/incoming/"
cd "$WORK"

# The files and folders of the library, with hashes, without what changes by itself (index, journal).
snapshot() {
  (cd library && find . ! -path './.shelf/index.db*' ! -path './.shelf/journal*' \( -type d -o -type f \) | sort | while IFS= read -r p; do
    if [ -d "$p" ]; then echo "dir $p"; else echo "$(shasum -a 256 "$p" | cut -d' ' -f1) $p"; fi
  done)
}

echo "== M1: import, search, edit, reindex, scan"
"$BIN" init library
"$BIN" import incoming --library library | tee import.txt
grep -q "Imported 2 files: 1 new work, 1 added to an existing work" import.txt
"$BIN" search event --library library --json | tee search.json
grep -q '"total":1' search.json
ID="$(sed -E 's/.*"id":"([0-9A-Z]{26})".*/\1/' search.json)"
"$BIN" show "$ID" --library library | grep -q '^Event Streams in Practice$'
"$BIN" edit "$ID" --add-tag e2e --library library
"$BIN" stats --library library --json | grep -q '"works":1'
rm library/.shelf/index.db
"$BIN" reindex --library library
"$BIN" search e2e --library library | grep -q 'Event Streams in Practice'
"$BIN" scan --library library | grep -q 'unchanged'

echo "== M2: organize and undo (the exact initial state, by hash)"
"$BIN" organize --library library | grep -q 'Would move 1 file in 1 work' # the EPUB still has the name of the source
snapshot > before.txt
"$BIN" organize --library library --pattern '{author}/{title}.{ext}' | tee dry.txt
grep -q 'Would move 2 files in 1 work' dry.txt
snapshot | diff before.txt - # a dry run changes nothing
"$BIN" organize --apply --library library --pattern '{author}/{title}.{ext}' | grep -q 'Moved 2 files in 1 work'
"$BIN" search event --library library --json | grep -q '"total":1'
"$BIN" undo --library library | grep -q 'Reverted the last organize run'
snapshot | diff before.txt -
"$BIN" organize --library library --pattern '{nope}.{ext}' && { echo "an invalid pattern must fail"; exit 1; } || [ $? -eq 2 ]

echo "== M2: tags"
"$BIN" tag add bulk --query 'tag:e2e' --library library | grep -q "Added 'bulk' to 1 work"
"$BIN" tag rename bulk renamed --library library | grep -q "Renamed 'bulk' to 'renamed' in 1 work"
"$BIN" tag list --library library | grep -q 'renamed'
"$BIN" undo --library library | grep -q 'Reverted the last tag rename run'
"$BIN" tag list --library library | grep -q 'bulk'
"$BIN" tag rm bulk "$ID" --library library | grep -q "Removed 'bulk' from 1 work"

echo "== M2: duplicates and the trash"
EPUB="$(find library -name '*.epub' | head -n 1)"
cp "$EPUB" "${EPUB%.epub} (1).epub"
"$BIN" scan --library library > /dev/null
"$BIN" dupes --library library | grep -q 'identical files'
snapshot > dupes-before.txt
printf '\n' | "$BIN" dupes --interactive --library library | grep -q 'Moved 1 file to the trash'
[ ! -e "${EPUB%.epub} (1).epub" ]
"$BIN" trash empty --library library | grep -q 'Would delete 1 file'
"$BIN" undo --library library | grep -q 'Reverted the last dupes run'
snapshot | diff dupes-before.txt -
printf '\n' | "$BIN" dupes --interactive --library library > /dev/null
"$BIN" trash empty --yes --library library | grep -q 'Deleted 1 file'
"$BIN" dupes --library library | grep -q 'No duplicates.'

echo "== M3: MCP server (stdio handshake, tools, clean stdout)"
# A client keeps standard input open until it has its answers; the server ends when the input ends.
mcp_session() {
  {
    printf '%s\n' \
      '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"e2e","version":"1"}}}' \
      '{"jsonrpc":"2.0","method":"notifications/initialized"}' \
      '{"jsonrpc":"2.0","id":2,"method":"tools/list"}' \
      "$@"
    sleep 6
  } | "$BIN" mcp --library library 2> mcp-stderr.txt
}
mcp_session '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"search_books","arguments":{"query":"event"}}}' > mcp.txt
[ ! -s mcp-stderr.txt ] # logs go to stderr, and nothing is logged on a clean run
# every line of standard output is one JSON-RPC message
while IFS= read -r line; do case "$line" in '{'*'"jsonrpc":"2.0"'*'}') ;; *) echo "stray stdout line: $line"; exit 1;; esac; done < mcp.txt
for tool in search_books semantic_search get_book read_passage list_tags find_duplicates propose_organization add_tags remove_tags; do
  grep -q "\"name\":\"$tool\"" mcp.txt
done
grep -q 'Event Streams in Practice' mcp.txt

echo "== M4: web server (API, files with ranges, covers, the interface without Node.js, token and cookie)"
# Covers: import made them (a rendered JPEG for the PDF); shelf covers has nothing left to do.
find library -name 'cover.jpg' | grep -q .
"$BIN" covers --library library | grep -q 'Created 0 covers'
"$BIN" covers --json --library library | grep -q '"created":0'

# The server runs the way a user would run it: from a reduced PATH, so that nothing can fall back on a Node.js on the machine.
if PATH=/usr/bin:/bin command -v node > /dev/null 2>&1; then echo "note: node is on the reduced PATH"; fi
serve_start() {
  env -i PATH=/usr/bin:/bin HOME="$PWD" "$BIN" serve --library library --port 0 "$@" > serve-out.txt 2> serve-err.txt &
  SERVE_PID=$!
  for _ in $(seq 1 100); do grep -q 'Listening on' serve-err.txt && break; sleep 0.1; done
  grep -q 'Listening on' serve-err.txt
  BASE="$(sed -n 's#^Listening on \(http://[^ ]*\).*#\1#p' serve-err.txt | head -n 1)"
}
serve_stop() {
  kill -TERM "$SERVE_PID"
  wait "$SERVE_PID" # exit code 0: an orderly stop on SIGTERM
  [ ! -s serve-out.txt ] # nothing on standard output
}

serve_start
curl -sS "$BASE/api/v1/status" -o status.json
grep -q '"works":' status.json
curl -sS "$BASE/api/v1/works" -o works.json
grep -q '"total":' works.json
grep -q '"hasCover":true' works.json
curl -sS "$BASE/api/v1/search?q=event" -o web-search.json
grep -q 'Event Streams in Practice' web-search.json
grep -q '"fileId":' web-search.json

# A file with range requests, byte for byte against the file in the library.
curl -sS "$BASE/api/v1/works?q=format%3Apdf" -o pdf-works.json
BOOK="$(sed -n 's/.*"items":\[{"id":"\([^"]*\)".*/\1/p' pdf-works.json)"
curl -sS "$BASE/api/v1/works/$BOOK" -o detail.json
FILE_ID="$(sed -n 's/.*"files":\[{"id":"\([^"]*\)".*/\1/p' detail.json)"
FILE_URL="$BASE/api/v1/files/${FILE_ID// /%20}"
PDF="$(find library -name 'Event Streams in Practice.pdf' | head -n 1)"
curl -sS "$FILE_URL" -o whole.bin
cmp whole.bin "$PDF"
curl -sS -D range.txt -H 'Range: bytes=100-199' "$FILE_URL" -o part.bin
head -n 1 range.txt | grep -q ' 206'
grep -qi '^content-range: bytes 100-199/' range.txt
dd if="$PDF" bs=1 skip=100 count=100 2> /dev/null | cmp - part.bin
curl -sS -D tail.txt -H 'Range: bytes=-50' "$FILE_URL" -o tail.bin
tail -c 50 "$PDF" | cmp - tail.bin
code="$(curl -s -o /dev/null -w '%{http_code}' -H 'Range: bytes=99999999-' "$FILE_URL")"
[ "$code" = 416 ]
curl -sSI "$FILE_URL" -o head.txt
grep -qi '^content-length: ' head.txt
grep -qi '^accept-ranges: bytes' head.txt

# The cover over HTTP is the file on disk.
curl -sS "$BASE/api/v1/works/$BOOK/cover" -o web-cover.bin
cmp web-cover.bin "$(dirname "$PDF")/cover.jpg"

# The interface comes out of the binary: the page, its script and style, the policy, the data of PDF.js, the routes of the browser.
curl -sS -D page-head.txt "$BASE/" -o page.html
grep -q '<title>shelf</title>' page.html
grep -qi "^content-security-policy: .*script-src 'self'" page-head.txt
SCRIPT="$(sed -n 's#.*src="\(/assets/[^"]*\.js\)".*#\1#p' page.html | head -n 1)"
STYLE="$(sed -n 's#.*href="\(/assets/[^"]*\.css\)".*#\1#p' page.html | head -n 1)"
curl -sSf "$BASE$SCRIPT" -o /dev/null
curl -sSf "$BASE$STYLE" -o /dev/null
curl -sS -D wasm-head.txt "$BASE/pdfjs/wasm/openjpeg.wasm" -o /dev/null
grep -qi '^content-type: application/wasm' wasm-head.txt
code="$(curl -s -o /dev/null -w '%{http_code}' -H 'Accept: text/html' "$BASE/read/anything~book.pdf")"
[ "$code" = 200 ]
code="$(curl -s -o /dev/null -w '%{http_code}' "$BASE/api/v1/unknown")"
[ "$code" = 404 ]

# A change over HTTP is a change of the library: the command line sees it, and undo takes it back in one step.
curl -sS -X PUT -H 'Content-Type: application/json' -d '{"tags":["e2e-web"]}' "$BASE/api/v1/works/$BOOK/tags" -o web-tags.json
grep -q '"added":1' web-tags.json
"$BIN" tag list --library library | grep -q 'e2e-web'
"$BIN" undo --library library > /dev/null
if "$BIN" tag list --library library | grep -q 'e2e-web'; then echo "the tag of the web call is still there after undo"; exit 1; fi

# Duplicates over HTTP: a copy is found, kept away by the action, and the command line brings it back.
cp "$PDF" "${PDF%.pdf} (2).pdf"
"$BIN" scan --library library > /dev/null
snapshot > web-dupes-before.txt
curl -sS "$BASE/api/v1/duplicates" -o web-dupes.json
grep -q '"kind":"identical"' web-dupes.json
curl -sS -X POST -H 'Content-Type: application/json' -d "{\"fileId\":\"$FILE_ID\"}" "$BASE/api/v1/duplicates/keep" -o web-keep.json
grep -q '"trashed":1' web-keep.json
[ ! -e "${PDF%.pdf} (2).pdf" ]
"$BIN" undo --library library | grep -q 'Reverted the last dupes run'
snapshot | diff web-dupes-before.txt -
serve_stop

# Access: a token is needed for the API and not for the interface; the link with the token sets a cookie.
serve_start --token s3cret
code="$(curl -s -o /dev/null -w '%{http_code}' "$BASE/api/v1/status")"
[ "$code" = 401 ]
code="$(curl -s -o /dev/null -w '%{http_code}' -H 'Authorization: Bearer s3cret' "$BASE/api/v1/status")"
[ "$code" = 200 ]
code="$(curl -s -o /dev/null -w '%{http_code}' "$BASE/")" # the page and its files are public
[ "$code" = 200 ]
curl -sS -c cookies.txt -D token-head.txt "$BASE/library?token=s3cret" -o /dev/null
head -n 1 token-head.txt | grep -q ' 303'
grep -qi '^set-cookie: shelf_token=' token-head.txt
code="$(curl -s -b cookies.txt -o /dev/null -w '%{http_code}' "$BASE/api/v1/status")"
[ "$code" = 200 ]
serve_stop

# An address that is not loopback needs a token (exit code 2, nothing on standard output).
code=0
"$BIN" serve --library library --port 0 --bind 0.0.0.0 > bind-out.txt 2> bind-err.txt || code=$?
[ "$code" = 2 ]
grep -q 'requires --token' bind-err.txt
[ ! -s bind-out.txt ]

if [ -n "${SHELF_E5_DIR:-}" ]; then
  echo "== M3: model, embeddings, semantic search"
  "$BIN" model install "$SHELF_E5_DIR" --library library | grep -q 'multilingual-e5-small'
  "$BIN" reindex --embeddings --library library > embed.txt
  "$BIN" semantic 'Ströme von Ereignissen in der Praxis' --library library | tee semantic.txt
  grep -q 'Event Streams in Practice' semantic.txt
  "$BIN" semantic 'Ströme von Ereignissen in der Praxis' --chunks --hybrid --library library --json | grep -q '"locator":"page=1"'
  mcp_session '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"semantic_search","arguments":{"query":"Ströme von Ereignissen"}}}' > mcp-semantic.txt
  [ ! -s mcp-stderr.txt ]
  grep -q 'Event Streams in Practice' mcp-semantic.txt
else
  echo "SHELF_E5_DIR is not set: semantic search skipped"
fi
echo "end-to-end check passed"
