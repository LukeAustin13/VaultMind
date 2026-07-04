# Changelog

All notable changes to MindVault. Format: keep-a-changelog-ish; versions are single-source
in `src/MindVault.Core/MindVaultVersion.cs`.

## 0.6.0 — 2026-07-05 (Organisation Intelligence: the brain routes attention)

Organisation becomes a token-compression and navigation layer: the vault now tells agents
what to read first, what to skip, what it costs, and how well organised it is — with
numbers and reasons. Audit: `docs/ORGANISATION_INTELLIGENCE_AUDIT.md`.

### Added
- **Route cards** — `route` CLI + `mindvault_build_route_card`: read-first (≤5, with
  reasons, token estimates and summary snippets), read-if-needed, explicit do-not-read
  with reasons, constraints/decisions/mistakes/risks/tasks in force, suggested next tool
  calls, token budget + estimated savings vs reading every candidate. Ambiguity returns
  candidates, never a guess.
- **Read plans** — `read-plan` CLI + `mindvault_build_read_plan`: strict ordered plan
  (max 5 reads, maps/hubs first), expectedUse per step, explicit stop conditions, one
  sanctioned narrowed-search fallback.
- **Generated summaries** — `summarize` CLI + `mindvault_generate_summaries`:
  deterministic extractive blocks (`mindvault-summary` markers) with summary/agentUse/
  keyPoints/needsReview; dry-run default, snapshot-first apply, block-splice only,
  idempotent. Route snippets and the capsule goal fallback consume them.
- **Typed relationship graph** — `graph build|relationships|explain` CLI +
  `mindvault_build_graph`/`mindvault_explain_relationships`: explicit wiki links typed by
  endpoint types (task_tracks_decision, mistake_prevented_by, risk_mitigated_by,
  supersedes, caused_by, implements, review_finding_for, blocks, duplicates, …) with
  reasons + confidence; `.mindvault/link-graph.jsonl` sidecar; two-hop explanations
  computed live.
- **Low-value detection** — `low-value` CLI + `mindvault_find_low_value_notes`: the
  do-not-read list (archived/superseded/rejected/hidden/negative-feedback/thoughts/
  orphans/stale logs/missing project/large-without-summary), reasons on every row.
- **Token accounting** — `TokenEstimator` (ceil(chars/4)) + `ContextBudget`;
  `token-audit` CLI + `mindvault_token_audit`: totals by tier, largest notes,
  unsummarized large notes, split candidates, capsule-vs-route cost, waste + fixes.
- **Organisation score** — `organisation-score` CLI + `mindvault_organisation_score`:
  11 explainable categories with evidence, strengths/weaknesses, recommended fixes,
  estimated token waste and savings-if-fixed.
- **Organisation compiler** — `compile` CLI + `mindvault_compile_brain`: maps +
  summaries + graph + health + score in one pass; dry-run by default, snapshot-first on
  apply, never moves notes.
- **Map v2** — rebuild now emits Start Here, agent-route pointer, Non-Negotiables,
  Do Not Repeat (prevention rules), Work Areas, Recent Sessions, Needs Review, Orphans,
  Broken Links, Large Notes Missing Summaries and an Organisation Score line — same
  markers, human text still preserved verbatim. `map rebuild --v2` is accepted (v2 is
  the only format).
- **Skills** — new `mindvault-route-card` and `mindvault-read-plan`; project-context/
  work-context/session-handoff/organisation/vault-hygiene now teach route-before-search,
  scoped reads and stop conditions. Pack is 13 skills.
- **Tests** — 341 total (was 322): TokenEfficientVault fixture + the 15 organisation
  evals (route bounds, do-not-read, stop conditions, budget enforcement, summary/map
  human-text preservation, typed-graph explanations, hidden/useful feedback flow, skills
  stop-guidance) + compile-dry-run, section-read and search-caution guards.

### Changed
- MCP surface 45 → **55 tools** (all additive; nothing renamed or removed).
- `mindvault_read_note` gained optional `section` and `maxChars` (the 60 KB full-body
  read is now opt-in, not the only option).
- Search results gained a null-omitted `caution` field for feedback-hidden/negative
  notes — annotation only; FTS ranking is untouched and hidden notes remain findable.
