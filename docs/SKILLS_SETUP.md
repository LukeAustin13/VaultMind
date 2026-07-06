# MindVault Skills Pack

## What this is

`/skills` contains eight portable **Claude Code skills** — workflow instructions that teach
Claude Code *when and how* to use the MindVault MCP tools while it works in any of your
projects. The split of responsibilities:

- **MCP server** (`src/MindVault.Mcp`) provides the *tools* (`mindvault_*`).
- **Skills** (this pack) provide the *workflows*: when to load context, what counts as a
  decision worth recording, how to keep tasks in sync, how to leave handoff notes.

The skills are plain Markdown. They contain no code, run no shell commands, and never touch
the vault through the filesystem — they exclusively reference the safe MindVault MCP tools:

```
mindvault_status            mindvault_search           mindvault_read_note
mindvault_list_notes        mindvault_create_project   mindvault_create_decision
mindvault_create_task       mindvault_append_to_note   mindvault_update_frontmatter
mindvault_link_notes        mindvault_archive_note     mindvault_validate_vault
mindvault_get_project_context                          mindvault_rebuild_index
mindvault_get_context_pack  mindvault_check_draft      mindvault_supersede_decision
mindvault_start_session     mindvault_end_session      mindvault_health
mindvault_diagnostics
```

Every skill follows the same structure contract — **Trigger conditions / Required
workflow / Do not / Efficiency rules / Safety rules** — and a test fails the build if a
section is missing or an unsafe tool name appears. Pack overview: [skills/README.md](../skills/README.md).

## Prerequisite: configure the MCP server first

The skills assume the MindVault MCP server is already registered and its tools are available
in the project where you drop them. Set that up per [MCP_SETUP.md](MCP_SETUP.md) — the skills
degrade gracefully (they tell you MCP is missing), but they are useless without it.

## The nine skills

| Skill | Use it to |
| --- | --- |
| `mindvault-session-handoff` | Bracket a session: one-call briefing pack in, one-call concise handoff out |
| `mindvault-project-context` | Load concise project memory (goal, constraints, decisions, tasks, risks, warnings) before working |
| `mindvault-decision-capture` | Record durable decisions with draft checks first and proper supersede lifecycle |
| `mindvault-task-sync` | Create (draft-checked), update, and close durable tasks as implementation progresses |
| `mindvault-implementation-log` | End sessions with a structured handoff entry (or append one manually) |
| `mindvault-review-memory` | Persist review/risk/tech-debt findings and escalate serious ones into tasks/decisions |
| `mindvault-architecture-memory` | Keep the vault's system picture true when structure is discovered or changed |
| `mindvault-vault-hygiene` | Check vault health by severity, diagnose issues, recommend (never auto-apply) safe fixes |
| `mindvault-organisation` | Keep the vault organised safely: dry-run placement, thought promotion, hub map blocks, link repair |

## Where they live and how to install them

Generated location in this repo:

```
MindVault/
  skills/
    README.md
    mindvault-project-context/SKILL.md
    mindvault-decision-capture/SKILL.md
    mindvault-task-sync/SKILL.md
    mindvault-implementation-log/SKILL.md
    mindvault-review-memory/SKILL.md
    mindvault-vault-hygiene/SKILL.md
    mindvault-session-handoff/SKILL.md
    mindvault-architecture-memory/SKILL.md
```

To install into another project, copy the folders (all eight, or just the ones you want)
into that project's `.claude/skills/` directory:

```
SomeProject/
  .claude/
    skills/
      mindvault-project-context/
        SKILL.md
      mindvault-decision-capture/
        SKILL.md
      mindvault-task-sync/
        SKILL.md
      mindvault-implementation-log/
        SKILL.md
      mindvault-review-memory/
        SKILL.md
      mindvault-vault-hygiene/
        SKILL.md
      mindvault-session-handoff/
        SKILL.md
      mindvault-architecture-memory/
        SKILL.md
```

Windows (from the MindVault repo root):

```bat
xcopy /E /I skills SomeProject\.claude\skills
```

macOS/Linux:

```bash
mkdir -p SomeProject/.claude/skills && cp -R skills/* SomeProject/.claude/skills/
```

To make the skills available in **every** project on a machine, copy them to
`~/.claude/skills/` instead. Restart or reload Claude Code after copying so it picks up the
new skills.

## Testing each skill

After installing (and with the MCP server configured), verify in a Claude Code session in
the target project:

1. **project-context** — say "load the MindVault context for this project before we start."
   Expect: `mindvault_status` → `mindvault_get_project_context` → a short summary, and only
   a handful of `mindvault_read_note` calls at most.
2. **decision-capture** — say "we're going with X over Y — record that decision."
   Expect: a duplicate check (list/search) before `mindvault_create_decision`, then section
   fills via `mindvault_append_to_note`.
3. **task-sync** — say "log a follow-up task to add retries to the sync client."
   Expect: search first, then `mindvault_create_task`; "mark it done" should trigger
   `mindvault_update_frontmatter` with `status: done`.
4. **implementation-log** — after some work, say "write a handoff log to MindVault."
   Expect: one dated block appended to the project note's Active Work section.
5. **review-memory** — after a review, say "store these findings in MindVault."
   Expect: duplicate check, structured findings appended, critical items proposed as tasks.
6. **vault-hygiene** — say "check the vault's health."
   Expect: status + validate, a severity-grouped report, recommendations only — no
   mutations unless you explicitly approve a specific fix.
7. **session-handoff** — say "start a session on this project: hardening the parser."
   Expect: one `mindvault_start_session` call and a short summary of the brief; at the end
   of the session, one `mindvault_end_session` call with summary + tests (plus any
   end-of-session decisions/mistakes/tasks batched into it).
8. **architecture-memory** — after mapping a subsystem, say "record this architecture in
   MindVault." Expect: a check for an existing architecture note, then a compact append —
   bullets and arrows, not an essay.

## Avoiding duplicate notes, tasks and decisions

Every writing skill enforces **search-before-create**, but you can help:

- Use one consistent project name per repo (the name of the project note in `01_Projects`).
- Prefer updating an existing task/decision over creating a near-duplicate; the skills
  append to or supersede existing notes when they find them.
- If two notes for the same thing slip through anyway, `mindvault-vault-hygiene` will surface
  the duplicate titles, and the fix (archive one) is applied only when you ask.

## What the skills are not

They are not the MCP server, they do not install or configure it, and they grant no new
capabilities — they only shape how Claude Code uses the already-configured safe tools. A
project without the MindVault MCP server gets a clear "not configured" message, nothing else.
