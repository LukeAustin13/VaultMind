# Typed Graph

`LinkGraphService` derives a typed relationship graph deterministically from what already
exists: explicit wiki links typed by their endpoint note types, frontmatter project
membership, decision supersession, and exact normalized-title collisions. It never invents an
edge and never duplicates plain link data — it *interprets* it. A task linking a decision IS
`task_tracks_decision`; no new Markdown syntax.

## Edge-typing rules

`Classify(a, b)` types an explicit link by its endpoints and returns the **canonical
direction** (the edge always points the same way regardless of which side held the link).
Rules, in evaluation order:

| Type | Endpoints (from → to) | Canonical direction | Confidence | Reason |
|---|---|---|---|---|
| `supersedes` | decision → decision, one superseded | active → superseded | 0.9 | an active decision links a superseded one |
| `related_to` | decision ↔ decision (neither superseded) | as-linked | 0.7 | explicit link between decisions |
| `belongs_to_project` | any ↔ project | note → project | 0.9 (explicit link) / 1.0 (frontmatter) | explicit link to / frontmatter project |
| `task_tracks_decision` | task → decision | task → decision | 0.9 | the task tracks the decision it links |
| `mistake_prevented_by` | mistake → task | mistake → task | 0.9 | the linked task is the prevention for this mistake |
| `risk_mitigated_by` | risk → task | risk → task | 0.9 | the linked task mitigates this risk |
| `risk_mitigated_by` | risk → decision | risk → decision | 0.8 | the linked decision addresses this risk |
| `caused_by` | mistake → bug | mistake → bug | 0.8 | this mistake traces back to the linked bug |
| `caused_by` | mistake → decision | mistake → decision | 0.6 | the linked decision contributed to this mistake |
| `implements` | architecture → decision | architecture → decision | 0.8 | the architecture implements the linked decision |
| `review_finding_for` | review ↔ any | review → subject | 0.8 | review findings apply to the linked note |
| `blocks` | task ↔ task, one blocked | blocker → blocked | 0.5 | a blocked task links this task as its blocker |
| `related_to` | same-type ↔ same-type | as-linked | 0.7 | explicit link between `<type>` notes |
| `duplicates` | exact normalized-title collision | first-path → others | 0.6 | identical normalized titles |
| `references` | any other explicit link | as-linked | 0.7 | explicit wiki link |

## Sources and confidence

Every edge carries a `Source`: `explicit-link` (typed wiki links), `frontmatter` (project
membership, confidence 1.0) or `title-collision` (duplicates, confidence 0.6). When two rules
produce the same from/to/type key, the higher-confidence edge wins.

## Archived exclusion — except supersedes

Templates, legacy map files and thoughts are ineligible endpoints. Archived notes (in the archive folder
or `status: archived`) are excluded from every edge **except** `supersedes` — the one
relationship that is meant to touch archived history. Frontmatter membership and title-collision
edges skip archived notes entirely.

## The sidecar

`build` writes `.mindvault/link-graph.jsonl` — one JSON edge per line — an operational sidecar
that is **disposable, like the index**. It is a compiled snapshot: it goes stale until the next
build. `relationships` and `explain` do NOT use it — they recompute edges live from the current
vault, so they are never stale.

```bash
mindvault graph build --project "MindVault"          # writes the sidecar
mindvault graph relationships --note "Task - Add config validation"
mindvault graph explain --from "Decision - Use flat config" --to "Task - Add config validation"
```

MCP: `mindvault_build_graph` (project?) → `{ notes, edges, edgesByType, sidecar }`;
`mindvault_explain_relationships` (from, to) → `{ found, path, explanation }`. There is no MCP
tool for per-note relationships — that is CLI-only (`graph relationships --note`).

## Explain: direct and 2-hop

`Explain(from, to)` first looks for a **direct** edge touching both notes (highest confidence
wins) and reports its type, reason and confidence. If there is none, it searches **two hops**:
the strongest shared-neighbour path (`ea.Confidence + eb.Confidence` descending, ties on the
middle note's path), reported as "A relates to B via `<mid>`: type1 (reason), then type2
(reason)". If nothing connects them within two hops it says so and suggests adding an explicit
link. See [LINKING.md](LINKING.md) and [DECISION_GRAPH.md](DECISION_GRAPH.md) for the
untyped/decision-specific graphs.

## Limitations

- Edges only come from *explicit* links, frontmatter and exact title collisions — an implied
  relationship with no link produces no edge.
- Confidences are fixed heuristics per rule, not learned.
- The sidecar is a snapshot; rebuild it (`graph build` or `compile`) after link changes.
- `explain` stops at two hops; deeper relationships are not traced.
