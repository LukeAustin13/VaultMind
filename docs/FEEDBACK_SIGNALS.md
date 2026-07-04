# Feedback Signals

Deterministic retrieval feedback — the brain learns which memory is load-bearing without
embeddings, telemetry or magic. Stored as an append-only sidecar
(`.mindvault/feedback.jsonl`); vault Markdown is never touched.

## Usage

```bash
mindvault pin  --note "Decision: Use SQLite FTS5"
mindvault hide --note "Old Task"
mindvault feedback --note "Decision: Use SQLite FTS5" --signal useful --reason "good architecture context"
mindvault feedback --note "Old Task" --signal noisy --reason "obsolete"
mindvault feedback --note "Old Task" --signal clear      # reset everything for the note
```

MCP: `mindvault_record_feedback` (note, signal, reason).

## Signals and effect

| signal | effect |
| --- | --- |
| `pinned` | always surfaces first; joins capsule suggestedReads ("pinned by feedback") |
| `hidden` | never appears in capsules, work-context, related notes or link suggestions |
| `useful` | +2 ranking score |
| `noisy` | −2 |
| `outdated` | −3 |
| `wrong` | −4 |
| `clear` | resets flags and score for the note |

Scores accumulate across entries (two `useful` = +4). In work-context, a note whose score
drops to zero or below disappears from results.

## Where it applies — and where it deliberately doesn't

Applies to: context capsules, work-context, related notes, link suggestions, suggested
reads. Does NOT apply to: raw `mindvault_search` — the FTS hot path stays pure so search
results remain explainable by ranking factors alone.

## Mechanics

- Entries are JSONL: `{ts, stem, path, signal, reason}`. Malformed lines are skipped.
- Keyed by **normalized file stem**, which survives organize/promote moves (both preserve
  file names). Renaming a file in Obsidian orphans its feedback — re-record after renames.
- The sidecar is operational data: deleting `.mindvault/feedback.jsonl` resets all
  feedback and harms nothing else.
- Feedback is explicit only. Nothing auto-decays, auto-hides or auto-pins — silent
  relevance drift is how brains get gaslit.
