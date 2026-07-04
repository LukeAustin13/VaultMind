# God-Tier Pass — Results (v0.3.0)

Date: 2026-07-04. Companion to [GOD_TIER_REVIEW.md](GOD_TIER_REVIEW.md) (the audit and
plan). This file records what actually shipped, what deliberately did not, and the proof.

## 1. What was improved and why it matters

| Improvement | Why it matters for agentic work |
| --- | --- |
| **Project aliases + repoNames** (`aliases:`/`repoNames:` on project notes) | The killer workflow starts in a repo. Before: a repo named `mind-vault` could not find project "MindVault" — the #1 identity gap. Now every project lookup resolves through aliases. |
| **Project detection with confidence** (`detect-project`, `mindvault_detect_project`) | Deterministic tiers: exact title → alias → repoName → condensed (`mind_vault` = `mind-vault` = `MindVault`). Fuzzy token overlap only *suggests*. Ambiguity returns candidates — never a guess. |
| **Duplicate gate in the write path** | `check_draft` was advisory and skippable. Now the creates themselves refuse likely duplicates (`DUPLICATE_SUSPECTED` + candidates), incl. project names colliding with another project's alias. Override: `--allow-duplicate` / `allowDuplicate: true`. Memory spam is how vaults rot; the gate is where it must be. |
| **Related notes** (`related`, `mindvault_find_related`) | One call answers "what surrounds this decision?" — outgoing links, backlinks, active project memory, similar-title same-type notes, each with a reason. Replaces 3–4 searches. |
| **Health verdict** | `doctor` and `mindvault_health` now lead with good/warning/critical + reasons. Critical = mutations unsafe; an agent can branch on one field. |
| **Dry-run mutations** | `dryRun` on append / update-frontmatter / archive: full validation, preview of the exact change, proven no-write. |
| **Context confidence** | `mindvault_get_project_context` reports `confidence`/`resolvedVia` and warns when it resolved through an alias, and its queries now match notes filed under an alias. |

## 2. Before/after behaviour

| Scenario | Before (0.2.0) | After (0.3.0) |
| --- | --- | --- |
| Agent in repo `mind-vault`, project note "MindVault" | "Project not found" → agent likely creates a duplicate project | Resolves via condensed/alias tier with `confidence: high`; creating the duplicate is refused |
| Agent creates "Ship the v1" when "Ship v1" exists | Created; warning arrives after the fact | Refused with candidates; `allowDuplicate` overrides |
| "What tasks relate to this decision?" | 3–4 searches, manual joins | One `mindvault_find_related` call with reasons |
| "Is the brain usable?" | Read doctor's 15 fields and decide yourself | `health: WARNING — validation found 3 critical issue(s)` |
| "What would archiving do?" | Run it and check the snapshot | `--dry-run` prints from/to/steps; nothing changes |

## 3. Commands added/changed

- New: `detect-project ["<name>"]` (defaults to the current folder name), `related "<ref>" [--limit n]`
- Changed (additive): `create …` gains `--allow-duplicate` and refuses likely duplicates;
  `append`/`update-frontmatter`/`archive` gain `--dry-run`; `doctor` leads with the
  verdict (exit code unchanged); `check-draft --json` gains `likelyDuplicatePaths`.

## 4. MCP tools added/changed (21 → 23)

- New: `mindvault_detect_project`, `mindvault_find_related` (both read-only, compact).
- Changed (additive): creates gain `allowDuplicate` + structured refusal
  `{ created: false, reason: "possible_duplicate", candidates }`; append/update/archive
  gain `dryRun`; `mindvault_health` gains `verdict`; `mindvault_check_draft` gains
  `likelyDuplicatePaths`. No tool renamed or removed; no existing field removed.

## 5. Skills changed

`mindvault-project-context` (detect-first project identification),
`mindvault-decision-capture` (duplicate refusal + find_related for linking),
`mindvault-task-sync` (duplicate refusal discipline), `mindvault-vault-hygiene`
(verdict-first health check). All eight still pass the 5-section contract tests.

