# MindVault — Implementation Notes

Working notes and execution checklist for the v0.1 implementation. `PLAN.md` is the
contract; this file records how it was executed and the concrete decisions taken where
the plan left room.

## Execution checklist

### Phase 1 — Foundation
- [x] Solution + projects (`MindVault.Core`, `MindVault.Cli`, `MindVault.Mcp`, `MindVault.Tests`), net10.0
- [x] Packages: Microsoft.Data.Sqlite (+ patched SQLitePCLRaw bundle), Markdig, YamlDotNet, ModelContextProtocol, xUnit
- [x] Config loader with priority: `--vault` > `MINDVAULT_VAULT_PATH` > `config/mindvault.config.local.json`
- [x] `config/mindvault.config.example.json` committed; local config git-ignored
- [x] Vault path validation with clear setup error
- [x] `init` creates required folders + `.mindvault` operational dirs + templates
- [x] `status` command

### Phase 2 — Parsing and indexing
- [x] Frontmatter extraction + flat-YAML parser (YamlDotNet; nested YAML detected and rejected for managed notes)
- [x] Heading extraction (Markdig, code-fence safe, line numbers preserved)
- [x] Wiki-link extraction (`[[Target]]`, `[[Target|alias]]`, `[[Target#section]]`)
- [x] Tag extraction (frontmatter list/scalar + inline `#tags`)
- [x] SQLite schema: `notes`, `note_tags`, `note_links`, `note_headings`, `note_frontmatter`, `fts_notes` (FTS5)
- [x] Incremental `scan` (mtime + size, content hash stored), full `rebuild-index`
- [x] Skip folders: `.obsidian`, `.trash`, `.git`, `node_modules`, `bin`, `obj`, `.mindvault`

### Phase 3 — Querying
- [x] `list` (+ `--type`, `--project`, `--status`, `--tag`, `--limit`, `--json`)
- [x] `read` (resolve + print; `--json`)
- [x] `search` via FTS5 (+ filters, snippet, bm25 rank, safe fallback for invalid FTS syntax)
- [x] Note-ref resolver: exact path > title > slug > wiki-normalized; ambiguity is an error listing candidates
- [x] `validate` and `doctor`

### Phase 4 — Safe writes
- [x] Snapshot service: `.mindvault/snapshots/YYYY-MM-DD/<timestamp>-<safe-name>.md` before every mutation of an existing file
- [x] `create project` / `create decision` / `create task` from templates
- [x] `append` to section (heading-level aware; `--create-section` supported)
- [x] `update-frontmatter` (single flat key; nested values rejected; `updated` maintained)
- [x] `link` (adds `[[target]]` to the `links` frontmatter list)
- [x] `archive` (snapshot → `status: archived` → move to `99_Archive` → reindex)
- [x] `backup` (zip of vault Markdown into `.mindvault/backups`)
- [x] Path traversal / outside-vault writes rejected centrally (`PathGuard`)

### Phase 5 — MCP
- [x] `MindVault.Mcp` stdio server on the official C# MCP SDK
- [x] All 14 required `mindvault_*` tools; no raw file/shell/SQL tools
- [x] `mindvault_get_project_context` returns compact project bundle
- [x] Logging routed to stderr (stdout is reserved for the protocol)

### Phase 6 — Tests and docs
- [x] Fixture vault under `tests/MindVault.Tests/fixtures/SampleVault`
- [x] Tests for config priority, init, parsing, indexing, FTS search, resolution,
      ambiguity, creates, append, frontmatter update, archive, snapshots, traversal
      rejection, rebuild, project context, validate, doctor, CLI entry points
- [x] README + docs/SETUP.md + docs/MCP_SETUP.md + docs/VAULT_SCHEMA.md + docs/TOOLING.md
- [x] `dotnet build` and `dotnet test` pass

### Phase 7 — Claude Code skills pack
- [x] Six focused skills under `/skills`, each with frontmatter (`name`, `description`)
      and a compact workflow: project-context, decision-capture, task-sync,
      implementation-log, review-memory, vault-hygiene
- [x] Skills reference only the 14 safe `mindvault_*` MCP tools — no shell commands,
      no raw filesystem writes, no whole-vault dumps
- [x] Write-skills enforce search-before-create to avoid duplicate notes/tasks/decisions;
      hygiene skill is strictly read-and-recommend unless the user approves a specific fix
- [x] `docs/SKILLS_SETUP.md` (what/prereqs/install into `.claude/skills/`/testing/dedupe)
      and README section linking to it
- [x] review-memory works within the safe tool surface: findings are appended to project
      note sections and escalated via `create_task` / `create_decision` (no raw note-type
      creation tool exists, by design)

