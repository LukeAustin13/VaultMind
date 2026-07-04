---
name: mindvault-mistake-ledger
description: Record durable lessons in the MindVault mistake ledger when something went wrong in a way that could repeat — wrong assumption, broken workflow, misused API, bad default. Use when a mistake cost real time and has a statable prevention rule. Not for routine bugs or one-off typos.
---

# MindVault: Mistake Ledger

A mistake that taught something is memory; record the lesson once, and future sessions
inherit it as a do-not-repeat rule.

## Trigger conditions

Use when:
- A mistake this session cost real time AND a future agent could plausibly repeat it.
- The user says "remember not to do that again" (or words to that effect).
- A review/postmortem produced a concrete prevention rule.
- You start work in an unfamiliar area: `mindvault_list_mistakes` first — the ledger is
  the cheapest way to not repeat history (capsules surface it automatically).

Do NOT use for: routine bugs you fixed in minutes, one-off typos, failures without a
prevention rule, or anything already recorded (near-duplicates are refused).

## Required workflow

1. Check first: `mindvault_list_mistakes` for the project — if the lesson exists, append
   new evidence to that note with `mindvault_append_to_note` instead of duplicating.
2. Record: `mindvault_add_mistake` with a short title, the `lesson` (what happened and
   why), and the `prevention` rule (the sentence a future agent must obey). The create
   refuses near-duplicates — heed it; `allowDuplicate: true` only with explicit user
   confirmation.
3. Fill depth only if it earns its keep: `mindvault_append_to_note` on **What Happened**
   and **Root Cause** — a few lines each, written for a reader with zero session context.
4. Link the mistake to what it relates to (the decision it contradicts, the task that
   prevents it) — `mindvault_suggest_links` proposes these with reasons; apply the real
   ones with `mindvault_link_notes`. If prevention needs real work, create that task via
   the task-sync skill and link it.
5. When a lesson stops applying (the API changed, the workflow is gone):
   `mindvault_resolve_mistake` — it leaves the ledger's history intact but stops
   appearing in capsules and do-not-repeat lists. Never archive-to-forget a mistake.

Expected final behaviour: one compact lesson with a prevention rule, linked to its
context, surfacing automatically in every future capsule until deliberately resolved.

## Do not

- Do not record a mistake without a prevention rule — a lesson you can't act on is noise.
- Do not duplicate an existing lesson; strengthen the existing note.
- Do not resolve a mistake because it is embarrassing; resolve it because it no longer applies.
- Do not write blame narratives — record mechanism, not fault.

## Efficiency rules

- One list-check, one add, at most two short appends — a lesson costs four calls.
- The `lesson` and `prevention` parameters make the note useful at creation; the appends
  are optional depth, not a requirement.

## Safety rules

- Use only the `mindvault_*` MCP tools — never write vault files directly or via shell.
- The content gate applies: never paste secrets or tokens into a lesson; describe them.
- Resolving is a status change through the safe pipeline; the ledger is never deleted.
