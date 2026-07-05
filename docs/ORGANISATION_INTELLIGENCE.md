# Organisation Intelligence

The v0.6.0 organisation-intelligence layer is a token-compression + navigation layer for
agents. It sits on top of the vault (files-canonical, index-disposable) and answers, cheaply
and deterministically: *what should I read, in what order, what should I skip, and how much
will it cost?* Nothing here invents content and nothing (except `--apply`) writes.

## The pieces

| Piece | Service | What it gives an agent |
|---|---|---|
| Route card | `RouteCardService` | The 3–5 notes to read first, with reasons, token estimates, snippets, and a do-not-read list. See [ROUTE_CARDS.md](ROUTE_CARDS.md). |
| Read plan | `ReadPlanService` | A strict ordered itinerary (≤5 reads) with stop conditions. See [READ_PLANS.md](READ_PLANS.md). |
| Summaries | `SummaryService` | An extractive `mindvault-summary` block per large note so "what is this?" is cheap. See [GENERATED_SUMMARIES.md](GENERATED_SUMMARIES.md). |
| Typed graph | `LinkGraphService` | Explicit links typed by endpoint (task_tracks_decision, supersedes, …) + explain. See [TYPED_GRAPH.md](TYPED_GRAPH.md). |
| Low-value detection | `LowValueService` | The do-not-read list with per-note reasons. |
| Token audit | `TokenAuditService` | Where the tokens go: totals by tier, largest notes, unsummarized notes, estimated waste. See [TOKEN_EFFICIENCY.md](TOKEN_EFFICIENCY.md). |
| Organisation score | `OrganisationScoreService` | 11 explainable categories tying structure to token cost. See [ORGANISATION_SCORE.md](ORGANISATION_SCORE.md). |
| Map block | `MapService` | One-read orientation appended to the project hub: start-here, do-not-repeat, health + score. See [MAPS.md](MAPS.md). |
| Compiler | `OrganisationCompiler` | One pass that builds all of the above. |

## The compiler

`mindvault compile` (MCP: `mindvault_compile_brain`) runs one pass over the vault: for each
project it creates/rebuilds the hub's map block and regenerates summaries, then builds the
typed link-graph sidecar, the health report, the token audit and the organisation score.

- **Dry-run by default.** Nothing is written without `--apply`. The dry-run report says what
  it *would* create/rebuild/write.
- **Snapshot-first on apply.** Every note write (map block, summary block) goes through the
  snapshot-first writers; only content between generated markers changes. Rebuilds are
  idempotent — an unchanged map block (ignoring its timestamp) is not rewritten.
- **Never moves notes.** Placement moves stay with `mindvault organize` /
  `mindvault_organize_vault`.
- Compiles all projects by default, capped at `ProjectCap = 25` (a warning names the
  overflow); pass `--project` for one.

```bash
mindvault compile                       # dry-run, all projects
mindvault compile --project "MindVault" --apply
```

MCP: `mindvault_compile_brain` (project?, apply?) — returns `dryRun`, `overallScore` and an
`artifacts` list (map / summaries / link-graph / health-report / token-audit /
organisation-score, each with a status like "would rebuild" / "applied N").

## Which tool when

| You want to… | Use |
|---|---|
| Start work on a project, minimal reads | route card, then follow its read-first list |
| A rigid ordered plan you can follow literally | read plan |
| Know a single note without reading it | its generated summary (`summarize` first) |
| Understand *why* two notes relate | `graph explain` |
| Know what to skip and why | low-value / the route's do-not-read |
| Diagnose where tokens are wasted | token audit |
| One number + actionable weaknesses | organisation score |
| One cheap orientation read for humans + agents | the hub's map block |
| Rebuild everything above in one go | compile |

## How they fit together

Route cards and read plans are the front door. Both seed from work-context (goal / current
file / query) and exclude low-value notes from every read list, surfacing them as
do-not-read instead. Route snippets prefer a note's generated summary line over raw body
text, so summaries make routing cheaper. The token audit and score both read the summary
and low-value signals to put numbers on waste. The hub's map block embeds the score line and
the health sections. The compiler is the orchestrator that produces the map block, summaries
and graph in a single, dry-run-by-default pass.

Dependency direction (no cycles): Route → {LowValue, WorkContext, ProjectContext,
Summaries}; TokenAudit → {Capsule, Route}; Score → {Organizer, Audits, LinkIntel,
TokenAudit}; Map → {Score, LinkIntel, Organizer, Sessions}.

## Limitations

- Token counts are estimates (`ceil(chars/4)`, file-size based), not a model tokenizer —
  see [TOKEN_EFFICIENCY.md](TOKEN_EFFICIENCY.md).
- Everything is extractive and deterministic; there is no LLM in this layer, so a poorly
  written note yields a poor summary (marked `needsReview`).
- The graph sidecar and hub map blocks are compiled artefacts, not live views — they go
  stale until the next `compile` / `map rebuild` (relationships/explain are computed live and
  stay fresh).
- The compiler never moves or deletes notes; low-value notes are flagged, never touched.
