# Agent Workflows

How Claude Code / Fable should drive MindVault over MCP. The skills pack
([SKILLS_SETUP.md](SKILLS_SETUP.md)) teaches these automatically; this is the reference.

## The core loop

```
mindvault_start_session (project, task)     ← one call: briefing + log setup
    │  read 1–5 of pack.recommendedNextReads with mindvault_read_note
    │  honor pack.doNotForget and pack.warnings
    ▼
work (code lives in the repo, not the vault)
    │  mindvault_check_draft → mindvault_create_task / mindvault_create_decision
    │  mindvault_update_frontmatter for task status changes
    │  mindvault_supersede_decision when a decision replaces an older one
    ▼
mindvault_end_session (summary, tests, followUps)   ← one call: handoff
```

## Tool cheat sheet (55 tools, all safe)

| Situation | Tool |
| --- | --- |
| Which project is this repo? | `mindvault_detect_project` (repo folder name in, project + confidence out) |
| Starting substantial work | `mindvault_start_session` |
| What should I read first (and skip)? | `mindvault_build_route_card` (goal/file/query in; read-first ≤5 + do-not-read out) |
| Strict tool-call discipline | `mindvault_build_read_plan` (ordered reads + stop conditions) |
| Cheapest project orientation | `mindvault_get_project_map` (one read: the hub's map block — decisions, risks, health) |
| What should agents NOT read? | `mindvault_find_low_value_notes` (reasons on every row) |
| Where do the tokens go? | `mindvault_token_audit` (largest, unsummarized, capsule vs route) |
| How well organised is the brain? | `mindvault_organisation_score` (11 categories with evidence) |
| Why do these two notes matter together? | `mindvault_explain_relationships` (+ `mindvault_build_graph` for the sidecar) |
| Cheap per-note briefs for big notes | `mindvault_generate_summaries` (dry-run first; generated block only) |
| Rebuild the whole navigation layer | `mindvault_compile_brain` (dry-run first: maps + summaries + graph + score) |
| Mode-specific / budgeted briefing | `mindvault_build_context_capsule` (coding/debugging/review/planning/handoff/release/architecture) |
| About to edit a specific file | `mindvault_get_work_context` (currentFile / query / note — reasons on every result) |
| Continuing after a gap | `mindvault_recall` (window) + `mindvault_recent_sessions` (where it stopped) |
| Mid-session milestone | `mindvault_checkpoint_session` (sparingly; the handoff matters more) |
| A result was gold / noise | `mindvault_record_feedback` (pinned/hidden/useful/noisy/outdated/wrong) |
| Something went wrong, lesson learned | `mindvault_add_mistake` / `mindvault_list_mistakes` / `mindvault_resolve_mistake` |
| Full brain state in one call | `mindvault_brain_ops` |
| Unpromoted drafts | `mindvault_list_inbox` |
| Quick orientation, no session | `mindvault_get_context_pack` / `mindvault_get_project_context` (`detailLevel: brief`) |
| One-page project overview | `mindvault_get_project_map` (the hub's map block; `mindvault_rebuild_map` after big changes) |
| Find a specific memory | `mindvault_search` (project scope + filters; `explain` for debugging) |
| Read one note (or one section) | `mindvault_read_note` (`section`/`maxChars` scope the read) |
| Unsure it's true or durable yet | `mindvault_capture_thought` (agent inbox, not memory) |
| Thought confirmed → make it durable | `mindvault_promote_note` (validates, dedupes, files correctly) |
| Before creating anything durable | `mindvault_check_draft` |
| Record decision / task / project | `mindvault_create_decision` / `_task` / `_project` |
| Decision replaced an old one | `mindvault_supersede_decision` |
| Progress + status changes | `mindvault_append_to_note`, `mindvault_update_frontmatter` |
| What should this note link to? | `mindvault_suggest_links` (reasons + confidence; apply with `mindvault_link_notes`) |
| Cross-reference notes | `mindvault_link_notes` |
| What surrounds this decision/task? | `mindvault_find_related` (links, backlinks, project siblings, with reasons) |
| Notes look misfiled | `mindvault_organize_vault` (dry-run proposals; `apply: true` only with approval) |
| Preview a risky change | `dryRun: true` on append / update_frontmatter / archive |
| Retire a note | `mindvault_archive_note` (never delete) |
| Wrapping up | `mindvault_end_session` |
| Quick self-check (is the vault usable?) | `mindvault_health` |
| Link rot / structure doubts | `mindvault_find_broken_links`, `mindvault_find_orphans`, `mindvault_audit_frontmatter`, `mindvault_audit_aliases` |
| Vault feels wrong | `mindvault_diagnostics`, `mindvault_validate_vault`, `mindvault_rebuild_index` |
| Index/scan state | `mindvault_status` |

## Rules that keep the vault trustworthy

1. **Pack before search, search before read, read before create.** Cheapest sufficient step.
2. **Check drafts.** Near-duplicates and vague titles caught before they exist beat cleanup.
3. **Never contradict silently.** A decision in the pack is binding until superseded — if
   the task requires breaking it, tell the user and supersede properly.
4. **One handoff per session.** Not a transcript; outcome, tests, follow-ups.
5. **Bounded reads.** 1–5 notes per orientation. The pack exists so you don't crawl.
6. **Honest status.** "Tests: not run" is better handoff data than silence.
7. **Thoughts are not memory.** Uncertain knowledge goes to the inbox via
   `mindvault_capture_thought`; only confirmed knowledge is promoted. Never auto-promote.
8. **Organise with proposals.** Dry-run first, apply only with approval, one link at a
   time with its reason read.

## Anti-patterns

- Listing the whole vault "to get an overview" → use the context pack.
- Creating a fresh task per micro-step → batch into the parent task's Status Notes.
- Flipping a decision's status by hand → `mindvault_supersede_decision` keeps the graph true.
- Re-recording a known risk with new wording → append to the existing risk note.
