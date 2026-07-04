# Superpower Brain Pass — Results (v0.5.0)

Date: 2026-07-04. Companion to [SUPERPOWER_BRAIN_AUDIT.md](SUPERPOWER_BRAIN_AUDIT.md).
MindVault is now a session-aware Memory OS: it briefs (capsules), scopes (work-context),
remembers time (recall), learns (feedback), refuses secrets (content gate), keeps a
mistake ledger, and reports its own state (brain ops).

## 1. What shipped

| Capability | Where |
| --- | --- |
| Context capsules (7 modes, char budget, doNotRepeat, source paths) | `CapsuleService`, CLI `capsule`, `mindvault_build_context_capsule`, [CONTEXT_CAPSULES.md](CONTEXT_CAPSULES.md) |
| Work-context (current-file / query / note, reasons everywhere) | `WorkContextService`, CLI `work-context`, `mindvault_get_work_context`, [WORK_CONTEXT.md](WORK_CONTEXT.md) |
| Recall (since / on-this-day, grouped, mtime fallback) | `RecallService`, CLI `recall`, `mindvault_recall` |
| Feedback signals (sidecar jsonl, stem-keyed) | `FeedbackService`, CLI `pin`/`hide`/`feedback`, `mindvault_record_feedback`, [FEEDBACK_SIGNALS.md](FEEDBACK_SIGNALS.md) |
| Content risk scanner (block secrets, warn injection, redacted) | `ContentRiskScanner` + write-path gates, `RISKY_CONTENT`, [SAFETY_SCANNER.md](SAFETY_SCANNER.md) |
| Mistake ledger verbs + capsule integration | `CreateMistake`/`ResolveMistake`, CLI `mistake add|list|resolve`, 3 MCP tools, [MISTAKE_LEDGER.md](MISTAKE_LEDGER.md) |
| Inbox verbs over thought/promotion machinery | CLI `inbox add|list|promote|reject`, `mindvault_list_inbox` |
| Session lifecycle (checkpoint/handoff aliases, recent, dry-run) | `SessionService.Recent`, CLI `session checkpoint|handoff|recent`, `mindvault_checkpoint_session`, `mindvault_recent_sessions` |
| Brain ops rollup with pinned tool count | `OpsService`, CLI `ops`, `mindvault_brain_ops` |
| Skills v3 (11 skills) | +`mindvault-work-context`, +`mindvault-mistake-ledger`; session-handoff & project-context updated |

MCP surface 34 → **45 tools** (all additive). Version 0.5.0.

## 2. Verified behaviour (all from this session's tool output)

- **Build/test:** Release build 0 warnings; **322/322 tests green** (was 302).
- **Smoke on a live 80-note fixture vault:** capsule (coding) rendered goal,
  non-negotiables and decisions-in-force with paths at `confidence: exact`;
  `mistake add` → the debugging capsule immediately carried
  `Do Not Repeat: … Always benchmark Release builds only`; work-context returned grouped,
  reason-tagged results (`matches the query; same project; status accepted`); recall
  grouped the new mistake (created) and the session log (updated) correctly; the scanner
  blocked a private-key append with exit 2 and a fully redacted message; handoff → `session
  recent` listed it newest-first; `ops` reported verdict GOOD, 45 tools, honest counts and
  a concrete fix list; organize/links/map/validate all still work on the same vault.
- **Two real bugs were caught and fixed by evals/smoke, honestly:** (1) capsule
  `doNotRepeat` preferred the lesson text over the actionable prevention rule — swapped;
  (2) work-context multi-word queries hit FTS implicit-AND and returned nothing —
  OR-joined like context packs, then re-verified live.

## 3. Benchmarks (same machine, after the pass; 10k notes)

| metric | 0.5.0 | 0.4.0 run | verdict |
| --- | --- | --- | --- |
| cold scan | 1,536 ms | 1,688 ms | no regression |
| incremental scan | 34.5 ms | 28.9 ms | noise (0.3.0 measured 41.5) |
| search (ranked) | 10.8 ms | 12.1 ms | no regression — FTS path untouched by design |
| project context | 4.0 ms | 4.2 ms | unchanged |
| context pack | 32.1 ms | 25.0 ms | within the 25–29 ms historical band + noise |
| validate | 182 ms | 153 ms | noise — identical issue counts (1050c/400w/807i) across all three passes |

All new features are on-demand commands; none touch the scan/search hot paths. Feedback
deliberately does NOT apply to raw search.

## 4. Deliberately rejected (spec items included)

- **Semantic rerank stub (spec §15).** A disabled `ISemanticReranker`/`NullSemanticReranker`
  interface is dead code with zero callers — rejected under the no-unneeded-abstractions
  rule. If a deterministic eval ever proves a semantic gap, the interface ships in the
  same change that uses it.
- **Duplicate MCP tools:** `mindvault_write_handoff` (= `mindvault_end_session`),
  `mindvault_inbox_add` (= `mindvault_capture_thought`), `mindvault_inbox_promote`
  (= `mindvault_promote_note`), `mindvault_inbox_reject` (= `mindvault_archive_note`),
  `mindvault_apply_link` (= `mindvault_link_notes`). The CLI has all the ergonomic
  spellings; the MCP surface stays one-tool-per-capability.
- **Auto-decaying/auto-applied feedback** — all signals are explicit, with reasons.
- Vault chat, dashboards, cloud sync, sharing, Obsidian plugin — out of scope, as ever.

## 5. Known limitations (honest)

- The risk scanner catches regex-shaped secrets and injection phrasing; high-entropy
  strings with no known shape pass. It scans inbound writes only — it is a seatbelt, not
  DLP, and does not retro-scan existing notes.
- Feedback is keyed by file stem: renaming a note in Obsidian orphans its feedback (moves
  and promotions are safe — they preserve file names).
- Work-context file matching is token-based (camelCase + folder tokens through FTS); a
  note that discusses a file without naming it will not match.
- Recall trusts frontmatter dates first; hand-edited wrong dates mislead it (mtime is only
  the fallback).
- Capsule "current goal" reads the hub's `## Goal` section only; mode ordering is fixed,
  not configurable (by design — determinism over knobs).
- `session start` returns the context pack, not a capsule — kept for MCP contract
  stability; the capsule is one extra call when the mode matters.
- Raspberry Pi remains unmeasured for the new commands.

## 6. Acceptance

dotnet restore/build/test: pass (0 warnings, 322/322). Benchmarks: no material
regression. Smoke: every spec-listed command ran against a real fixture vault this
session, including `ops` — which now exists. The quality bar — cleaner, more connected,
more navigable, safer, more useful after every session — is enforced by the tools
themselves: capsule-first sessions, duplicate + content gates on the way in, feedback and
the mistake ledger compounding across sessions, and brain-ops telling the human what to
fix next.
