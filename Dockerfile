# syntax=docker/dockerfile:1

# shelf serve in a container: one Native AOT executable plus vec0.so (sqlite-vec) next to it.
#
#   docker build --build-arg VERSION=$(git describe --tags) -t shelf .
#   docker run --rm --entrypoint /app/shelf -v "$PWD/books:/library" shelf init /library   # once
#   docker run -d -p 8080:8080 -e SHELF_TOKEN=secret -v "$PWD/books:/library" shelf
#
# The library (books, index.db, installed models) lives in the volume /library. The container listens on
# 0.0.0.0, so shelf needs a token: SHELF_TOKEN, or web.token in /library/.shelf/config.toml.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
ARG NODE_VERSION=26.0.0
# Version of shelf, e.g. v0.5.2 or 0.5.2 (leading "v" is dropped). .git is not in the build context, so pass it in.
ARG VERSION=

# Native AOT needs clang and zlib; the UI build needs Node.js (see .node-version).
RUN apt-get update \
    && apt-get install -y --no-install-recommends clang zlib1g-dev curl xz-utils ca-certificates \
    && rm -rf /var/lib/apt/lists/*
RUN case "$TARGETARCH" in arm64) NODE_ARCH=arm64 ;; *) NODE_ARCH=x64 ;; esac \
    && curl -fsSL "https://nodejs.org/dist/v${NODE_VERSION}/node-v${NODE_VERSION}-linux-${NODE_ARCH}.tar.xz" \
       | tar -xJ -C /usr/local --strip-components=1

WORKDIR /src
COPY . .

RUN case "$TARGETARCH" in arm64) RID=linux-arm64 ;; *) RID=linux-x64 ;; esac \
    && tools/fetch-sqlite-vec.sh "$RID" \
    && dotnet publish src/Shelf.Cli/Shelf.Cli.csproj -c Release -r "$RID" -p:PublishAot=true \
       ${VERSION:+-p:Version="${VERSION#v}"} -o /out

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS runtime

# libfontconfig1: SkiaSharp (PDF covers) loads it at runtime.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libfontconfig1 \
    && rm -rf /var/lib/apt/lists/* \
    && useradd --create-home --uid 10001 shelf \
    && mkdir /library \
    && chown shelf /library

COPY --from=build /out/ /app/
COPY --chmod=755 <<'SCRIPT' /app/docker-entrypoint.sh
#!/bin/sh
set -e
if [ -n "$SHELF_TOKEN" ]; then
    set -- --token "$SHELF_TOKEN" "$@"
fi
exec /app/shelf serve --library /library --bind 0.0.0.0 --port 8080 "$@"
SCRIPT

USER shelf
WORKDIR /library
VOLUME /library
EXPOSE 8080
ENTRYPOINT ["/app/docker-entrypoint.sh"]