### Phase 8 — Docker + MCP HTTP transport
- [x] MCP server gained a second transport: streamable HTTP via `ModelContextProtocol.AspNetCore`
      (`--transport/--host/--port/--auth-token/--no-auth` + `MINDVAULT_MCP_*` env equivalents;
      CLI args win). HTTP refuses to start without a token unless anonymity is explicit;
      auth middleware does a constant-time Bearer comparison; `/healthz` is the only open route.
- [x] Verified live on Windows: 401 without/with wrong token, initialize succeeds with token,
      stdio path regression-tested, no-token startup refusal exits 2 with guidance.
- [x] Multi-stage Dockerfile: SDK 10 build stage pinned to `$BUILDPLATFORM` cross-compiling
      for `$TARGETARCH` (fast Pi builds from a PC), aspnet:10.0 runtime (HTTP transport needs
      ASP.NET Core), publishes MCP + CLI, entrypoint dispatch script generated inside the
      image (immune to Windows CRLF), non-root `USER $APP_UID`, `EXPOSE 7777` only.
- [x] `docker-compose.example.yml`: vault volume → `/vault`, `MINDVAULT_VAULT_PATH=/vault`,
      auth token env, `restart: unless-stopped`, **localhost-only port binding by default**,
      LAN binding + `user:` override documented as explicit opt-ins.
- [x] `.dockerignore` per spec (source/tests/docs/skills/config example stay in context).
- [x] docs/DOCKER.md: Pi + local dev walkthroughs, CLI-through-Docker, MCP-through-Docker,
      buildx single- and multi-platform commands, state table, 8-point security section.
- [x] Docker not installed on the dev machine, so `docker build` could not be run here.
      Verified instead: every dotnet step the Dockerfile executes was emulated locally —
      `restore -a arm64 --os linux`, publish of MCP for linux-arm64 and CLI for linux-amd64
      (`amd64` alias accepted), both outputs carrying the correct native `libe_sqlite3.so`.
- [x] 9 new tests for `McpOptions` (priority, defaults, port validation, token enforcement).

### Phase 9 — Hardening pass (post-review improvements)
- [x] **Concurrency**: all `IndexDatabase` operations serialised behind one lock (the shared
      SQLite connection is no longer exposed to interleaved commands/transactions from
      concurrent HTTP tool calls); `WriteService` mutations additionally take a write lock so
      read-modify-write cycles cannot interleave within a process. Cross-process writes remain
      last-writer-wins (documented; snapshots + `restore` make it recoverable).
- [x] **Freshness**: `EnsureIndex` became `EnsureFresh` — query paths run an incremental
      mtime+size rescan when the index is older than `scanStalenessSeconds` (config, default
      60, 0 disables). A long-running Docker/HTTP server now self-heals after external edits.
- [x] **Schema versioning**: `PRAGMA user_version` (now 2). Version mismatch → tables dropped,
      recreated, and transparently repopulated on the next query (`NeedsRescan` flag).
- [x] **FTS stemming**: `tokenize = 'porter unicode61'` — "searching" now matches "search"
      (part of the schema-version bump, so old indexes migrate automatically).
- [x] **Restore + prune**: `restore "<ref>" [--snapshot <path>]` restores from the newest (or
      chosen) snapshot, snapshotting current content first; explicit snapshot paths must live
      under the snapshot folder. `prune [--days n]` deletes snapshot day-folders past
      `snapshotRetentionDays` (default 30) and only runs explicitly.
- [x] **Template exclusion**: notes under `08_Templates/` no longer resolve by title/stem/slug
      (exact path still works) and can't be matched as projects — placeholder titles like
      "Project Name" can't shadow or collide with real notes. Project lookup moved into SQL
      (`FindProjects`, templates excluded, no in-memory limit).
- [x] **MCP tool annotations**: readOnly/destructive/idempotent/openWorld hints on all 14 tools.
- [x] **Docker**: image `HEALTHCHECK` via `mindvault status`; `MINDVAULT_MCP_AUTH_TOKEN_FILE` /
      `--auth-token-file` for Docker-secrets-style token delivery (explicit token wins).
- [x] **CI**: `.github/workflows/ci.yml` — restore/build/test on ubuntu + buildx build of the
      image for linux/amd64 and linux/arm64 (closes the "no local Docker" verification gap).
- [x] **.gitattributes**: `*.sh` and the fixture vault pinned to LF before the first commit.
- [x] **MCP integration tests**: the real server is launched over stdio from the test suite via
      the official `McpClient`; asserts the exact tool surface and round-trips
      `mindvault_status` + `mindvault_get_project_context`.

### Phase 10 — OP hardening pass (intelligence & product quality)
See `docs/OP_HARDENING_PLAN.md` for the full contract. Landed:
- [x] **Ranked search**: title-weighted bm25 (4x), exact-title x2.0, all-terms-in-title x1.5,
      recency x1.25/x1.1, archived x0.25 (excluded by default), superseded/rejected x0.5,
      templates excluded; `--updated-after/-before`, `--include-archived`, `--explain`
      (per-result factors), matched-section detection, project-first scope with marked
      global fallback. Deterministic, bounded candidate pool.
