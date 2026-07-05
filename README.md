# MindVault

MindVault is a portable, local-first "vault brain" that lets AI agents (Claude Code, Fable,
or any MCP-capable agent) safely read, search, create, update, link, archive and validate an
**Obsidian vault**. Point it at a local vault folder and it gives agents structured, guarded
tools instead of raw filesystem access.

## What MindVault is

- A **CLI** (`mindvault`) for humans and scripts: scan, search, read, create, append, validate,
  organize, promote, map, link-audit, capsule, work-context, recall, ops.
- An **MCP server** exposing 55 safe `mindvault_*` tools over stdio or token-protected HTTP
  (the HTTP mode powers the [Docker / Raspberry Pi deployment](docs/DOCKER.md)) — including
  repo-to-project detection, [context capsules](docs/CONTEXT_CAPSULES.md) (7 modes,
  char-budgeted), [work-context](docs/WORK_CONTEXT.md) for the file you are editing,
  time-window recall, a session lifecycle
  (start/checkpoint/handoff/recent), a [mistake ledger](docs/MISTAKE_LEDGER.md) whose
  lessons surface as do-not-repeat rules, deterministic
  [feedback signals](docs/FEEDBACK_SIGNALS.md), a
  [content risk scanner](docs/SAFETY_SCANNER.md) that refuses secrets, the
  [organisation layer](docs/ORGANISATION.md) (placement proposals, thought promotion,
  generated project map blocks, link intelligence, audits) and a one-call brain-ops rollup.
- A **SQLite FTS5 index** that is pure, rebuildable cache — your Markdown files stay canonical.
- A **safety layer**: every mutation snapshots the note first, writes are confined to the vault,
  archive replaces delete, and YAML frontmatter is validated after every write.

## What MindVault is not

- Not an Obsidian plugin (Obsidian stays the human UI; MindVault works with Obsidian closed).
- Not a web dashboard, cloud service, or multi-user system.
- Not a vector database — search is SQLite FTS5, no embeddings in v0.1.
- Not a generic filesystem bridge: there is no `write_file`, `delete_file`, `run_shell` or raw
  SQL tool, and there never will be one in the MCP surface.

## Requirements

- .NET 10 SDK
- A local Obsidian vault folder (or any folder of Markdown notes)

## Setup

```bash
git clone <repo>
cd MindVault
copy config\mindvault.config.example.json config\mindvault.config.local.json
# edit config\mindvault.config.local.json and set "vaultPath" to your vault
dotnet restore
dotnet build
dotnet run --project src/MindVault.Cli -- status
dotnet run --project src/MindVault.Cli -- init
dotnet run --project src/MindVault.Cli -- scan
dotnet run --project src/MindVault.Cli -- search "test"
```

### Multi-machine setup

Clone the repo on each machine and repeat **only** the config step, pointing `vaultPath` at
that machine's local copy of the (synced) vault:

```json
"vaultPath": "D:\\Obsidian\\VaultName"
```

`config/mindvault.config.local.json` is git-ignored, so every machine keeps its own path.
Nothing else is machine-specific.

## Configuration rules

Vault path resolution, highest priority first:

1. CLI argument `--vault "C:\Path\To\Vault"`
2. Environment variable `MINDVAULT_VAULT_PATH`
3. `config/mindvault.config.local.json` (searched upward from the working directory)

The committed `config/mindvault.config.example.json` is documentation only — it is **never**
loaded. If no vault path is configured, commands fail with a setup message that tells you
exactly what to do.

Other settings (`indexPath`, `snapshotPath`, `defaultArchiveFolder`) are relative to the vault
root and rarely need changing.

## CLI usage

```bash
dotnet run --project src/MindVault.Cli -- <command>
```

| Command | Purpose |
| --- | --- |
| `status` | Show version, config source, vault path, index state |
| `version` / `--version` | Print the app + index schema version |
| `init` | Create the required folders and templates |
| `scan` | Incrementally index changed Markdown notes |
| `rebuild-index` | Clear and rebuild the SQLite index |
| `index status\|verify\|rebuild` | Index health report, drift detection, rebuild |
| `search "<query>" [--type --project --tag --status --limit]` | FTS5 full-text search |
| `read "<note-ref>"` | Print a note (ref = path, title, filename or `[[link]]`) |
| `list [--type --project --status --tag --limit]` | List notes, most recently updated first |
| `detect-project ["<name>"]` | Map a repo/folder name to a vault project via aliases + repoNames |
| `related "<note-ref>"` | Links, backlinks and related notes, each with a reason |
| `create project "<name>"` | New project note in `01_Projects` (refuses likely duplicates; `--allow-duplicate` overrides) |
| `create decision --project "<p>" --title "<t>"` | New decision linked to a project (aliases resolve) |
| `create task --project "<p>" --title "<t>"` | New task linked to a project (aliases resolve) |
| `append --note "<ref>" --section "<heading>" --content "<text>"` | Append under a heading (`--content-file` and `--create-section` supported) |
| `update-frontmatter --note "<ref>" --key "<k>" --value "<v>"` | Set one flat frontmatter key |
| `link --from "<ref>" --to "<ref>"` | Add a `[[wiki link]]` to the source note's links |
| `archive "<note-ref>" [--dry-run]` | Snapshot, mark `archived`, move to `99_Archive` (`--dry-run` previews; also on `append`/`update-frontmatter`) |
| `restore "<note-ref>" [--snapshot <path>]` | Restore a note from its newest (or a chosen) snapshot; the current content is snapshotted first |
| `backup` | Zip all vault Markdown into `.mindvault/backups` |
| `prune [--days n]` | Delete snapshots older than the retention window (default 30 days) |
| `context "<project>" [--brief\|--deep]` | Rich project context: goal, tasks, decisions, warnings |
| `context-pack "<project>" [--task "<t>"]` | Compact agent briefing (markdown or `--output json`) |
| `check-draft --type t --project p --title "<t>"` | Quality-check a note idea before creating it |
| `check-note "<note-ref>"` | Quality-check an existing note |
| `decision list\|graph\|supersede` | Decision lifecycle: active list, relations graph, safe supersede |
| `session start\|log\|end` | Session briefing pack and concise handoff entries |

