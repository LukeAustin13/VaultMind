---
name: task-continuity
description: Use this skill to save in-progress task state at the end of a session and resume it at the start of the next. Invoke it when wrapping up a session with work still in progress, or at the start of a session to find out what was left unfinished. It does not store general project notes (use the memory system directly) or plan new work (use planner). Also use it DURING a long in-flight task to keep working memory on disk so progress survives a context reset (Live Working-Memory Mode).
license: MIT
metadata:
  stack: agnostic
  version: 1.2
  last-reviewed: 2026-07-03
---

# Task Continuity

## Use When

- Wrapping up a session with one or more tasks still in progress.
- Starting a new session and asking "what was I working on?" or "pick up where we left off".
- A task is paused mid-way and needs to be resumed in a future session.
- Marking a task as completed, blocked, or abandoned at the end of a session.
- User says "save my progress", "save session state", "resume last task", or similar.

## Do Not Use When

- The task is completed within the same session — no handoff is needed.
- The user wants to plan new work — use **planner**.
- The user wants project-level notes that belong in general memory — use the memory system directly.
- The task is trivial (a single question, a one-off lookup) — not worth preserving.

## Two Modes

This skill operates in two modes, selected based on context:

- **Session End** — Save the state of in-progress tasks to memory before the session closes.
- **Session Start** — Retrieve in-progress tasks from memory and help the user resume.

---

## Live Working-Memory Mode

Distinct from the Session End/Start modes above, which handle cross-session save and resume. This mode keeps working memory on disk and updates it *during* a single long in-flight task, so progress survives a context reset within the same session.

Use it when a task is large enough that the work itself risks outrunning the context window — not at the end, but throughout execution.

### 1. Open a live task file

Create `task-[slug].md` at the start of the task and keep it open as the working record. Seed it with:

- **Goal** — what this task must accomplish, in one or two sentences.
- **Plan / checklist** — the phases or steps, as a checklist to tick off.
- **Findings** — facts learned that the work depends on (file paths, decisions, constraints).
- **Progress** — current phase and what is done so far.

### 2. Apply the 2-Action Rule

After roughly every two read or search actions, append what was learned to the file. Keep entries short and factual. The point is that if context resets right now, the file alone is enough to continue — do not batch updates until the end.

### 3. Reread before significant decisions

Before any significant decision or the start of a new phase, reread the Goal and Plan from the file. This anchors the work to the original intent rather than to a drifting in-context summary.

### 4. Update status after each phase

When a phase completes, mark it done in the checklist and update the Progress section. Record any new findings the phase produced.

### 5. Completion check

Before declaring the task done, walk the checklist: every item must be both done and verified. If any item is unchecked or unverified, the task is not complete.

### Quality Bar (Live Working-Memory Mode)

- The live file is current enough that a cold reader could resume from it after a context reset with no other input.
- Updates land during execution under the 2-Action Rule — not deferred to the end where a reset would lose them.

For minimising token and context spend during such long tasks, pair this with **token-economy**.

---

## Session End Process

### 1. Identify tasks worked on this session

Review the conversation to identify tasks that were worked on but not completed:

- Check any active `TaskList` entries from this session.
- Identify tasks that were discussed, partially implemented, or blocked.
- Exclude: completed tasks, trivial lookups, one-off questions.

### 2. For each task, capture state

For each in-progress or blocked task, gather:

- **Goal** — what this task is trying to accomplish and why.
- **Status** — `in-progress` or `blocked`.
- **Accomplished** — what was done in this session (bullet points).
- **Next Steps** — the exact next actions needed to continue, specific enough to start without further conversation.
- **Blockers** — anything preventing progress (if status is blocked).
- **Key Files** — file paths that were read, modified, or are central to the task.
- **Context** — any non-obvious background: design decisions made, options ruled out, constraints discovered.

### 3. Write or update task memory files

For each task, write a memory file to the project's memory directory. Use the template at `templates/task-memory.md`. Name the file `task-[slug].md` where the slug is short and descriptive (e.g., `task-auth-rewrite.md`, `task-order-api-v2.md`).

The description frontmatter field must include the status so MEMORY.md entries stay informative without opening every file:

```
description: Refactoring session token storage for compliance — status: in-progress
```

