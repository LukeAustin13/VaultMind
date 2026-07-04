# God-Tier Review — MindVault as a Project Intelligence Layer

Date: 2026-07-04. Reviewed at v0.2.0 (post Superpower + efficiency passes), 249/249 tests
green, benchmarks re-verified before any change (10k notes: cold scan 2.0 s, project
context 5.0 ms, ranked search 12.4 ms).

This is a hard audit of what MindVault is versus what an AI coding agent actually needs
from an external brain. Where something is mediocre, it says so.

## 1. Current architecture summary

- **Markdown files are canonical; SQLite is a disposable cache.** FTS5 (`porter
  unicode61`, title-weighted bm25) plus `notes` / `note_tags` / `note_links` /
  `note_headings` / `note_frontmatter` side tables. Schema versioned via
  `PRAGMA user_version`; mismatch triggers reset + rescan.
- **One service layer (`MindVault.Core`)** consumed by two thin heads: a CLI
  (`MindVault.Cli`, ~27 commands) and an MCP server (`MindVault.Mcp`, 21 tools, stdio +
  token-protected HTTP).
- **Safety pipeline on every mutation:** vault-jail path guard → cross-process write lock →
  snapshot → atomic temp-file write → post-write YAML re-parse with snapshot rollback →
  reindex. Archive instead of delete. Restore is itself snapshotted.
- **Concurrency:** one in-process `Sync` monitor serialises scans/writes;
  `.mindvault/write.lock` serialises across processes; reads are never blocked.
- **Retrieval:** deterministic — FTS candidates, then a rescoring pass (exact-title,
  all-terms-in-title, recency, archived/superseded penalties). No embeddings.
- **Agent surface:** project context / context packs / sessions / draft checks as
  first-class services; 8 skills teach usage discipline.

## 2. What is already strong

- **Mutation safety is genuinely boring** (the good kind): 15-case torture suite, snapshot
  rollback proven, ambiguous refs never mutate, path traversal rejected everywhere.
- **Performance is real and measured** — the efficiency pass numbers are in
  PERFORMANCE_RESULTS.md with honest before/after methodology; scans are parallel and
  single-transaction; queries are sargable.
- **Diagnostics depth:** doctor detects placeholder paths, edited-example-config misuse,
  container mounts, MCP env presence without values; `index verify` catches drift.
- **The context pack concept is right:** compact refs, not note dumps; task-aware
  reordering; do-not-forget list; warnings about stale/contradictory memory.
- **Retrieval + agent evals exist** and assert ranking order and output bounds, not just
  "no exception".

## 3. What is weak (ruthless)

1. **Repo→project identification does not exist.** The killer workflow starts with "a
   Claude session opens in a repo". MindVault matches project notes by exact title/stem
   only. A repo named `mind-vault` cannot find a project note titled `MindVault`. There
   are no aliases, no repo names, no detection tool. This is the single biggest gap.
2. **Project resolution has no confidence signal.** `FindProjects` returns rows or
   nothing; a caller can't distinguish "exact match" from "you guessed". Ambiguity is
   handled (candidates in the error) but near-miss resolution simply fails.
3. **Duplicate prevention is advisory at the wrong moment.** `check_draft` is excellent —
   but it's a *separate tool the agent may skip*. The create paths themselves only block
   on exact file collision; a near-duplicate ("Use SQLite FTS5" vs "Adopt SQLite FTS5")
   sails through and the warning arrives *after* the note exists. Memory spam is the #1
   way agents ruin a vault; the gate must be in the write path.
4. **No related-notes capability.** Backlinks exist internally (`GetBacklinkPaths`) but
   nothing exposes "given this decision, what tasks/risks/reviews touch it?" An agent has
   to burn 3–4 searches to reconstruct what one graph query should return.
5. **No single health verdict.** `doctor`, `validate`, `index verify` and
   `mindvault_health` each report fragments; nothing rolls up to Good/Warning/Critical
   that an agent (or human) can branch on in one call.
6. **Aliases also matter for the `project:` frontmatter field.** Notes whose `project:`
   says `mindvault` are invisible to a context query for `MindVault`'s title/stem pair.
7. **Docs sprawl: 27 files, no 5-minute path.** SETUP.md is thorough but long; a new
   machine needs one page of copy-paste. (Fix by adding exactly one QUICKSTART, not more
   docs.)
8. **No mutation preview.** Snapshots make everything reversible, but an agent can't ask
   "what *would* this do?" before `archive`/`update-frontmatter`/`append` — the three
   most-used mutations.

Minor (noted, not fixed here): `CheckNote` still loads all notes for link resolution
(bounded, cold path); `RecentLogs` ordering depends on dated `####` subheadings.

## 4. Top 10 highest-leverage improvements

