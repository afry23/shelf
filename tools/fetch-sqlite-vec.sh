#!/usr/bin/env bash
# Downloads the official sqlite-vec v0.1.9 loadable-extension release assets (GitHub release
# asset URLs only, one request per asset, no crawling), verifies the pinned SHA-256 from
# sqlite-vec.sha256, and unpacks each archive into its own new directory vendor/<rid>/.
# Nothing is installed and nothing from the archive is executed.
# Usage: ./fetch-sqlite-vec.sh [rid ...]   (rid: osx-arm64 osx-x64 linux-x64 linux-arm64; default: all)
# Prints the extracted library path per rid. Portable bash 3.2.
# The libraries land in vendor/sqlite-vec/<rid>/, where Shelf.Index.csproj picks them up (ADR 0004, hand-off M3).
set -euo pipefail

cd "$(dirname "$0")"
VERSION=0.1.9
BASE="https://github.com/asg017/sqlite-vec/releases/download/v${VERSION}"

RIDS=("$@")
if [ ${#RIDS[@]} -eq 0 ]; then
  RIDS=(osx-arm64 linux-x64)
fi

sha256_of() {
  if command -v sha256sum >/dev/null 2>&1; then sha256sum "$1" | awk '{print $1}'; else shasum -a 256 "$1" | awk '{print $1}'; fi
}

for RID in "${RIDS[@]}"; do
  case "$RID" in
    osx-arm64) PLAT=macos-aarch64; LIB=vec0.dylib ;;
    osx-x64) PLAT=macos-x86_64; LIB=vec0.dylib ;;
    linux-x64) PLAT=linux-x86_64; LIB=vec0.so ;;
    linux-arm64) PLAT=linux-aarch64; LIB=vec0.so ;;
    *) echo "unknown rid: $RID" >&2; exit 2 ;;
  esac
  ASSET="sqlite-vec-${VERSION}-loadable-${PLAT}.tar.gz"
  DEST="../vendor/sqlite-vec/$RID"
  if [ -f "$DEST/$LIB" ] && [ -f "$DEST/.verified" ] && [ "$(cat "$DEST/.verified")" = "$ASSET" ]; then
    echo "$DEST/$LIB (already verified)"
    continue
  fi
  EXPECTED="$(grep -F "  $ASSET" sqlite-vec.sha256 | awk '{print $1}')"
  if [ -z "$EXPECTED" ]; then echo "no pinned checksum for $ASSET" >&2; exit 1; fi
  rm -rf "$DEST"
  mkdir -p "$DEST/archive" "$DEST/files"
  curl --fail --silent --show-error --location --output "$DEST/archive/$ASSET" "$BASE/$ASSET"
  ACTUAL="$(sha256_of "$DEST/archive/$ASSET")"
  if [ "$ACTUAL" != "$EXPECTED" ]; then
    echo "checksum mismatch for $ASSET: expected $EXPECTED, got $ACTUAL" >&2
    rm -rf "$DEST"
    exit 1
  fi
  tar -xzf "$DEST/archive/$ASSET" -C "$DEST/files"
  if [ ! -f "$DEST/files/$LIB" ]; then echo "$LIB not found in $ASSET" >&2; ls -la "$DEST/files" >&2; exit 1; fi
  cp "$DEST/files/$LIB" "$DEST/$LIB"
  echo "$ASSET" > "$DEST/.verified"
  echo "$DEST/$LIB"
done
