# Generated Summaries

`SummaryService` writes deterministic, extractive summaries into a dedicated generated block
so an agent (or a route card) can answer "what is this note?" without reading the body. No
LLM, no invention: the summary is the note's own first paragraph, the key points its own
headings/bullets, and `agentUse` a fixed phrase per note type.

## The marker block

The block lives between two markers, distinct from the map block's `mindvault-generated`
markers so both can coexist in one note — a project hub commonly carries both:

```
<!-- mindvault-summary:start -->
summary: <the note's first paragraph, ≤200 chars, word-safe>
agentUse: <fixed phrase for this note's type/status>
keyPoints:
- <H2/H3 heading or top-level bullet>
- …
needsReview: true            # only when the summary is weak
source: generated from headings/frontmatter/body
updated: yyyy-MM-dd
<!-- mindvault-summary:end -->
```

Fields: `summary`, `agentUse`, `keyPoints` (omitted if none), `needsReview` (only when set),
`source`, `updated`.

## Extractive rules

- **summary** — the first *real* paragraph: consecutive plain-text lines, skipping headings,
  bullets, quotes, tables, images, HTML comments and code fences. Capped at 200 chars, cut on
  a word boundary with " …". If there is no paragraph it falls back to
  `"<type>: <title>."` (and the summary is marked `needsReview`).
- **keyPoints** — H2/H3 headings (via `NoteParser.ExtractHeadings`), up to 5. If fewer than 2
  headings, it tops up from top-level `- `/`* ` bullets.
- **agentUse** — a *fixed* phrase per note type, not extracted. Examples: project → "Project
  hub — goal, non-negotiables and open questions live here."; decision in force → "Decision in
  force — check before contradicting it."; a **superseded** decision → "Superseded decision —
  historical context only; do not treat as in force."; mistake → "Do-not-repeat rule — read
  the prevention before similar work."
- **needsReview** is set when the summary fell back, `keyPoints` is empty, or the body
  (without the block) is under 200 chars — i.e. the note is too thin to summarize well.

## Which notes, and the size threshold

Project-wide runs (`ForProject`) only consider **managed, non-thought** notes outside
`08_Templates/` and the archive, not archived/superseded, whose file size is at least
`LargeBodyChars = 2400` (~600 tokens) — measured **excluding** any generated map block, so a
hub is not pushed over the threshold by its own map content. Templates are skipped entirely,
as are any legacy `09_Maps` map files (`Propose` returns null for them). Single-note runs
(`ForNote`) summarize the one note directly and error if it is a template or a legacy map
file. Project runs cap at 100 proposals per run (a warning names the overflow).

## Dry-run, apply and snapshots

Dry-run is the default: `apply` must be true to write. On apply the block is spliced in via
`ctx.Writer.ReplaceBody`, which is **snapshot-first** — human text is never touched, only the
block. `Splice` replaces an existing block in place, or inserts a new one after the H1 (or at
the top).

The same marker-ambiguity protection the map block uses applies here: the summary splice
classifies the `mindvault-summary` marker occurrences as None, Single or Ambiguous, and on
**Ambiguous** (the marker strings appear more than once, or malformed) it **refuses to write** —
no splice, no snapshot, nothing changed — and warns you to edit the note so the literal marker
appears exactly once. MindVault never guesses which occurrence is the real block, because
guessing risks overwriting human text. If you quote a summary marker in prose, break the literal
up so it is not an exact substring match. See [MAPS.md](MAPS.md#when-the-markers-are-ambiguous-mindvault-refuses-to-write).

```bash
mindvault summarize --project "MindVault"            # dry-run preview
mindvault summarize --project "MindVault" --apply
mindvault summarize --note "Architecture - Storage layout" --apply
```

MCP: `mindvault_generate_summaries` (project? | note?, apply?) — pass one of `project`/`note`,
not both. Returns `dryRun`, `notesConsidered`, `proposals`, `applied`, `warnings`.

## Idempotence

Re-running does nothing when the block is already current. `Propose` compares the existing
block to the regenerated one after stripping the volatile `updated:` line
(`StripVolatile`) — so a same-day-different-date rebuild is a no-op and `applied` stays 0.

## How consumers use summaries

- **Route cards** — `WithSnippets` calls `SummaryService.ExtractSummaryLine(body)` to pull the
  one-line `summary:` as a read-first snippet, preferring it over the first raw body line.
- **Capsules** — fall back to the hub's generated summary for the goal when the hub has no
  `## Goal` section.
- **Token audit / score** — `HasSummaryBlock` drives `summaryCoverage` and the "large note with
  no summary" waste signal; a large note without a block is flagged low-value
  ([TOKEN_EFFICIENCY.md](TOKEN_EFFICIENCY.md)).

## Limitations

- Extractive only — a poorly structured note yields a poor summary, hence `needsReview`.
- Only notes ≥ 2400 bytes are summarized project-wide; small notes are read whole anyway.
- Summary blocks are the one genuinely invasive feature; mitigated by dry-run default,
  snapshot-per-note and block-only splicing.
