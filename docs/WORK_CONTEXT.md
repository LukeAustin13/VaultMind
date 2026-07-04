# Work Context

"I am editing `WriteService.cs` — what does the vault know that touches it?" Work-context
answers that in one call, with a reason on every result.

## Usage

```bash
mindvault work-context --project "MindVault" --current-file "src/MindVault.Core/WriteService.cs"
mindvault work-context --project "MindVault" --query "snapshot archive safety"
mindvault work-context --project "MindVault" --note "Decision: Use SQLite FTS5"
```

MCP: `mindvault_get_work_context` (project + exactly ONE of currentFile / query / note).

## How results are found and ranked

Seeds (one input, one strategy):
- **current-file** — the path is tokenized (file stem, camelCase parts, parent folder) and
  matched through ranked FTS within the project.
- **query** — ranked FTS within the project (vault-wide fallback is flagged in warnings).
- **note** — graph expansion via related-notes (links, backlinks, project siblings,
  similar titles).

Deterministic boosts on top of the seeds: same project (+1), actionable status
(active/accepted/open/blocked, +1), pinned (+3), positive feedback (+score). Negative
feedback subtracts; anything at zero or below is dropped. Excluded always: archived,
superseded, hidden, templates, maps, raw thoughts.

## Output

Grouped by type — decisions, tasks, risks, mistakes, reviews, logs/memory — each item with
the reasons it matched (`matches the current file (WriteService.cs); same project; status
accepted; pinned`), plus `suggestedReads` (top results overall) and `warnings`.

## Discipline (see the mindvault-work-context skill)

One call before risky edits; read 1–3 suggested reads; treat decisions/constraints/
mistakes you find as binding; close the loop with `mindvault_record_feedback` when a
result was clearly right or wrong.
