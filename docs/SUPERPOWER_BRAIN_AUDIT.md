# Superpower Brain Audit — from Vault Tool to Memory OS

Date: 2026-07-04. Audited at v0.4.0 (302/302 tests green, benchmarks regression-free).
This pass turns MindVault into a local-first **Memory OS for coding agents**: not just
"can answer questions about the vault" but "briefs the agent, tracks the session, recalls
the window, learns from feedback, refuses to store secrets, and reports its own state".

## 1. Current architecture summary

- Markdown canonical, SQLite FTS5 disposable index; one Core service layer under a thin
  CLI (40+ commands) and a 34-tool MCP surface; snapshot-first mutations, vault jail,
  archive-not-delete, write lock, atomic writes with post-write YAML verify.
- Project intelligence (0.3.0): alias/repoName detection with confidence tiers, duplicate
  gate in creates, related notes, health verdict, dry-run mutations.
- Organisation layer (0.4.0): deterministic placement + `organize` (dry-run default),
  thought/mistake types with capture + promotion, generated maps (09_Maps), link
  suggestions with reasons, broken-link/orphan detection, frontmatter/alias audits.

## 2. What MindVault already does well

Safety (snapshot/rollback torture-tested), identity (repo→project, never guesses),
anti-spam (duplicate refusal in the write path), placement (proposal-first organize),
navigation (maps, related notes, reason-tagged link suggestions), compact retrieval
(context packs, refs-not-bodies, bounded everything), honest ops (doctor verdict,
index verify, benchmarks with real numbers).

## 3. What is missing vs a top 0.1% coding-agent brain

1. **No mode-aware briefing.** Context packs exist but there is no single budgeted
   capsule with do-not-repeat rules, superseded warnings, open questions and source
   paths, shaped for coding vs debugging vs review vs handoff.
2. **No mid-session lifecycle.** start/end exist; there is no checkpoint verb, no
   "recent sessions" read, no capsule at start.
3. **No work-context.** "I am editing WriteService.cs — what memory touches this?" has
   no answer today; related-notes needs a note, not a file or query.
4. **No time-window recall.** "What changed in the last 7 days?" requires manual search.
5. **No feedback loop.** Retrieval cannot learn that a note was useful, noisy, outdated,
   wrong, pinned or hidden. Deterministic feedback is the highest-leverage ranking signal
   available without embeddings.
6. **No content risk gate.** An agent can currently write a private key or a prompt-
   injection payload into durable memory; nothing scans on the way in.
7. **No first-class mistake ledger commands.** The `mistake` type exists but capturing a
   lesson takes template knowledge; no add/list/resolve verbs, and capsules don't surface
   do-not-repeat rules.
8. **No single ops rollup.** doctor + validate + audits + index status exist separately;
   no one-call brain-state view with recommended fixes.

## 4. Highest-leverage upgrades (this pass, in order)

1. Feedback signals (sidecar `.mindvault/feedback.jsonl`, stem-keyed) — feeds every
   ranking surface.
2. Content risk scanner in the write paths (block high-confidence secrets, warn on
   injection language, redact evidence).
3. Context capsules (7 modes, char-budgeted, source-backed, feedback-aware).
4. Work-context (current-file / query / note modes, reasons on every result).
5. Recall (since/on-this-day windows, grouped by type, mtime fallback).
6. Session checkpoint/handoff verbs + recent-sessions read; MCP checkpoint + recent.
7. Mistake ledger verbs (add/list/resolve) + capsule integration (doNotRepeat).
8. Brain ops rollup (one call, counts + verdict + recommended fixes).
9. Inbox verbs (add/list/promote/reject) over the existing thought/promotion machinery.
10. Skills v3 (+work-context, +mistake-ledger; teach capsule-first sessions), evals on a
    new EliteBrainVault fixture, docs, 0.5.0.

## 5. Rejected hype features (deliberate)

