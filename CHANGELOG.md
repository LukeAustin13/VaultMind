# Changelog

All notable changes to MindVault. Format: keep-a-changelog-ish; versions are single-source
in `src/MindVault.Core/MindVaultVersion.cs`.

## 0.3.0 — 2026-07-04 (God-Tier Pass: project intelligence layer)

From "vault tools" to a project intelligence layer: repo→project identity, duplicate
refusal in the write path, practical graph queries and a single health verdict.
Review and honest results: `docs/GOD_TIER_REVIEW.md`, `docs/GOD_TIER_RESULTS.md`.

### Added
- **Project aliases + repo binding** — project notes may declare `aliases:` and
  `repoNames:` frontmatter (no index schema change). All project lookups (context, packs,
  sessions, creates, draft checks) resolve through them, and context queries match notes
  filed under an alias.
- **Project detection** — `detect-project` CLI + `mindvault_detect_project` MCP tool:
  deterministic confidence tiers (exact title → alias → repoName → condensed
  separator-insensitive comparison; token overlap only ever *suggests*). Ambiguity returns
  candidates, never a guess. Project context now reports `confidence`/`resolvedVia` and
  warns when it resolved through an alias.
- **Duplicate gate in creates** — `create project/decision/task` REFUSE likely duplicates
  (same name, near-identical same-type title, project name colliding with another
  project's alias) with stable code `DUPLICATE_SUSPECTED` + candidate paths;
  `--allow-duplicate` / `allowDuplicate: true` overrides deliberately. MCP payload:
  `{ created: false, reason: "possible_duplicate", candidates: [...] }`.
- **Related notes** — `related` CLI + `mindvault_find_related` MCP tool: outgoing links,
  backlinks, active same-project memory and same-type similar titles, each with a reason;
  deduplicated, deterministic, bounded.
- **Health verdict** — `doctor` (CLI + JSON) leads with good/warning/critical + reasons;
  `mindvault_health` gained a `verdict` field. Critical = unsafe to mutate (unwritable
  vault/snapshots, placeholder path, schema mismatch); warning = usable but dirty.
- **Dry-run mutations** — `--dry-run` / `dryRun: true` on append, update-frontmatter and
  archive: full validation + a preview of what would change, proven by test to write
  nothing (no file change, no move, no snapshot, no index touch).
- **Docs** — QUICKSTART.md (5-minute second machine), VAULT_SCHEMA alias/repoNames
  section, updated tool tables (23 tools), ERROR_CODES row, GOD_TIER_REVIEW/RESULTS.
- **Tests** — 278 total (was 249): detection tiers, alias context/creates, ambiguity,
  duplicate refusal + override, related-notes determinism, dry-run no-write proofs,
  health verdicts, MCP surface pinned at 23.

### Changed
- `mindvault status`/`doctor`/MCP report v0.3.0.
- Creating a near-duplicate note now fails loudly instead of warning after the fact
  (override available). This is the only intentional behaviour break, and it is the point.
- Fewer junk snapshots: append/update-frontmatter snapshot after validation passes, not
  before (rollback semantics unchanged).

## 0.2.0 — 2026-07-04 (Superpower Pass)

Sharper, not bigger: production-hardening for the local single-owner deployment.

### Added
- **Cross-process write lock** — `.mindvault/write.lock` held per mutation; fresh foreign
  locks fail clearly (`WRITE_LOCKED`), stale locks (default 600 s) are taken over; reads
  are never blocked. Config: `writeLockStaleSeconds`.
- **Stable error codes** on all MindVault errors, surfaced in CLI `--json` (`code`) and MCP
  error payloads. See `docs/ERROR_CODES.md`.
- **Index tooling** — `index status` (schema/size/counts/scan), `index verify` (deleted-
  file-indexed, unindexed files, stale states, FTS mismatch, bad paths, schema mismatch),
  `index rebuild` (alias of `scan --full`).
- **Diagnostics** — `doctor` now probes vault/snapshot writability, detects placeholder
  paths, edited-example-config misuse, Docker (`/vault` mount, container detection),
  reports the running user and MINDVAULT_MCP_* env presence (never token values).
  `status` gained the app version and placeholder warning.
- **MCP diagnostics tools** — `mindvault_health` (fast) and `mindvault_diagnostics`
  (deeper, with validation summary); both secrets-free and host-path-free by test.
  MCP surface is now **21 tools**.
- **Version identity** — `mindvault --version` / `version`, MCP ServerInfo and diagnostics
  report the real version; index schema version surfaced everywhere relevant.
- **Sync-conflict handling** — Syncthing/Dropbox conflict copies are never indexed and are
  reported by `validate` (`sync-conflict-file`).
- **Benchmarks** — `tools/benchmarks` harness + deterministic `FixtureVaultGenerator` +
  `generate-fixture-vault` dev command (refuses non-empty directories). Measured results
  in `docs/PERFORMANCE_RESULTS.md`.
- **Evals** — retrieval evals (ranking-order assertions) and agent-behaviour evals (output
  bounds, skill contracts, unsafe-name hard fails), plus a consolidated mutation-torture
  suite and write-lock/index-drift/Obsidian-compat tests. 249 tests total (was 180).
- **CLI ergonomics** — `--verbose` (timing to stderr), `--quiet` (mutation chatter off,
  results still print).
- **Docs** — ERROR_CODES, SYNC_AND_CONCURRENCY, CONFIG_DIAGNOSTICS, PERFORMANCE,
  PERFORMANCE_RESULTS, RETRIEVAL_EVALS, AGENT_EVALS, OP_WORKFLOW, RELEASE_CHECKLIST,
  SUPERPOWER_PASS_PLAN, SUPERPOWER_FINAL_AUDIT; skills/README.
- **Skills** — all 8 restructured with enforced sections (Trigger conditions / Required
  workflow / Do not / Efficiency rules / Safety rules), tested by `AgentEvalTests`.

### Performance (efficiency pass, same day)
- Scans: parallel parsing + one bulk SQLite transaction per scan + `synchronous=NORMAL`
  (the index is a disposable cache) — cold scan at 10k notes 9.1 s → 1.6 s on desktop,
  and eliminates the one-fsync-per-note pattern that would have crawled on Pi SD cards.
- Queries: filtered lookups rewritten to be sargable (`project`/`type` indexes now used)
  — project context at 10k notes 74.5 ms → 3.7 ms; session start 67.8 ms → 6.4 ms.
- Search: snippets generated only for the returned page, not the 100-candidate pool;
  per-query ranking inputs computed once instead of per candidate.
- I/O: `state.json` reads are mtime-cached (matters for the long-lived MCP server);
  project-context section extraction skips full Markdown parsing; link warnings load one
  note's links instead of the entire link table; validation fetches frontmatter key
  presence in one query. Full before/after: docs/PERFORMANCE_RESULTS.md.

### Changed
- MCP server version string now tracks the real app version (was hardcoded 0.1.0).
- `doctor` output format extended (JSON shape gained fields; existing fields unchanged).
- MCP HTTP 401 body now starts with `MCP_AUTH_FAILED:`.

### Fixed
- Fixture generator small-vault edge: archived tasks are now generated at every size.
- A previously indexed conflict file is dropped from the index on the next scan.

## 0.1.0 — 2026-07-03

Initial implementation: C#/.NET 10 core with SQLite FTS5 ranked search, safe mutation
pipeline (vault jail, snapshot-first, atomic writes, YAML verify/rollback), 25+ CLI
commands, 19 safe MCP tools (stdio + token-protected HTTP), decision graph with supersede,
sessions and context packs, draft checks, three-severity validation, Docker/ARM64 support,
skills pack, 180 tests.
