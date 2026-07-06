# Context Capsules

A capsule is the one artifact an agent should load before working: everything the vault
knows that constrains this session, char-budgeted, source-backed, shaped by what you are
about to do.

## Usage

```bash
mindvault capsule --project "MindVault"                    # coding mode, 8000-char budget
mindvault capsule --project "MindVault" --mode debugging
mindvault capsule --project "MindVault" --format json
mindvault capsule --project "MindVault" --max-chars 4000
```

MCP: `mindvault_build_context_capsule` (project, mode, maxChars, format?, includeSources?).
`format` is `"json"` or `"markdown"` and returns exactly one (as of 0.8.0 it no longer emits
both — that was ~2× the payload). Source paths are returned only with `includeSources: true`
(off by default; every included item already carries its own path).

## Modes

`coding` · `debugging` · `review` · `planning` · `handoff` · `release` · `architecture`

A mode is a priority ordering, not different data: debugging leads with known mistakes,
do-not-repeat rules and recent logs; review leads with decisions and constraints; planning
leads with open questions and tasks. Under a tight budget, the mode decides what survives —
trimming removes items from the lowest-priority sections first.

## What's inside

`project` + `confidence`/`resolvedVia` (detection tier), `mode`, `currentGoal`,
`nonNegotiables`, `activeDecisions` (superseded/rejected never appear here),
`supersededDecisionWarnings` ("do not follow it"), `openTasks`, `blockedTasks`,
`openRisks`, `constraints`, `recentImplementationLogs`, `knownMistakes`, `doNotRepeat`
(the prevention rules from the mistake ledger), `suggestedReads`, `openQuestions`,
`warnings`, `sourcePaths` (the receipts — every note the capsule drew from).

## Rules

- **Compact and source-backed.** Refs + reasons, never note bodies. Every included item
  carries its own path; the aggregate `sourcePaths` list is returned only with
  `includeSources: true`.
- **Budgeted.** `--max-chars` (default 8000, clamped 1000–32000) is enforced by
  deterministic trimming, mode-priority order.
- **Archived and superseded are excluded** — superseded decisions appear only as warnings.
- **Ambiguity returns candidates.** An ambiguous project name returns the candidate list
  (CLI exit 3), never a guessed capsule.
- **Feedback-aware.** Hidden notes never appear; pinned notes rank first and join
  `suggestedReads` ("pinned by feedback"). See [FEEDBACK_SIGNALS.md](FEEDBACK_SIGNALS.md).
- **Deterministic.** Same vault + same feedback → byte-identical capsule.

## Capsule vs session brief vs work-context

- `mindvault_start_session` — the budgeted session brief + log setup. **Start here**; for most
  sessions it replaces calling the capsule and the route card separately at the start.
- **Capsule** — mode-shaped, hard-budgeted, mistake-aware briefing; a **mid-session** refresh
  when the work shifts into a distinct mode (debugging, review, planning…) or context is
  tight.
- Work-context ([WORK_CONTEXT.md](WORK_CONTEXT.md)) — memory around one specific file,
  query or note; use before risky edits.