- [x] **OP project context**: goal + non-negotiables + open questions extracted from the
      project note; active/blocked task split; recent implementation logs; architecture
      notes; recommended next reads with reasons; warnings (stale tasks, superseded-status
      contradictions, duplicate titles, broken project-note links, missing goal);
      `detailLevel` brief/standard/deep.
- [x] **Context packs** (`context-pack` CLI, `mindvault_get_context_pack`): generated
      briefing (never a store) with do-not-forget list and optional task-aware relevance
      (OR-term ranked search, project-scoped). Markdown + JSON, compact by construction.
- [x] **Draft checks** (`check-draft`/`check-note`, `mindvault_check_draft`): exact-dup
      blockers, token-Jaccard near-dup warnings, vague-title detection, missing-project
      blockers, supersede suggestions; create results now carry advisory warnings.
- [x] **Decision graph**: flat `supersedes`/`superseded_by`/`related` relations;
      `decision list` (in-force by default) / `graph` / `supersede` (both notes
      snapshot-first, second-write failure rolls back the first);
      `mindvault_supersede_decision`; `superseded-status-mismatch` validation.
- [x] **Sessions** (`session start|log|end`, `mindvault_start_session`/`_end_session`):
      start returns the pack and ensures `06_Agent_Memory/Log - <Project>.md`; end writes
      one dated handoff block (summary/tests/follow-ups) that feeds back into context.
- [x] **Validation severities** Critical/Warning/Info + new checks: stale tasks, large
      notes, active-links-to-archived, superseded-status-mismatch, vault/snapshot
      writability probes, index-rebuilt info; timing in the report.
- [x] **Templates**: 10 concise templates (project, decision +rejected-alternatives,
      task +context/links/status-notes, risk, constraint, architecture, implementation-log,
      review, prompt, memory).
- [x] **Atomic writes**: temp-file + move for every note write (non-.md suffix so a torn
      temp can never be indexed).
- [x] **Skills**: all six rewritten around packs/draft-checks/sessions/supersede; new
      `mindvault-session-handoff` and `mindvault-architecture-memory`. A guard test proves
      skills reference only the 19 safe tools.
- [x] **Docs**: OP_USAGE, AGENT_WORKFLOWS, CONTEXT_PACKS, DECISION_GRAPH, SESSION_WORKFLOW,
      VAULT_HYGIENE, OP_HARDENING_PLAN, FINAL_SELF_AUDIT; README/SKILLS_SETUP updated.
- [x] **Tests**: 180 total (35 new across ranked search, context/packs, draft checks,
      decision graph, sessions, hardening guards incl. Docker-files sanity and
      MCP-surface/skills consistency).

## Decisions taken where the plan left room

1. **Note title** = first `# H1` if present, else filename stem. Both the title and the
   filename stem are indexed and usable as note refs / wiki-link targets.
2. **File locations for created notes**: projects → `01_Projects/<Name>.md`,
   decisions → `04_Decisions/Decision - <Title>.md`, tasks → `01_Projects/Task - <Title>.md`.
   Documented in `docs/VAULT_SCHEMA.md`; `validate` warns when managed notes live elsewhere.
3. **`create decision` / `create task` require the project note to exist** — this keeps
   `project:` references resolvable and is cheap to satisfy (`create project` first).
4. **Flat YAML** means: top-level mapping whose values are scalars or sequences of
   scalars (`tags`/`links` lists are flat per the plan's own templates). Mappings inside
   values, or sequences containing mappings/sequences, are rejected.
5. **`update-frontmatter` on `tags`/`links`** splits the value on commas into a flat list;
   every other key is written as a single scalar.
6. **Line endings are preserved**: files that use CRLF are written back with CRLF.
7. **FileSystemWatcher is not implemented** (plan: "only if simple and safe" — it is
   neither on a synced vault). `enableWatcher` is parsed, `doctor` reports the watcher
   as disabled, and `scan` is the explicit refresh path.
8. **`backup`** produces `.mindvault/backups/vault-backup-<timestamp>.zip` containing all
   Markdown files (skip-list folders excluded). Snapshots remain the per-mutation safety net.
9. **Index auto-heal**: read/query commands run an incremental scan automatically when the
   index file does not exist yet; they never full-scan when an index is present.
10. **Extra CLI command `project-context`** mirrors `mindvault_get_project_context` so the
    most important MCP tool can be smoke-tested without an MCP client.
11. **Creation of a brand-new file takes no snapshot** (there is nothing to snapshot);
    every mutation of an existing file snapshots first, including archive moves.
12. **SQLitePCLRaw bundle pinned to 3.x** to clear the NU1903 advisory against the 2.1.11
    native lib pulled in transitively by Microsoft.Data.Sqlite.
