#!/usr/bin/env bash
# PDF -> reflowable EPUB with docling (layout) and pandoc (ADR 0022): the alternative to tools/pdf2epub.sh for books where
# marker reads words as formulas. Use it as the converter of shelf convert:
#   [convert]
#   command = "/path/to/shelf/tools/pdf2epub-docling.sh {input} {output}"
# Needs on the PATH: docling (uv tool install docling) and pandoc. Not part of the shelf binary.
set -euo pipefail

if [ "$#" -ne 2 ]; then
  echo "usage: pdf2epub-docling.sh <input.pdf> <output.epub>" >&2
  exit 2
fi
input=$1
output=$2

for tool in docling pandoc; do
  command -v "$tool" >/dev/null 2>&1 || { echo "$tool was not found; see tools/pdf2epub-docling.sh" >&2; exit 127; }
done

work=$(mktemp -d "${TMPDIR:-/tmp}/shelf-pdf2epub-XXXXXX")
trap 'rm -rf "$work"' EXIT

docling --to md --output "$work" "$input" >&2
markdown=$(find "$work" -name '*.md' -print -quit)
if [ -z "$markdown" ]; then
  echo "docling wrote no Markdown file" >&2
  exit 1
fi

# docling lets control characters of the PDF through (measured: 0x03 and 0x08); XML does not allow them, so the EPUB
# would be invalid. A link without a scheme ("www.example.org") would be read as a file of the EPUB.
clean="$work/clean.md"
LC_ALL=C tr -d '\000-\010\013\014\016-\037' < "$markdown" | sed -E 's#\]\((www\.)#](https://\1#g' > "$clean"

title=$(basename "$input" .pdf)
# Same as pdf2epub.sh: the headings move up one level when there is no "# " above them.
shift=0
if ! grep -q '^# ' "$clean"; then
  shift=-1
fi

(cd "$work" && pandoc clean.md -f markdown -t epub3 --shift-heading-level-by="$shift" --metadata "title=$title" -o "$output")
