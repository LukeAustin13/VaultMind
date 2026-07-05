---
name: mindvault-project-context
description: Load concise project memory (goal, constraints, decisions, tasks, risks, warnings) from the MindVault vault via MCP before coding, debugging, refactoring, reviewing, or resuming earlier work. Use at the start of a substantial work session on a project, or whenever the user refers to prior decisions or context that is not in the repo.
---

# MindVault: Load Project Context

Pull just enough durable project memory out of MindVault to work correctly, then get on with
the actual task.

## Trigger conditions

Use when:
- Starting substantial work (coding, debugging, refactor, review) on a project that has a vault.
- The user refers to prior decisions, constraints, or context that is not in the repo.
- Resuming work after a break or a different session.

Do NOT use when:
- The question is answerable from the repo alone (code, git history, README).
- You already loaded context for this project in this session — reuse it.
- The task is a one-line edit with no decision surface.

## Required workflow

1. Call `mindvault_status`. If it fails, say that the MindVault MCP server is not configured
   (see `docs/MCP_SETUP.md` in the MindVault repo) and continue the task without vault context.
2. Identify the project: call `mindvault_detect_project` with the repo folder name (or what
   the user called it). It resolves exact titles, declared aliases and repo names with a
   confidence tier and returns candidates instead of guessing — if it returns candidates,
   pick with the user; do not assume.
3. **If you are about to do real work, prefer `mindvault_start_session`** (project + a
   one-line task description): it returns the full context pack AND sets up the session log
   for your handoff later. For a quick look without a session, call
   `mindvault_get_context_pack` (with the task description) or
   `mindvault_get_project_context` (`detailLevel: "brief"` for a glance, `"deep"` when the
   project is unfamiliar). For a mode-shaped briefing (debugging, review, planning…) under
   a hard char budget, `mindvault_build_context_capsule` — it adds do-not-repeat rules and
   superseded-decision warnings. Before editing a specific file, `mindvault_get_work_context`
   with that file path finds the memory that touches it.
3.5. **Before any broad search or note-list sweep, get a route:**
   `mindvault_build_route_card` (project + your goal) returns the 3–5 notes to read first
   with reasons and token estimates, plus an explicit do-not-read list; for strict
   tool-call discipline, `mindvault_build_read_plan` turns that into ordered steps with a
   stop condition. `mindvault_get_project_map` returns the hub's map block — the single
   cheapest orientation read.
4. If detection found nothing, try `mindvault_search` with one to three likely name variants
   (optionally `type: "project"`). If still nothing: ask the user, or continue without vault
   context and mention that a project note can be created (`mindvault_create_project`) —
   ideally with `aliases:`/`repoNames:` frontmatter so detection works next time.
5. Read at most **1–5** specific notes with `mindvault_read_note` — start from the pack's
   `recommendedNextReads`, which are ordered by importance. For a structural overview of an
   unfamiliar project, the hub's map block (`mindvault_get_project_map`, or
   `mindvault_list_maps` then read the hub) is the cheapest orientation there is — prefer it
   over `mindvault_list_notes` sweeps.
6. Take the pack's `warnings` seriously (stale tasks, contradictory decisions, duplicates) —
   mention relevant ones to the user rather than silently working around them.
7. Continue the coding task using that context. Do not silently contradict an accepted
   decision or constraint — if the task requires it, flag the conflict to the user first
   (and see the decision-capture skill about superseding). If the work surfaces memory you
   are NOT sure is true or durable, park it with `mindvault_capture_thought` (see the
   organisation skill) instead of writing a doubtful decision or memory note.

Expected final behaviour: a few bullets of loaded context, warnings surfaced, and the real
task under way — not a context essay.

## Do not

- Do not list or read the whole vault, ever.
- Do not re-fetch context you already have in this session.
- Do not contradict accepted decisions or constraints silently.
- Do not turn this into a report; the context serves the task.

## Efficiency rules

- One context-pack call first; search only as a fallback (`explain: true` on
  `mindvault_search` shows why results ranked as they did, if retrieval looks off).
- Read 1–5 notes maximum; scope big reads with `mindvault_read_note`'s `section` or
  `maxChars` instead of pulling whole bodies.
- Respect route-card `doNotRead` guidance — archived/superseded/hidden notes are flagged
  so you never spend tokens on them.
- Stop gathering the moment you have enough context to act.
- Keep the summary to a few bullets; this skill exists to save context, not fill it.

## Safety rules

- Access the vault only through the `mindvault_*` MCP tools (some clients show them as
  `mcp__mindvault__mindvault_*`) — never via file tools or shell commands.
- This skill is read-only: it creates and modifies nothing.
- If MCP is unavailable, degrade gracefully — say so and continue without vault context.