- Capsules fall back to the hub's generated summary line when the hub has no Goal
  section (clearly labelled as such).
- Version 0.5.0 → 0.6.0.

## 0.5.0 — 2026-07-04 (Superpower Brain: a Memory OS for coding agents)

MindVault becomes a session-aware brain: mode-shaped context capsules, work-context for
the file in front of you, time-window recall, deterministic feedback signals, a content
gate that refuses secrets, a first-class mistake ledger and a one-call ops rollup.
Audit and honest results: `docs/SUPERPOWER_BRAIN_AUDIT.md`, `docs/SUPERPOWER_BRAIN_RESULTS.md`.

### Added
- **Context capsules** — `capsule` CLI + `mindvault_build_context_capsule`: 7 modes
  (coding/debugging/review/planning/handoff/release/architecture), hard char budget with
  mode-priority trimming, source paths on everything, do-not-repeat rules from the
  mistake ledger, superseded-decision warnings, candidates (never a guess) on ambiguity.
- **Work-context** — `work-context` CLI + `mindvault_get_work_context`: memory related to
  a source file (token-matched), a query, or a note; reasons on every result;
  archived/superseded/hidden never appear.
- **Recall** — `recall` CLI + `mindvault_recall`: changes since '7 days'/a date, or
  on-this-day anniversaries, grouped by type; frontmatter dates first, index mtime
  fallback; archived excluded and counted honestly.
