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

## Tool cheat sheet (21 tools, all safe)

| Situation | Tool |
| --- | --- |
| Starting substantial work | `mindvault_start_session` |
| Quick orientation, no session | `mindvault_get_context_pack` / `mindvault_get_project_context` (`detailLevel: brief`) |
| Find a specific memory | `mindvault_search` (project scope + filters; `explain` for debugging) |
| Read one note | `mindvault_read_note` |
| Before creating anything durable | `mindvault_check_draft` |
| Record decision / task / project | `mindvault_create_decision` / `_task` / `_project` |
| Decision replaced an old one | `mindvault_supersede_decision` |
| Progress + status changes | `mindvault_append_to_note`, `mindvault_update_frontmatter` |
| Cross-reference notes | `mindvault_link_notes` |
| Retire a note | `mindvault_archive_note` (never delete) |
| Wrapping up | `mindvault_end_session` |
| Quick self-check (is the vault usable?) | `mindvault_health` |
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

## Anti-patterns

- Listing the whole vault "to get an overview" → use the context pack.
- Creating a fresh task per micro-step → batch into the parent task's Status Notes.
- Flipping a decision's status by hand → `mindvault_supersede_decision` keeps the graph true.
- Re-recording a known risk with new wording → append to the existing risk note.
