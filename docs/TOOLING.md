# MindVault Tooling Reference

## Projects

| Project | Role |
| --- | --- |
| `src/MindVault.Core` | All behaviour: config, parsing, SQLite index, search, resolver, safe writes, snapshots, validation, doctor, project context, backup |
| `src/MindVault.Cli` | Thin command dispatcher over Core (`CliRunner` is test-driven directly) |
| `src/MindVault.Mcp` | Stdio MCP server over the same Core services |
| `tests/MindVault.Tests` | xUnit suite with a fixture vault copied to a temp dir per test |

## CLI

All commands accept `--vault <path>` (overrides config) and most accept `--json`.
Exit codes: `0` ok · `1` unexpected error or `validate` found errors · `2` known error /
bad usage / missing config · `3` ambiguous note reference.

```
status
init
scan
rebuild-index
validate [--json]
doctor [--json]
detect-project ["<name>"] [--json]
related "<note-ref>" [--limit n] [--json]
search "<query>" [--type t] [--project p] [--tag t] [--status s] [--limit n] [--json]
read "<note-ref>" [--json]
list [--type t] [--project p] [--status s] [--tag t] [--limit n] [--json]
create project "<name>" [--allow-duplicate]
create decision --project "<p>" --title "<t>" [--allow-duplicate]
create task --project "<p>" --title "<t>" [--allow-duplicate]
create thought "<title>" [--content "<text>"]
promote "<note-ref>" --to <decision|memory|task|risk|mistake> [--project "<p>"] [--allow-duplicate]
organize [--project "<p>"] [--apply] [--json]
map create|rebuild --project "<p>" [--json]
map list [--json]
links suggest (--note "<ref>" | --project "<p>") [--limit n] [--json]
links apply --note "<from>" --to "<target>" [--json]
links broken [--json]
links orphans [--json]
frontmatter audit [--project "<p>"] [--json]
aliases audit [--json]
capsule --project "<p>" [--mode m] [--format markdown|json] [--max-chars n]
work-context --project "<p>" (--current-file f | --query q | --note "<ref>") [--limit n]
recall [--project "<p>"] [--since "7 days"|yyyy-MM-dd] [--on-this-day] [--format f]
ops [--json]
pin --note "<ref>"
hide --note "<ref>"
feedback --note "<ref>" --signal useful|noisy|outdated|wrong|clear [--reason r]
mistake add --title "<t>" [--project p] [--lesson l] [--prevention p]
mistake list [--project p] [--all]
mistake resolve "<note-ref>"
inbox add --title "<t>" [--content c] [--project p]
inbox list [--project p]
inbox promote "<ref>" --to <type>
inbox reject "<ref>"
compile [--project "<p>"] [--apply] [--json]
route --project "<p>" [--goal g | --current-file f | --query q] [--format markdown|json]
      [--max-notes n] [--max-chars n] [--max-tokens n]
read-plan --project "<p>" [--goal g | --current-file f] [--max-reads n]
token-audit [--project "<p>"] [--json]
summarize (--project "<p>" | --note "<ref>") [--apply] [--json]
organisation-score [--project "<p>"] [--json]
graph build [--project "<p>"] [--json]
graph relationships --note "<ref>" [--limit n] [--json]
graph explain --from "<ref>" --to "<ref>" [--json]
low-value [--project "<p>"] [--json]
session checkpoint --project "<p>" --summary s [--dry-run]
session handoff --project "<p>" --summary s [--tests t] [--followups f] [--dry-run]
session recent --project "<p>" [--limit n]
append --note "<ref>" --section "<heading>" --content "<text>" [--create-section] [--dry-run]
append --note "<ref>" --section "<heading>" --content-file "<path>"
update-frontmatter --note "<ref>" --key "<key>" --value "<value>" [--dry-run]
link --from "<ref>" --to "<ref>"
archive "<note-ref>" [--dry-run]
restore "<note-ref>" [--snapshot <path>]
backup
prune [--days n]
project-context "<project>" [--limit n]
```

Notes:

- `search` uses FTS5 syntax (`term1 term2`, `"exact phrase"`, `term*`); invalid syntax
  automatically falls back to a quoted phrase search.
- `read`/`append`/`archive` accept any note reference form; ambiguity is an error.
- `update-frontmatter` on `tags` or `links` splits the value on commas into a flat list.
- `project-context` always emits JSON (it exists to mirror `mindvault_get_project_context`).

- `restore` picks the newest snapshot for the note by default; the note's current content is
  snapshotted first, so a restore is itself undoable.
- `prune` retention comes from `snapshotRetentionDays` in the config (default 30) and is only
  ever run explicitly.

## Performance behaviour

- Query commands (`search`, `list`, `read`, …) build the index if missing and auto-refresh it
  incrementally when it is older than `scanStalenessSeconds` (default 60, 0 disables). The
  refresh compares mtime + size only; unchanged files are never re-read.
- The index schema is versioned (`PRAGMA user_version`); after an upgrade that changes the
  schema or FTS tokenizer, the index resets and repopulates transparently on first use.
- FTS uses the `porter unicode61` tokenizer, so word variants match ("searching" → "search").
- Index access is serialised per process, and vault mutations take a write lock — concurrent
  MCP tool calls over HTTP cannot interleave read-modify-write cycles.
- Every MindVault write reindexes just the touched note.
- All SQL is parameterised; FTS results are capped (default 10, max 100).

## Development

```bash
dotnet build          # whole solution
dotnet test           # 96 tests, self-contained (fixture vault in %TEMP%)
dotnet run --project src/MindVault.Cli -- <command> --vault <path>
```

Key extension points:

- New CLI command → add a case in `CliRunner.Run` + a method; keep logic in Core.
- New MCP tool → add a method to `MindVaultTools` with `[McpServerTool]` + `[Description]`;
  never expose raw file/SQL/shell primitives.
- New validation rule → extend `ValidationService.Validate` and document the code in
  `docs/VAULT_SCHEMA.md`.
- Index schema changes → update `IndexDatabase.EnsureSchema`; the index is cache, so users
  just run `rebuild-index` (delete `.mindvault/index.sqlite` if a migration would be needed).