| `validate` | Severity-graded vault problems (exit 1 on criticals) |
| `doctor` | Health verdict (good/warning/critical) + full environment diagnosis |
| `project-context "<project>"` | Compact JSON project bundle (same as the MCP tool) |
| `generate-fixture-vault --path <new-dir>` | Dev/test: synthetic vault for benchmarks/evals |

Search is ranked (title-weighted bm25, recency boost, archived deprioritised) with
`--explain` to see why results matched, date filters, and project-first scoping.
See [docs/USAGE.md](docs/USAGE.md) for the full command tour, and
[docs/CONTEXT_PACKS.md](docs/CONTEXT_PACKS.md) / [docs/DECISION_GRAPH.md](docs/DECISION_GRAPH.md) /
[docs/SESSION_WORKFLOW.md](docs/SESSION_WORKFLOW.md) / [docs/VAULT_HYGIENE.md](docs/VAULT_HYGIENE.md)
for the concepts. Agent-side guidance lives in [docs/AGENT_WORKFLOWS.md](docs/AGENT_WORKFLOWS.md);
the practical day-to-day loop is [docs/WORKFLOW.md](docs/WORKFLOW.md).

Most commands accept `--json` for machine-readable output (failures carry a stable `code` —
see [docs/ERROR_CODES.md](docs/ERROR_CODES.md)), `--vault <path>` to override the configured
vault, plus `--verbose` (timing to stderr) and `--quiet` (suppress mutation chatter).

Note references never guess: if `"Duplicate Note"` matches two files you get the candidate
list and exit code 3 — use the exact path instead.

## Docker / Raspberry Pi

MindVault runs as a container on any Linux/ARM64 or x64 Docker host — including a Raspberry
Pi — with the Obsidian vault mounted at `/vault` and the MCP server exposed as a LAN-only,
token-protected HTTP endpoint (localhost binding by default). The same image runs CLI
commands: `docker compose run --rm mindvault status`. Full guide, Pi walkthrough, buildx
instructions and the security model: [docs/DOCKER.md](docs/DOCKER.md). Updating a deployed
Pi safely (pull never touches your vault, config or token): `./scripts/pi-update.sh` —
see [docs/UPDATING.md](docs/UPDATING.md).

## MCP usage

See [docs/MCP_SETUP.md](docs/MCP_SETUP.md). The server speaks **stdio** (default) and
**HTTP** (`--transport http`, token-protected; used by the Docker setup). Short version —
register the server with your MCP client using stdio:

```json
{
  "mcpServers": {
    "mindvault": {
      "command": "dotnet",
      "args": [
        "run", "--project", "C:/path/to/MindVault/src/MindVault.Mcp", "--"
      ],
      "env": { "MINDVAULT_VAULT_PATH": "C:/path/to/YourVault" }
    }
  }
}
```

Tools exposed (55): `mindvault_status`, `mindvault_search`, `mindvault_read_note`,
`mindvault_list_notes`, `mindvault_create_project`, `mindvault_create_decision`,
`mindvault_create_task`, `mindvault_append_to_note`, `mindvault_update_frontmatter`,
`mindvault_link_notes`, `mindvault_archive_note`, `mindvault_validate_vault`,
`mindvault_get_project_context`, `mindvault_rebuild_index`, `mindvault_get_context_pack`,
`mindvault_check_draft`, `mindvault_supersede_decision`, `mindvault_start_session`,
`mindvault_end_session`, `mindvault_health`, `mindvault_diagnostics`,
`mindvault_detect_project`, `mindvault_find_related`, `mindvault_capture_thought`,
`mindvault_promote_note`, `mindvault_organize_vault`, `mindvault_create_map`,
`mindvault_rebuild_map`, `mindvault_list_maps`, `mindvault_suggest_links`,
`mindvault_find_broken_links`, `mindvault_find_orphans`, `mindvault_audit_frontmatter`,
`mindvault_audit_aliases`, `mindvault_build_context_capsule`, `mindvault_get_work_context`,
`mindvault_recall`, `mindvault_record_feedback`, `mindvault_brain_ops`,
`mindvault_checkpoint_session`, `mindvault_recent_sessions`, `mindvault_list_inbox`,
`mindvault_add_mistake`, `mindvault_list_mistakes`, `mindvault_resolve_mistake`,
`mindvault_get_project_map`, `mindvault_build_route_card`, `mindvault_build_read_plan`,
`mindvault_token_audit`, `mindvault_generate_summaries`, `mindvault_organisation_score`,
`mindvault_build_graph`, `mindvault_explain_relationships`,
`mindvault_find_low_value_notes`, `mindvault_compile_brain`.

