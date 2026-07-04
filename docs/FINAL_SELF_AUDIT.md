# MindVault — Final Self-Audit (post-hardening pass)

Honest assessment after the OP hardening pass. Test state at writing: `dotnet build` clean,
**180/180 tests green**. Docker has **not** been executed in this environment (no Docker
installed); see §9.

## 1. What is now strong

- **Retrieval**: deterministic ranked search (title weight, exact/recency boosts, archived
  and superseded deprioritised, templates excluded), date filters, matched-section output,
  project-first scoping with visible fallback, and `--explain` to debug ranking. Porter
  stemming catches word variants.
- **Agent onboarding**: one call (`mindvault_start_session` / `context-pack`) yields goal,
  non-negotiables, decisions in force, tasks, risks, warnings, and ordered next reads —
  with refs, not dumps. Task descriptions sharpen relevance.
- **Write discipline**: draft checks catch duplicates/near-duplicates/vagueness before
  creation; every create surfaces advisory warnings; decision supersede keeps the record
  contradiction-free and validation flags mismatches.
- **Safety**: vault jail, snapshot-first, atomic temp-file writes, YAML verify-with-rollback,
  archive-not-delete, restore, two-note operations roll back on partial failure,
  environment writability probes in validate. One coordination lock (reentrant) serialises
  scans and writes; the index connection is single and lock-guarded.
- **Honesty loops**: sessions end with tests-run recorded ("not recorded" when omitted);
  status reports `rescanPending`; validate reports its own runtime.

## 2. What is still weak

- Near-duplicate detection is title-token Jaccard only — it will miss semantically
  duplicate notes with disjoint wording.
- Matched-section detection uses the first highlighted term; multi-topic notes can report
  a plausible-but-adjacent section.
- `recentImplementationLogs` sorts lexically by heading text (works because entries start
  with dates; a hand-written non-dated heading sorts arbitrarily).
- Decision graph resolves references only among decisions of the queried scope; a
  supersede link across projects renders as a node without an edge.
- CLI human output for `context` is functional, not beautiful.

## 3. Known limitations

- Search is lexical (FTS5 + stemming), not semantic. Embeddings remain deliberately out
  (revisit only if deterministic retrieval proves insufficient in real use).
- Change detection is mtime+size unless `verifyContentHash` is enabled.
- One frontmatter key per `update-frontmatter` call; comments inside frontmatter are lost
  on structural rewrites (key order is preserved).
- Cross-process concurrent writers remain last-writer-wins (snapshots + restore recover).
- Session state is stateless by design — there is no "current session" tracking; `end`
  trusts the caller to name the project.

## 4. Security considerations

- MCP surface is 19 tools, all funneled through resolver/PathGuard; no raw file/shell/SQL.
  A guard test pins the tool list and the skills' references to it.
- HTTP mode requires a bearer token (constant-time compare), binds localhost by default,
  and the compose example never publishes beyond one interface. Do not expose publicly.
- `mindvault_status` over MCP reports the vault *name*, not host paths. Internal errors
  return sanitized messages; full detail goes to stderr only.
- The container runs non-root; the vault mount is the container's entire data reach.

## 5. Deployment risks

- Docker image build is CI-verified (buildx amd64+arm64) but has not been run by a human
  on a real Pi in this pass. First `docker compose up` on the Pi is the confirming step.
- Schema bumps reset and repopulate the index transparently; on very large vaults the
  first post-upgrade query pays the rebuild cost.

## 6. Data-loss risks

- Lowest of any pass so far: snapshot-before-every-mutation + atomic replace + YAML
  verify/rollback + supersede rollback + restore. Residual risks: (a) `prune` deletes old
  snapshots when explicitly invoked — that is its job, but it is irreversible; (b) a
  crash between the archive status-write and the file move leaves the note archived-in-place
  (reported honestly, recoverable from snapshot); (c) snapshots live inside the vault —
  they are not an off-disk backup (`backup` + external backups remain necessary).

## 7. Agent misuse risks

- Over-creation is damped (draft checks, warnings) but not blocked — a determined agent
  can still create noise; warnings make it visible.
- `session log` could be spammed; skills teach restraint but nothing enforces a rate.
- An agent could archive aggressively; the hygiene skill requires per-note user approval,
  and archive is reversible, but the tool itself does not require confirmation.
- Bulk mutation loops (e.g. supersede in a cycle A→B→A) are not detected; validation
  would surface status contradictions after the fact.

## 8. Performance risks

- Ranked search adds a bounded rescoring pass (≤100 candidates) and per-result section
  lookups (≤limit body fetches from the FTS table) — negligible on vaults of thousands
  of notes, unmeasured beyond that.
- Context/pack building reads exactly one note body plus indexed queries.
- `verifyContentHash: true` hashes every mtime+size-matching file per scan — that is its
  documented cost; keep it off on the Pi unless mtime-preserving syncs are in play.
- Validation now probes the filesystem (2 tiny writes) and runs 3 extra indexed queries.

## 9. Manual Pi/Docker validation checklist (not yet performed)

```bash
docker compose up -d --build                      # builds ARM64 natively
docker compose run --rm mindvault init
docker compose run --rm mindvault scan
docker compose run --rm mindvault session start --project "<p>" --task "smoke"
docker compose run --rm mindvault search "anything" --explain
docker compose exec mindvault mindvault doctor
curl http://127.0.0.1:7777/healthz               # "ok"
curl -i -X POST http://127.0.0.1:7777/           # 401 without token
# then connect Claude Code over LAN with the bearer token and run the skills' test prompts
```

Also verify on the Pi: vault mount ownership (`user:` override), snapshot writes on the
mounted volume, and `validate` reporting no `vault-unwritable`/`snapshot-unwritable`.

## 10. Recommended next development pass

1. Run the Pi checklist above; fix whatever reality disagrees with.
2. Dog-food on a real vault for two weeks; tune ranking weights from `--explain` evidence.
3. Consider optional embeddings (off by default, rebuildable, local-only) **only if**
   lexical retrieval demonstrably misses; wire as a re-ranker, not a replacement.
4. Add `related`-link suggestions to draft checks (cheap: shared-tag + token overlap).
5. Structured `context --output markdown` for humans, mirroring the pack renderer.
6. Consider a soft rate-guard on session `log` entries per day.
