# Route Cards

A route card is an agent navigation *briefing*: the 3–5 notes to read first (with reasons,
token estimates and summary snippets), what can wait, what NOT to read and why, the
constraints and do-not-repeat rules in force, and the next tool calls — all under a token
budget. Call it BEFORE a broad search. Deterministic; ambiguous projects return candidates,
never a guess.

## Inputs

`RouteCardService.Build(project, goal?, currentFile?, query?, budget?)`. The seed comes from
exactly one input, in this precedence:

- **currentFile** — seeds work-context by file; purpose becomes "edit `<file>` without
  violating recorded memory".
- **goal / query** — seeds work-context by query; purpose is "achieve: …" or "answer: …".
- **neither** — no work-context; purpose is "general orientation on `<project>`", and the
  card falls back to the project's own trail (the hub and its map block, the session log,
  recent decisions, active tasks).

## The three read lists

- **readFirst** — up to `maxFirst` (`budget.MaxNotes` clamped 1–10; default
  `DefaultReadFirst = 5`). The project hub leads (its map block is the one-read orientation),
  then work-context suggested reads (or the project trail). Each gets a summary snippet: the
  generated `summary:` line wins, else the first plain-text line, truncated to
  `budget.MaxSnippetChars`.
- **readIfNeeded** — the next 5 candidates, plus anything demoted for budget.
- **doNotRead** — up to `DoNotReadCap = 8` low-value notes, ordered by reason count.

### Hard vs advisory low-value reasons

Only **hard** reasons exclude a note from the read lists. The `hardReasons` array is:

```
archived, superseded, rejected decision, hidden by feedback, negative feedback, raw thought
```

Hygiene flags (large/no-summary, unlinked orphan, missing/ambiguous project, stale log) are
*advice, not a veto* — they still appear in `doNotRead`, but the note can still be routed
(otherwise an unsummarized hub would vanish from its own route). Every low-value note is
listed in `doNotRead` regardless; only the hard set is filtered from reads.

## Typed context and the rest of the card

Alongside the read lists: `ActiveConstraints` (non-negotiables + constraint titles, ≤8),
`RelevantDecisions`, `RelevantMistakes` (do-not-repeat), `OpenRisks`, `ActiveTasks` — each
sourced from the seeded work-context when present, else the project hub view.
`SuggestedNextToolCalls` names concrete follow-ups: a `mindvault_read_note` for the top read,
a `mindvault_build_context_capsule` when unseeded, and a narrowed `mindvault_search` "only if
the reads above leave the goal unclear".

## Budget and savings

`tokenBudget` = `MaxEstimatedTokens`, else `MaxChars/4`, else `DefaultTokenBudget = 4000`.
Enforcement demotes the last read-first note into readIfNeeded until the sum fits — overflow
moves down, never silently disappears (one note always survives).

`estimatedTokenSavings` = `max(0, baseline − readFirstTokens)`. **Baseline** = the distinct
token sum of *every* candidate the card surfaced (readFirst + readIfNeeded + doNotRead +
decisions + mistakes + risks + tasks) — i.e. the naive cost of reading everything the card
considered. The saving is that cost minus what read-first actually costs.

## Ambiguity

An ambiguous project name returns `RouteCardOutcome(Card: null, Candidates: [...])`. The CLI
exits 3 and lists candidates; MCP returns `{ ambiguous: true, candidates }`. A not-found name
throws the helpful not-found error instead.

## CLI

```bash
mindvault route --project "MindVault" --goal "improve config validation"
mindvault route --project "MindVault" --current-file "src/MindVault.Core/WriteService.cs"
mindvault route --project "MindVault" --query "snapshot safety" --format json
mindvault route --project "MindVault" --max-notes 3 --max-tokens 1500
```

Default output is **markdown** (`RouteCardService.ToMarkdown`); `--json` or `--format json`
returns the structured card. Flags: `--goal | --current-file | --query`, `--max-notes`,
`--max-chars`, `--max-tokens`.

## MCP

`mindvault_build_route_card` (project, goal?, currentFile?, query?, maxNotes?, maxTokens?,
format?) — `format` is `"json"` (default) or `"markdown"`. Returns `{ routeCard }`,
`{ markdown }`, or `{ ambiguous, candidates }`.

## Limitations

- Snippets are best-effort file reads; an IO error just leaves the snippet null.
- Token figures are estimates (see [TOKEN_EFFICIENCY.md](TOKEN_EFFICIENCY.md)).
- The card seeds from one input; combine runs if you have several angles.
- It reflects what the vault has recorded — an empty vault yields a thin card. See the
  read plan for the stricter itinerary form ([READ_PLANS.md](READ_PLANS.md)).
