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
search "<query>" [--type t] [--project p] [--tag t] [--status s] [--limit n] [--json]
read "<note-ref>" [--json]
list [--type t] [--project p] [--status s] [--tag t] [--limit n] [--json]
create project "<name>"
create decision --project "<p>" --title "<t>"
create task --project "<p>" --title "<t>"
append --note "<ref>" --section "<heading>" --content "<text>" [--create-section]
append --note "<ref>" --section "<heading>" --content-file "<path>"
update-frontmatter --note "<ref>" --key "<key>" --value "<value>"
link --from "<ref>" --to "<ref>"
archive "<note-ref>"
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
