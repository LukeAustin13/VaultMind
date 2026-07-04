# MindVault — Superpower Pass Plan

Goal: sharper, not bigger. Production-grade for a single-owner, local-first, Pi-hosted
AI-agent memory. Written before any code change in this pass; the checklist in §11 is the
contract.

## 1. Current strongest parts

- **Write safety chain**: vault jail (PathGuard) → snapshot-first → atomic temp+move →
  YAML verify-with-rollback → reindex. Two-note supersede rolls back on partial failure.
- **Deterministic ranked retrieval**: title-weighted bm25 + explainable rescoring
  (exact-title, recency, archived/superseded penalties, template exclusion), project-first
  scope with visible fallback.
- **Agent onboarding**: one call (`session start` / context pack) returns goal,
  non-negotiables, decisions in force, tasks, risks, warnings, next reads — refs, not dumps.
- **In-process concurrency**: one reentrant coordination lock (`VaultContext.Sync`) shared
  by scans and writes; single lock-guarded SQLite connection.
- **Guard-tested constraints**: MCP surface pinned by test, skills-safety pinned by test,
  Docker compose binding pinned by test, temp-file leftovers pinned by test.

## 2. Current weakest parts

- **No cross-process write protection.** Two MindVault instances (or CLI + HTTP MCP
  container) writing the same vault interleave freely; last writer wins silently.
- **No error codes.** Errors are prose only; agents and scripts cannot branch on them.
- **No version identity.** No `--version`, MCP reports hardcoded "0.1.0", no changelog.
- **No measured performance.** All performance claims so far are reasoning, not numbers.
- **Diagnostics are thin.** `doctor` doesn't check writability, placeholder paths, Docker
  context, or MCP env config; MCP has no health/diagnostics tools at all.
- **Index verification is missing.** Schema-version reset exists, but nothing detects a
  drifted index (deleted files still indexed, FTS row mismatch) short of a full rebuild.
- **Sync-conflict files are indexed as normal notes**, polluting search after a bad sync.

## 3. Things that look impressive but may be useless

- **`session log` breadcrumbs** — risk of noise exceeding value; keep but do not grow.
- **Decision graph edges across projects** — renders nodes without edges (scope-limited
  resolution); accept and document rather than build a cross-project resolver nobody asked for.
- **`--explain` ranking factors** — only useful if actually used to tune weights; keep, but
  the retrieval evals in this pass are what make ranking claims real.
- **Backlinks in every read** — cheap, occasionally useful; watch that it never grows into
  graph analytics.

## 4. Things that could make agents noisy or dangerous

- Over-creation of tasks/decisions (damped by draft checks, not blocked) — skills must carry
  explicit anti-noise rules with trigger conditions, not vibes.
- `session log` spam — skills say "sparingly"; add explicit "when NOT to log" rules.
- Aggressive archiving — reversible, but skills must require user approval per note.
- Vague skills = model improvisation. Every skill gets exact tool order, "do not" list,
  efficiency and safety rules, enforced by a test.
- MCP diagnostics must never return env vars, tokens, or host paths (leak channel).

## 5. Retrieval failure modes

1. Exact-title query loses to a recently-updated near-match (weights untested) → eval test.
2. Project-scoped query silently misses global canonical note → fallback exists; eval it.
3. Archived/superseded notes outrank live ones → penalties exist; eval them.
4. Vague queries return either nothing (FTS AND) or noise → OR-join exists in packs; eval
   candidate quality for short vague queries.
5. Sync-conflict duplicates split bm25 mass and pollute results → skip patterns (this pass).
6. Stemming false friends (porter) — accepted, documented limitation.
7. Stale index after external edit → staleness TTL exists; `index verify` (this pass) makes
   drift diagnosable.

## 6. Write/snapshot failure modes

1. Crash between archive status-write and file move → archived-in-place, recoverable;
   documented. Torture test asserts snapshot exists before any content change.
2. Second write of supersede fails → rollback from snapshot; torture test simulates.
3. Snapshot dir unwritable → mutation without safety net; validate probes exist (critical);
   torture test asserts snapshot precedes content change ordering.
4. Disk full mid-write → atomic temp+move means target either old or new, never torn.
5. Concurrent cross-process writes → write.lock (this pass).
6. External edit between MindVault read and write (Obsidian open) → lost update; accepted,
   snapshots + docs (SYNC_AND_CONCURRENCY) state the usage model.

## 7. MCP failure modes

1. stdout pollution breaking stdio protocol → logging already stderr-only; keep it that way.
2. Unauthenticated HTTP exposure → token required unless explicitly disabled; unchanged.
3. Diagnostics leaking secrets/paths → new tools return whitelisted compact fields only; test.
4. Tool errors leaking stack traces/host paths → sanitized catch-all exists; add stable codes.
5. Long outputs blowing agent context → body truncation exists; agent evals assert bounds.

## 8. Docker/Pi deployment failure modes

1. Vault mount owned by wrong UID → doctor now reports user/writability; DOCKER.md covers
   `user:` override.
2. `/vault` not mounted → doctor detects Docker and missing /vault (this pass).
3. Placeholder config path used in container → doctor placeholder warning (this pass).
4. Image never actually run on ARM hardware → cannot fix from this machine; CI buildx
   validates build; honest checklist stands.
