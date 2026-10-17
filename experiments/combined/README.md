# experiments/combined

Spike for [ADR 0008](../../docs/adr/0008-combined-aot-binary.md): one Native AOT binary (`proto-shelf`) that contains System.CommandLine,
CsToml, SQLite + FTS5 + sqlite-vec, VersOne.Epub, PdfPig, the MCP SDK and the ASP.NET Core web server with the embedded Svelte build,
on `osx-arm64`. Not part of `shelf.slnx`, not built by CI. Plan: [`docs/slices/m0-spike-combined.md`](../../docs/slices/m0-spike-combined.md).

- `Combined/`: the app. Commands: `config get`, `db check`, `extract`, `mcp`, `serve`. The MCP and web code are copies of
  `experiments/mcp/Server` and `experiments/web/Server` in the namespaces `Proto.Mcp` and `Proto.Web`.
- `check.sh`: builds the Svelte UI of `experiments/web` and the two reused check clients, publishes the AOT and JIT app, runs rows C1-C8 and
  the observations X-1/X-2, writes `results/local/osx-arm64.txt`. Apple Silicon Mac only.

```
./check.sh
```
