# Demo Script — MindVault in 5 minutes

A live walkthrough that shows the brain, not just the search box. Run inside any repo with
MindVault configured (`mindvault status` works). Every step is copy-paste.

## 0. The brain scores its own organisation (20s)

```bash
mindvault ops
mindvault organisation-score --project "MindVault"
```

→ first the health rollup, then the headline: `Organisation Score: NN/100`, eleven
categories each with evidence, weaknesses ranked, and an estimated token waste number —
the brain telling you exactly why it is (or is not) cheap for agents to use.

## 0.25 The brain routes the agent (30s)

```bash
mindvault route --project "MindVault" --goal "improve safe writes"
mindvault read-plan --project "MindVault" --goal "improve safe writes"
```

→ the route card: 3–5 read-first notes with reasons, token price tags and summary
snippets, an explicit **Do Not Read** list, constraints and do-not-repeat rules in force.
The read plan turns it into ordered steps with a stop condition — the agent knows when to
STOP reading. Compare `token-audit`'s capsule-vs-route line to see the saving.

## 0.4 The brain compresses itself (30s)

```bash
mindvault token-audit --project "MindVault"
mindvault summarize --project "MindVault" --dry-run
mindvault graph build --project "MindVault"
mindvault low-value --project "MindVault"
mindvault compile --project "MindVault" --dry-run
```

→ where the tokens go; which large notes would get generated summary blocks (dry-run —
nothing written); the typed relationship graph (`task_tracks_decision`,
`mistake_prevented_by`, …) written to a disposable sidecar; the do-not-read list with
reasons; and the one-command compiler that would rebuild the whole navigation layer.

## 0.5 Start like an agent starts (30s)

```bash
mindvault session start --project "MindVault"
mindvault capsule --project "MindVault" --mode coding
```

→ the session brief, then the mode-shaped capsule: goal, non-negotiables, decisions in
force, do-not-repeat rules from the mistake ledger, superseded-decision warnings — all
under a char budget, every line source-backed.

## 0.75 Ask about the file you are editing (20s)

```bash
mindvault work-context --project "MindVault" --query "snapshot archive safety"
```

→ the decisions/tasks/risks/mistakes touching that work, each with the reason it matched.

## 1. A messy capture (20s)

```bash
mindvault create thought "Use SQLite FTS5 for search" --content "porter tokenizer handles word variants; bm25 is enough, no embeddings needed"
```

→ lands in `00_Inbox`, `type: thought`, `status: draft`. Cheap, unjudged, out of the way.

## 2. The vault knows where things belong (30s)

```bash
mindvault organize --dry-run
```

→ proposals with reasons (`type=decision, status=accepted, project=MindVault`), uncertain
notes under *needs review*, and **nothing moved**. Then:

```bash
mindvault organize --apply
```

→ each move snapshot-first, links intact.

## 3. Thought becomes durable memory (30s)

```bash
mindvault promote "Use SQLite FTS5 for search" --to decision --project "MindVault"
```

→ frontmatter rewritten, H1 retitled, `[[MindVault]]` linked, filed into `04_Decisions`,
content preserved byte-for-byte — and it *refuses* if a similar decision already exists.

## 4. The vault suggests its own connections (30s)

```bash
mindvault links suggest --note "Use SQLite FTS5 for search"
```

→ reason-tagged suggestions (`decision-to-task relationship; shared title tokens: sqlite`).
Apply exactly one:

```bash
mindvault links apply --note "Use SQLite FTS5 for search" --to "Task - Add SQLite index tests"
```

Run it twice — the second call is a no-op, never a duplicate link.

## 5. One page that explains the project (40s)

```bash
mindvault map rebuild --project "MindVault"
mindvault read "MindVault Map"
```

→ the v2 map: Start Here, agent-route pointer, goal, non-negotiables, decisions, tasks,
risks, do-not-repeat rules, work areas, recent sessions, needs-review/orphans/broken-links
health, large-notes-missing-summaries and an organisation score line — every entry a
clickable `[[link]]` in Obsidian. Your own sections outside the generated markers survive
every rebuild.

## 6. The vault audits itself (40s)

```bash
mindvault links broken
mindvault links orphans
mindvault frontmatter audit
mindvault aliases audit
```

→ every finding carries a proposed fix (`fix: update-frontmatter --key project --value
"MindVault"`), nothing is auto-fixed.

## 7. Repo → project, no config (20s)

```bash
mindvault detect-project
```

→ maps the current folder name (`mind-vault`, `mind_vault`, `MindVault`…) to the project
note with a confidence tier — the reason agents in any clone of the repo find the same brain.

## 8. The brain remembers what went wrong (30s)

```bash
mindvault mistake add --project "MindVault" --title "Trusted stale index" \
  --lesson "search returned hours-old results" --prevention "always scan after external edits"
mindvault capsule --project "MindVault" --mode debugging
```

→ the capsule now carries the do-not-repeat rule. Try to store a secret:

```bash
mindvault append --note "MindVault" --section "Architecture" --content "-----BEGIN RSA PRIVATE KEY-----"
```

→ **blocked** (`RISKY_CONTENT`), value never echoed.

## 9. Hand off and recall (30s)

```bash
mindvault session handoff --project "MindVault" --summary "demo complete" --tests "dotnet test green"
mindvault recall --project "MindVault" --since "7 days"
mindvault session recent --project "MindVault"
```

→ the handoff appears in the window; `recent` shows exactly where the last session stopped.

## 10. Verify in one line (10s)

```bash
mindvault validate && mindvault index verify
```

Close: everything shown was snapshot-first and reversible (`mindvault restore`), retrieval
learns from `pin`/`feedback` without embeddings, and the whole surface is exposed to
Claude Code as 55 safe `mindvault_*` MCP tools — no raw file, shell or SQL access.
