---
name: mindvault-session-handoff
description: Bracket a coding session with MindVault - start with a one-call project briefing (context pack + session log setup), end with a one-call concise handoff entry. Use at the beginning of substantial work on a project with a MindVault vault, and again when wrapping up or being interrupted mid-task.
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
  task:    one line describing what this session is for (optional but sharpens the pack)
```

Returns the context pack — goal, non-negotiables, task-relevant notes, decisions in force,
active/blocked tasks, risks, constraints, `recommendedNextReads`, `doNotForget`, warnings —
and ensures the project's implementation-log note exists. Then:

1. Skim the pack; read at most 1–5 of the `recommendedNextReads` with `mindvault_read_note`.
2. Surface any `warnings` that affect this session (stale tasks, contradicted decisions).
3. If a tracked task covers this session's work, mark it `active`
   (`mindvault_update_frontmatter`, see `mindvault-task-sync`).
4. Work.

Ending work (or being interrupted):

```
mindvault_end_session
  project:   <project>
  summary:   one line — what was accomplished (or exactly where it stopped)
  tests:     what was run and the result, e.g. "dotnet test green (180)"
  followUps: remaining risks / next steps, comma-separated — or omit for none
```

Before calling it: close out tracked tasks (`done`) or note blockers (`blocked` + Status
Notes); capture any real decision made this session (`mindvault-decision-capture`).

Expected final behaviour: one dated, honest handoff block in `Log - <Project>` that a cold
reader next week can resume from.

## Do not

- Do not call `mindvault_end_session` more than once per session, or after every small step.
- Do not narrate: the summary is outcome and state, not a play-by-play.
- Do not end a long session silently — if you did substantial work, write the handoff.
- Do not spam mid-session `log` entries; prefer none.

## Efficiency rules

- Exactly one start call and one end call per session.
- Read at most 1–5 recommended notes before working.
- The handoff is 3 lines of arguments — if you are writing paragraphs, cut.

## Safety rules

- Use only the `mindvault_*` MCP tools — never write vault files directly or via shell.
- Be honest in `tests`: "not run" is a valid and useful handoff fact — never claim green
  tests that were not run.
- The end-session write goes through the snapshot-first pipeline; never bypass it by
  editing the log note through other means.