- **Feedback signals** — `pin`/`hide`/`feedback` CLI + `mindvault_record_feedback`:
  pinned/hidden/useful/noisy/outdated/wrong/clear in an append-only sidecar
  (`.mindvault/feedback.jsonl`, stem-keyed so moves don't orphan it). Shapes capsules,
  work-context, related notes and link suggestions; the FTS hot path stays pure.
- **Content risk scanner** — blocks private keys / AWS / GitHub / sk- / bearer tokens
  (`RISKY_CONTENT`, override `--allow-risky-content`/`allowRiskyContent`) and warns on
  prompt-injection language across append, frontmatter, thought capture, mistakes and
  session writes. Evidence is redacted — matched values never appear anywhere.
- **Mistake ledger verbs** — `mistake add|list|resolve` CLI + `mindvault_add_mistake`/
  `mindvault_list_mistakes`/`mindvault_resolve_mistake`; lessons carry prevention rules
  that capsules surface until deliberately resolved.
- **Inbox verbs** — `inbox add|list|promote|reject` CLI + `mindvault_list_inbox`
  (add/promote/reject map to the existing capture/promote/archive tools by design).
- **Session lifecycle** — `session checkpoint|handoff` aliases with `--dry-run`,
  `session recent`, + `mindvault_checkpoint_session` and `mindvault_recent_sessions`.
- **Brain ops** — `ops` CLI + `mindvault_brain_ops`: health verdict, managed/inbox/orphan/
  broken/duplicate/collision counts, archived ratio, feedback volume, latest session,
  pinned MCP tool count and recommended fixes. Counts only, no content.
- **Skills** — new `mindvault-work-context` and `mindvault-mistake-ledger`; session-handoff
  and project-context updated for capsules/recall/checkpoints. Pack is 11 skills.
- **Tests** — 322 total (was 302): EliteBrainVault fixture + capsule
  inclusion/exclusion/budget/ambiguity, work-context by file, recall windows, feedback
  boost/hide/clear, scanner block-without-leaking, handoff-in-recall, session recent,
  mistake and inbox round trips, ops counts; tool-count constant pinned by guard test.

### Changed
- MCP surface 34 → **45 tools** (all additive; nothing renamed or removed). Duplicates
  the spec listed were deliberately NOT added: `write_handoff`=`end_session`,
  `inbox_add`=`capture_thought`, `inbox_promote`=`promote_note`,
  `inbox_reject`=`archive_note`, `apply_link`=`link_notes`.
- `WriteResult` gained `riskWarnings`; append/update-frontmatter/capture/end-session
  tools accept `allowRiskyContent` (additive parameters).
- Capsule do-not-repeat prefers the mistake's Prevention Task over its lesson text.
- **Rejected**: semantic-rerank stub interface (dead code until an eval proves a semantic
  gap), auto-decaying feedback, vault chat/dashboard/cloud/plugin (as ever).
- Version 0.4.0 → 0.5.0.

## 0.4.0 — 2026-07-04 (Organisation & Linking: the vault organises itself)

MindVault stops being retrieval-only: it now knows where notes belong, keeps thoughts
distinct from durable memory, generates navigation maps, suggests meaningful links and
audits its own hygiene — all dry-run/proposal-first and snapshot-backed.
Details: `docs/ORGANISATION.md`, `docs/THOUGHTS_AND_MEMORY.md`, `docs/LINKING.md`,
`docs/MAPS.md`; honest results in `docs/TOP_0_1_BRAIN_RESULTS.md`.

### Added
- **Organisation engine** — `organize [--project] [--apply]` CLI +
  `mindvault_organize_vault`: deterministic placement proposals with reasons and
  confidence, dry-run by default, snapshot-first atomic moves on apply, needs-review for
  anything uncertain (broken YAML, unresolvable project, untyped notes in managed
  folders, destination collisions). Placement rules live in `PlacementPolicy`
  (per-project subfolders deliberately OFF — documented in ORGANISATION.md).
- **Thought vs memory model** — new managed types `thought` and `mistake` (+ templates).
  `create thought` CLI and `mindvault_capture_thought` (agent inbox
  `06_Agent_Memory/Inbox`); `promote --to decision|memory|task|risk|mistake` CLI +
  `mindvault_promote_note`: validates, resolves the project through detection (never
  guesses), runs the duplicate gate, preserves body content byte-for-byte, keeps the file
  name, retitles the H1 only when no backlinks would break, moves to placement and
  suggests missing sections.
- **Map notes** — new `09_Maps` folder; `map create|rebuild|list` CLI +
  `mindvault_create_map`/`mindvault_rebuild_map`/`mindvault_list_maps`. Generated block
  between `<!-- mindvault-generated:start/end -->` markers; human text outside markers is
  preserved verbatim on rebuild (proven by test).
- **Link intelligence** — `links suggest|apply|broken|orphans` CLI +
  `mindvault_suggest_links`/`mindvault_find_broken_links`/`mindvault_find_orphans`:
  reason-tagged, score-thresholded suggestions (type relationships, shared tags/tokens,
  body mentions; single weak signals are dropped), broken-target detection and orphan
  detection (thoughts excluded by design). Apply = the existing `mindvault_link_notes`
  (dedup + snapshot); a duplicate `mindvault_apply_link` tool was deliberately not added.
- **Audits** — `frontmatter audit [--project]` and `aliases audit` CLI +
  `mindvault_audit_frontmatter`/`mindvault_audit_aliases`: findings with a proposed fix
  each (missing/invalid keys, unresolvable or alias-shaped `project:` values, notes not
  linked to their hub, duplicate/redundant aliases, cross-project alias collisions incl.
  condensed-form collisions). Read-only; nothing auto-fixes.
- **Skills** — new `mindvault-organisation` (dry-run first, no bulk moves, no
  auto-promotion, maps for navigation); six existing skills updated to route through
  suggest-links, maps, thought capture and the audits. Pack is now 9 skills.
- **Tests** — 301 total (was 278): new `OrganisationVault` fixture plus organize/promote/
  map/link/audit evals (dry-run mutates nothing; apply snapshots; ambiguity goes to
  needs-review; map rebuild preserves human text; suggestions carry the decision-to-task
  reason and exclude archived; orphan/broken detection; alias collision; nested-YAML
  audit; link-apply dedup; no deep folder chaos). MCP surface pinned at 34 tools.

### Changed
- MCP surface 23 → **34 tools** (all additive; nothing renamed or removed).
- `validate`'s `missing-project-note` now accepts declared aliases/repoNames as valid
  `project:` values (they already resolved everywhere else — this was an alias-feature
  gap, not a behaviour break).
- `init` now also creates `09_Maps`, `06_Agent_Memory/Inbox` and the Thought/Mistake
  templates. **Run `mindvault init` once after upgrading** — until then `validate`
  reports the missing `09_Maps` folder.
- Version 0.3.0 → 0.4.0.

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
