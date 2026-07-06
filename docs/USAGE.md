# MindVault Usage

The short version of everything MindVault can do, with commands. All commands accept
`--vault <path>` and most accept `--json`.

## Daily driving

```bash
mindvault status                      # config, index, rescan-pending
mindvault scan                        # incremental (mtime+size; content-hash opt-in)
mindvault scan --full                 # full rebuild (same as rebuild-index)
mindvault validate                    # critical / warning / info report
mindvault doctor                      # health summary
```

## Retrieval

```bash
mindvault search "retry policy"                              # ranked: title-weighted, recency-boosted
mindvault search "retry policy" --project Alpha              # project first, vault-wide fallback
mindvault search "retry" --type decision --status accepted
mindvault search "sync" --updated-after 2026-06-01 --updated-before 2026-07-01
mindvault search "old plan" --include-archived               # archived included, deprioritised
mindvault search "retry" --explain                           # per-result ranking factors
```

Ranking: bm25 with title weighted 4×, then exact-title ×2.0, all-terms-in-title ×1.5,
updated ≤14d ×1.25 / ≤60d ×1.1, archived ×0.25. Results carry the matched section (`§`).

## Project context and packs

```bash
mindvault context "Alpha"                    # goal, tasks (active/blocked), decisions, warnings
mindvault context "Alpha" --brief            # glance
mindvault context "Alpha" --deep --json      # everything, doubled limits
mindvault context-pack "Alpha"               # markdown briefing for starting work
mindvault context-pack "Alpha" --task "add retry to sync client"   # task-relevant notes first
mindvault context-pack "Alpha" --output json
```

## Write discipline

```bash
mindvault check-draft --type decision --project Alpha --title "Use Polly for retries"
mindvault check-note "Task - Ship v1"
mindvault create decision --project Alpha --title "Use Polly for retries"   # warnings included
```

`check-draft` blockers = the create would fail or duplicate; warnings/suggestions are
advisory (near-duplicates, vague titles, supersede candidates).

## Decision graph

```bash
mindvault decision list --project Alpha            # active decisions (+relations)
mindvault decision list --project Alpha --all      # include superseded/rejected/archived
mindvault decision graph --project Alpha --json    # nodes + supersedes/related edges
mindvault decision supersede --old "Decision - Use X" --new "Decision - Use Y"
```

## Sessions

```bash
mindvault session start --project Alpha --task "harden search"   # budgeted brief + log note (--max-chars caps it)
mindvault session log --project Alpha --summary "index schema decided"   # sparingly
mindvault session end --project Alpha --summary "weighted search shipped" \
    --tests "dotnet test green (180)" --followups "tune recency boost"
```

Over MCP, `mindvault_start_session` returns a budgeted session brief (goal, non-negotiables,
decisions in force, do-not-repeat rules, open/blocked tasks, risks, constraints, a
token-priced read-first / do-not-read list, and what changed since the last handoff) rather
than a full context pack, and `mindvault_end_session` can batch end-of-session decisions,
mistakes and tasks into the one handoff call. See [AGENT_WORKFLOWS.md](AGENT_WORKFLOWS.md).

## Safety net

```bash
mindvault restore "Alpha"                     # newest snapshot back (itself snapshotted)
mindvault restore "Alpha" --snapshot <path>
mindvault backup                              # zip of all vault markdown
mindvault prune --days 30                     # delete old snapshots (explicit only)
```

Every mutation: vault-jailed, snapshot-first, atomic write, YAML-verified (rollback on
failure), reindexed. Archive instead of delete, always.

## Deeper docs

[AGENT_WORKFLOWS.md](AGENT_WORKFLOWS.md) · [CONTEXT_PACKS.md](CONTEXT_PACKS.md) ·
[DECISION_GRAPH.md](DECISION_GRAPH.md) · [SESSION_WORKFLOW.md](SESSION_WORKFLOW.md) ·
[VAULT_HYGIENE.md](VAULT_HYGIENE.md) · [MCP_SETUP.md](MCP_SETUP.md) · [DOCKER.md](DOCKER.md)
