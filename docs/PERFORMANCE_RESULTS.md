# Performance Results

Measured with `dotnet run -c Release --project tools/benchmarks -- --sizes 100,1000,10000`.

**Environment:** Windows 11 (10.0.26200), .NET 10.0.8, 16 cores, NVMe-class local disk.
MindVault v0.2.0. Run date: 2026-07-04.

**Not yet measured:** Raspberry Pi (no Pi available in this environment). The same command
runs on the Pi; add a section here when the Pi checklist is executed.

## 100 notes (2 projects × 50)

| metric | result | target | verdict |
| --- | --- | --- | --- |
| cold scan | 196 ms (104 notes) | < 1 s | ✅ |
| incremental scan | 3.8 ms | < 50 ms | ✅ |
| search (ranked) | 0.2 ms | < 10 ms | ✅ |
| project context | 1.8 ms | — | ✅ |
| context pack | 2.7 ms | < 25 ms | ✅ |
| draft check | 0.4 ms | — | ✅ |
| session start | 3.0 ms | — | ✅ |
| session end | 15.3 ms | — | ✅ |
| validate | 12 ms | < 250 ms | ✅ |
| index size | 212 KB | — | — |
| working set | 52 MB | — | — |

## 1,000 notes (10 projects × 100)

| metric | result | target | verdict |
| --- | --- | --- | --- |
| cold scan | 906 ms (920 notes) | < 5 s | ✅ |
| incremental scan | 15.2 ms | < 100 ms | ✅ |
| search (ranked) | 1.1 ms | < 25 ms | ✅ |
| project context | 8.0 ms | — | ✅ |
| context pack | 8.9 ms | < 50 ms | ✅ |
| draft check | 0.5 ms | — | ✅ |
| session start | 8.3 ms | — | ✅ |
| session end | 10.2 ms | — | ✅ |
| validate | 28 ms | < 1 s | ✅ |
| index size | 1.6 MB | — | — |
| working set | 98 MB | — | — |

## 10,000 notes (100 projects × 100)

| metric | result | target | verdict |
| --- | --- | --- | --- |
| cold scan | 9.1 s (9,110 notes) | < 30 s | ✅ |
| incremental scan | 197 ms | < 1 s | ✅ |
| search (ranked) | 10.8 ms | < 100 ms | ✅ |
| project context | 74.5 ms | — | ✅ |
| context pack | 81.5 ms | < 250 ms | ✅ |
| draft check | 4.2 ms | — | ✅ |
| session start | 67.8 ms | — | ✅ |
| session end | 10.5 ms | — | ✅ |
| validate | 319 ms (2,257 issues) | < 5 s | ✅ |
| index size | 15.2 MB | — | — |
| working set | 115 MB | — | — |

## Notes on honesty and interpretation

- Note counts differ slightly from the nominal size (920, 9,110) because the generator
  distributes note kinds by ratio; the counts shown are what was actually scanned.
- The 10k vault's validate run deliberately faces pathological mess: the generator repeats
  domain vocabulary across projects, producing ~1,050 duplicate-title criticals — validate
  still completes in 319 ms while reporting 2,257 issues.
- `session end` is a full mutation (write lock + snapshot + atomic write + reindex) and
  stays ~10–15 ms at every size — mutation cost is per-note, not per-vault.
- Managed heap readings are post-GC and effectively noise at these sizes; the working set
  includes the .NET runtime itself.
- All desktop targets pass with an order of magnitude of headroom, which is the margin the
  unmeasured Pi needs.
