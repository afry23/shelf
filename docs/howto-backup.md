# Encrypted backup with restic (Hetzner Storage Box)

[restic](https://restic.net) encrypts on the client (AES-256, authenticated), de-duplicates and keeps snapshots. The target only ever sees encrypted blobs. This guide uses a Hetzner Storage Box over SFTP; any other restic backend (S3, B2, rclone) works the same way with a different repository URL.

## What to back up

Back up the whole library folder: books, sidecars (`book.json`, `annotations.json`), covers, `.shelf/config.toml`, `.shelf/review.json` and `.shelf/trash/`.

Leave out:

- `.shelf/index.db*`: shelf rebuilds it from the sidecars with `shelf reindex`.
- `.shelf/journal/`: short-lived undo data.

`shelf reindex` drops the embeddings; `shelf reindex --embeddings` fills them again (hours on a big library). If that matters to you, do not exclude `index.db*`, but stop `shelf serve` / `shelf mcp` before the backup so the database is consistent.

## 1. Prepare the Storage Box

In the Hetzner console, enable SSH support for the Storage Box. Then create a key and install it (the Storage Box SSH port is 23):

```sh
ssh-keygen -t ed25519 -f ~/.ssh/storagebox_shelf -C "shelf-backup"
cat ~/.ssh/storagebox_shelf.pub | ssh -p23 u123456@u123456.your-storagebox.de install-ssh-key
```

Add it to `~/.ssh/config`, so restic needs no extra options:

```
Host storagebox
    HostName u123456.your-storagebox.de
    User u123456
    Port 23
    IdentityFile ~/.ssh/storagebox_shelf
    ServerAliveInterval 60
```

Check: `sftp storagebox` must log in without a password.

## 2. Password and repository

The password protects all data. **If you lose it, the backup is unreadable.** Store it in a password manager and keep an offline copy.

```sh
install -m 600 /dev/null ~/.config/restic-shelf.pass
printf '%s' 'a-long-random-password' > ~/.config/restic-shelf.pass

export RESTIC_REPOSITORY=sftp:storagebox:shelf-backup
export RESTIC_PASSWORD_FILE=~/.config/restic-shelf.pass

restic init
```

The path `shelf-backup` is relative to the home directory of the Storage Box.

## 3. Back up

Excludes go into a file, e.g. `~/.config/restic-shelf.exclude`:

```
.shelf/index.db*
.shelf/journal
```

```sh
restic backup ~/library --exclude-file ~/.config/restic-shelf.exclude --tag shelf
```

The first run uploads everything. Later runs only upload new or changed files, so adding books is cheap.

## 4. Automate

Script `~/bin/shelf-backup.sh` (`chmod +x`):

```sh
#!/bin/sh
set -eu
export RESTIC_REPOSITORY=sftp:storagebox:shelf-backup
export RESTIC_PASSWORD_FILE="$HOME/.config/restic-shelf.pass"

restic backup "$HOME/library" --exclude-file "$HOME/.config/restic-shelf.exclude" --tag shelf
restic forget --tag shelf --keep-daily 7 --keep-weekly 4 --keep-monthly 12 --prune
restic check --read-data-subset=5%
```

Run it daily:

- **Linux, cron:** `30 2 * * * /home/you/bin/shelf-backup.sh >> /home/you/.local/state/shelf-backup.log 2>&1`
- **macOS, launchd:** a LaunchAgent in `~/Library/LaunchAgents/` with `ProgramArguments` = the script and `StartCalendarInterval` (Hour 2, Minute 30).

For the Docker setup (`compose.yaml`), back up the host folder that is mounted as `/library` (`SHELF_LIBRARY`, default `./library`). Its owner is UID 10001, so run restic with enough rights to read it.

## 5. Check

```sh
restic snapshots              # list snapshots
restic check                  # structure of the repository
restic check --read-data      # also reads all data (slow, downloads everything)
restic stats latest           # size of the last snapshot
```

## 6. Restore

```sh
restic restore latest --target ~/library-restored
shelf reindex --library ~/library-restored
shelf reindex --library ~/library-restored --embeddings   # optional
```

Single files or folders: `restic restore latest --target /tmp/r --include '/Users/you/library/Author'`. Browse a snapshot: `restic mount ~/mnt` (needs FUSE).

**Test a restore** into an empty folder after the first backup and then every few months. A backup that was never restored is only a hope.

## Notes

- Several machines can share one repository, but concurrent writes lock each other; use one repository per machine if in doubt.
- Do not delete the repository folder by hand; use `restic forget --prune`.
- Rotate the password with `restic key add` / `restic key remove`; the data is not re-encrypted.
- The Storage Box is one copy at one provider. For a second copy, `restic copy` to another repository, or keep an offline disk.

Sources: <https://restic.readthedocs.io>, <https://docs.hetzner.com/storage/storage-box/access/access-ssh/>.
