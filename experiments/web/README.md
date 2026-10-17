# experiments/web

Spike for [ADR 0007](../../docs/adr/0007-web-backend-aot.md): ASP.NET Core Minimal APIs and an embedded Svelte UI under
Native AOT on `osx-arm64`. Not part of `shelf.slnx`, not built by CI. Plan: [`docs/slices/m0-spike-web.md`](../../docs/slices/m0-spike-web.md).

- `Ui/`: Svelte 5 + Vite + TypeScript page (`npm ci && npm run build` writes `Ui/dist`). Node.js is needed for the build only.
- `Server/`: AOT server `proto-web` (`--bind`, `--port`, `--token`, `--file`): JSON API under `/api/v1`, file range requests,
  OpenAPI document, bearer token, the Svelte build embedded as resources.
- `Client/`: HTTP check client that starts server processes and runs rows W3-W10, writing a normalized response dump.
- `check.sh`: Svelte build (W2), AOT publish (W1), client against the AOT server (W3-W10) with `Ui/dist` moved away and an empty
  working directory, client against a JIT server and dump diff (W11). Writes `results/local/osx-arm64.txt`. Apple Silicon Mac only.

```
./check.sh
```