`mindvault_start_session` is the one to reach for first: one call returns the full context
pack (goal, non-negotiables, task-relevant notes, decisions in force, tasks, risks,
warnings, recommended next reads) and sets up the session log for the handoff at the end
(`mindvault_end_session`). See [docs/AGENT_WORKFLOWS.md](docs/AGENT_WORKFLOWS.md).

## Skills pack (Claude Code workflows)

[`/skills`](skills) is a portable pack of thirteen Claude Code skills — workflow
instructions that teach Claude Code when and how to use the MindVault MCP tools:
session-bracketed work (brief in, hand off out), route cards and read plans before any
broad search (read less, stop sooner), context packs before coding, draft checks before
creating notes, decision capture with proper supersede lifecycle, task sync, review
memory, architecture memory, vault hygiene and safe organisation (dry-run placement,
thought promotion, hub map blocks, link repair). Drop the folders into any project's `.claude/skills/` once the
MCP server is configured. The skills reference only the safe `mindvault_*` tools — no shell,
no raw file writes (enforced by a test). Setup, install and per-skill test instructions:
[docs/SKILLS_SETUP.md](docs/SKILLS_SETUP.md).

## Safety model

1. **Markdown is canonical.** SQLite is rebuildable cache; `rebuild-index` regenerates it.
2. **Snapshots before every mutation** to `.mindvault/snapshots/YYYY-MM-DD/<timestamp>-<name>.md`.
3. **No hard delete.** `archive` snapshots, sets `status: archived` and moves to `99_Archive`.
4. **Vault jail.** All paths are resolved and verified inside the vault; traversal is rejected,
   and writes into `.mindvault`/`.obsidian` are blocked.
5. **Flat YAML only.** Nested frontmatter is rejected in managed notes; writes are re-parsed
   after saving and rolled back from the snapshot if the YAML would be broken.
6. **No dangerous MCP tools.** No raw file, shell or SQL access is exposed to agents.

## Troubleshooting

- *"No vault path configured"* — copy the example config to
  `config/mindvault.config.local.json` and set `vaultPath`, or pass `--vault`.
- *Search returns nothing* — run `scan` (or `rebuild-index`); the index is only updated
  explicitly or after MindVault's own writes.
- *"Note reference is ambiguous"* — two notes share a title; use the relative path shown in
  the candidates list.
- *Index seems wrong after heavy external edits* — `rebuild-index` is always safe; the files
  are the source of truth.
- *MCP server exits immediately* — it prints the setup error to stderr; set
  `MINDVAULT_VAULT_PATH` in the MCP config `env` block (the server usually does not inherit
  your shell working directory).

## Known limitations

- No file watcher. External edits are picked up by `scan`, by any MindVault write, or
  automatically by query commands once the index is older than `scanStalenessSeconds`
  (default 60; the auto-refresh is incremental — only changed files are re-parsed).
- `update-frontmatter` handles one key per call; `tags`/`links` values are comma-split lists.
- Reformatting: structural frontmatter edits rewrite the YAML block (key order is preserved,
  comments inside frontmatter are not).
- Inline code spans are not masked during tag/link extraction (fenced code blocks are).
- Search is keyword FTS (with porter stemming and ranked rescoring), not semantic.
- Concurrent writes: serialised within one process, and guarded across processes by
  `.mindvault/write.lock` (fresh foreign locks fail clearly; stale ones are taken over).
  A simultaneous Obsidian edit of the same note remains last-writer-wins; snapshots make
  that recoverable. See [docs/SYNC_AND_CONCURRENCY.md](docs/SYNC_AND_CONCURRENCY.md).
- `.canvas` files are ignored (not indexed); sync-conflict copies are ignored and flagged
  by `validate`.

## Repository layout

```
src/MindVault.Core   core services: config, parsing, index, search, safe writes, validation
src/MindVault.Cli    the mindvault CLI
src/MindVault.Mcp    MCP server, stdio + token-protected HTTP (official C# MCP SDK)
skills/              portable Claude Code skills pack (see docs/SKILLS_SETUP.md)
tests/MindVault.Tests xUnit suite + fixture vault
docs/                setup, MCP, Docker, skills, schema and tooling reference
config/              example config (copy to *.local.json, which is git-ignored)
Dockerfile           multi-stage, multi-arch (amd64/arm64) container build
docker-compose.example.yml  LAN-safe compose template (see docs/DOCKER.md)
```
