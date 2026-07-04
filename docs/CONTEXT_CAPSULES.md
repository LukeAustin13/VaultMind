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

MCP: `mindvault_build_context_capsule` (project, mode, maxChars) — returns the structured
capsule AND its rendered markdown.

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
  carries its path; `sourcePaths` lists everything used.
- **Budgeted.** `--max-chars` (default 8000, clamped 1000–32000) is enforced by
  deterministic trimming, mode-priority order.
- **Archived and superseded are excluded** — superseded decisions appear only as warnings.
- **Ambiguity returns candidates.** An ambiguous project name returns the candidate list
  (CLI exit 3), never a guessed capsule.
- **Feedback-aware.** Hidden notes never appear; pinned notes rank first and join
  `suggestedReads` ("pinned by feedback"). See [FEEDBACK_SIGNALS.md](FEEDBACK_SIGNALS.md).
- **Deterministic.** Same vault + same feedback → byte-identical capsule.

## Capsule vs context pack vs work-context

- `mindvault_start_session` / context pack — session-scoped briefing + log setup. Start here.
- **Capsule** — mode-shaped, hard-budgeted, mistake-aware briefing; best when context is
  tight or the work has a distinct mode.
- Work-context ([WORK_CONTEXT.md](WORK_CONTEXT.md)) — memory around one specific file,
  query or note; use before risky edits.