Confirm each memory file was actually written (read it back or list the directory) before reporting the session state as saved.

### 4. Update MEMORY.md

Add or update the entry in `MEMORY.md` for each task memory file written or modified:

```
- [Task: Auth middleware rewrite](task-auth-rewrite.md) — in-progress: refactoring session token storage for compliance
```

When a task is completed or abandoned, update the MEMORY.md entry to reflect that:

```
- [Task: Auth middleware rewrite](task-auth-rewrite.md) — completed: session token storage rewrite done
```

### 5. Report what was saved

Tell the user:

- Which tasks were saved and their status.
- The next steps recorded for each.
- Any task that was marked completed or abandoned this session.
- Any task that was skipped and why.

---

## Session Start Process

### 1. Scan MEMORY.md for open tasks

Look in `MEMORY.md` for any entries linking to files named `task-*.md`. These are task memory files.

### 2. Read each open task file

For each task file found, read it and check:

- **Status** — skip tasks with status `completed` or `abandoned`.
- **Last Session** — note the date. If it was weeks ago, flag that context may be stale.
- **Next Steps** — what was recorded when the session ended.
- **Blockers** — whether the blocker is still likely to apply.

### 3. Present open tasks to the user

Present each open task clearly:

```
Open Tasks from Previous Sessions

1. [Task title]
   Status: in-progress
   Last session: YYYY-MM-DD
   Next steps:
   - [step one]
   - [step two]
   Key files: [list]

2. ...
```

If there are no open tasks, say so briefly and proceed.

### 4. Confirm which to resume

Ask the user which task to resume, or whether they are starting something new.

Do not begin executing — confirm first.

### 5. Load task context

Once the user selects a task:

- Verify that key files listed in the memory file still exist — if any are missing or renamed, note it.
- Re-read the key files most central to resuming the work.
- Surface design decisions and constraints from the Context section.
- Confirm the next steps still make sense given the current codebase state.

---

## Task Lifecycle

```
new → in-progress → completed
                 → blocked → in-progress → completed
                 → abandoned
```

- **new**: Task identified but not started. Do not save yet — wait until work begins.
- **in-progress**: Active work has started. Save at the end of any session where progress was made.
- **blocked**: Progress stopped due to a dependency, decision, or external factor. Capture the blocker explicitly.
- **completed**: Task is done. Update the status in the memory file and the MEMORY.md entry.
- **abandoned**: Task will not be completed. Update the status but keep the file — it may have useful context.

Remove a task entry from MEMORY.md only when it has been completed or abandoned and the entry would genuinely mislead rather than inform.

---

## Memory File Format

See [templates/task-memory.md](templates/task-memory.md) for the full file format. The frontmatter description must carry the current status so the index stays scannable.

---

## Output Format

### Session End

```
Session State Saved

Saved (in-progress): - [Task title] → [memory file path] — next steps: [list]
Saved (blocked): - [Task title] → [memory file path] — blocked by: [description]
Completed this session: - [Task title]
Not saved: - [Task excluded and why]
```

### Session Start

The task list from step 3 of the Session Start process, ending with:

```
Which task would you like to resume, or are you starting something new?
```

---

## Quality Bar

- Next steps are specific enough to start immediately without further conversation.
- Context captures decisions and constraints — not what the code does (that is derivable from the code).
- Stale blockers are flagged when a task was blocked and the blocker may since have resolved.
- Completed tasks are marked completed, not left as in-progress.
- MEMORY.md entries are updated, not just created — no duplicates.
- Key files are verified to still exist when resuming before being cited.
- Session start presents options and confirms before resuming execution.

## Failure Modes To Avoid

- **Saving every conversation.** Only save genuine multi-session work. Not every question is a task.
- **Vague next steps.** "Continue the implementation" tells the next session nothing. Name the method, file, and action.
- **Stale in-progress tasks.** A memory full of tasks that were actually finished days ago loses trust. Keep status current.
- **Saving code in memory.** Record file paths and what role they play — not the code itself.
- **Resuming without confirming.** Present open tasks and ask — do not silently begin executing.
- **Treating memory as current truth.** Files may have been renamed, deleted, or changed. Verify before acting.
- **Orphaned memory files.** If a task is removed from MEMORY.md, its file still exists and wastes index space next time. Update or remove both together.
