# Organisation Engine

MindVault does not just search the vault — it keeps it organised. This page is the folder
policy, the placement rules, and the safe `organize` workflow. Companions:
[THOUGHTS_AND_MEMORY.md](THOUGHTS_AND_MEMORY.md) (note lifecycle), [LINKING.md](LINKING.md)
(link intelligence), [MAPS.md](MAPS.md) (the hub's generated map block).

## Folder policy

`init` creates this structure (existing content is never touched):

```
00_Inbox            raw human capture — unsorted notes, quick thoughts
01_Projects         project hub notes + their work items (tasks, bugs, features), flat
02_Areas            ongoing areas of responsibility, not finite projects
03_Resources        reference material, research; 03_Resources/Architecture for arch notes
04_Decisions        durable decisions / ADR-style records
05_Prompts          reusable prompts and agent instructions
06_Agent_Memory     agent memory: implementation logs, session notes, durable memories
06_Agent_Memory/Inbox         unpromoted agent thought drafts
06_Agent_Memory/Meetings      meeting notes
06_Agent_Memory/Mistakes      the mistake ledger
06_Agent_Memory/Constraints   non-negotiables that guide future agents
06_Agent_Memory/Risks         open project dangers
07_Reviews          audits, code/architecture/release reviews
08_Templates        note templates (excluded from validation and search-adjacent features)
99_Archive          archived notes — the only "delete"
.mindvault          operational cache only (index, snapshots, backups, logs)
```

## Placement rules (deterministic)

| type | belongs in |
| --- | --- |
| project | `01_Projects` |
| task, bug, feature | `01_Projects` (work items live with work) |
| decision | `04_Decisions` |
| prompt | `05_Prompts` |
| research | `03_Resources` (any subfolder is fine) |
| architecture | `03_Resources/Architecture` (anywhere under 03_Resources is acceptable) |
| memory | `06_Agent_Memory` (any subfolder is fine) |
| meeting | `06_Agent_Memory/Meetings` |
| mistake | `06_Agent_Memory/Mistakes` |
| constraint | `06_Agent_Memory/Constraints` |
| risk | `06_Agent_Memory/Risks` |
| review | `07_Reviews` |
| thought | `00_Inbox` or `06_Agent_Memory/Inbox` |

There is no `map` row: as of v0.7.0 the project map is a generated block on the project hub
note ([MAPS.md](MAPS.md)), not a note that needs placing.

**Design decision — per-project subfolders are OFF.** Tasks stay flat in `01_Projects`
next to their hub, matching the existing create paths. Shallow and predictable beats
clever and nested: one subfolder level exists only where a flat folder would rot into a
graveyard (the typed subfolders inside `06_Agent_Memory`). `bug`/`feature` go to
`01_Projects` rather than agent memory because they are work items, not memories.

These rules live in `PlacementPolicy` and drive `organize` and promotion only. The
`outside-structure` **validation** contract is unchanged — upgrading MindVault adds zero new
validation warnings to an existing vault. `09_Maps` is no longer a required folder (`init`
does not create it); the project map lives on the hub instead.

## The organize workflow

```bash
mindvault organize --dry-run              # same as bare `organize`: propose only
mindvault organize --project "MindVault" --dry-run
mindvault organize --apply                # execute the proposals
```

MCP: `mindvault_organize_vault` (`project?`, `apply` — false by default).

Every proposal explains itself:

```json
{
  "dryRun": true,
  "proposals": [
    {
      "note": "Decision: Use SQLite FTS5",
      "currentPath": "00_Inbox/Random SQLite Decision.md",
      "proposedPath": "04_Decisions/Decision - Use SQLite FTS5.md",
      "reason": "type=decision, status=accepted, project=MindVault; canonical name applied (no backlinks to break)",
      "confidence": "high"
    }
  ],
  "needsReview": [],
  "warnings": []
}
```

Rules the engine enforces:

- **Dry-run by default.** Nothing moves without `--apply` / `apply: true`.
- **Archived notes and templates are never touched.**
- **Uncertainty is never moved.** Broken YAML, a `project:` that does not resolve (or is
  ambiguous), untyped notes squatting in managed folders, and destination collisions all go
  to `needsReview` with a reason — for the human.
- **Apply is snapshot-first and atomic** per note, and moves preserve content byte-for-byte.
- **Links survive.** Wiki links target titles/stems, so folder moves never break them.
  Canonical renames (`Decision - <Title>.md`) are proposed **only** when the note has zero
  backlinks.
- Output is capped at 200 proposals per run (a warning says so) — apply and re-run.

## How agents should use this

Load the `mindvault-organisation` skill: dry-run first, apply only with user approval,
never bulk-move, park uncertainty in the inbox (`mindvault_capture_thought`), promote
deliberately (`mindvault_promote_note`). See [AGENT_WORKFLOWS.md](AGENT_WORKFLOWS.md).

## How humans should use this in Obsidian

Write anywhere — the inbox is yours; nothing judges an untyped note in `00_Inbox`,
`02_Areas` or `03_Resources`. When you want tidiness, run `organize --dry-run`, read the
reasons, then `--apply`. Every move is snapshotted; `restore` undoes anything.
