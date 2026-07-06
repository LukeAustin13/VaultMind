# Read Plans

A read plan is the *itinerary* to the route card's *briefing*: a strict, ordered tool-call
plan the agent follows literally and stops when the stop conditions are met. At most five
reads, the project hub (with its map block) before raw notes, an explicit stop condition,
do-not-read guidance, and a narrowed search as the only sanctioned fallback.

Like the route card it wraps, the read plan is a **mid-session** discipline tool as of 0.8.0
— the session brief from `mindvault_start_session` already gives you a read-first list to
start from. Reach for a read plan when a fresh goal mid-session needs a strict, ordered read
path rather than a briefing.

## Briefing vs itinerary

The route card ([ROUTE_CARDS.md](ROUTE_CARDS.md)) tells you *what is true and what to read*;
the read plan tells you *exactly what to do, in what order, and when to stop*. `ReadPlanService`
is built **on** the route card (`ctx.Routes.Build(...)` with `MaxNotes: maxReads`), so the two
always agree — the plan is the card's read-first list turned into numbered steps.

## Step shape

Each `ReadPlanStep` has:

| Field | Meaning |
|---|---|
| `Order` | 1-based position; steps are `1..N` contiguous. |
| `Action` | `read_note` for note steps; `get_work_context` for the optional final step. |
| `Note` | The note path to read (null for the work-context step). |
| `Reason` | Why this note is on the plan (carried from the route card). |
| `ExpectedUse` | What to get out of it, phrased per note type — e.g. a project hub: "orient: decisions, risks and do-not-repeat rules in one read"; a decision: "know what is already decided before changing anything". |

When a `currentFile` is given and there is room under `maxReads`, the plan appends a
`get_work_context` step ("confirm no decision, constraint or mistake governs this edit").

## stopWhen

The stop conditions are fixed (`ReadPlanService.StopConditions`):

1. the current goal and its constraints are clear
2. active risks and do-not-repeat rules are known
3. you can state the next concrete change without another read

Follow the plan literally and stop when these hold — do not keep reading.

## doNotRead and fallbackSearch

`DoNotRead` is carried straight from the route card's low-value list (reasoned, and the hard
set is already excluded from the steps). `FallbackSearch` is the route card's suggested
narrowed `mindvault_search` call — the only sanctioned way to go beyond the plan, and only
if the reads leave the goal unclear. Run it with `snippetChars: 0` for refs-only hits when you
just need the candidate paths, then scope the read with `read_note`'s `section` / `maxChars`.

## Bounds

`maxReads` is clamped to 1–`DefaultMaxReads = 5`. The plan never exceeds five reads.

## CLI

```bash
mindvault read-plan --project "MindVault" --goal "improve config validation"
mindvault read-plan --project "MindVault" --current-file "src/MindVault.Core/WriteService.cs" --max-reads 3
```

Read plans are **always JSON** — a read plan is an agent artifact (mirrors project-context),
so there is no markdown form. An ambiguous project prints `{ ok: false, ambiguous: true,
candidates }` and exits 3.

## MCP

`mindvault_build_read_plan` (project, goal?, currentFile?, maxReads=5). Returns
`{ readPlan }` or `{ ambiguous: true, candidates }`.

## Limitations

- The plan is only as good as the route card it wraps — a thin vault yields a short plan.
- Token estimates are inherited from the card (see [TOKEN_EFFICIENCY.md](TOKEN_EFFICIENCY.md)).
- Stop conditions are advisory to the agent; nothing enforces that reading actually stops.
