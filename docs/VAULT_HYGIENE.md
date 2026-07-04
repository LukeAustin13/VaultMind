# Vault Hygiene

How to keep a MindVault vault trustworthy, and what the tooling checks.

## Commands

```bash
mindvault validate            # full check, exit 1 if criticals
mindvault validate --json     # counts + issues, machine-readable
mindvault doctor              # health summary (paths, index, counts, watcher)
mindvault check-note "<ref>"  # per-note quality check
mindvault prune --days 30     # delete old snapshots (explicit only)
mindvault rebuild-index       # always safe: markdown is canonical
```

MCP: `mindvault_validate_vault` returns severity counts plus the top issues (capped).

## What validate checks

**Critical** — breaks correctness or safety:
missing required folders · invalid YAML · nested YAML in managed notes · missing required
frontmatter keys · invalid statuses · duplicate titles (breaks resolution) · references to
nonexistent project notes · vault or snapshot directory not writable.

**Warning** — degrades trust:
missing templates · broken wiki links · notes outside the expected folder structure ·
ambiguous file names · `superseded_by` present but status not `superseded`.

**Info** — worth knowing:
stale open/active tasks (>60 days untouched) · notes over 100 KB (split for retrieval
quality) · active notes linking to archived ones · index was missing and got rebuilt.

## Freshness model

- Query commands auto-refresh incrementally when the index is older than
  `scanStalenessSeconds` (default 60; `0` disables).
- Change detection is mtime+size; set `"verifyContentHash": true` in the config to also
  hash-compare files whose mtime+size match (catches `git checkout`-style restores, at the
  cost of hashing on scan).
- `rescanPending` in `status` means the index schema changed and the next query repopulates.

## Cleanup principles

1. Markdown is canonical — when in doubt, `rebuild-index`, never hand-edit the SQLite file.
2. Archive, never delete: `mindvault archive` snapshots, marks `archived`, moves to
   `99_Archive`. Reversible by design.
3. Snapshots accumulate; prune them consciously (`prune`), not automatically.
4. Fix duplicate titles first — they break note resolution for every other operation.
5. Agents must not bulk-fix: the hygiene skill requires per-note user approval.
