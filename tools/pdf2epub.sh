#!/usr/bin/env bash
# PDF -> reflowable EPUB with marker (layout, reading order of columns, footnotes) and pandoc (ADR 0022).
# Use it as the converter of shelf convert:
#   [convert]
#   command = "/path/to/shelf/tools/pdf2epub.sh {input} {output}"
# Needs on the PATH: marker_single (uv tool install marker-pdf) and pandoc. Not part of the shelf binary.
set -euo pipefail

if [ "$#" -ne 2 ]; then
  echo "usage: pdf2epub.sh <input.pdf> <output.epub>" >&2
  exit 2
fi
input=$1
output=$2

for tool in marker_single pandoc; do
  command -v "$tool" >/dev/null 2>&1 || { echo "$tool was not found; see tools/pdf2epub.sh" >&2; exit 127; }
done

work=$(mktemp -d "${TMPDIR:-/tmp}/shelf-pdf2epub-XXXXXX")
trap 'rm -rf "$work"' EXIT

marker_single "$input" --output_format markdown --output_dir "$work" >&2
markdown=$(find "$work" -name '*.md' -print -quit)
if [ -z "$markdown" ]; then
  echo "marker_single wrote no Markdown file" >&2
  exit 1
fi

# Title from the PDF file name; shelf puts the real metadata of the work in the library, not in this file.
title=$(basename "$input" .pdf)
# Run from the Markdown folder so that images next to it are found and embedded.
(cd "$(dirname "$markdown")" && pandoc "$(basename "$markdown")" -f markdown -t epub3 --metadata "title=$title" -o "$output")
