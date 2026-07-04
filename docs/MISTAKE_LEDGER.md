# Mistake Ledger

Mistakes with repeat-prevention value become durable memory. The ledger lives in
`06_Agent_Memory/Mistakes/` as `type: mistake` notes, and its active lessons surface in
every context capsule as `knownMistakes` + `doNotRepeat` rules.

## Usage

```bash
mindvault mistake add --project "MindVault" --title "Trusted stale index" \
  --lesson "Search returned hours-old results after git operations" \
  --prevention "Always run scan after external edits"
mindvault mistake list --project "MindVault" [--all]
mindvault mistake resolve "Mistake - Trusted stale index"
```

MCP: `mindvault_add_mistake`, `mindvault_list_mistakes`, `mindvault_resolve_mistake`.

## The note

Sections: **What Happened** (narrative), **Root Cause** (why), **How To Avoid It** (the
lesson — filled from `--lesson`), **Prevention Task** (the rule a future agent must follow
— filled from `--prevention`). Frontmatter carries project, dates and links; the create
links the project hub automatically when a project is given.

Capsules read **Prevention Task first, then How To Avoid It** for the `doNotRepeat` line —
the actionable rule beats the description.

## Lifecycle

- `add` runs the duplicate gate (near-duplicate lessons are refused — strengthen the
  existing note) and the content gate (no secrets in lessons).
- Active lessons (`status: active/open`) appear in capsules and `mistake list`.
- `resolve` sets `status: done`: the lesson stops appearing in capsules but stays in the
  ledger — history is never deleted.
- Link a mistake to the decision it contradicts or the prevention task that fixes it
  (`mindvault_suggest_links` proposes exactly these).

## When to record one

The bar (from the mindvault-mistake-ledger skill): the mistake cost real time AND has a
statable prevention rule. Routine bugs, typos and one-offs don't qualify — a ledger full
of noise stops being read.
