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
One call returns a budgeted brief: goal, non-negotiables, decisions in force, do-not-repeat
rules, open/blocked tasks, risks, constraints, a token-priced read-first / do-not-read list,
and `deltaSinceLastHandoff` (what changed since your previous handoff). Read the readFirst
notes at most — scope each read with `mindvault_read_note`'s `section` / `maxChars`. If the
brief's warnings matter today (stale task you're about to duplicate, contradicted decision),
deal with them first. The brief replaces calling `build_context_capsule` + `build_route_card`
separately at the start; both remain for a mid-session refresh.

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
That single block is what makes the next session start warm. Over MCP,
`mindvault_end_session` can also batch the decisions, mistakes and tasks that surface right at
the close into the same call — each runs the same duplicate and content gates as the
standalone tools, so honest closing drops from 5–8 calls to one.

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
