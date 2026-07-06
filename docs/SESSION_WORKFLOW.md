# Session Workflow

Two calls bracket a coding session: brief in, hand off out. No daemon, no hidden state —
entries land in a normal vault note (`06_Agent_Memory/Log - <Project>.md`, type `memory`)
through the same snapshot-first write pipeline as everything else.

## CLI

```bash
mindvault session start --project Alpha --task "harden search" [--max-chars 6000]
# → prints the session brief; creates Log - Alpha.md on first use

mindvault session log --project Alpha --summary "index schema decided"
# → optional mid-session breadcrumb (#### entry). Use sparingly.

mindvault session end --project Alpha --summary "weighted search shipped" \
    --tests "dotnet test green (180)" --followups "tune recency boost"
# → dated ### handoff block: summary, tests, follow-ups
```

## MCP

- `mindvault_start_session(project, task?, maxChars?)` → a budgeted session brief plus log
  setup. `maxChars` defaults to 6000.
- `mindvault_end_session(project, summary, tests?, followUps?, decisions?, mistakes?, tasks?)`
  → handoff written, plus any batched captures.
- `mindvault_recall(project, since?, ...)` → accepts `since: "last-handoff"` alongside dates
  and `"7 days"`.

## The session brief

`mindvault_start_session` returns a **budgeted brief**, not a full context pack — for most
sessions it replaces calling `mindvault_build_context_capsule` and `mindvault_build_route_card`
separately (both still exist for mid-session use). Inside the `maxChars` budget (a soft
target — see below) it carries:

- the current goal and non-negotiables;
- decisions in force and the do-not-repeat rules from the mistake ledger;
- open and blocked tasks, open risks, constraints;
- a token-priced **read-first** list paired with a **do-not-read** list;
- **`deltaSinceLastHandoff`** — what changed since this agent's previous handoff, as counts
  plus up to 10 items.

Each fact appears once. Read the read-first notes (scoped with `section` / `maxChars`) and
skip the do-not-read list; the delta tells you what moved while you were away without a
separate `mindvault_recall`.

`maxChars` is a soft target, not a hard cap: the trim drops lower-priority sections first
(known mistakes, then reasons, delta items, risks, read-first extras) but never truncates the
goal, the decisions in force, or the do-not-repeat rules — so a brief can modestly exceed the
budget when those core facts are large.

## Handoff-relative recall

`mindvault_recall` accepts `since: "last-handoff"`: the window starts at the most recent
handoff heading in the project's session log. When no handoff exists yet it falls back to a
7-day window and says so with a warning. This is the precise "what changed since I last
worked here" query, distinct from a fixed date or `"7 days"`.

`since: "last-handoff"` requires a `project` argument (there is no vault-wide "last handoff")
and errors without one; calendar values like a date or `"7 days"` keep working without a
project.

## Batched session close

`mindvault_end_session` can close a session honestly in **one call**. Alongside the handoff it
accepts optional arrays of decisions, mistakes and tasks to capture — each decision carries a
title and optional content; each mistake a title with an optional lesson and prevention rule;
each task either a title (to create) or a reference plus a status (to update an existing
task). Every item runs the **same duplicate and risky-content gates** as its standalone tool,
and each returns a per-item outcome: `created`, `updated` (a task status change),
`skipped_duplicate`, `blocked`, or `error`. One bad item never aborts the handoff or the
other items — the handoff
is written first, then the captures are applied and reported individually. This drops an
honest close from the old 5–8 MCP calls (one `end_session` plus a `create_decision` /
`add_mistake` / `create_task` each) to one. Captures that surface **mid**-session still use
the standalone tools.

## What the log note looks like

```markdown
## Sessions

### 2026-07-04 16:40 — weighted search shipped

- Tests: dotnet test green (180)
- Follow-ups: tune recency boost
```

The three newest `###` entries feed `recentImplementationLogs` in the project context and
pack, so the next session's briefing includes what the last one did.

## Discipline

- **Start** is read-only (plus one-time log-note creation) — starting a session never
  spams the vault.
- **End once per session**, with facts: what happened, what was verified, what's left.
  "Tests: not recorded" is written honestly when omitted.
- `session log` exists for rare milestones; a session with five log entries is doing it wrong.
- Real follow-ups become tasks (`mindvault_create_task`, or batched into the end call), not
  log lines; real decisions get captured properly. The handoff references outcomes — it isn't
  the storage for them.
