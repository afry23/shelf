#!/usr/bin/env bash
# End-to-end check of the native shelf binary: import, search, edit and reindex (M1), then organize, undo, tags,
# duplicates and the trash (M2). Usage: native.sh <path to shelf> <empty work directory>
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
echo "end-to-end check passed"
