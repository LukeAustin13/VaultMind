---
name: mindvault-task-sync
description: Create, update, and close durable project tasks in the MindVault vault while implementing. Use when work surfaces a real follow-up that outlives the session, when starting a tracked task (mark it active), or when finishing one (mark it done). Not for TODO-comment-sized items.
---

# MindVault: Sync Tasks

Keep MindVault's task notes in step with real implementation work.

## Trigger conditions

Use when:
- Work surfaces durable, actionable follow-up someone could pick up in a later session:
  a feature slice, a migration, a fix that is out of scope right now, a review follow-up.
- You start working on an already-tracked task (mark it `active`).
- You finish tracked work (mark it `done` with a one-line result).

Do NOT use for: sub-30-minute chores you are about to do anyway, vague ideas
("improve performance"), TODO-comment-sized items, or duplicates of existing tasks.

## Required workflow

1. Identify the project (`mindvault_get_project_context`). If the project note is missing,
   create it with `mindvault_create_project` first.
2. **Run `mindvault_check_draft`** (`type: "task"`, project, proposed title) before creating.
   It catches duplicates and near-duplicates (update those instead) and flags titles too
   vague to act on later — rename rather than argue with it.
3. New durable task → `mindvault_create_task` (project + short actionable title, e.g.
   "Add retry to sync client"). Then use `mindvault_append_to_note` to fill
   **Description** and **Acceptance Criteria** — a few lines each. Use **Context** for the
   why and **Status Notes** for progress remarks.
4. Status changes → `mindvault_update_frontmatter` on the task note with `key: "status"`:

   | Status | Meaning |
   | --- | --- |
   | `open` | captured, not started |
   | `active` | being worked on now |
   | `blocked` | waiting on something — say what, in Status Notes |
   | `done` | finished and verified |
   | `cancelled` | deliberately not doing — say why, in Status Notes |
   | `archived` | set by `mindvault_archive_note` when tidying old tasks |

5. Link the task to a related decision or note with `mindvault_link_notes` when the
   connection matters (task implements decision X, task follows from review Y).
6. When you finish tracked work in a session: set the task `done` and add a one-line result
   to its **Status Notes** section via `mindvault_append_to_note`.

Expected final behaviour: the vault's open-task list matches reality — no phantom open
tasks, no untracked durable follow-ups, no near-duplicate twins.

## Do not

- Do not create a task without running the draft check first.
- Do not create a near-duplicate — update the existing task instead.
- Do not batch several outcomes into one task; one outcome per task. Micro-items go in the
  parent task's Status Notes.
- Do not archive tasks unless the user asked for a cleanup.

## Efficiency rules

- Check → create → two short appends; a task costs four calls, not ten.
- Status flips are one `mindvault_update_frontmatter` call — no re-reading the whole note.
- If the draft check blocks, stop and update the existing note; do not negotiate with it.

## Safety rules

- Use only the `mindvault_*` MCP tools — never write vault files directly or via shell.
- Never delete: closing is a status change, tidying is `mindvault_archive_note` (reversible),
  and only when the user asks.
- Say what blocked a task in its Status Notes — a bare `blocked` status helps nobody.
