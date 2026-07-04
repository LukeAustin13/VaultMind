# Link Intelligence

Links are what turn a pile of notes into a brain. MindVault suggests links with reasons,
detects broken links and orphans, and applies links safely — it never links things behind
your back.

## Suggestions

```bash
mindvault links suggest --note "Decision: Use SQLite FTS5" [--limit n]
mindvault links suggest --project "MindVault" [--limit n]
```

MCP: `mindvault_suggest_links` (note | project, limit).

Each suggestion scores concrete, deterministic signals:

| signal | weight | example reason |
| --- | --- | --- |
| type relationship in the same project (decision↔task, decision↔risk, risk↔task, mistake↔task, review↔task/risk, architecture↔decision, bug↔task/decision) | 2 | `decision-to-task relationship` |
| project hub not yet linked | 2 | `project hub` |
| same type + 2 or more shared title tokens | 2 | `same type, shared title tokens: sqlite, index` |
| candidate's title literally appears in the note body | 2 | `mentioned in this note's body` |
| same project (any type) | 1 | `same project` |
| shared specific tag (type-mirroring tags like `decision` carry no signal) | 1 | `shared tag 'retrieval'` |
| shared title tokens across signals | 1 | `shared title tokens: sqlite, fts5` |

A candidate needs a **score of 2 or more** to appear at all — one weak signal (generic
word overlap, mere same-project membership) is noise, not a suggestion. Score ≥ 4 is
`high` confidence, otherwise `medium`. Already-linked pairs (either direction), archived
notes, templates, maps and raw thoughts are never suggested.

Example output:

```json
{
  "from": "Decision: Use SQLite FTS5",
  "fromPath": "04_Decisions/Decision - Use SQLite FTS5.md",
  "to": "Task: Add SQLite index tests",
  "toPath": "01_Projects/Task - Add SQLite index tests.md",
  "reason": "decision-to-task relationship; same project; shared title tokens: sqlite",
  "confidence": "high"
}
```

## Applying a link

```bash
mindvault links apply --note "Decision: Use SQLite FTS5" --to "Task: Add SQLite index tests"
```

`links apply` is the existing safe link write (identical to `link --from --to`): snapshot
first, `[[stem]]` added to the frontmatter `links:` list, **no-op if already linked**
(dedup is normalization-based, so `[[MindVault]]` vs `[[mindvault]]` cannot double up).

MCP: `mindvault_link_notes` **is** the apply tool — a separate `mindvault_apply_link` was
deliberately not added; two tools with identical semantics would only bloat the surface.

## Broken links and orphans

```bash
mindvault links broken      # wiki links whose target matches no note title/stem
mindvault links orphans     # managed, non-archived notes with no links either direction
```

MCP: `mindvault_find_broken_links`, `mindvault_find_orphans`.

- Broken-link detection covers frontmatter `links:` and body `[[links]]` uniformly and
  skips template-authored links.
- Orphan detection excludes thoughts (inbox captures are *supposed* to be unlinked),
  templates, maps and archived notes. Fix orphans by linking them (`links suggest` on the
  orphan helps) or archiving what is obsolete.
- Both outputs are capped at 100 rows with a `truncated` flag.

## Rules

- Suggestions are never auto-applied — a human or an approving agent applies them one at a
  time, having read the reason.
- Applying always snapshots first and never duplicates.
- Archived notes are not suggested and not counted as connections, by default and by design.
