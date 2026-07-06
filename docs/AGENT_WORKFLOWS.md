# Agent Workflows

How Claude Code / Fable should drive MindVault over MCP. The skills pack
([SKILLS_SETUP.md](SKILLS_SETUP.md)) teaches these automatically; this is the reference.

MindVault's consumers are almost entirely AI agents, so the loop below is optimised for
tokens-per-session, not human ergonomics: one brief in, scoped reads while you work, one
batched handoff out.

## The core loop

```
mindvault_start_session (project, task, maxChars?)   ← one call: budgeted brief + log setup
    │  read the brief's readFirst list with mindvault_read_note (section= / maxChars= to scope)
    │  honor doNotRepeat, non-negotiables, decisions-in-force; skip the doNotRead list
    │  check deltaSinceLastHandoff for what moved since your last handoff
    ▼
work (code lives in the repo, not the vault)
    │  mindvault_get_work_context for the specific file/task at hand
    │  mindvault_search (snippetChars: 0 for refs-only) → mindvault_read_note (section=)
    │  mid-session captures: mindvault_check_draft → mindvault_create_task / _decision
    ▼
mindvault_end_session (summary, tests, followUps, decisions[]?, mistakes[]?, tasks[]?)
                                                     ← one call: handoff + batched captures
```

### Before / after call counts

The brief and the batched close collapse the two ends of the loop:

| Phase | Before (≤0.7) | After (0.8) |
| --- | --- | --- |
| Session start | `start_session` + `build_context_capsule` + `build_route_card` (3 calls) | `start_session` (1 call — the brief covers all three) |
| Session close | `end_session` + N standalone `create_decision`/`add_mistake`/`create_task` (5–8 calls typical) | `end_session` with `decisions[]`/`mistakes[]`/`tasks[]` (1 call) |

`build_context_capsule` and `build_route_card` still exist for **mid-session** refreshes
(a new mode, a new file, a second angle); the brief just means you rarely need them at the
start. Standalone `create_decision` / `add_mistake` / `create_task` remain for captures made
**mid-session** — batch only the ones that surface right at the close.

### The core tool profile

Set `MINDVAULT_TOOL_PROFILE=core` (see [MCP_SETUP.md](MCP_SETUP.md)) to expose only the 20
tools this loop uses. The full 55-tool schema costs an agent roughly 9–12k tokens of context
every session before any work happens; the core profile cuts that to about a third. Switch to
the default `full` profile when you do maintenance or hygiene work (audits, organise, compile,
maps, summaries, graph) — those tools are not in the core set.

## Tool cheat sheet (55 tools, all safe)

| Situation | Tool |
| --- | --- |
| Which project is this repo? | `mindvault_detect_project` (repo folder name in, project + confidence out) |
| Starting substantial work | `mindvault_start_session` (budgeted brief in one call; `maxChars` to tighten) |
| Mid-session read-first refresh | `mindvault_build_route_card` (goal/file/query in; read-first ≤5 + do-not-read out) |
| Strict tool-call discipline | `mindvault_build_read_plan` (ordered reads + stop conditions) |
| Cheapest project orientation | `mindvault_get_project_map` (one read: the hub's map block — decisions, risks, health) |
| What should agents NOT read? | `mindvault_find_low_value_notes` (reasons on every row) |
| Where do the tokens go? | `mindvault_token_audit` (largest, unsummarized, capsule vs route) |
| How well organised is the brain? | `mindvault_organisation_score` (11 categories with evidence) |
| Why do these two notes matter together? | `mindvault_explain_relationships` (+ `mindvault_build_graph` for the sidecar) |
| Cheap per-note briefs for big notes | `mindvault_generate_summaries` (dry-run first; generated block only) |
| Rebuild the whole navigation layer | `mindvault_compile_brain` (dry-run first: maps + summaries + graph + score) |
| Mid-session mode-specific briefing | `mindvault_build_context_capsule` (coding/debugging/review/planning/handoff/release/architecture; `format` returns one of json/markdown) |
| About to edit a specific file | `mindvault_get_work_context` (currentFile / query / note — reasons on every result) |
| Continuing after a gap | `mindvault_recall` (`since: "last-handoff"` for the exact window — needs a project; date/'7 days' also accepted) + `mindvault_recent_sessions` (where it stopped) |
| Mid-session milestone | `mindvault_checkpoint_session` (sparingly; the handoff matters more) |
| A result was gold / noise | `mindvault_record_feedback` (pinned/hidden/useful/noisy/outdated/wrong) |
| Something went wrong, lesson learned | `mindvault_add_mistake` / `mindvault_list_mistakes` / `mindvault_resolve_mistake` |
| Full brain state in one call | `mindvault_brain_ops` |
| Unpromoted drafts | `mindvault_list_inbox` |
| Quick orientation, no session | `mindvault_get_context_pack` / `mindvault_get_project_context` (`detailLevel: brief`) |
| One-page project overview | `mindvault_get_project_map` (the hub's map block; `mindvault_rebuild_map` after big changes) |
| Find a specific memory | `mindvault_search` (project scope + filters; `snippetChars: 0` for refs-only; `explain` for debugging) |
| Read one note (or one section) | `mindvault_read_note` (`section`/`maxChars` scope the read — the single biggest per-read saver) |
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
| Wrapping up | `mindvault_end_session` (batch end-of-session decisions/mistakes/tasks into it) |
| Quick self-check (is the vault usable?) | `mindvault_health` |
| Link rot / structure doubts | `mindvault_find_broken_links`, `mindvault_find_orphans`, `mindvault_audit_frontmatter`, `mindvault_audit_aliases` |
| Vault feels wrong | `mindvault_diagnostics`, `mindvault_validate_vault`, `mindvault_rebuild_index` |
| Index/scan state | `mindvault_status` |

## Rules that keep the vault trustworthy

1. **Brief before search, search before read, read before create.** Cheapest sufficient step.
2. **Check drafts.** Near-duplicates and vague titles caught before they exist beat cleanup.
3. **Never contradict silently.** A decision in the brief is binding until superseded — if
   the task requires breaking it, tell the user and supersede properly.
4. **One handoff per session.** Not a transcript; outcome, tests, follow-ups — and the
   end-of-session captures batched into the same call.
5. **Bounded reads.** Read the brief's readFirst list, scope each read with `section` /
   `maxChars`, and stop once the goal is clear. The brief exists so you don't crawl.
6. **Honest status.** "Tests: not run" is better handoff data than silence.
7. **Thoughts are not memory.** Uncertain knowledge goes to the inbox via
   `mindvault_capture_thought`; only confirmed knowledge is promoted. Never auto-promote.
8. **Organise with proposals.** Dry-run first, apply only with approval, one link at a
   time with its reason read.

## Anti-patterns

- Listing the whole vault "to get an overview" → start with the session brief.
- Calling `build_context_capsule` **and** `build_route_card` at the start → the brief already
  covers both; reach for them only for a mid-session refresh.
- Firing off `create_decision` / `add_mistake` / `create_task` one by one at the close →
  batch end-of-session captures into `mindvault_end_session`.
- Creating a fresh task per micro-step → batch into the parent task's Status Notes.
- Flipping a decision's status by hand → `mindvault_supersede_decision` keeps the graph true.
- Re-recording a known risk with new wording → append to the existing risk note.