| # | Improvement | Serves |
| --- | --- | --- |
| 1 | `aliases:` + `repoNames:` on project notes, honoured by all project lookups | agent usefulness, multi-machine |
| 2 | Project detection with tiered confidence (exact → alias → repo-name → normalized → slug → token-fuzzy), candidates on ambiguity | retrieval quality, safety |
| 3 | `mindvault_detect_project` MCP tool + `detect-project` CLI | agent usefulness |
| 4 | Duplicate gate **inside** create paths (hard stop on high-confidence, `--allow-duplicate` override, candidates in the error) | vault cleanliness |
| 5 | `mindvault_find_related` + `related` CLI: links, backlinks, same-project siblings, shared-token matches, with reasons | retrieval quality |
| 6 | Health verdict rollup (Good/Warning/Critical) in doctor + `mindvault_health` | observability |
| 7 | `--dry-run` on archive / update-frontmatter / append (proven no-write) | safety/reversibility |
| 8 | Alias-aware `project:` scoping in context/pack/list queries | retrieval quality |
| 9 | Retrieval evals for all of the above (alias fetch, detect tiers, duplicate refusal, related ranking) | testability |
| 10 | One QUICKSTART.md (5-minute second machine), tool-table refresh, skills updated to the new tools | dev ergonomics |

All ten are implemented in this pass.

## 5. What will NOT change, and why

- **The architecture.** Files-canonical + disposable SQLite index survived two hard
  passes and delivers ms-level queries at 10k notes. It is not wrong; it stays.
- **No embeddings.** The deterministic layer passes all retrieval evals and the failure
  cases we can name (alias mismatch, repo-name mismatch) are *identity* problems, not
  semantic-similarity problems — solved exactly by #1/#2 above at zero runtime cost.
  Embeddings remain a future option once a deterministic eval provably fails.
- **No web dashboard, no Obsidian plugin.** Out of scope; Obsidian is the UI.
- **No new context-budget service.** Budgeting already exists where it matters (limit
  clamps 1–50, `brief/standard/deep`, 60k body cap, 100-issue cap, snippets only for the
  returned page). A dedicated service would be an abstraction with one caller each —
  exactly the over-engineering this repo avoids.
- **Existing CLI/MCP contracts.** Everything below is additive (new tools, new optional
  params/flags, new fields). Nothing renamed, nothing removed.
- **Snapshot/archive semantics.** Untouched.

## 6. Implementation checklist

- [x] `ErrorCodes.DuplicateSuspected` + ERROR_CODES.md row
- [x] `ProjectDetectService` (tiers, candidates, alias/repoName reads from the existing
      `note_frontmatter` table — zero schema change)
- [x] `IndexDatabase.GetProjectAliasRows()` helper (one query)
- [x] Alias-aware `WriteService.FindProject` + `ProjectContextService.Get` (resolvedVia
      surfaced; ambiguous → candidates, never guess)
- [x] Alias-expanded `projectNames` for context/pack queries
- [x] `RelatedNotesService` + CLI `related` + MCP `mindvault_find_related`
- [x] Duplicate gate in `CreateProject/CreateDecision/CreateTask` (+ alias collision =
      duplicate project), `--allow-duplicate` / `allowDuplicate`
- [x] Health verdict in `DoctorService` + CLI doctor + `mindvault_health`
- [x] `dryRun` on archive / update-frontmatter / append (CLI `--dry-run`, MCP param)
- [x] MCP: `mindvault_detect_project`, `mindvault_find_related` (21 → 23 tools; guard
      tests updated)
- [x] Tests: alias/detect, related, duplicate gate, dry-run-no-write, verdict; all
      existing 249 stay green (278 total)
- [x] Docs: QUICKSTART.md, VAULT_SCHEMA.md (aliases/repoNames), MCP_SETUP tool table,
      ERROR_CODES, README, AGENT_WORKFLOWS, CHANGELOG (0.3.0), skills updates,
      GOD_TIER_RESULTS.md
- [x] Version bump 0.2.0 → 0.3.0

## 7. Risk assessment

| Risk | Mitigation |
| --- | --- |
| Alias resolution creates a *wrong* project match | Tiered detection; only unique high-confidence tiers auto-resolve; token-fuzzy never auto-resolves — it only suggests candidates |
| Duplicate gate blocks a legitimate create | Hard stop only at high confidence (exact-ish title overlap, same type+project); explicit `--allow-duplicate` override; error lists candidates so the agent can update instead |
| New tools bloat the MCP surface | +2 tools only, both read-only, both compact; every other change is an optional param |
| Ranking regressions | Retrieval evals assert order; benchmarks re-run after |
| Dry-run accidentally mutates | Tests assert file bytes + index rows unchanged after dry-run |
| Perf regression from alias lookups | One indexed query per detection; detection runs only when exact match fails |

## 8. Verification plan

1. `dotnet build -c Release` → 0 warnings.
2. `dotnet test -c Release` → all green (249 existing + new).
3. Benchmarks re-run at 1k/10k; project context must stay ≤ ~5 ms at 10k; results
   recorded honestly in PERFORMANCE_RESULTS.md only if they change materially.
4. Manual smoke on a generated fixture vault: `status`, `scan`, `search "decision"`,
   `detect-project`, `related`, `doctor`, `validate`, duplicate-gate round trip.
5. GOD_TIER_RESULTS.md written with what shipped, what didn't, and why.
