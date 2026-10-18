#!/usr/bin/env bash
# Downloads the multilingual-e5-small files that shelf pins (src/Shelf.Semantic/ModelCatalog.cs: same revision,
# same SHA-256) into the given folder, for the scenarios and the end-to-end check that read SHELF_E5_DIR.
# A file that is already there with the right hash is kept. Usage: ./fetch-e5-model.sh <folder>
set -euo pipefail

DEST="${1:?usage: fetch-e5-model.sh <folder>}"
BASE="https://huggingface.co/intfloat/multilingual-e5-small/resolve/614241f622f53c4eeff9890bdc4f31cfecc418b3/onnx"
FILES=(
  "model.onnx ca456c06b3a9505ddfd9131408916dd79290368331e7d76bb621f1cba6bc8665"
  "sentencepiece.bpe.model cfc8146abe2a0488e9e2a0c56de7952f7c11ab059eca145a0a727afce0db2865"
)

sha256_of() {
  if command -v sha256sum >/dev/null 2>&1; then sha256sum "$1" | awk '{print $1}'; else shasum -a 256 "$1" | awk '{print $1}'; fi
}

mkdir -p "$DEST"
for entry in "${FILES[@]}"; do
  NAME="${entry% *}"
  EXPECTED="${entry#* }"
  if [ -f "$DEST/$NAME" ] && [ "$(sha256_of "$DEST/$NAME")" = "$EXPECTED" ]; then
    echo "$DEST/$NAME (already verified)"
    continue
  fi
  curl --fail --silent --show-error --location --retry 3 --output "$DEST/$NAME.part" "$BASE/$NAME"
  if [ "$(sha256_of "$DEST/$NAME.part")" != "$EXPECTED" ]; then
    rm -f "$DEST/$NAME.part"
    echo "checksum mismatch for $NAME" >&2
    exit 1
  fi
  mv "$DEST/$NAME.part" "$DEST/$NAME"
  echo "$DEST/$NAME"
done
