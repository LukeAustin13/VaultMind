# Daily Workflow

How to run MindVault as your project brain across all repos, without ceremony that doesn't
pay for itself.

## The standing setup

- **The Pi hosts MindVault** (Docker, HTTP MCP, bearer token, LAN-only). It is the one
  authoritative AI writer for the vault.
- **Claude Code connects over HTTP** (`mcp.json` per [MCP_SETUP.md](MCP_SETUP.md)) — or
  stdio against a local checkout when the Pi is off.
- **Obsidian stays the human UI.** You read, edit and think in Obsidian; agents work
  through the safe tools; sync (Syncthing/iCloud/…) moves the Markdown between machines.
- **Skills installed** in `~/.claude/skills/` so every project gets the same discipline.

## Per session

**Start of a coding session:**
```
session start (mindvault_start_session) — project + one line of task intent
```
One call: goal, non-negotiables, decisions in force, task-relevant notes, warnings,
`doNotForget`. Read 1–5 of the recommended notes at most. If the pack's warnings matter
today (stale task you're about to duplicate, contradicted decision), deal with them first.

**During the session — capture only what is durable:**
- A real decision was made (chose X over Y, locked a schema) → draft-check, then
  `mindvault_create_decision`; if it replaces an old one, `mindvault_supersede_decision`.
- A real follow-up emerged that outlives the session → draft-check, then
  `mindvault_create_task`.
- Everything else stays in the repo/commit messages. When in doubt, don't write a note.

**End of the session:**
```
session end — summary + tests (honestly: "not run" is valid) + follow-ups
```
That single block is what makes the next session start warm.

## Weekly (5 minutes)

```
mindvault validate      # severity-graded health report
mindvault doctor        # environment: writability, index schema, Docker, MCP env
mindvault index verify  # cache drift check (rebuild only if it says so)
```
Fix criticals, skim warnings, archive dead tasks (deliberately, per note). `prune` old
snapshots when `.mindvault/` feels heavy; `backup` before anything scary.

## Before a big refactor

1. `context "<project>" --deep` — the whole picture, including contradictions.
2. `decision graph --project "<project>"` — what's in force and what superseded what.
3. Refactor. Capture the decisions the refactor makes; supersede the ones it kills.

## Before a release

1. Follow [RELEASE_CHECKLIST.md](RELEASE_CHECKLIST.md) for MindVault itself.
2. For your product projects: review the project's open risks and decisions
   (`context-pack` + `decision list`), close what shipped, archive what died.

## What NOT to do

- Don't let agents log every breadcrumb — the handoff entry is the record; `session log`
  is for rare mid-session milestones.
- Don't run two writing MindVault instances against one vault ([SYNC_AND_CONCURRENCY.md](SYNC_AND_CONCURRENCY.md)).
- Don't hand-edit statuses that have tooling (supersede, archive) — the tooling keeps the
  graph consistent.
- Don't expose the HTTP endpoint beyond the LAN. Ever.
