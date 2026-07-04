# Thoughts vs Memory

A top-tier external brain distinguishes what it *suspects* from what it *knows*. MindVault
encodes that as a note lifecycle: raw thoughts live in an inbox and never pollute durable
memory until they are deliberately promoted.

## The model

| kind | meaning | default status | home |
| --- | --- | --- | --- |
| `thought` | raw, uncertain, temporary — not yet durable | `draft` | `00_Inbox` (human) / `06_Agent_Memory/Inbox` (agent) |
| `memory` | confirmed, useful, durable project knowledge | `active` | `06_Agent_Memory` |
| `decision` | durable choice with reasoning and consequences | `accepted` | `04_Decisions` |
| `task` | actionable work item | `open` | `01_Projects` |
| `risk` | unresolved project danger | `open` | `06_Agent_Memory/Risks` |
| `mistake` | durable lesson about what not to repeat | `active` | `06_Agent_Memory/Mistakes` |
| `constraint` | rule/non-negotiable that guides future agents | `active` | `06_Agent_Memory/Constraints` |

Thoughts are deliberately cheap: capturing one runs no duplicate gate (only exact file
collisions block), and orphan detection ignores them — an unlinked inbox thought is
expected, not a problem. The gate applies at **promotion**, when a thought tries to become
durable.

## Capture

```bash
mindvault create thought "Adopt WAL checkpoints" --content "spotted during the perf pass"
```

MCP: `mindvault_capture_thought` (title, content?) — always lands in
`06_Agent_Memory/Inbox`, the agent's drafts folder.

## Promotion

```bash
mindvault promote "Adopt WAL checkpoints" --to decision --project "MindVault"
mindvault promote "<note-ref>" --to memory | task | risk | mistake
```

MCP: `mindvault_promote_note` (note, to, project?, allowDuplicate?).

What promotion does, in order:

1. **Validates.** Target must be decision/memory/task/risk/mistake. Only thoughts and
   untyped notes can be promoted — durable notes get status changes, not re-promotion.
2. **Resolves the project — never guesses.** Explicit `--project` wins, then the note's own
   `project:`; both resolve through detection (aliases work). decision/task/risk *require*
   a project; the error lists known projects.
3. **Runs the duplicate gate** against the real title (capture prefix stripped). A
   near-duplicate refuses with `DUPLICATE_SUSPECTED` + candidates; `--allow-duplicate`
   overrides deliberately.
4. **Snapshots, then rewrites frontmatter**: `type`, a sensible default `status`,
   `project:`, a `[[project]]` link, tags swapped from `thought` to the target type,
   `updated` bumped.
5. **Retitles the H1** (`# Thought: X` → `# Decision: X`) — but only when nothing links to
   the old title; otherwise it stays and a suggestion tells you.
6. **Preserves the body verbatim and keeps the file name**, so existing links never break.
7. **Moves the note** to its placement folder (see [ORGANISATION.md](ORGANISATION.md)).
8. **Suggests what is missing** — the sections a durable note of that type should carry
   (e.g. a decision without `## Reasoning`).

## Rules of thumb

- Agent unsure whether something is true or worth keeping → capture a thought, keep coding.
- Human brain-dump → `00_Inbox`, untyped or `type: thought`; promote later or never.
- Nothing auto-promotes, ever. Promotion is an explicit, single-note act.
- The weekly tidy: `organize --dry-run` + a pass over both inboxes deciding
  promote / leave / archive.
