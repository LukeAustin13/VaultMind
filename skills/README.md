# MindVault Skills Pack

Thirteen portable Claude Code skills that teach an agent *when and how* to use the
MindVault MCP tools. The MCP server provides the capabilities; these skills provide the
judgment: when to load context, what to read first and what to skip, what deserves a
note, how to avoid duplicates, how to hand off.

## Structure contract

Every skill has the same five sections, and a test in the MindVault repo
(`AgentEvalTests.EverySkillHasTheFiveRequiredSections`) fails the build if one is missing:

| Section | Answers |
| --- | --- |
| **Trigger conditions** | When to invoke it — and when not to |
| **Required workflow** | The exact tool order, ending with the expected final behaviour |
| **Do not** | The specific mistakes this skill exists to prevent |
| **Efficiency rules** | The call/read budget, so context is spent on work, not ceremony |
| **Safety rules** | Only `mindvault_*` tools, snapshot-first writes, honesty requirements |

## The thirteen skills

| Skill | One-liner |
| --- | --- |
| `mindvault-session-handoff` | One call to brief in, one call to hand off (capsule + recall aware) |
| `mindvault-project-context` | Load concise project memory before working |
| `mindvault-route-card` | Token-budgeted read-first/do-not-read brief before any broad search |
| `mindvault-read-plan` | Strict ordered read plan (max 5 reads) with stop conditions |
| `mindvault-work-context` | What does the vault know about the file/task in front of you? |
| `mindvault-decision-capture` | Record durable decisions; supersede, never hand-edit |
| `mindvault-task-sync` | Draft-checked task creation and status sync |
| `mindvault-mistake-ledger` | Durable lessons with prevention rules; capsules surface them |
| `mindvault-implementation-log` | One dated handoff block per meaningful session |
| `mindvault-review-memory` | Persist review findings; escalate criticals |
| `mindvault-architecture-memory` | Keep one true system picture per project |
| `mindvault-vault-hygiene` | Severity-grouped health report; fixes only on approval |
| `mindvault-organisation` | Dry-run placement, thought promotion, hub map blocks, link repair |

## Hard safety line

The skills reference **only** the safe MindVault MCP tools (`mindvault_*`). They never
instruct shell commands, raw file writes, or vault dumping — a guard test scans every
SKILL.md and fails on any unknown or unsafe tool name.

## Install

Copy the skill folders into a project's `.claude/skills/` (or `~/.claude/skills/` for all
projects) and reload Claude Code. Full setup and per-skill test prompts:
[docs/SKILLS_SETUP.md](../docs/SKILLS_SETUP.md).
