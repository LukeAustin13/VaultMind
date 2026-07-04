---
name: mindvault-organisation
description: Keep the MindVault vault organised, linked and navigable without being destructive. Use when notes look misfiled, when the inbox has unpromoted thoughts, when a project needs a map, when links/orphans need repair, or during a periodic tidy-up the user asked for. Suggest first, apply only with approval.
---

# MindVault: Organise the Vault

Make the vault cleaner, more connected and easier for a human to read in Obsidian —
using proposals and previews, never bulk rewrites.

## Trigger conditions

Use when:
- The user asks to tidy, organise or review vault structure.
- You notice misfiled notes (a decision in the inbox, a risk in the wrong folder).
- The inbox holds raw thoughts that have been confirmed as durable knowledge.
- A project has grown and needs a navigation map, or its map is stale.
- Search keeps missing things because links, aliases or frontmatter are poor.

Do NOT use for: archiving sweeps the user did not ask for, renaming files by hand, or
"improving" human-written notes.

## Required workflow

1. **See before touching.** Run `mindvault_organize_vault` (dry-run is the default) and read
   the proposals and their reasons. Uncertain notes appear under needsReview — those are for
   the human, not for you.
2. **Apply only with approval.** Only pass `apply: true` after the user has seen the
   proposals or has explicitly asked for an auto-tidy. Every move is snapshotted first and
   reversible with `mindvault_status`-visible snapshots (restore via the CLI).
3. **Capture uncertainty as thoughts, not memory.** When you are not sure something is true
   or durable, `mindvault_capture_thought` — it lands in the agent inbox, out of context
   packs' way. Never write a doubtful "fact" as a decision or memory note.
4. **Promote deliberately.** When a thought is confirmed, `mindvault_promote_note` with the
   right target type and project. It validates fields, refuses duplicates, preserves the
   content and files the note correctly. Never auto-promote thoughts in bulk.
5. **Link with reasons.** Run `mindvault_suggest_links` for the note or project; each
   suggestion carries a reason and confidence. Apply the ones that make sense with
   `mindvault_link_notes` — one at a time, never blindly all of them.
6. **Keep maps fresh.** After meaningful memory changes, `mindvault_rebuild_map` (or
   `mindvault_create_map` the first time). Read a map via `mindvault_list_maps` +
   `mindvault_read_note` for a compact project overview instead of listing the vault.
7. **Audit periodically.** `mindvault_find_broken_links`, `mindvault_find_orphans`,
   `mindvault_audit_frontmatter` and `mindvault_audit_aliases` report problems with a
   proposed fix each. Fix what is mechanical (frontmatter values, missing links) with the
   normal tools; report the rest to the user.
8. **Compile the navigation layer.** `mindvault_compile_brain` (dry-run by default) builds
   everything agent-facing in one pass: maps, generated summaries for large notes
   (`mindvault_generate_summaries`), the typed link graph (`mindvault_build_graph`) and
   the health/score reports. `mindvault_organisation_score` says WHY the vault is or is
   not well organised — its weaknesses list is the prioritised tidy-up plan, and
   `mindvault_token_audit` shows the token cost of leaving it messy.

Expected final behaviour: every note has an obvious home, thoughts and durable memory stay
distinct, maps mirror reality, and a human opening Obsidian understands the structure.

## Do not

- Do not move notes without a dry-run first and user approval for the apply.
- Do not bulk-move, bulk-link or bulk-promote anything.
- Do not promote a thought into durable memory unless it was confirmed.
- Do not apply link suggestions without reading their reasons.
- Do not touch the generated block markers in map notes; human text outside them is sacred.
- Do not create deep folder hierarchies — the placement policy is deliberately shallow.
- Do not delete anything; archiving via mindvault_archive_note is the only removal, and only
  when the user asks.

## Efficiency rules

- One `mindvault_organize_vault` dry-run tells you everything misplaced — do not walk the
  vault with `mindvault_list_notes` looking for problems.
- Maps are the cheap orientation read: one note instead of a dozen queries.
- Audits return proposals — act on them directly instead of re-diagnosing.
- A capture is one `mindvault_capture_thought` call; a promotion is one
  `mindvault_promote_note` call. No read-modify-write chains.

## Safety rules

- Use only the `mindvault_*` MCP tools — never write vault files directly or via shell.
- Dry-run by default; apply is an explicit, user-approved step.
- Everything the organiser changes is snapshotted first and restorable.
- Never guess a project: detection and promotion refuse ambiguity — pass the project the
  user confirmed.
- Leave needsReview items to the human; they are flagged precisely because automation would
  guess wrong.
