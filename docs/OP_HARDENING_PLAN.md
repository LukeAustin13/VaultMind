# MindVault — OP Hardening Plan

The contract for the intelligence/product-quality pass. Written before implementation;
checklist updated as items land.

## 1. Current architecture summary

- **Core** (`MindVault.Core`): config chain (`--vault` > env > local json), `VaultContext`
  service graph with one coordination lock (`Sync`) serialising scans and writes,
  `Lazy<IndexDatabase>` over a single locked SQLite connection (schema v2, FTS5 porter),
  flat-YAML frontmatter codec (key+value quoting, CR-safe), Markdig-based parser,
  incremental scanner (mtime+size, opt-in content hash), resolver (path > title > stem >
  slug > normalized; templates and operational folders excluded), snapshot-first writer
  (append/frontmatter/link/archive/restore, atomic-ish, verify-and-rollback), validation,
  doctor, project context, backup, prune.
- **CLI** (`MindVault.Cli`): 21 commands, `--json` throughout, exit codes 0/1/2/3.
- **MCP** (`MindVault.Mcp`): 14 safe tools, stdio + token-protected HTTP, annotations.
- **Skills**: 6 workflow skills; **Docker**: multi-arch image + compose; **CI**: build+test+buildx.
- **Tests**: 145 green.

## 2. Current weak spots

1. Search is raw bm25: no title weighting, no recency, archived notes rank equally,
   no date filters, no way to see *why* something matched, no project-preferred scope.
2. `get_project_context` returns flat lists only — no goal, non-negotiables, blocked
   split, implementation-log trail, next-read guidance, or contradiction warnings.
3. No pre-write quality gate: an agent can create near-duplicate/vague notes silently.
4. Decisions have no lifecycle links (supersedes/superseded_by/related) and no safe
   supersede operation, so contradicting decisions accumulate as equally "accepted".
5. No session lifecycle: agents have no one-call "brief me" / "write my handoff".
6. Validation lacks info-level checks (stale tasks, oversized notes, archived-but-linked,
   superseded-but-active) and environment probes (snapshot dir writable).
7. Templates cover only 3 of the 13 managed types.
8. Skills don't know about any of the above (they can't — the tools don't exist yet).
9. Writes are not atomic (a crash mid-`File.WriteAllText` can leave a torn note; the
   snapshot exists, but the note on disk is broken until manually restored).

## 3. Highest-leverage upgrades (ranked)

1. **Context pack + upgraded project context** — the single thing agents consume first.
2. **Weighted/filtered/explainable search** — retrieval quality gates everything else.
3. **Draft checks** — prevents memory garbage at the source.
4. **Session start/end** — one-call briefing + one-call handoff.
5. **Decision graph + supersede** — keeps the decision record trustworthy over time.
6. **Validation severity + new checks**, **templates**, **atomic writes**, **skills/docs**.

## 4. Safety risks (and mitigations)

- New write paths (supersede touches TWO notes; session log appends): all must go through
  the existing snapshot-first, verify-and-rollback, `ctx.Sync`-locked pipeline. Supersede
  snapshots both notes before touching either.
- Atomic replace: write temp file in the same directory, then `File.Move(overwrite)`
  (atomic on the same NTFS/ext4 volume). Temp files use a `.mindvault-tmp` suffix so the
  scanner never indexes them (not `.md`).
- No new MCP tool may accept a raw path outside resolver/PathGuard flow. Draft check,
  sessions, context pack are read-mostly; supersede reuses the resolver.

## 5. Retrieval risks

- Rescoring must be deterministic (no randomness, stable tie-break by path).
- Candidate pool for rescoring is bounded (limit×4, cap 100) so ranking can't scan the vault.
- Archived exclusion must not hide results silently — `--include-archived` and the
  global-fallback marker make scope visible.
- Section detection reads the FTS-stored body for top results only (bounded).

## 6. Agent-behaviour risks