- **Embeddings / semantic rerank stub.** Even the disabled-by-default interface is dead
  code with one hypothetical caller — rejected per the "no abstraction layers before
  they're needed" rule. Deterministic retrieval + feedback signals is the design; if an
  eval ever proves a semantic gap, the stub can be added in the same PR that uses it.
- **Vault chat, dashboards, cloud sync, sharing, Obsidian plugin** — out of scope, as ever.
- **Auto-applied feedback learning** (auto-hide "old" notes etc.) — feedback is explicit
  and human/agent-recorded only; silent relevance decay is how brains get gaslit.
- **Duplicate MCP tools for existing semantics** — `write_handoff` = `mindvault_end_session`,
  `inbox_add` = `mindvault_capture_thought`, `inbox_promote` = `mindvault_promote_note`,
  `inbox_reject` = `mindvault_archive_note`, `apply_link` = `mindvault_link_notes`. CLI
  gets the ergonomic spellings; the MCP surface stays one-tool-per-capability.

## 6. Implementation checklist

- [ ] FeedbackService + CLI pin/hide/feedback + `mindvault_record_feedback`; hidden
      excluded and pinned/useful boosted in capsule, work-context, related, suggestions
- [ ] ContentRiskScanner + `RISKY_CONTENT` + `--allow-risky-content`/`allowRiskyContent`
      through append / frontmatter / thought capture / mistake add / session writes
- [ ] CapsuleService + CLI `capsule` + `mindvault_build_context_capsule`
- [ ] WorkContextService + CLI `work-context` + `mindvault_get_work_context`
- [ ] RecallService + CLI `recall` + `mindvault_recall`
- [ ] Session `checkpoint`/`handoff` aliases + `session recent` +
      `mindvault_checkpoint_session` + `mindvault_recent_sessions`
- [ ] Mistake verbs + 3 MCP tools; capsule `knownMistakes`/`doNotRepeat`
- [ ] Inbox verbs + `mindvault_list_inbox`
- [ ] OpsService + CLI `ops` + `mindvault_brain_ops`; `McpToolCount` const pinned by test
- [ ] EliteBrainVault fixture + capsule/work-context/recall/feedback/scanner/handoff evals
- [ ] Guard tests 34 → 45 tools; skills 9 → 11
- [ ] Skills v3, docs (CONTEXT_CAPSULES, WORK_CONTEXT, MISTAKE_LEDGER, FEEDBACK_SIGNALS,
      SAFETY_SCANNER, SESSION_WORKFLOW update, DEMO_SCRIPT update), CHANGELOG, 0.5.0

## 7. Risk assessment

| Risk | Mitigation |
| --- | --- |
| Feedback keyed by path breaks on moves | Keyed by normalized stem (stable across organize/promote, which preserve file names) |
| Scanner false positives block legitimate notes | Only high-confidence secret shapes block (private key blocks, AKIA/ghp_/sk-/bearer tokens); injection language only warns; explicit override flag |
| Secret values leaking into errors/logs | Evidence is `code + length + offset`, never the matched text |
| Capsule bloat | Hard char budget with priority-ordered trimming; refs + reasons, never bodies |
| New surfaces slow hot paths | All new features are on-demand commands; search/scan/context untouched; benchmarks re-run before/after |
| Tool-count creep (34 → 45) | Every tool is a distinct capability; five spec-listed duplicates rejected (see §5); guard test pins the surface |
| Session/pack internals misunderstood | One bounded Opus audit mapped SessionService/ContextPackService/DecisionService/SearchService APIs with file:line evidence before capsule work started |

## 8. Verification plan

1. `dotnet build -c Release` → 0 warnings; `dotnet test` → all green (302 + new).
2. Benchmarks 1k/10k re-run; no material regression vs the 0.4.0 numbers.
3. Smoke on a generated fixture vault: capsule (coding), session start/checkpoint/recent,
   work-context by query and by file, recall --since, mistake add/list, inbox add/list,
   pin + feedback then capsule reflects it, scanner blocks a fake private key, ops.
4. SUPERPOWER_BRAIN_RESULTS.md written with honest outcomes, rejections and limitations.
