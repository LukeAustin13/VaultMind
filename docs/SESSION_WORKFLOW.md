# Session Workflow

Two calls bracket a coding session: brief in, hand off out. No daemon, no hidden state —
entries land in a normal vault note (`06_Agent_Memory/Log - <Project>.md`, type `memory`)
through the same snapshot-first write pipeline as everything else.

## CLI

```bash
mindvault session start --project Alpha --task "harden search"
# → prints the context pack; creates Log - Alpha.md on first use

mindvault session log --project Alpha --summary "index schema decided"
# → optional mid-session breadcrumb (#### entry). Use sparingly.

mindvault session end --project Alpha --summary "weighted search shipped" \
    --tests "dotnet test green (180)" --followups "tune recency boost"
# → dated ### handoff block: summary, tests, follow-ups
```

## MCP

- `mindvault_start_session(project, task?)` → `{ pack, logNote, logNoteCreated }`
- `mindvault_end_session(project, summary, tests?, followUps?)` → handoff written

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
- Real follow-ups become tasks (`mindvault_create_task`), not log lines; real decisions get
  captured properly. The handoff references outcomes — it isn't the storage for them.
