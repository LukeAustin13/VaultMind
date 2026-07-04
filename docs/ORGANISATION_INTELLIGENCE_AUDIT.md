# Organisation Intelligence + Token Compression — Audit (pre-implementation)

Date: 2026-07-04. Basis: direct reads of MapService/LinkIntelligenceService/WorkContextService/
SectionExtractor this session, plus one bounded Opus audit agent (full API map of Core/CLI/MCP/tests
+ 10 token-efficiency findings with file:line evidence). Version at audit time: 0.5.0, 45 MCP tools,
322 tests.

## 1. Current organisation architecture

- **Files-canonical, index-disposable.** SQLite FTS5 index (`.mindvault/index.sqlite`, schema v2)
  holds `NoteSummary` metadata + FTS body; note bodies live in Markdown only.
- **Placement** is static policy (`PlacementPolicy`): type → folder; `OrganizeService.Plan/Apply`
  proposes moves (Confidence always "high", uncertain items → `needsReview`, never moved).
- **Maps** (`MapService`): one generated note per project in `09_Maps/`, content between
  `mindvault-generated` markers rebuilt on demand; human text outside markers preserved.
- **Link intelligence** (`LinkIntelligenceService`): reason-tagged suggestions (score ≥2, never
  auto-applied), broken links, orphans. Untyped: a wiki link is just a link.
- **Retrieval layers**: search (FTS + deterministic rescoring) → context pack → capsule (7 modes,
  char budget) → work-context (file/query/note) → recall (time). Feedback sidecar shapes
  capsule/work-context/related/suggestions but deliberately not raw search.

## 2. Current map/link/promotion/context behaviour

- Map generated block = 12 sections × up to 10 bullets; no agent-route guidance, no health info,
  no do-not-read info. Rebuild is explicit (`map rebuild`).
- Links: suggestions carry reasons + confidence; graph is implicit (rows in `note_links`), no
  relationship types, no explainability between two arbitrary notes.
- Promotion: thought → typed note via `PromoteNote` (duplicate-gated, backlink-gated rename).
  Inbox verbs wrap it. Nothing auto-promotes.
- Context: capsule trims to a char budget by mode priority; work-context seeds from exactly one
  input; both return titles+paths+reasons, not bodies. `read_note` is the token sink.

## 3. Where agents still waste tokens (evidence-backed)

1. **`mindvault_read_note` returns up to 60,000 chars** (MindVaultTools `MaxBodyChars`), no
   section/char selector — an agent needing one section pays for the whole note. *Highest single win.*
2. **No do-not-read guidance anywhere.** Feedback-hidden / negative-scored / superseded notes are
   excluded from capsule/work-context but `search`/`list_notes` will happily serve them and nothing
   warns the agent before it reads them.
3. **No summaries.** Every "what is this note?" question costs a full read. There is no cheap
   per-note summary an agent (or route card) can use instead of the body.
4. **`get_project_context` standard mode duplicates rows** (`RecentNotes` overlaps the typed lists;
   `RecommendedNextReads` re-references list heads). Context pack `DoNotForget` restates
   NonNegotiables/Constraints/OpenRisks in the same payload.
5. **No reading order.** Capsule says *what is true*; nothing says *what to read, in what order,
   when to stop*. Agents fall back to search sweeps (the exact behaviour skills currently have to
   ban by prose).
6. **No token accounting.** Nothing measures which notes are oversized, what a capsule/route costs,
   or where the vault's token waste concentrates — so neither humans nor agents can fix it.

## 4. Where vault structure still creates ambiguity

- Untyped links: a decision↔task link and a random mention look identical to a reader.
- Duplicate-ish titles are flagged only at write time (draft check) and in doctor counts; nothing
  clusters them for navigation.
- Stale implementation logs and rejected/superseded decisions sit alongside live memory with no
  machine-readable "skip this" marker.
- Thoughts outside the inbox and notes with missing/unresolvable projects are findable only via
  separate audits.

## 5. Where capsule / work-context over- and under-read

- **Over-read:** capsule always renders every populated section up to the budget even when the mode
  needs 3 of them; map rebuild always renders all 12 sections incl. `_(none)_` placeholders; ops
  recomputes doctor+orphans+audits per call.
- **Under-read:** work-context returns nothing on multi-token queries with zero FTS overlap rather
  than suggesting a narrowed search; capsule's goal comes only from the hub `## Goal` section —
  if absent, the goal is silently missing (a generated summary is a natural fallback).
- Neither output estimates its own token cost, so callers can't trade depth for budget.

## 6. Top 10 upgrades by token-saving impact

1. **Route cards** — read-first ≤5 with reasons + token estimates + do-not-read: replaces N
   searches + M speculative reads per session. (High)
2. **Read plans** — strict ordered tool-call plan with stop conditions: converts "search until
   bored" into ≤5 reads. (High)
