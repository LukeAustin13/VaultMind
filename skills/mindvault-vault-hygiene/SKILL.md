---
name: mindvault-vault-hygiene
description: Check MindVault health, diagnose indexing/schema/link problems, and recommend safe cleanup. Use when vault search results look stale or wrong, when the user asks for a vault health check or cleanup, or after large external edits to the Obsidian vault.
---

# MindVault: Vault Hygiene

Diagnose first, report clearly, change nothing without permission.

## Trigger conditions

Use when:
- Vault search results look stale or wrong.
- The user asks for a vault health check or cleanup.
- Large external edits happened in Obsidian or through sync.

Do NOT use as a ritual before every session — `mindvault_health` inside other workflows
covers the quick check; this skill is the deep diagnosis.

## Required workflow

1. `mindvault_health` — the `verdict` field is the headline: `good` / `warning` /
   `critical`. If it is `good` and the user only wanted reassurance, report that and stop.
2. `mindvault_status` — note count, `rescanPending`, last scan time. An old scan time after
   known external edits explains "stale search" on its own.
3. `mindvault_validate_vault` — collect the severity counts and issue list:
   - **critical** — invalid/nested YAML, missing required frontmatter, invalid statuses,
     duplicate titles, missing project notes, missing folders, unwritable vault/snapshot
     directories. These break correctness or safety; surface them first.
   - **warning** — broken wiki links, sync-conflict files, notes outside the expected
     structure, ambiguous file names, superseded decisions still carrying an active status.
   - **info** — stale tasks, oversized notes, active notes linking to archived ones.
4. For structure and link problems, the targeted audits go deeper than validate — each
   finding carries a proposed fix:
   - `mindvault_organize_vault` (dry-run by default) — misfiled notes with a reason per move
   - `mindvault_find_broken_links` — wiki links whose target does not exist
   - `mindvault_find_orphans` — managed notes nothing connects to
   - `mindvault_audit_frontmatter` — missing/invalid keys, inconsistent project names,
     notes not linked to their hub
   - `mindvault_audit_aliases` — duplicate aliases and cross-project collisions
   - `mindvault_organisation_score` — 11 explainable categories with evidence; the
     weaknesses list is the prioritised cleanup plan
   - `mindvault_token_audit` — where agents waste tokens: largest notes, large notes
     without summaries, capsule-vs-route cost
   - `mindvault_find_low_value_notes` — what agents should not be reading, with reasons
5. For deeper index doubts, `mindvault_diagnostics` adds the schema version and a
   validation summary in one call.
6. Rebuild **only when justified**: call `mindvault_rebuild_index` if the index is missing,
   clearly stale (external edits after the last scan), or visibly wrong (search/list results
   contradict files the user shows you). A rebuild is always safe — Markdown is canonical —
   but do not run it ritually on every invocation.
7. Report findings grouped by severity with counts, what each group breaks (e.g. duplicate
   titles break note resolution), and the affected note paths.
8. Recommend safe fixes, mapped to safe tools, as **suggestions**:
   - misfiled notes → show the `mindvault_organize_vault` dry-run proposals; apply only
     after explicit approval (snapshot-first, reversible)
   - large notes without summaries → show the `mindvault_generate_summaries` dry-run;
     apply only after approval (generated block only, human text untouched)
   - stale hub map blocks / missing navigation → `mindvault_compile_brain` dry-run shows
     what a full compile would rebuild; apply only after approval
   - unpromoted inbox thoughts that are now confirmed → `mindvault_promote_note`
   - wrong/missing frontmatter or status → `mindvault_update_frontmatter` on the specific note
   - superseded-status-mismatch → `mindvault_supersede_decision` (or a status fix) on the pair
   - stale finished notes → `mindvault_archive_note` (never delete; archive is reversible)
   - stale `open`/`active` tasks → ask whether to close (`done`/`cancelled`) or archive them
   - missing folders/templates → run `init` from the MindVault CLI (user action)
   - broken links → create the missing note, correct the link text in Obsidian (user
     action), or point the note at the right target with `mindvault_link_notes`;
     `mindvault_suggest_links` helps re-home orphans
   - sync-conflict files → the human resolves them in Obsidian; MindVault ignores them

Expected final behaviour: a severity-grouped report with concrete per-note suggestions —
and zero mutations unless the user approved a specific one.

## Do not

- Do not archive, update, or otherwise modify any note unless the user explicitly asked
  for that specific fix.
- Do not bulk-rewrite or bulk-archive. If many notes need the same fix, list them and ask;
  proceed note-by-note only after explicit confirmation.
- Do not invent work: if validation is clean, say so and stop.
- Do not rebuild the index ritually.

## Efficiency rules

- Health → status → validate is the whole diagnosis for most cases; three calls.
- Group the report by severity; lead with criticals; skip empty groups.
- One rebuild at most, and only with a stated justification.

## Safety rules

- Use only the `mindvault_*` MCP tools — never touch vault files directly or via shell.
- Every suggested fix maps to a snapshot-first safe tool; there is no delete, only archive.
- Rebuilding the index never touches Markdown — it is the one always-safe repair.
