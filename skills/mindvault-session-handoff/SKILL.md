---
name: mindvault-session-handoff
description: Bracket a coding session with MindVault - start with a one-call budgeted session brief (goal, non-negotiables, decisions, do-not-repeat rules, tasks, risks, read-first/do-not-read lists, what changed since last handoff), end with one batched handoff that also records the session's decisions, mistakes and tasks. Use at the beginning of substantial work on a project with a MindVault vault, and again when wrapping up or being interrupted mid-task.
---

# MindVault: Session Handoff

Two calls bracket a session — one to brief in, one to hand off.

## Trigger conditions

Use when:
- Beginning substantial work on a project that has a MindVault vault (start).
- Wrapping up, or being interrupted mid-task after real work happened (end).

Do NOT use when:
- The interaction is a quick question or a trivial edit — no session ceremony for that.
- A session was never started and nothing durable happened — then there is nothing to hand off.

## Required workflow

Starting work:

```
mindvault_start_session
  project: <project>
  task:    one line describing what this session is for (optional but sharpens the brief)
  maxChars: optional budget for the brief (default 6000)
```

Returns a budgeted session brief — goal, non-negotiables, decisions in force, do-not-repeat
rules, open/blocked tasks, risks, constraints, a token-priced read-first list paired with a
do-not-read list, and `deltaSinceLastHandoff` (what changed since your previous handoff) —
and ensures the project's implementation-log note exists. This one call replaces reaching for
`mindvault_build_context_capsule` and `mindvault_build_route_card` separately at the start.
Then:

1. Check `deltaSinceLastHandoff` for what moved while you were away — no separate recall
   needed for the usual case. For a wider window, `mindvault_recall` (`since: "last-handoff"`
   for the exact gap) and `mindvault_recent_sessions` for where the last session stopped.
2. Read the brief's read-first notes with `mindvault_read_note`, scoped with
   `section`/`maxChars`; skip the do-not-read list. Stop once the goal, constraints and risks
   are clear. If the work later shifts into a distinct mode or a new file mid-session,
   `mindvault_build_context_capsule` / `mindvault_build_route_card` give a fresh briefing.
3. Surface any warnings that affect this session (stale tasks, contradicted decisions).
4. If a tracked task covers this session's work, mark it `active`
   (`mindvault_update_frontmatter`, see `mindvault-task-sync`).
5. Work. A genuine milestone mid-session may earn ONE `mindvault_checkpoint_session`
   breadcrumb; prefer none.

Ending work (or being interrupted) — one batched call:

```
mindvault_end_session
  project:   <project>
  summary:   one line — what was accomplished (or exactly where it stopped)
  tests:     what was run and the result, e.g. "dotnet test green (180)"
  followUps: remaining risks / next steps, comma-separated — or omit for none
  decisions: any decisions that surfaced at the close (each a title + optional content)
  mistakes:  any lessons that surfaced at the close (each a title + optional lesson/prevention)
  tasks:     any follow-ups that surfaced at the close (a title to create, or a ref + status
             to update an existing task)
```

Batch the decisions, mistakes and tasks that surface right at the close into this one call
rather than making a separate create call for each — each batched item runs the same
duplicate and content gates as its standalone tool and reports its own outcome
(created / updated / skipped_duplicate / blocked / error), and the handoff is written first
regardless. Captures made
**mid**-session still go through the standalone tools as they happen
(`mindvault-decision-capture`, `mindvault-mistake-ledger`, `mindvault-task-sync`). Before
calling it, also close out tracked tasks (`done`) or note blockers (`blocked` + Status Notes).

Expected final behaviour: one dated, honest handoff block in `Log - <Project>` — plus any
end-of-session decisions/mistakes/tasks recorded in the same call — that a cold reader next
week can resume from.

## Do not

- Do not call `mindvault_end_session` more than once per session, or after every small step.
- Do not call `mindvault_build_context_capsule` and `mindvault_build_route_card` at the start
  as well — the brief already covers both; save them for a mid-session refresh.
- Do not fire a separate create call for each end-of-session decision, mistake or task —
  batch them into `mindvault_end_session` instead.
- Do not narrate: the summary is outcome and state, not a play-by-play.
- Do not end a long session silently — if you did substantial work, write the handoff.
- Do not spam `mindvault_checkpoint_session` entries; one per genuine milestone, prefer none.
- Do not paste secrets or tokens into summaries — the content gate will refuse them.

## Efficiency rules

- Exactly one start call and one end call per session; the brief and the batched close are
  each a single call.
- Read only the brief's read-first notes before working; scope each read with
  `mindvault_read_note`'s `section`/`maxChars` and stop once the goal is clear.
- Prefer `deltaSinceLastHandoff` in the brief over a separate recall for the common "what
  changed" question.
- The handoff summary is a few lines — if you are writing paragraphs, cut.

## Safety rules

- Use only the `mindvault_*` MCP tools — never write vault files directly or via shell.
- Be honest in `tests`: "not run" is a valid and useful handoff fact — never claim green
  tests that were not run.
- The end-session write goes through the snapshot-first pipeline; never bypass it by
  editing the log note through other means.
