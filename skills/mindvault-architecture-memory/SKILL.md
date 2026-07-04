---
name: mindvault-architecture-memory
description: Persist durable architecture knowledge (component structure, data flow, boundaries, trade-offs) into the MindVault vault when it is discovered or changed - after designing a subsystem, mapping unfamiliar code, or making a structural change others must understand. Not for code-level details that belong in the repo.
---

# MindVault: Architecture Memory

Keep the vault's picture of the system true.

## Trigger conditions

Use when structural knowledge was discovered or changed: component responsibilities and
boundaries, data flow between parts, integration points, deliberate structural trade-offs,
"why it is shaped this way" knowledge invisible in any single file.

Do NOT use for: function signatures, file listings, anything `git log` or the code itself
already answers, or speculative designs nobody approved.

## Required workflow

1. Load `mindvault_get_project_context` — check `relevantArchitecture` for an existing
   architecture note before writing anything new.
2. **Existing architecture note?** Append the new knowledge to the right section of that
   note with `mindvault_append_to_note` (typical sections: Overview, Components, Data Flow,
   Key Decisions, Known Trade-offs). Update, don't fork.
3. **No architecture note, small system?** Use the project note's `"Architecture"` section:
   `mindvault_append_to_note` with a compact block — components as bullets, one line each,
   plus data-flow arrows in text (`CLI/MCP -> Core services -> SQLite index`).
4. Structural knowledge earned through a decision ("split the importer into its own
   service because …") is a decision: run `mindvault_check_draft`, then
   `mindvault_create_decision`, and reference it from the architecture text via a
   `[[wiki link]]` in the appended content or `mindvault_link_notes`.
5. When a structural change makes recorded architecture wrong, fix the note the same way —
   append a dated correction to the relevant section rather than leaving the stale text
   uncontradicted. Flag the contradiction to the user if it is surprising.

Expected final behaviour: one true, compact system picture per project — bullets and
arrows a future agent absorbs in one read, corrections dated so the newest statement wins.

## Do not

- Do not fork a second architecture note when one exists — update it.
- Do not scatter sub-system detail across new notes; it goes in sections of the one picture.
- Do not write prose walls — bullets and arrows.
- Do not leave stale architecture text uncontradicted after a structural change.

## Efficiency rules

- One context load, one or two appends. If it takes more calls, the content is too granular.
- Date corrections ("2026-07-04: importer now writes through the queue, not directly")
  so readers can see which statement wins without archaeology.

## Safety rules

- Use only the `mindvault_*` MCP tools — never write vault files directly or via shell.
- Architecture claims must reflect code you actually saw, not assumptions.
- Decisions embedded in architecture go through the draft check and decision tools, keeping
  the decision graph consistent.
