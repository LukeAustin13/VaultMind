# MindVault Skills Pack

Eight portable Claude Code skills that teach an agent *when and how* to use the MindVault
MCP tools. The MCP server provides the capabilities; these skills provide the judgment:
when to load context, what deserves a note, how to avoid duplicates, how to hand off.

## Structure contract

Every skill has the same five sections, and a test in the MindVault repo
(`SkillContractTests`) fails the build if one is missing:

| Section | Answers |
| --- | --- |
| **Trigger conditions** | When to invoke it — and when not to |
| **Required workflow** | The exact tool order, ending with the expected final behaviour |
| **Do not** | The specific mistakes this skill exists to prevent |
| **Efficiency rules** | The call/read budget, so context is spent on work, not ceremony |
| **Safety rules** | Only `mindvault_*` tools, snapshot-first writes, honesty requirements |

## The eight skills

| Skill | One-liner |
| --- | --- |
| `mindvault-session-handoff` | One call to brief in, one call to hand off |
| `mindvault-project-context` | Load concise project memory before working |
| `mindvault-decision-capture` | Record durable decisions; supersede, never hand-edit |
| `mindvault-task-sync` | Draft-checked task creation and status sync |
| `mindvault-implementation-log` | One dated handoff block per meaningful session |
| `mindvault-review-memory` | Persist review findings; escalate criticals |
| `mindvault-architecture-memory` | Keep one true system picture per project |
| `mindvault-vault-hygiene` | Severity-grouped health report; fixes only on approval |

## Hard safety line

The skills reference **only** the safe MindVault MCP tools (`mindvault_*`). They never
instruct shell commands, raw file writes, or vault dumping — a guard test scans every
SKILL.md and fails on any unknown or unsafe tool name.

## Install

Copy the skill folders into a project's `.claude/skills/` (or `~/.claude/skills/` for all
projects) and reload Claude Code. Full setup and per-skill test prompts:
[docs/SKILLS_SETUP.md](../docs/SKILLS_SETUP.md).
