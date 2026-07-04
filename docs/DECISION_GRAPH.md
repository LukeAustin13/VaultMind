# Decision Graph

Decisions rot when contradicting ones pile up as equally "accepted". MindVault keeps the
record trustworthy with three flat frontmatter relations and one safe operation.

## Frontmatter relations (flat lists of wiki links)

```yaml
supersedes:
  - "[[Decision - Use X]]"       # this decision replaces those
superseded_by:
  - "[[Decision - Use Y]]"       # this decision was replaced by those
related:
  - "[[Decision - Caching]]"     # relevant, not replacing
```

Plain flat YAML — Obsidian users can read and edit them; `validate` flags contradictions
(`superseded-status-mismatch`: a note with `superseded_by` whose status isn't `superseded`).

## Commands

```bash
mindvault decision list --project Alpha         # decisions in force (default filter)
mindvault decision list --project Alpha --all   # include superseded/rejected/archived
mindvault decision graph --project Alpha        # nodes + edges (text; --json for structure)
mindvault decision supersede --old "Decision - Use X" --new "Decision - Use Y"
```

MCP: `mindvault_supersede_decision(oldRef, newRef)`; listing comes through
`mindvault_list_notes` / the context pack, which already exclude superseded decisions.

## What supersede does (atomically, snapshot-first)

1. Snapshots **both** notes before touching either.
2. Old note: `status: superseded`, `superseded_by: [[new]]`, `updated` bumped.
3. New note: `supersedes: [[old]]`, `updated` bumped.
4. Both re-verified (YAML) and reindexed. If the second write fails, the first is rolled
   back from its snapshot — never a half-linked pair.

## Agent guidance

- `mindvault_check_draft` on a new decision suggests supersede candidates (token-overlap
  with existing decisions) — that's the moment to link the lifecycle, not later.
- Never hand-edit statuses to retire a decision; the operation keeps both sides consistent.
- Superseded decisions stay in place (and in `--all` listings) as history — archive them
  only when they stop being useful context entirely.
