# Sync and Concurrency

MindVault runs against a real Obsidian vault that may be open in Obsidian and synced
between machines. This page states the safe usage model and exactly what protects what.

## The safe usage model

1. **One authoritative AI writer.** The Pi-hosted MindVault (or your single local instance)
   is the only MindVault that writes to a given vault at a time.
2. **Obsidian/manual edits are fine.** Markdown is canonical; MindVault picks up external
   edits on its next scan (automatic within `scanStalenessSeconds`, default 60). If context
   looks stale after big external edits, run `scan` — or `validate` for a full report.
3. **Do not point two MindVault instances at the same vault for writes.** The write lock
   turns that mistake from silent corruption into a clear `WRITE_LOCKED` error, but the
   supported model remains one writer.

## Protection layers

| Threat | Protection |
| --- | --- |
| Two writes interleaving in one process (HTTP MCP handles two tool calls) | One in-process coordination lock serialises all scans and mutations |
| Two MindVault *processes* writing at once | `.mindvault/write.lock`, held per mutation |
| Crash mid-write tearing a note | Atomic temp-file + rename; the target is always the old or the new content |
| A bad write producing invalid YAML | Post-write re-parse; automatic restore from the snapshot |
| Any mutation regretted later | Snapshot-first: every mutation copies the note into `.mindvault/snapshots/` before touching it; `restore` brings it back |
| Sync conflict copies polluting search | Syncthing (`*.sync-conflict-*`) and Dropbox (`* (conflicted copy …)`) patterns are never indexed; `validate` flags them for the human |
| `.obsidian`, `.trash`, `.git`, `.mindvault`, temp files being indexed | Dot-folders are skipped by the scanner; `.mindvault-tmp` files are not `.md`; the same rules gate note *writes* (PathGuard) |
| Index drift after external edits | mtime+size change detection each scan; `index verify` reports drift; `index rebuild` fixes anything |

## The write lock

- **File:** `.mindvault/write.lock` (JSON: pid, machine, createdUtc).
- **Held for:** the duration of one mutation — normally well under a second.
- **Fresh foreign lock:** write commands fail with `WRITE_LOCKED` and a clear message.
  **Reads and searches are never blocked.**
- **Stale lock:** older than `writeLockStaleSeconds` (default 600) means the holder crashed;
  the next writer takes the lock over automatically. A leftover lock can never brick the
  vault.
- **Manual clear:** if you are certain no MindVault instance is running, deleting the file
  is always safe.

## What is NOT protected (accepted trade-offs)

- **Obsidian editing the same note during a MindVault write** — last writer wins. The
  losing version is recoverable: MindVault's write snapshotted the note first, and Obsidian
  keeps its own file-recovery history. In practice: don't have the same note mid-edit in
  Obsidian while an agent rewrites it.
- **Sync engines racing the vault mid-write** — the atomic rename keeps each file
  consistent, but a sync snapshot taken between two related writes (e.g. a supersede pair)
  can propagate a half-updated *pair*. `validate` flags the resulting contradiction
  (`superseded-status-mismatch`).
- **Cross-machine lock enforcement through sync lag** — the lock file syncs like any file,
  with delay. It protects same-filesystem concurrency reliably; across synced machines it
  is best-effort. The real rule is layer 1: one authoritative writer.

## Recommended setup

- Pi hosts MindVault (Docker); Claude Code connects over HTTP with the bearer token.
- Obsidian on your machines syncs the vault (Syncthing/iCloud/whatever you use).
- Weekly (or when in doubt): `mindvault validate` and `mindvault index verify`.