- Over-logging: session `end` writes one block; `log` exists but skills teach restraint.
- Duplicate creation: draft check is advisory (warnings, not hard blocks) except where the
  create would fail anyway (exact title collision).
- Vault dumping: every new tool has caps; context pack carries refs, not full bodies.
- Ambiguity: project ambiguity keeps throwing with candidates — never guess.

## 7. Performance risks

- Rescoring + section lookup adds per-search work: bounded candidates, top-page-only
  section lookup, single body fetch per result from the FTS table.
- Context pack does at most: 1 project-note read + a handful of indexed queries + 1
  optional task-relevance search.
- Validation adds probes (2 tiny file writes) and 3 indexed queries — negligible.
- Template count ×10 only affects `init` (one-time) and adds ~7 indexed notes.

## 8. Test gaps to close

Ranked/filtered/explained search; archived handling; date filters; project fallback;
context (goal/non-negotiables/blocked/warnings/detail levels); context packs (md+json,
task-aware); draft checks (dupes, near-dupes, vague, missing project); decision
list/graph/supersede; session start/log/end; validation severities + new checks; atomic
write leaves no temp files; MCP output compactness; skills-reference-only-safe-tools;
Docker files still present and sane.

## 9. Implementation checklist

- [x] IndexDatabase: weighted-bm25 candidate query with updated-after/before +
      include-archived filters; `GetHeadings`, `GetFtsBody`, `GetFrontmatterValues`,
      `GetLargeNotes` helpers
- [x] SearchService: deterministic rescoring (title/exact/recency/archived), project-scope
      fallback, matched-section detection, `--explain` factors
- [x] SectionExtractor: section text / bullets / dated subheadings from note bodies
- [x] ProjectContextService: currentGoal, nonNegotiables, blocked split,
      recentImplementationLogs, relevantArchitecture, knownUnknowns,
      recommendedNextReads, warnings (stale/contradiction/broken-link/dup), detailLevel
- [x] ContextPackService: markdown + JSON packs, optional task-aware relevance,
      "do not forget" section
- [x] DraftCheckService: check-draft + check-note (dupes, near-dupes via token Jaccard,
      required fields, vagueness, missing sections, supersede suggestions)
- [x] WriteService: create results carry warnings; atomic replace writes; supersede
      operation (both notes snapshotted, statuses + supersedes/superseded_by set)
- [x] SessionService: start (ensures log note, returns pack), log, end (structured
      handoff block)
- [x] ValidationService: Critical/Warning/Info severities; stale-task, large-note,
      archived-but-linked, superseded-but-active, env-probe checks; timing
- [x] NoteTemplates: 10 concise templates; VaultStructure template list updated
- [x] CLI: `context`, `context-pack`, `check-note`, `check-draft`, `decision
      list|graph|supersede`, `session start|log|end`, `scan --incremental|--full`,
      search flags (`--explain`, `--include-archived`, `--updated-after/-before`)
- [x] MCP: `mindvault_get_context_pack`, `mindvault_check_draft`,
      `mindvault_supersede_decision`, `mindvault_start_session`,
      `mindvault_end_session`; search + context tool params extended
- [x] Skills: 6 updated to use packs/draft-checks/sessions/supersede; new
      `mindvault-session-handoff`, `mindvault-architecture-memory`
- [x] Docs: OP_USAGE, AGENT_WORKFLOWS, CONTEXT_PACKS, DECISION_GRAPH, SESSION_WORKFLOW,
      VAULT_HYGIENE; README + SKILLS_SETUP updated
- [x] Tests for all of the above; full suite green
- [x] FINAL_SELF_AUDIT.md

## 10. Intentionally out of scope

- Embeddings/vector search (deterministic retrieval first; revisit only if it proves
  insufficient in real use), web dashboard, cloud sync, Obsidian plugin, hard delete,
  raw file/SQL tools, background daemon, multi-user auth, automatic vault-wide
  summarisation, FileSystemWatcher (staleness TTL already covers freshness).
