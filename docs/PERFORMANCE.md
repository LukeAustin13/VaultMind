# Performance

MindVault must feel instant on a desktop and comfortable on a Raspberry Pi. This page
defines how performance is measured and what "fast enough" means. Real measured numbers
live in [PERFORMANCE_RESULTS.md](PERFORMANCE_RESULTS.md) — if a number is not there, it
was not measured.

## How to run the benchmarks

```bash
dotnet run -c Release --project tools/benchmarks -- --sizes 100,1000,10000
```

The harness generates a synthetic vault per size (`FixtureVaultGenerator` — deterministic,
includes broken links, archived notes, superseded chains, duplicate-ish titles and stale
tasks), then measures each operation and prints a Markdown table. `--keep` retains the
generated vaults for inspection. You can also generate a vault manually:

```bash
mindvault generate-fixture-vault --path C:\tmp\synthetic --projects 10 --notes-per-project 100
```

(The generator refuses non-empty directories — it can never touch a real vault.)

## What is measured

1. Cold scan (build the index from nothing)
2. Incremental scan (nothing changed)
3. Ranked search latency
4. Project context latency
5. Context pack latency (task-aware)
6. Draft check latency
7. Session start latency
8. Session end latency (a real mutation: lock + snapshot + append + reindex)
9. Full validation latency
10. SQLite index size
11. Managed heap / process working set

## Reference vault sizes and targets (desktop-class hardware)

| Size | Cold scan | Incremental scan | Search | Context pack | Validate |
| --- | --- | --- | --- | --- | --- |
| Small (100 notes) | < 1 s | < 50 ms | < 10 ms | < 25 ms | < 250 ms |
| Medium (1,000 notes) | < 5 s | < 100 ms | < 25 ms | < 50 ms | < 1 s |
| Large (10,000 notes) | < 30 s | < 1 s | < 100 ms | < 250 ms | < 5 s |

Raspberry Pi guidance: expect roughly 3–6× desktop times on a Pi 4/5 with an SD card
(unmeasured until the Pi checklist runs — see PERFORMANCE_RESULTS.md for status). The
targets above already leave that headroom: even at 6× a 10k-note vault stays interactive
for everything except the cold scan, which is a once-per-rebuild cost.

## Where the time goes

- **Cold scan** is parse-bound (Markdig + YAML + SHA-256 per note) and scales linearly.
- **Incremental scan** is a directory walk + mtime/size compare; index writes only for
  changed files.
- **Search** is one FTS5 query + a bounded rescoring pass (≤100 candidates) + ≤limit
  section lookups.
- **Validate** is table scans + link resolution, linear in notes + links, plus two tiny
  write probes.
- `verifyContentHash: true` re-hashes every unchanged-looking file each scan — keep it off
  on the Pi unless your sync preserves mtimes on real edits.
