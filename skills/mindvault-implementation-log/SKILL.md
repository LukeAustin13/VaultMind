---
name: mindvault-implementation-log
description: Write a concise, durable implementation log / handoff note into the MindVault vault after a meaningful coding session - significant feature work, a tricky fix, a refactor, or anything a future session (or another person) will need to pick up cleanly. Not for trivial edits.
---

# MindVault: Implementation Log

Leave a compact handoff trail so the next session starts warm.

## Trigger conditions

Use after meaningful sessions: a feature landed, a bug hunted down, a refactor completed, a
milestone reached, or work interrupted midway that someone must resume.

Do NOT use for tiny edits — a log nobody needs is noise. If the session was trivial, write
nothing.

## Required workflow

Primary path — the session was started with `mindvault_start_session` (see the
session-handoff skill): finish it with **one call**:

```
mindvault_end_session
  project:   <project>
  summary:   one line — what was accomplished
  tests:     e.g. "dotnet test green (180)" — or omit and it records "not recorded"
  followUps: remaining risks / next steps, or omit
```

That writes a structured, dated handoff block to the project's implementation-log note
(`06_Agent_Memory/Log - <Project>.md`) through the snapshot-first pipeline. Done.

Fallback — no session was started:

1. Load `mindvault_get_project_context` (you need the project note's name, and the context
   prevents contradicting recorded state).
2. `mindvault_append_to_note` — `noteRef`: the project note, `section`: `"Active Work"`
   (use `createSection: true` with `"Implementation Log"` if the section is missing):

   ```markdown
   ### 2026-07-04 — <one-line summary>

   - Changed: <areas/modules at folder-or-component level>
   - Verified: <tests/builds run and result>
   - Status: <done | in progress — exactly where it stopped>
   - Follow-ups: <bullets, or "none">
   ```

Expected final behaviour: exactly one dated block, 5–10 lines, that lets a cold reader
resume the work without asking questions.

## Do not

- Do not write more than one block per session.
- Do not paste diffs, command output, or file-by-file play-by-play.
- Do not let real follow-ups rot in a log line — create them properly (see
  `mindvault-task-sync`); real decisions get captured (see `mindvault-decision-capture`)
  and mentioned in the log entry.
- Do not log hopes: state facts ("tests pass", "build broken at X").

## Efficiency rules

- Primary path is one tool call. The fallback is two.
- 5–10 lines per block; component-level granularity, not file-level.
- If you cannot say what was verified, say "not run" — that is shorter and truer.

## Safety rules

- Use only the `mindvault_*` MCP tools — never write vault files directly or via shell.
- All writes go through the snapshot-first pipeline.
- Never claim tests/builds succeeded when they were not run — the log is a trust anchor.
