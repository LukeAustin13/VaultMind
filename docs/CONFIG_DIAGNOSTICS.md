# Config Diagnostics

Setup problems should diagnose themselves. `mindvault status` is the fast check;
`mindvault doctor` is the full physical.

## Vault path resolution (highest priority wins)

1. `--vault <path>` on the command line
2. `MINDVAULT_VAULT_PATH` environment variable
3. `config/mindvault.config.local.json` (searched upward from the working directory and
   from the executable location)

The example config (`config/mindvault.config.example.json`) is **never** loaded — copy it
to `mindvault.config.local.json` (git-ignored) and edit that.

## `mindvault status` — the fast check

Shows: app version, vault path and which source configured it, a placeholder-path warning,
folder initialization state, index existence/note count/last scan, and whether a rescan is
pending. JSON: `status --json`.

## `mindvault doctor` — the full physical

| Field | Meaning / what to do when it's wrong |
| --- | --- |
| `appVersion` | Build identity; include it in bug reports |
| `vaultPath` + `configSource` | Which vault and which of the three sources chose it |
| `localConfigFound` / `configFilePath` | Whether a local config file is in play; "not found" with env/CLI source is normal |
| `vaultExists` / `vaultWritable` | Write probe results; unwritable = every mutation will fail (fix permissions / Docker `user:`) |
| `indexPath`, `indexExists`, `indexSchemaVersion` vs `expectedSchemaVersion` | Mismatch = the index will auto-reset on next use; `index rebuild` does it now |
| `snapshotPath` / `snapshotWritable` | Unwritable snapshots = mutations lose their safety net (they refuse via probe warnings) |
| `runningInContainer` / `containerVaultMounted` | Docker detection; inside a container without `/vault` mounted = compose volumes are wrong |
| `user` | The account MindVault runs as — the usual suspect in mount-permission problems |
| `mcpEnvironment` | MINDVAULT_MCP_* presence: transport/host/port values, `authTokenSet`/`authTokenFileSet` **booleans only — never the token**, `allowAnonymous` |
| `warnings` | Every detected problem in one list |

### Warnings doctor can raise

- **Placeholder vault path** — the configured path still looks like documentation
  (`C:\Path\To\Your\ObsidianVault`, `changeme`, …). Point it at the real vault.
- **Edited example config** — `mindvault.config.example.json` was customised but no local
  config exists; the example is never loaded, so copy it to
  `config/mindvault.config.local.json`.
- **Vault/snapshot not writable** — permissions or Docker UID mapping.
- **Container without /vault** — fix the compose `volumes:` entry.
- **MCP anonymous enabled** — acceptable only for local development on a trusted machine.

## Cross-machine setup recipe

Same vault on desktop + laptop + Pi:

1. Each machine sets `MINDVAULT_VAULT_PATH` to its local sync copy (or uses a local config).
2. Run `mindvault doctor` once per machine — everything green means paths, permissions and
   index are right for that machine.
3. Only ONE machine (the Pi) runs a writing MindVault instance at a time — see
   [SYNC_AND_CONCURRENCY.md](SYNC_AND_CONCURRENCY.md).

## MCP-side diagnostics

Agents can self-check without shell access: `mindvault_health` (fast: writable, index
fresh, note count, version) and `mindvault_diagnostics` (adds transport, schema versions,
validation summary, warnings). Both are secrets-free and host-path-free by design and by
test.
