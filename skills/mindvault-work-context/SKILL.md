---
name: mindvault-work-context
description: Pull the vault memory that touches the exact thing being worked on — a source file, a task description, or a note — before making risky edits. Use when starting to modify unfamiliar code, before changing behaviour a decision might govern, or when the user asks "what do we know about X?".
---

# MindVault: Work Context

Ask the vault what it knows about the thing in front of you, before you change it.

## Trigger conditions

Use when:
- About to modify a file or subsystem you have not touched this session.
- The change might collide with a recorded decision, constraint or known mistake.
- The user asks what is known/decided/risky about a specific area.
- Continuing work after a gap — pair it with `mindvault_recall` for the time window and
  `mindvault_recent_sessions` for where the last session stopped.

Do NOT use for: general project orientation (that is the capsule/session start), or when
this session already loaded work-context for the same file/area.

## Required workflow

1. One call: `mindvault_get_work_context` with the project and exactly ONE of:
   - `currentFile` — the source path you are editing (best for risky edits),
   - `query` — a few words describing the work,
   - `note` — a note reference to expand from.
2. Read the reasons, not just the titles — every result says why it matched (file tokens,
   type relationship, pinned, status). Trust high-reason results first.
3. Read at most 1–3 of the suggested reads with `mindvault_read_note`.
4. Respect what you find: decisions in force are binding, known mistakes are do-not-repeat
   rules, constraints are non-negotiable. Conflicts get flagged to the user, not coded over.
5. Close the loop with feedback when a result was clearly right or wrong:
   `mindvault_record_feedback` (`useful` / `noisy` / `outdated`, with a reason). Pin the
   note you keep coming back to; hide what keeps polluting results. This is how retrieval
   gets better — but never hide or pin in bulk.

Expected final behaviour: the edit proceeds informed by the 2–3 notes that actually govern
it, and retrieval got one honest feedback signal richer.

## Do not

- Do not pass more than one input — the tool refuses to guess which you meant.
- Do not fall back to vault-wide `mindvault_search` sweeps when work-context returns
  little; little usually means the vault genuinely has nothing — say so.
- Do not record feedback signals you cannot justify with a reason.
- Do not treat suggested reads as required reads; the budget is 1–3.

## Efficiency rules

- One work-context call replaces three searches plus manual triage.
- The reasons ARE the triage — do not re-read every result to decide relevance.
- Scope follow-up reads: `mindvault_read_note` takes `section` and `maxChars` — pull the
  one section you need, not the whole note. Stop reading once the governing decision,
  constraint or mistake is known.
- For a goal broader than one file, `mindvault_build_route_card` /
  `mindvault_build_read_plan` bound the whole read budget up front.
- Feedback is one call, recorded at the moment you know the verdict, not batched later.

## Safety rules

- Use only the `mindvault_*` MCP tools — never read vault files directly or via shell.
- Work-context is read-only; feedback writes only to the operational sidecar, never to
  vault Markdown.
- Hidden notes stay hidden for everyone — hide only what is genuinely noise, and say why
  in the reason.
