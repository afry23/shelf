# shelf

[![build](https://github.com/afry23/shelf/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/afry23/shelf/actions/workflows/build.yml)
[![release](https://img.shields.io/github/v/release/afry23/shelf?include_prereleases)](https://github.com/afry23/shelf/releases)
[![license](https://img.shields.io/badge/license-GPL--3.0-blue)](LICENSE)

A fast, lean e-book manager for **one library** that stays sorted in a human-readable way on your file system.

- Tags (hierarchical, bulk editing)
- Fast full-text search (SQLite FTS5, BM25) over metadata and book content
- Local, offline semantic search (German and English)
- Duplicate detection with preview and undo
- Automatic organization by a configurable folder pattern
- Several formats of one book (EPUB + PDF) are managed as **one work**
- Three equal ways in: **CLI**, **MCP server** for AI assistants, **web UI** with built-in PDF and EPUB reader

shelf ships as one self-contained Native AOT binary. No Python, no Docker, no vector database, no telemetry.

## Why not Calibre?

Calibre is slow and broad for this use case, and it couples your books to its own database. shelf keeps everything in plain folders. The library stays fully usable without shelf.

## Install

Download the archive for your platform from the [GitHub Releases](https://github.com/afry23/shelf/releases) (`shelf-<version>-<rid>.tar.gz`). Supported platforms are `osx-arm64` and `linux-x64`. The release also builds `linux-arm64` and `win-x64`, but those are not verified end to end.

Keep the files that come in the archive next to the binary (for example `vec0` of sqlite-vec).

```sh
tar -xzf shelf-<version>-osx-arm64.tar.gz
./shelf-<version>-osx-arm64/shelf --version
```

Build from source (.NET 10 SDK, Node.js for the UI, clang and zlib for Native AOT on Linux):

```sh
tools/fetch-sqlite-vec.sh osx-arm64        # or linux-x64
dotnet publish src/Shelf.Cli/Shelf.Cli.csproj -c Release -r osx-arm64 -p:PublishAot=true -o out
```

### Docker

```sh
docker compose run --rm --entrypoint /app/shelf shelf init /library   # once
echo "SHELF_TOKEN=change-me" > .env
docker compose up -d --build
```

The container listens on `0.0.0.0`, so a token is mandatory. The compose file publishes the port on `127.0.0.1` only. The library folder must belong to UID 10001. See `compose.yaml` and `Dockerfile`.

## Quick start

```sh
shelf init ~/Books                          # create a library
cd ~/Books
shelf import ~/Downloads/ebooks --dry-run   # preview: what is recognized, grouped, filed
shelf import ~/Downloads/ebooks             # do it
shelf search "event sourcing"               # full-text search
shelf model install                         # once: download the embedding model
shelf reindex --embeddings                  # build embeddings (hours on a big library, resumable)
shelf semantic "books about event sourcing in distributed systems"
shelf serve                                 # web UI on http://127.0.0.1:8420
```

shelf finds the library from the current folder (or a parent), from `--library`, or from `default_library` in the global config.

## How a library looks

```
~/Books/
  Fowler, Martin/
    Patterns of Enterprise Application Architecture (2002)/
      Patterns of Enterprise Application Architecture.epub
      Patterns of Enterprise Application Architecture.pdf
      book.json          <- sidecar: source of truth for metadata, tags, progress
      cover.jpg
  .shelf/
    config.toml          <- library config
    index.db             <- SQLite index and cache (rebuildable)
    review.json          <- review queue
    journal/             <- undo data
    models/              <- embedding model
    trash/               <- deleted files, restorable with `shelf undo`
```

`book.json` is the source of truth. The SQLite database is only an index: `shelf reindex` rebuilds it from the folders and sidecars.

## Changing files by hand

You may use `cp`, `mv` and `rm` in the library folder. Afterwards run `shelf scan`. It looks only at what changed (new, changed, removed files) and brings the index in line. You do not need `shelf reindex` for this.

| What you do | What `shelf scan` does |
|---|---|
| Copy or move a file into an existing **work folder** (next to its `book.json`) | The file joins that work, is read and indexed, and is added to the sidecar |
| Put a file anywhere else in the library | Reports `untracked <path>` and does nothing. Use `shelf import --move` on its folder, or `shelf add` / `shelf create` |
| Create a new work folder with a `book.json` | The folder is indexed as a new work |
| Replace a file with other content | The file is read again |
| Delete a file | The file leaves the index. **The sidecar still names it** |
| Delete a work folder | The work leaves the index |
| Rename or move a work folder | The work keeps its content and gets the new path |
| Edit `book.json` | Title, tags and other metadata are updated without reading the files again |

Notes:

- `shelf scan` does not create embeddings. After adding books run `shelf reindex --embeddings`, which only creates the missing ones. Semantic search will not find new files before that.
- `rm` is final. `shelf delete <work> [file]` moves the file to `.shelf/trash/` and `shelf undo` brings it back.
- `shelf add <work> <file>` is safer than `cp`: it refuses a file with the same SHA-256 as one already in the library, and `--replace` keeps the old file in the trash.
- Do not run `shelf scan` while you are still copying a large file into the library.
- shelf does not watch the folder. A running `shelf serve` or `shelf mcp` sees changes only after a scan.

## `scan` or `reindex`?

| Command | Reads | Use it when |
|---|---|---|
| `shelf scan` | Only what changed: new, changed and removed files, changed sidecars. Files with the same size and time are skipped | Daily work. You added, replaced or deleted files, or edited a `book.json` |
| `shelf reindex` | **Everything**: all sidecars and the content of all files (full text, chunks, covers for works without one). The index is rebuilt from scratch | The index is damaged or lost, or you moved the library. Takes a while on a big library |
| `shelf reindex --metadata-only` | All sidecars, **no file content**. Fast, but full-text search finds only metadata until the content is read | You want a quick index of titles, authors and tags first. A later `shelf scan` reads the files |
| `shelf reindex --embeddings` | Nothing of the above. Only makes the **missing embeddings** for semantic search; the rest of the index is not touched | After a scan or reindex, or after changing `semantic.model`. Resumable |

Notes:

- `--embeddings` and `--metadata-only` cannot be combined.
- A plain `shelf reindex` drops the embeddings. Run `shelf reindex --embeddings` afterwards.
- `--embeddings` can be interrupted with Ctrl-C (exit code 1). Run the same command again to continue where it stopped. The file in progress at the interrupt is embedded again.
- If the model is not installed yet, `--embeddings` downloads it once.

Typical sequences:

```sh
# New books copied into work folders
shelf scan
shelf reindex --embeddings

# Index lost or damaged
shelf reindex
shelf reindex --embeddings

# Quick start on a huge library, content later
shelf reindex --metadata-only
shelf scan
shelf reindex --embeddings
```

## Embedding model

Semantic search (`shelf semantic`, hybrid search, `shelf similar`, MCP `semantic_search`) runs on a local embedding model via ONNX Runtime. Nothing is sent to a server.

**Which models are there?** Currently exactly one:

| Name | Languages | Size | Notes |
|---|---|---|---|
| `multilingual-e5-small` (default) | multilingual, tested with German and English | about 470 MB (`model.onnx`) plus 5 MB tokenizer | Pinned to one revision of [intfloat/multilingual-e5-small](https://huggingface.co/intfloat/multilingual-e5-small) on Hugging Face. Every file is checked by size and SHA-256; a file that differs is never installed |

There is no choice between models yet, so there is nothing to compare. The catalog (`src/Shelf.Semantic/ModelCatalog.cs`) is built so that more models can be added. Smaller or quantized variants are considered in [ADR 0014](docs/adr/0014-m3-semantic-performance.md) but not planned. If another model gets added, `semantic.model` selects it.

**Install it:**

```sh
shelf model install                  # download once (about 475 MB) into <root>/.shelf/models/multilingual-e5-small/
shelf model install ~/Downloads/e5   # offline: from a folder that holds model.onnx and sentencepiece.bpe.model
```

Without a network, fetch the two files on another machine with `tools/fetch-e5-model.sh <folder>` (same revision, same SHA-256), copy the folder over and install from it. `shelf reindex --embeddings` also downloads the model if it is missing.

**Use it:**

```sh
shelf reindex --embeddings                                   # make the vectors
shelf semantic "books about event sourcing in distributed systems"
shelf semantic "event sourcing" --chunks                     # passages with page or CFI location
shelf semantic "event sourcing" --hybrid                     # combined with full-text search
shelf similar <work-id>                                      # books like this one
```

**Select or change the model** with `semantic.model` in a config file (default `multilingual-e5-small`). Vectors of different models cannot be compared. After a change, `shelf reindex --embeddings` warns and makes all embeddings again.

**Cost:** The model uses about 1.3 GB of memory while loaded. A one-shot `shelf semantic` loads it on every start (about half a second). `shelf serve` and `shelf mcp` keep it loaded, so their queries answer in milliseconds. Embedding a big library on CPU takes hours (the reference library was estimated at about 2 hours).

## Configuration

shelf reads two TOML files. Later ones override earlier ones:

1. Built-in defaults
2. **Global:** `$XDG_CONFIG_HOME/shelf/config.toml`, or `~/.config/shelf/config.toml` if `XDG_CONFIG_HOME` is not set to an absolute path (also on macOS)
3. **Per library:** `<root>/.shelf/config.toml` (`shelf init` writes it with every setting as a commented default)

Missing files are fine. Unknown keys produce a warning on stderr. Invalid values are an error.

### Settings

| Key | Default | Where | Meaning |
|---|---|---|---|
| `grouping.auto_merge` | `0.9` | both | Confidence from which files are grouped automatically |
| `grouping.review` | `0.5` | both | Confidence from which a review entry is created (below: new work) |
| `grouping.auto_hamming` | `3` | both | SimHash distance counted as a confident text match |
| `grouping.max_hamming` | `5` | both | Largest SimHash distance counted as a weak text match |
| `grouping.probable_hamming` | `3` | both | Distance for probable duplicates across works |
| `grouping.probable_cosine` | `0.95` | both | Embedding similarity for probable duplicates |
| `organize.pattern` | `{author_sort}/{title} ({year})/{title}.{ext}` | both | Folder and file naming for `shelf organize` |
| `semantic.model` | `multilingual-e5-small` | both | Embedding model (changing it re-triggers embeddings) |
| `reader.preferred_format` | `epub` | both | Format the web reader opens (`epub` or `pdf`) |
| `mcp.read_only` | `false` | both | Disable the MCP write tools |
| `mcp.max_chars` | `20000` | both | Hard maximum for `read_passage` |
| `web.port` | `8420` | global | Port of `shelf serve` |
| `web.bind` | `127.0.0.1` | global | Address of `shelf serve` and `shelf mcp --http` |
| `web.token` | none | global | Access token for web and MCP over HTTP |
| `default_library` | none | global | Library name used outside any library folder |
| `[[libraries]]` | none | global | Named libraries (`name`, `path`) |

Pattern placeholders: `{author_sort}`, `{author}`, `{title}`, `{year}`, `{series}`, `{series_index}`, `{language}`, `{first_tag}`, `{ext}`. The last path part must end with `.{ext}`.

### Example

`~/.config/shelf/config.toml`:

```toml
default_library = "books"

[[libraries]]
name = "books"
path = "/Users/me/Books"

[[libraries]]
name = "work"
path = "/Users/me/WorkBooks"

[web]
port = 8420
```

`~/Books/.shelf/config.toml`:

```toml
[organize]
pattern = "{author_sort}/{title} ({year})/{title}.{ext}"

[reader]
preferred_format = "pdf"

[mcp]
read_only = true
```

### Environment variables

| Variable | Effect |
|---|---|
| `XDG_CONFIG_HOME` | Base folder of the global config |
| `SHELF_LOG_LEVEL` | Minimum log level on stderr |
| `NO_COLOR` | Turns off colored output |
| `SHELF_TOKEN` | Access token in the Docker setup (`compose.yaml`) |

## Commands

Every command supports `--json`. Commands that work on a library accept `--library <name|folder>`. Exit codes: `0` success, `1` error, `2` usage error. Destructive commands are a dry run unless you pass `--yes` or `--apply`.

| Command | Purpose |
|---|---|
| `shelf init <root>` | Create a library |
| `shelf import <path> [--move] [--dry-run]` | Import files, group formats into works |
| `shelf import --from rclone:<remote>:<path>` | Import from a cloud folder via your own `rclone` |
| `shelf scan` | Incremental scan for new or changed files |
| `shelf reindex [--embeddings]` | Rebuild the index; `--embeddings` only creates missing embeddings |
| `shelf search <query> [--title-only] [--limit n]` | Full-text search |
| `shelf semantic <text> [--chunks] [--hybrid] [--title-only]` | Semantic search; `--hybrid` combines it with full text |
| `shelf similar <work>` | Similar books |
| `shelf model install [<folder>]` | Install the embedding model (download, or offline from a folder) |
| `shelf show <work-id\|path>` | Details of a work |
| `shelf edit <work-id> [--title ...]` | Change metadata |
| `shelf enrich <work-id> [--apply]` | Load metadata and cover online (preview unless `--apply`) |
| `shelf tag add\|rm\|list\|rename\|merge` | Manage tags; `tag add <tag> --query "<search>"` tags in bulk |
| `shelf dupes [--interactive]` | Find and clean up duplicates |
| `shelf merge <a> <b>` / `shelf split <work> <file>` | Fix grouping by hand |
| `shelf add <work> <file> [--replace <file>]` | Add a file to a work, for example a new version |
| `shelf create <file> [--title ...]` | Create a work by hand |
| `shelf move <work> <file> (--to <work>\|--new)` | Move a file to another work |
| `shelf delete <work> [file]` | Move a file or work to the trash |
| `shelf review` | Resolve uncertain assignments |
| `shelf organize [--apply] [--pattern ...]` | Re-sort the library by pattern |
| `shelf undo` | Revert the last organize, tag, dupes or delete run |
| `shelf trash empty [--yes]` | Delete the trash for good |
| `shelf notes` | Notes and highlights |
| `shelf covers` | Cover maintenance |
| `shelf open <work-id> [--format pdf]` | Open with the default app |
| `shelf serve [--port] [--bind] [--token]` | Web UI and REST API |
| `shelf mcp [--http] [--port] [--bind] [--token]` | MCP server (stdio, or Streamable HTTP at `/mcp`) |
| `shelf stats` | Library statistics |
| `shelf completion bash\|zsh\|fish` | Print a shell completion script |

Run `shelf <command> --help` for all options.

### Search syntax

Free text, phrases `"..."`, prefixes `kube*`, negation `-tag:x`, and filters: `author:`, `title:`, `tag:`, `lang:`, `year:>2015`, `format:pdf`, `status:unread`.

```sh
shelf search 'author:kleppmann year:>2015 "stream processing"'
```

### Web UI

`shelf serve` binds to `127.0.0.1:8420`. Any other address needs a token (`--token` or `web.token`). The UI is embedded in the binary; Node.js is not needed at runtime. Keyboard: `/` focuses search, `j`/`k` navigates, `Enter` opens.

### MCP server

Add shelf to your AI assistant as a stdio server (`shelf mcp`). Tools: `search_books`, `semantic_search`, `get_book`, `read_passage`, `similar_books`, `list_tags`, `add_tags`, `remove_tags`, `find_duplicates`, `propose_organization`. Delete and move are never offered. Set `mcp.read_only = true` to disable the write tools.

#### Remote access over HTTP

`shelf mcp --http` serves MCP as Streamable HTTP at `/mcp` instead of stdio. Use it to run shelf on one machine (for example a Raspberry Pi) and connect from another.

On the machine that holds the library:

```sh
shelf mcp --http --bind 0.0.0.0 --port 8421 --token <secret>
```

- Any non-loopback `--bind` needs a token (`--token` or `web.token`). Without one, shelf refuses to start.
- The server is stateless: no session ID, every request stands alone.
- Clients must send `Authorization: Bearer <secret>`.

On the client machine, for example with Claude Code:

```sh
claude mcp add --transport http shelf http://raspberrypi.local:8421/mcp --header "Authorization: Bearer <secret>"
```

Notes:

- The token travels in clear text over plain HTTP. That is fine on a trusted LAN. Beyond that, put a TLS reverse proxy (Caddy, nginx) in front.
- Clients that only speak stdio (such as Claude Desktop) can use a bridge like `mcp-remote`, or start the server over SSH: `ssh raspberrypi shelf mcp`.
- Set `mcp.read_only = true` on the server to switch off `add_tags` and `remove_tags` for remote clients.

## Backup

Back up the whole library folder. That includes the books, the `book.json` and `annotations.json` sidecars, the covers, `.shelf/config.toml`, `.shelf/review.json` and `.shelf/trash/`. The trash holds files you may still want back with `shelf undo`. The sidecars are the source of truth: without them, tags, notes and reading progress are gone.

You can leave out `.shelf/index.db*` (rebuild with `shelf reindex`) and `.shelf/journal/`. If you keep `index.db`, stop `shelf serve` and `shelf mcp` before the backup so the database is consistent.

## Guides

- [Encrypted backup with restic](docs/howto-backup.md): what to back up, what to skip, restore.
- [Importing Humble Bundle e-books](docs/howto-humble-bundle.md): how to get your purchases into shelf.

## FAQ

**Do I lose my books if I stop using shelf?**
No. Books sit in readable folders, metadata in readable `book.json` files. Nothing depends on the tool.

**Can I delete `index.db`?**
Yes. `shelf reindex` rebuilds it from the folders and sidecars. Embeddings are dropped by a reindex; `shelf reindex --embeddings` creates them again.

**shelf grouped two different books into one work. What now?**
`shelf split <work> <file>` undoes it. Uncertain cases land in `shelf review` instead of being merged. You can tune `grouping.*` thresholds.

**A new version of a book arrived (for example a MEAP). How do I update?**
`shelf add <work> <file> --replace <old-file> --carry-notes`. The old file goes to the trash; `shelf undo` brings it back.

**I deleted something by mistake.**
Deleted files go to `.shelf/trash/`. `shelf undo` restores the last run. Only `shelf trash empty --yes` deletes for good.

**Does semantic search need internet?**
Only once, to download the model (`shelf model install`). Offline: `shelf model install <folder>` with the model files. After that, everything runs locally.

**Indexing embeddings takes hours.**
That is expected on thousands of books (CPU only). It runs in the background, can be interrupted, and resumes where it stopped.

**Does shelf send data anywhere?**
No telemetry. Network access happens only for the model download, for `shelf enrich` (ISBN, title and author of one work go to Open Library and Google Books), and for `shelf import --from rclone:...` (done by your own `rclone`).

**Can I make the web UI reachable from my network?**
Yes: `shelf serve --bind 0.0.0.0 --token <secret>`. A token is required for every non-loopback address. Put a TLS reverse proxy in front for anything beyond a trusted LAN.

**Can I use the MCP server from another machine?**
Yes: `shelf mcp --http --bind 0.0.0.0 --token <secret>` on the machine with the library, then connect to `http://<host>:8421/mcp` with a bearer token. See [Remote access over HTTP](#remote-access-over-http).

**Can I use the same library on two machines?**
No. A library belongs to one machine. Sync tools (iCloud, Syncthing) are not supported. Use a backup instead.

**Does shelf remove DRM or convert formats?**
No. Both are non-goals, as are plugins and e-reader sync.

**Scanned PDFs are not searchable.**
shelf does no OCR. Such files are indexed by metadata only.

**Which formats are supported?**
EPUB and PDF are indexed by content. MOBI, AZW3, CBZ, TXT and MD are tracked as files but not indexed.

## Troubleshooting

**`warning: <file>: unknown key '<key>'`**
A key in a config file is not known to shelf: typo, wrong file, or a global-only key in a library config. `web.*`, `default_library` and `[[libraries]]` belong in the global config. Remove or fix the key; shelf keeps running.

**`error:` about a config value at startup**
A known key has the wrong type or an invalid value, for example `reader.preferred_format` other than `epub` or `pdf`. The message names the file and key.

**shelf does not find my library**
shelf looks in the current folder and its parents, then at `--library`, then at `default_library`. Run from inside the library, pass `--library <name|folder>`, or set `default_library` in the global config.

**`shelf serve` or `shelf mcp --http` refuses to start**
A non-loopback `--bind` needs a token. Pass `--token` or set `web.token`. An empty token counts as none. Ports must be between 0 and 65535.

**Semantic search finds nothing**
The model or the embeddings are missing. Run `shelf model install`, then `shelf reindex --embeddings`. Indexing is resumable.

**The index looks wrong or is damaged**
Run `shelf reindex`. The index is rebuilt from folders and sidecars. Then `shelf reindex --embeddings`.

**Docker: the container cannot write to the library**
The library folder must belong to UID 10001 (`chown 10001 <folder>`), or use a named volume.

**Colors garble my output or logs**
Set `NO_COLOR=1`. Colors are off for `--json` and when stdout is not a terminal.

**More log detail**
Set `SHELF_LOG_LEVEL` (for example `Debug`). Logs go to stderr.

## Documentation

- [Product requirements](docs/prd.md)
- [Roadmap](docs/roadmap.md)
- [Architecture decisions](docs/adr/)

## Contributing

Every feature is developed behavior-driven with [Reqnroll](https://reqnroll.net). The rules are mandatory, see [CLAUDE.md](CLAUDE.md):

1. **Feature files first.** Specify the behavior in Gherkin under `tests/Shelf.Specs/Features/` before writing implementation code.
2. **Concrete examples.** Use `Scenario Outline` with `Examples` tables for variations instead of copied scenarios.
3. **English only** in feature files, scenario names, steps and examples.
4. **Traceability.** Tag each feature or scenario with its PRD requirement ID (for example `@F-GRP-1`) and its milestone (for example `@M1`).
5. **Implementation second.** Write step definitions (`tests/Shelf.Specs/Steps/`) alongside the implementation.
6. **Always green.** Run the full BDD suite. A task is not done while any scenario is red.

Unit tests complement the scenarios for internal details (title normalization, SimHash, pattern engine) but do not replace them. Browser scenarios of the web UI use Playwright, which downloads Chromium into its own cache at test time ([ADR 0015](docs/adr/0015-ui-tests-with-playwright.md)).

```sh
dotnet test
```

Requirements are in the [PRD](docs/prd.md), the plan in the [roadmap](docs/roadmap.md). Decisions on dependencies and architecture go into [`docs/adr/`](docs/adr/).

## License

GPL-3.0, see [LICENSE](LICENSE).
