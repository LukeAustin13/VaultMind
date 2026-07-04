# Performance Results

Measured with `dotnet run -c Release --project tools/benchmarks -- --sizes 100,1000,10000`.

**Environment:** Windows 11 (10.0.26200), .NET 10.0.8, 16 cores, NVMe-class local disk.
MindVault v0.2.0. Run date: 2026-07-04 (post efficiency pass).

**Not yet measured:** Raspberry Pi (no Pi available in this environment). The same command
runs on the Pi; add a section here when the Pi checklist is executed.

## Efficiency pass — before/after (same machine, same day, 10,000-note vault)

The efficiency pass changed four things: (1) scans parse in parallel and commit in ONE
SQLite transaction instead of one per note, with `synchronous=NORMAL` (the index is
disposable cache); (2) filtered queries were rewritten to be sargable so the
`project`/`type` indexes are actually used; (3) snippets are generated only for the
result page, not the 100-candidate pool; (4) redundant I/O removed (state.json is
mtime-cached, section extraction skips full Markdown parsing, link warnings load one
note's links instead of the whole link table).

| metric (10k notes) | before | after | change |
| --- | --- | --- | --- |
| cold scan | 9,061 ms | 1,619 ms | **5.6× faster** |
| incremental scan | 197 ms | 31–47 ms | **~5× faster** |
| project context | 74.5 ms | 3.7 ms | **~20× faster** |
| session start | 67.8 ms | 6.4 ms | **~10× faster** |
| context pack | 81.5 ms | 29 ms | **~3× faster** |
| validate | 319 ms | 146 ms | **~2× faster** |
| draft check | 4.2 ms | 2.2 ms | ~2× faster |
| search (ranked) | 10.8 ms | 11.9 ms | unchanged¹ |
| session end (mutation) | 10.5 ms | 11.9 ms | unchanged (snapshot+fsync dominated) |

¹ Search was already index-bound; the snippet-deferral mainly pays off on vaults with
large note bodies (snippet cost scales with body size), which the synthetic vault's small
notes don't exercise.

The per-note-commit elimination matters most on the Pi: each commit used to fsync the WAL,
and SD-card fsyncs cost 5–20 ms — a 10k cold scan was heading for minutes on that
hardware. Parallel parsing additionally uses all 4 Pi cores.

## Current numbers

### 100 notes (2 projects × 50)

| metric | result | target | verdict |
| --- | --- | --- | --- |
| cold scan | 150 ms (104 notes) | < 1 s | ✅ |
| incremental scan | 3.5 ms | < 50 ms | ✅ |
| search (ranked) | 0.2 ms | < 10 ms | ✅ |
| project context | 1.2 ms | — | ✅ |
| context pack | 1.8 ms | < 25 ms | ✅ |
| draft check | 0.3 ms | — | ✅ |
| session start | 2.0 ms | — | ✅ |
| session end | 13.3 ms | — | ✅ |
| validate | 15 ms | < 250 ms | ✅ |
| index size (db+wal) | 559 KB | — | — |
| working set | 51 MB | — | — |

### 1,000 notes (10 projects × 100)

| metric | result | target | verdict |
| --- | --- | --- | --- |
| cold scan | 189 ms (920 notes) | < 5 s | ✅ |
| incremental scan | 4.3 ms | < 100 ms | ✅ |
| search (ranked) | 1.0 ms | < 25 ms | ✅ |
| project context | 1.4 ms | — | ✅ |
| context pack | 2.9 ms | < 50 ms | ✅ |
| draft check | 0.4 ms | — | ✅ |
| session start | 2.6 ms | — | ✅ |
| session end | 3.6 ms | — | ✅ |
| validate | 18 ms | < 1 s | ✅ |
| index size (db+wal) | 1.9 MB | — | — |
| working set | 98 MB | — | — |

### 10,000 notes (100 projects × 100)

| metric | result | target | verdict |
| --- | --- | --- | --- |
| cold scan | 1.6 s (9,110 notes) | < 30 s | ✅ |
| incremental scan | 31–47 ms | < 1 s | ✅ |
| search (ranked) | 11.9 ms | < 100 ms | ✅ |
| project context | 3.7 ms | — | ✅ |
| context pack | 29.1 ms | < 250 ms | ✅ |
| draft check | 2.2 ms | — | ✅ |
| session start | 6.4 ms | — | ✅ |
| session end | 11.9 ms | — | ✅ |
| validate | 146 ms (2,257 issues) | < 5 s | ✅ |
| index size (db+wal) | 30.4 MB² | — | — |
| working set | 117 MB | — | — |

² Includes the transient WAL file, which after a fresh bulk scan still holds a copy of the
data; SQLite checkpoints it back into the ~15 MB main file automatically (and always on
connection close). Steady-state on-disk size is ~15 MB.

## Notes on honesty and interpretation

- Note counts differ slightly from the nominal size (920, 9,110) because the generator
  distributes note kinds by ratio; the counts shown are what was actually scanned.
- The 10k vault's validate run deliberately faces pathological mess (~1,050
  duplicate-title criticals, 2,257 issues total) and still completes in 146 ms.
- Mutations (`session end`) are per-note cost — snapshot + atomic write + reindex — and
  stay ~4–13 ms at every vault size.
- `synchronous=NORMAL` trades nothing that matters here: under WAL it cannot corrupt the
  database, and the worst case (a power cut losing the last commit) costs an incremental
  rescan of a disposable cache.
- All desktop targets pass with one to two orders of magnitude of headroom — the margin
  the unmeasured Pi needs.
