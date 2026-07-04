# Organisation Score

Eleven explainable 0–100 heuristics that tie vault structure to agent token cost. Every
category carries the evidence for its number; the point is not a vanity metric but a directly
actionable weaknesses list — fixing it makes route cards and capsules measurably cheaper.

```bash
mindvault organisation-score                 # whole vault
mindvault organisation-score --project "MindVault"
```

MCP: `mindvault_organisation_score` (project?) → `{ score }`.

## The 11 categories

Each is `Math.Clamp(score, 0, 100)`. Formulas are the actual heuristics in
`OrganisationScoreService.cs`:

| Category | Formula | Evidence string |
|---|---|---|
| `folderPlacement` | `100 − misplaced*100/managed − needsReview*2` | "N of M managed notes are misplaced; K need review" |
| `frontmatterQuality` | `100 − criticals*15 − warnings*3` | "C critical and W warning frontmatter finding(s) across N notes" |
| `linkCoverage` | `100 − orphans*100/linkable` (linkable = managed non-thought) | "O of L linkable managed notes have no links in either direction" |
| `mapCoverage` | `mapped*100/projects` (100 if no projects) | "M of P project(s) have a map in 09_Maps" |
| `summaryCoverage` | `largeWithSummary*100/largeNotes` (100 if none large) | "S of N large note(s) carry a generated summary" |
| `duplicateRisk` | `100 − dupClusters*20` (exact normalized-title clusters) | "K exact-title duplicate cluster(s) among managed notes" |
| `orphanRisk` | `100 − orphans*8` | "O orphan(s); B broken link(s) from scoped notes" |
| `staleMemoryRisk` | `100 − stale*100/staleable` | "X of Y open work item(s)/log(s) untouched for 90+ days" |
| `thoughtPromotionHygiene` | `100 − strayThoughts*25 − oldThoughts*5` | "S thought(s) outside the inbox; O older than 14 days awaiting promote/reject" |
| `tokenEfficiency` | `100 − waste*100/activeTokens` | "~W of ~A active tokens are waste (unsummarized large notes + oversized notes)" |
| `agentReadiness` | `passing*100/checks` | "map, links, ledger, sessions and goal all in place" or "missing: …" |

Notes on the trickier ones:

- **staleMemoryRisk** only counts *work-tracking* types (task/bug/feature/memory) that are
  not done/cancelled/superseded and untouched for `StaleDays = 90`. Old decisions are fine —
  they do not go stale.
- **thoughtPromotionHygiene** — "stray" = a thought outside `00_Inbox/` or
  `06_Agent_Memory/Inbox/`; "old" = created >14 days ago.
- **agentReadiness** checks: a map exists for every project, no broken links, the mistake
  ledger is in use, a session log updated within `RecentSessionDays = 14`, and (project-scoped
  only) the hub has a Goal section.

## Overall, strengths, weaknesses

- `OverallScore` = rounded average of the 11 category scores.
- **Strengths** = category names scoring **≥ 90**.
- **Weaknesses** = categories scoring **< 70**, ordered worst-first, each rendered as
  `name (score): evidence`.
- `RecommendedFixes` map weaknesses to commands (create maps, `summarize --apply`, `organize`,
  `links suggest`, `inbox list`, merge duplicates); a clean vault says "nothing urgent — the
  brain is well organised".

## Token waste and savings

- `EstimatedTokenWaste` — taken straight from the token audit
  ([TOKEN_EFFICIENCY.md](TOKEN_EFFICIENCY.md)).
- `EstimatedTokenSavingsIfFixed` = `max(0, unsummarizedTokens − notesWithoutSummaries*60)`
  (summaries replace raw reads at ~60 tokens each) `+ max(0, capsuleTokens − routeReadFirstTokens)`
  (the route becomes the entry point instead of a full capsule).

## No false precision

These are heuristics, and the code says so: every category carries an evidence string,
weaknesses cite counts, and no category claims more accuracy than "this many findings drove
this number". Do not read a 78 as a measurement — read the weaknesses list, which is the
actionable part. Token figures are estimates throughout.

## Limitations

- Weights (e.g. `*15` per critical frontmatter finding, `*20` per duplicate cluster) are
  chosen heuristics, not calibrated constants.
- Scores are point-in-time over the current index; run `compile`/scan first for freshness.
- The map embeds only the overall score + weakest category, not the full breakdown — run the
  command for that. See [MAPS.md](MAPS.md).
