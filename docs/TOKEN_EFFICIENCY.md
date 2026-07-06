# Token Efficiency

The token model behind route cards, read plans, audits and the organisation score. All of it
is deterministic and deliberately conservative — it over-counts slightly so budgets err
toward reading *less*, never more.

## Where the tokens go, and the levers

An agent pays for tokens in two places: the **tool schema** it loads once per session before
any work, and the **note content** it reads while working. The levers, cheapest first:

1. **Fixed schema cost — the core tool profile.** The full 55-tool schema costs an agent
   roughly 9–12k tokens of context every session, paid before the first useful call.
   `MINDVAULT_TOOL_PROFILE=core` exposes only the 20 session-loop tools and cuts that to about
   a third. This is the single largest per-session saving because it applies whether or not
   the agent reads anything. See [MCP_SETUP.md](MCP_SETUP.md).
2. **One-call session brief.** `mindvault_start_session` returns a budgeted brief (default
   `maxChars` 6000) instead of a full context pack, and replaces calling
   `build_context_capsule` + `build_route_card` separately at the start.
3. **Mid-session navigation — route cards and read plans.** Before a broad search, a route
   card ([ROUTE_CARDS.md](ROUTE_CARDS.md)) names the ≤5 notes to read first and the ones to
   skip; a read plan ([READ_PLANS.md](READ_PLANS.md)) turns that into ordered steps with a
   stop condition. These bound content reads once you are already working.
4. **Scoped reads.** `mindvault_read_note`'s `section` / `maxChars` pull one heading or a
   capped body instead of the full note — the biggest per-read saver (see below).
5. **Slimmer payloads.** `mindvault_search`'s `snippetChars: 0` returns refs only;
   `build_context_capsule`'s `format` returns json OR markdown, not both; capsule and route
   `sourcePaths` are behind `includeSources` (off by default).

## The estimator

`TokenEstimator` (in `TokenEstimator.cs`) is `ceil(chars / 4)`. It is not a model tokenizer:
it is a stable, explainable approximation.

- `Estimate(text)` → `(text.Length + 3) / 4`.
- `EstimateBytes(bytes)` → `(bytes + 3) / 4`. Audits and route cards use this over file sizes
  from `GetFileStates()`, treating bytes as chars. That slightly over-counts multi-byte text,
  and the bias is intentional — a safe over-count means a budget never lets an agent read
  more than it planned for.

## ContextBudget

`ContextBudget` (same file) is the reusable budget applied to route cards, read plans and
audited outputs:

| Field | Default | Meaning |
|---|---|---|
| `MaxNotes` | null | Cap on read-first notes (route clamps to 1–10; read plan to 1–5). |
| `MaxChars` | null | Char ceiling; a route converts it to a token budget via `(mc + 3) / 4`. |
| `MaxEstimatedTokens` | null | Token budget directly. Wins when set. |
| `MaxSnippetChars` | 240 | Truncation length for read-first summary snippets. |
| `IncludeArchived` | false | Whether archived notes are eligible. |

`EffectiveMaxChars` resolves the tighter of `MaxChars` and `MaxEstimatedTokens * 4`.
`ContextBudget.Default` is all-nulls. Route cards use `MaxEstimatedTokens`, else `MaxChars/4`,
else `DefaultTokenBudget = 4000`.

## Token audit

`mindvault token-audit` (MCP: `mindvault_token_audit`) answers "where do the tokens go?"
over file sizes. Output fields (`TokenAuditReport`):

- `NoteCount`, and totals: `TotalEstimatedTokens`, `ManagedEstimatedTokens`,
  `ActiveEstimatedTokens`, `ArchivedEstimatedTokens`.
- `CapsuleEstimatedTokens` vs `RouteReadFirstEstimatedTokens` — cost of a default capsule
  against a route card's read-first list (only when a project is given).
- `LargeNoteCount` / `LargeWithSummaryCount` — large actives (≥ `LargeBodyChars = 2400`) and
  how many carry a generated summary.
- `EstimatedTokenWaste` = unsummarized-large tokens + oversize excess (per-note tokens above
  `TooLargeTokens = 2000`).
- `LargestNotes`, `NotesWithoutSummaries`, `NotesLikelyTooLarge` (each top 10).
- `TokenWasteWarnings` and `RecommendedFixes` (e.g. run `summarize --apply`, split oversized
  notes; a healthy vault says "nothing urgent").

```bash
mindvault token-audit --project "MindVault"
```

## Scoped reads: the scalpel

`mindvault_read_note` gained two additive, token-saving options over the default cap
(`MaxBodyChars`):

- `section` — returns just one heading's content via `SectionExtractor.GetSectionText`; a
  miss returns the note's heading list so you can retry.
- `maxChars` — caps the returned body (`0` = default cap; capped at `MaxBodyChars`). An
  over-cap read is truncated with a hint to "pass maxChars or section to scope the read".

Prefer these over a full read — the tool description says so explicitly.

## Search cautions

Search cautions **do** exist. `SearchService` annotates (never re-ranks or drops) hits the
user has given feedback on. `CautionFor(path)` sets the `Caution` field on a `SearchResult`:

- `"hidden by feedback — skip unless the user asks"` when the note is hidden.
- `"negative feedback (score N) — likely low value"` when the feedback score is below zero.

FTS relevance stays reproducible; the agent just learns it is about to read a note the user
marked hidden or noisy before paying for the read. See [FEEDBACK_SIGNALS.md](FEEDBACK_SIGNALS.md).

## Limitations

- `ceil(chars/4)` and byte-as-char are approximations; treat every "~N tokens" as an
  estimate, not a measurement.
- Waste and savings figures are heuristics (see [ORGANISATION_SCORE.md](ORGANISATION_SCORE.md)
  for the "no false precision" stance).
- Summary-presence checks read files, so audits touch disk for large notes only (they are
  gated on file size first).
