# experiments/mcp

Spike for [ADR 0006](../../docs/adr/0006-mcp-sdk-aot.md): `ModelContextProtocol.Core` 2.2.0 under Native AOT on `osx-arm64`.
Not part of `shelf.slnx`, not built by CI. Plan: [`docs/slices/m0-spike-mcp.md`](../../docs/slices/m0-spike-mcp.md).

- `Server/`: stdio MCP server (`proto-mcp`) with four dummy tools and two resource templates, AOT published.
- `Client/`: raw newline-delimited JSON-RPC client (no SDK), runs checks M2-M9 and writes a normalized response dump.
- `check.sh`: AOT publish (M1), client against the AOT server (M2-M9), client against a JIT server and dump diff (M10).
  Writes `results/local/osx-arm64.txt`. Only runs on an Apple Silicon Mac.

```
./check.sh
```