3. **Section/char-scoped `read_note`** — additive `section`/`maxChars` params: the 60 KB sink
   becomes a scalpel. (High)
4. **Generated summaries** — deterministic extractive block per large note; route/capsule consume
   the summary instead of the body. (High)
5. **Low-value detection** — one service feeding doNotRead/route/score/search cautions: prevents
   reads of archived/superseded/hidden/stale/noisy notes. (High)
6. **Search/list feedback cautions** — annotate (not re-rank) hidden/negative hits so agents skip
   them; FTS ranking stays pure. (Med-high)
7. **Token audit** — makes waste visible (largest notes, unsummarized, capsule vs route cost) with
   fixes. (Med)
8. **Organisation score** — 11 explainable categories tying structure to token cost; makes the
   brain self-diagnosing. (Med)
9. **Typed graph** — explicit-link edges typed by endpoint types + jsonl sidecar + explain:
   fewer exploratory reads to understand why notes relate. (Med)
10. **Map v2** — Start Here / Agent Route / Do Not Repeat / health sections: one read orients both
    humans and agents. (Med)

## 7. What not to automate

- No auto-apply of summaries or moves vault-wide on ordinary writes — `compile`/`organize`/
  `summarize` stay explicit, dry-run by default, snapshot-first on apply.
- No auto-promotion of thoughts; no auto-hide from feedback scores; no auto-deletion of low-value
  notes (they are flagged, never touched).
- No LLM summarisation — extractive and deterministic only, or the vault starts lying.
- No re-ranking of raw FTS search by feedback (annotation only) — search must stay reproducible.
- No auto-rebuild of maps/graph on every write — they are compiled artefacts, not live views.

## 8. Implementation checklist

1. `TokenEstimator` + `ContextBudget` (ceil(chars/4); file-size based via `GetFileStates`).
2. `SummaryService` — `mindvault-summary:start/end` markers, extractive, `needsReview`,
   `TryGetSummary` for consumers; apply via `ReplaceBody` (snapshots built in).
3. `LinkGraphService` — typed edges from explicit links (typed by endpoint-type pairs),
   frontmatter membership, supersession, title-collision duplicates; `.mindvault/link-graph.jsonl`;
   relationships/explain computed live.
4. `LowValueService` — reasons: archived/superseded/hidden/negative-feedback/thought/orphan/
   large-unsummarized/stale-log/missing-project/rejected-decision.
5. `RouteCardService` + `ReadPlanService` — budgets, candidates on ambiguity, deterministic order.
6. `TokenAuditService` + `OrganisationScoreService` (11 categories, evidence strings).
7. Map v2 generated block + `OrganisationCompiler` orchestration.
8. `read_note` gains `section`/`maxChars`; search results gain feedback `caution` (additive).
9. CLI: compile/route/read-plan/token-audit/summarize/organisation-score/graph/low-value; map
   `--v2` accepted (v2 is the only rebuild output — documented).
10. MCP: +10 tools (45 → 55), guards + `McpToolCount` updated; version 0.6.0.
11. Fixture `TokenEfficientVault` + 15 evals; skills +route-card +read-plan, 5 updated (13 total).
12. Docs ×7 + README/CHANGELOG/TOOLING/MCP_SETUP/AGENT_WORKFLOWS/DEMO_SCRIPT.

## 9. Risk assessment

- **Circular service dependencies** — dependency direction fixed as: Route→{LowValue, WorkContext,
  ProjectContext, Summaries}; TokenAudit→{Capsule, Route}; Score→{Organizer, Audits, LinkIntel,
  TokenAudit}; Map→{Score, LinkIntel, Organizer, Sessions}. No cycles; enforced by review.
- **Map rebuild cost grows** (score inside the block). Acceptable: rebuild is explicit; measured in
  verification. If >1s on the 10k vault, the score line degrades to a pointer.
- **Summary blocks touch note bodies** — the one genuinely invasive feature. Mitigations: dry-run
  default project-wide, snapshot per note, block-splice only (same proven marker mechanism as maps),
  eval proving human text is byte-identical outside the block.
- **Guard-test surface grows to 55 tools** — every addition pinned by three independent guards.
- **False precision in scores** — every category carries an evidence string; weaknesses cite counts.

## 10. Verification plan

`dotnet restore/build/test` (Release, 0 warnings, all green). Benchmarks re-run (scan/search hot
paths untouched — assert no regression). Live smoke of all nine spec commands on a real vault:
organisation-score, token-audit, route (goal), read-plan (goal), compile --dry-run, summarize
--dry-run, graph build, low-value, map rebuild --v2. Evals 1–15 from the spec implemented as named
tests over `TokenEfficientVault`. Final report audited claim-by-claim against this session's tool
output.