5. Two containers sharing one vault → write.lock degrades it from corruption-risk to
   clear WRITE_LOCKED errors.

## 9. Performance bottlenecks (suspected — to be measured)

1. Cold scan: parse every note (Markdig + YAML + SHA256) — expected dominant cost.
2. Incremental scan: directory walk + mtime compare — should be milliseconds at 10k notes.
3. Search: FTS query + ≤100-candidate rescore + ≤limit section lookups.
4. Validation: full-table reads + link resolution — O(notes+links).
5. SQLite under WAL on SD-card (Pi) — index size recorded by benchmarks.
The benchmark tool measures all of these at 100/1k/10k notes; real numbers go to
PERFORMANCE_RESULTS.md, or the doc says they were not run.

## 10. Test blind spots (before this pass)

1. No ranking-order assertions (only membership).
2. No agent-behaviour contract tests (pack size bounds, no-dump guarantees, skill sections).
3. Mutation safety spread across files; no single torture suite; rollback ordering not
   asserted (snapshot-before-change).
4. No cross-process lock tests (feature didn't exist).
5. No index-drift tests (deleted-file-still-indexed, FTS mismatch).
6. No config-diagnostics tests (placeholder path, Docker detection).
7. No error-code assertions (codes didn't exist).
8. Obsidian realities untested: alias links, heading links, block refs, spaces in names,
   conflict-file patterns, `.canvas`.

## 11. Checklist for this pass

Plumbing
- [x] `MindVaultVersion` single-source version constant; CLI `--version`/`version`; MCP
      ServerInfo + diagnostics report it; index schema version surfaced.
- [x] Stable error codes on `MindVaultException` hierarchy; codes in CLI JSON errors and
      MCP error payloads; `docs/ERROR_CODES.md`; tests assert key codes.
- [x] Cross-process `.mindvault/write.lock` (short-lived, stale detection, clear
      WRITE_LOCKED failure, reads unaffected, reentrant in-process); config stale window;
      tests; `docs/SYNC_AND_CONCURRENCY.md`.
- [x] Sync-conflict file patterns skipped by scanner + surfaced by validate; `.canvas` and
      non-md ignored (already true — document); tests.
Index
- [x] `index status | verify | rebuild` CLI (rebuild = existing full scan; documented alias);
      verifier checks: count vs disk, missing/extra files, FTS row mismatch, stale mtimes,
      bad paths, schema version; tests.
Diagnostics
- [x] `doctor`/`status` upgraded: config source+file, vault/snapshot writability, placeholder
      path detection, Docker detection, /vault check, MCP env presence (never values),
      user info; `docs/CONFIG_DIAGNOSTICS.md`; tests.
- [x] MCP `mindvault_health` + `mindvault_diagnostics` (compact, no secrets/env/host paths);
      guard tests updated (21 tools); leak test.
Logging
- [x] CLI `--verbose` (timing to stderr) and `--quiet` (suppress info lines); MCP startup
      logs mode + vault existence/writability to stderr; no secrets in logs.
Performance
- [x] `FixtureVaultGenerator` (deterministic, seeded; projects/decisions/tasks/risks/
      architecture/logs/broken links/archived/superseded/duplicate-ish/stale).
- [x] `generate-fixture-vault` CLI command (refuses non-empty target).
- [x] `tools/benchmarks` console project: cold/incremental scan, search, context, pack,
      draft-check, session start/end, validate, index size, memory; `docs/PERFORMANCE.md`
      with targets; run at 100/1k/10k and record in `docs/PERFORMANCE_RESULTS.md`.
Evals
- [x] `tests/.../RetrievalEvals`: 10 ranking/behaviour cases incl. rank-order asserts;
      `docs/RETRIEVAL_EVALS.md`.
- [x] `tests/.../AgentWorkflowEvals`: pack/context/MCP output bounds, unsafe-name hard fail
      (skills + MCP source), skill content contracts, handoff conciseness, reversal
      conditions in decision template; `docs/AGENT_EVALS.md`.
- [x] `MutationTortureTests`: the 15 safety cases in one suite.
Skills
- [x] All 8 skills restructured with required sections (Trigger conditions / Required
      workflow / Do not / Efficiency rules / Safety rules); section-presence test;
      `skills/README.md`; SKILLS_SETUP + AGENT_WORKFLOWS updated.
Docs & hygiene
- [x] `docs/OP_WORKFLOW.md` (practical daily/weekly loop).
- [x] `CHANGELOG.md`, `docs/RELEASE_CHECKLIST.md`.
- [x] `docs/SUPERPOWER_FINAL_AUDIT.md` (brutally honest).
- [x] CI: already present (build+test+buildx) — verify it covers new projects; no secrets.
- [x] Final gate: restore/build/test green; benchmarks run; README/MCP_SETUP tool counts
      updated; report.

## 12. Explicitly out of scope

- Embeddings / semantic search (deterministic retrieval must be proven insufficient first).
- Web dashboard, cloud sync, public hosting, multi-user anything.
- File watcher (scan TTL covers it).
- Cross-process merge/CRDT for concurrent writers — the model is one authoritative writer.
- Rewriting the architecture: the current layering (Core services / thin CLI / thin MCP,
  SQLite as disposable cache, Markdown canonical) survives this audit — every weakness found
  in §2 is additive, none structural.
