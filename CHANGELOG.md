# Changelog

All notable changes to MindVault. Format: keep-a-changelog-ish; versions are single-source
in `src/MindVault.Core/MindVaultVersion.cs`.

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
