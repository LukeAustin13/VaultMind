---
name: mindvault-decision-capture
description: Capture a durable technical, product, architecture, dependency, schema, tooling/MCP, or safety decision into the MindVault vault. Use when a real decision has been made during the session (chose X over Y, locked in a schema, adopted or dropped a dependency, set a safety rule) - not for routine implementation details.
---

# MindVault: Capture a Decision

Record decisions that someone will need to understand or revisit months from now.

## Trigger conditions

Use when a real decision was made this session: technology and dependency choices,
architecture and schema decisions, API contracts, safety/permission rules, deliberate
trade-offs, reversals of earlier decisions.

Do NOT use for: variable naming, small refactors, formatting, one-line fixes, or anything
already obvious from the code itself.

## Required workflow

1. Identify the project (from the conversation, repo name, or `mindvault_get_project_context`).
   If the project note does not exist, create it first with `mindvault_create_project`.
2. **Run `mindvault_check_draft`** (`type: "decision"`, project, proposed title) BEFORE
   creating. It reports exact duplicates (blockers), near-duplicates (warnings) and
   possibly-conflicting older decisions (suggestions with paths). Heed it:
   - Blocker → update the existing note instead of duplicating.
   - "Possibly related decision … use the supersede operation" → if the new decision
     replaces the old one, create the new decision and then call
     `mindvault_supersede_decision` (old ref, new ref). That sets `status: superseded`
     and cross-links both notes safely — never flip statuses by hand.
3. Create the decision with `mindvault_create_decision` (project + short imperative title,
   e.g. "Use SQLite FTS5 for search"). The create itself REFUSES likely duplicates
   (`reason: "possible_duplicate"` with candidate paths): read the candidates and update or
   supersede the existing decision instead. Pass `allowDuplicate: true` only when the user
   has confirmed the new note is genuinely distinct — never to silence the check.
4. Fill the sections with `mindvault_append_to_note` on the new note — one call per section,
   a few sentences each:
   - **Context** — the situation and forces at play
   - **Decision** — what was decided, stated plainly
   - **Rejected Alternatives** — what lost, and why (one line per alternative)
   - **Reasoning** — why this option won
   - **Consequences** — what this makes easier/harder
   - **Reversal Conditions** — what would make us revisit it (do not skip this)
5. The create tool already links the decision to the project. Add further
   `mindvault_link_notes` calls only when the decision directly relates to another decision
   or task — `mindvault_find_related` on the new note shows what is worth linking.

Expected final behaviour: one compact decision note with all six sections filled, correctly
linked, with any replaced decision superseded — readable cold in six months.

## Do not

- Do not record more than one decision per note; split unrelated decisions.
- Do not skip Reversal Conditions — a decision without them cannot be safely revisited.
- Do not flip a replaced decision's status by hand — use `mindvault_supersede_decision`.
- Do not write essays; this is a record, not a design document.

## Efficiency rules

- One `mindvault_check_draft`, one create, ~6 short appends — that is the whole budget.
- Write for a future reader with no session context; a few sentences per section.
- If the draft check finds the decision already exists, update it and stop.

## Safety rules

- Use only the `mindvault_*` MCP tools — never write vault files directly or via shell.
- Every write goes through the snapshot-first pipeline; never work around a failed write by
  other means — report it.
- Superseding touches two notes atomically-with-rollback; never replicate it manually.
