# Context Packs

A context pack is a **generated, compact briefing assembled from existing vault notes** —
never a new data store. Delete every pack and nothing is lost; the Markdown notes remain
canonical. Packs exist so an agent can start work with one call instead of crawling.

## Getting one

```bash
mindvault context-pack "Alpha"                                  # markdown (default)
mindvault context-pack "Alpha" --task "add retry to sync client"
mindvault context-pack "Alpha" --output json
```

MCP: `mindvault_get_context_pack(project, task?, output?)` — JSON by default,
`output: "markdown"` for the rendered briefing. `mindvault_start_session` returns the same
pack plus session-log setup.

## What's inside

| Field | Source |
| --- | --- |
| `currentGoal` | project note `## Goal` (excerpt, capped) |
| `nonNegotiables` | project note `## Non-Negotiables` bullets |
| `relevantArchitecture` | `type: architecture` notes for the project |
| `relevantDecisions` | decisions in force (superseded/rejected excluded) |
| `activeTasks` | open/active + blocked tasks |
| `openRisks` / `constraints` | `type: risk` / `type: constraint` notes |
| `suggestedNextReads` | ordered refs with reasons (project note first) |
| `doNotForget` | non-negotiables + constraints + top open risks |
| `warnings` | stale tasks, contradicted decisions, duplicates, broken links |
| `taskRelevantNotes` | search hits for the task description (OR-matched, ranked) |

Everything is a title + path + status **reference** — the agent reads the full note only
when needed (`mindvault_read_note`).

## Task-aware packs

Passing `--task` runs a ranked, project-scoped search over the task's terms and (a) lists
the top hits as `taskRelevantNotes`, (b) floats matching decisions/architecture to the top
of their lists. Deterministic — same vault + same task = same pack.

## Size guarantees

Default limits keep a pack to a screenful (fixture-vault packs are < 4 KB of markdown).
Limits are capped server-side; there is no flag that dumps the vault.
