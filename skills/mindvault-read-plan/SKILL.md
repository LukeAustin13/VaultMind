---
name: mindvault-read-plan
description: Follow a strict, ordered read plan (max 5 reads, explicit stop conditions) instead of exploratory searching. Use when starting a concrete goal in a MindVault repo and tool-call discipline matters more than a briefing.
---

# MindVault: Read Plan

The route card is the briefing; the read plan is the itinerary. Follow it literally:
ordered steps, one purpose per read, and a stop condition that ends the reading. It is a
mid-session discipline tool — the `mindvault_start_session` brief already gives a read-first
list to start from; reach for a plan when a fresh goal needs a strict ordered path.

## Trigger conditions

Use when:
- Mid-session, a concrete goal or current file is known and you want the minimum tool-call
  path.
- Previous sessions in this repo drifted into 10+ vault reads.
- You are about to modify code and only need constraints/risks/do-not-repeat rules.

Do NOT use for: open-ended exploration of an unfamiliar vault (use the route card or the
hub's map block first), or anything that writes.

## Required workflow

1. One call: `mindvault_build_read_plan` with the project and `goal` or `currentFile`.
2. Execute the steps IN ORDER with `mindvault_read_note` — each step names the note, the
   reason, and what the read should give you. If a step's `expectedUse` is already
   satisfied, skip that step; never substitute a different note.
3. After every read, check `stopWhen`: the moment all stop conditions hold, STOP READING
   and start the work.
4. Honour `doNotRead` for the whole session, not just during the plan.
5. If the plan's reads genuinely leave the goal unclear, use the plan's single fallback
   search (already narrowed) — not a fresh vault-wide sweep. Run it with `snippetChars: 0`
   for refs-only hits, then scope the follow-up read.

Expected final behaviour: at most 5 reads, executed in order, ended by a stop condition
— then real work with the saved context window.

## Do not

- Do not reorder, extend or "complete" the plan once the stop conditions hold.
- Do not read the whole vault or any `doNotRead` note to be thorough — thorough is what
  the plan already was.
- Do not run repeated searches between steps; the fallback search is the only sanctioned
  one and it comes last.
- Do not treat maxReads as a target — stopping at 2 reads is success, not laziness.

## Efficiency rules

- The project hub (with its map block) comes first in every plan because one such read
  replaces several raw-note reads.
- Scope each read: pass `section` or `maxChars` to `mindvault_read_note` when the step
  only needs one part of the note.
- The stop conditions are the budget: reads after the stop line are pure token waste.

## Safety rules

- Use only the `mindvault_*` MCP tools — never read vault files directly or via shell.
- Read plans are read-only artifacts; executing one must not mutate the vault.
- If two steps contradict (a decision vs a newer mistake rule), flag it to the user
  instead of picking silently.