## 6. Tests added

278 total (was 249; all previous tests still green — three updated because refusing
duplicates IS the new intended behaviour they now assert):

- `ProjectDetectTests` (13): every confidence tier, condensed matching, shared-alias
  ambiguity, alias-scoped context queries, create-via-repo-name, alias-collision refusal,
  determinism.
- `RelatedNotesTests` (6): backlinks + project memory with reasons, self-exclusion,
  dedup, limits, determinism, CLI round trips.
- `DryRunTests` (5): byte-identical files, no move, no snapshot, no index change, same
  validation errors as a real run.
- `HealthVerdictTests` (6): warning on the messy fixture, good on a clean vault, critical
  on a placeholder path, CLI verdict line, MCP verdict field, MCP duplicate refusal payload.
- Guard tests now pin the MCP surface at exactly 23 tools.

## 7. Benchmarks (same machine as the 0.2.0 numbers, run after the pass)

Both runs were taken the same day on the same machine: a baseline immediately before this
pass and a run immediately after. Differences are within run-to-run noise — no regression.

| metric | 1k notes (after) | 10k notes (after) | 10k baseline (before) | verdict |
| --- | --- | --- | --- | --- |
| cold scan | 294 ms | 2,048 ms | 1,989 ms | noise (±3%) |
| incremental scan | 4.2 ms | 41.5 ms | 44.4 ms | noise |
| search (ranked) | 0.9 ms | 13.9 ms | 12.4 ms | noise |
| project context | 1.9 ms | 4.8 ms | 5.0 ms | unchanged — alias expansion costs ~nothing |
| context pack | 3.8 ms | 26.9 ms | 29.4 ms | noise |
| validate | 31 ms | 173 ms | 175 ms | unchanged |

The 0.2.0 efficiency-pass wins (docs/PERFORMANCE_RESULTS.md) are intact. The duplicate
gate adds one draft-check (~0.6–2.9 ms) to each create — a mutation that already costs
10–20 ms in snapshot+fsync; invisible in practice.

## 8. Known limitations (honest)

- Alias detection loads the (small) project list + alias rows per detection call; it is
  not cached. At realistic project counts (≤ a few hundred) this is single-digit ms.
- `related` similar-title matching examines up to 200 same-type notes — deliberately
  bounded, so on a vault with thousands of decisions the similarity group may miss distant
  candidates (links/backlinks/project groups are unaffected).
- Search (`mindvault_search --project`) still filters by exact project name, with the
  existing vault-wide fallback — alias expansion there would change `SearchCandidates`'
  signature for marginal gain since context/packs already alias-match.
- Dry-run exists for the three highest-traffic mutations, not all ten; the others remain
  covered by snapshot + restore.
- The Raspberry Pi remains unmeasured (no Pi in this environment); the same benchmark
  command runs there.

## 9. Deliberately not done (and why)

- **No embeddings** — the failure cases found in review were identity problems (alias,
  repo name), now solved deterministically at zero runtime cost. Embeddings stay a future
  option behind a proven eval failure.
- **No context-budget service** — budgets already exist at every output edge (limit
  clamps, brief/standard/deep, body/issue caps, page-only snippets); a service would be
  an abstraction with one caller each.
- **No dashboard, no Obsidian plugin, no architecture rewrite** — out of scope / not
  justified; the architecture survived a hard audit.
- **No new doc sprawl** — one QUICKSTART added; everything else merged into existing docs.

## 10. Next-level ideas (unranked, unpromised)

- Optional alias cache invalidated by scan generation, if detection ever shows up in traces.
- `mindvault_find_related` second hop (related-of-related, capped) for architecture notes.
- Pi benchmark section in PERFORMANCE_RESULTS.md once run on hardware.
- Search-side alias expansion if an eval demonstrates a real miss.
