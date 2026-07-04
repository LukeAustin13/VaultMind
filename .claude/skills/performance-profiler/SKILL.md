---
name: performance-profiler
description: Use this skill when you need to identify likely performance bottlenecks in code, databases, UI, network calls, containers, or deployment. The performance-profiler distinguishes between measured bottlenecks and suspected bottlenecks, and recommends targeted fixes with expected impact. It does not optimise code speculatively — it identifies where optimisation would matter most. It does not review general code quality (use code-reviewer) or fix bugs (use bug-hunter).
license: MIT
metadata:
  stack: agnostic
  version: 2.0
  last-reviewed: 2026-07-03
---

# Performance Profiler

## Use When
- The user reports that something is slow (page load, API response, build, query, startup).
- You are reviewing code that handles large datasets, frequent calls, or real-time requirements.
- You need to evaluate whether a design choice will cause performance problems.
- Database queries need performance analysis.
- Container or deployment performance needs assessment.

## Do Not Use When
- The code has a functional bug — use **bug-hunter**.
- You are reviewing code for general quality — use **code-reviewer**.
- You are optimising code structure without a performance concern — use **refactorer**.
- The problem is a design question — use **backend-architect**.

## Skill vs Agent

- Use this skill for a **standalone performance analysis** — hypothesis-driven, produces a bottleneck report with measured vs suspected classifications.
- Use the **`performance-reviewer` agent** for a **focused PR-pass** — checks for obvious regressions (N+1, allocations, blocking calls) without full profiling workflow.

## Inputs To Look For
- The specific complaint ("this page takes 10 seconds to load").
- Code paths involved in the slow operation.
- Database queries (raw SQL, EF Core LINQ, query plans).
- Network calls (HTTP clients, external API calls).
- Logs with timing information.
- Metrics or profiler output if available.
- Container resource limits (CPU, memory).

## Tool-Use Rules

- **Measurement-first.** If the system can be run, measure before hypothesising: a 10-line timing harness (`Stopwatch` around the suspect steps, timestamps in logs) or one real query plan beats an hour of reading code. "Suspected" classification is the fallback for systems that cannot be run here — not the default because measuring felt like effort.
- **Queries get plans, not opinions.** For any database suspect, obtain the actual execution plan (`EXPLAIN` / `EXPLAIN ANALYZE`, `SET STATISTICS IO, TIME ON`, EF Core `ToQueryString()` then run it) before recommending an index. An index recommendation without a plan is a guess with syntax.
- **Count the calls, not just the cost.** Grep the hot path for loops around I/O — one 5ms query inside a 200-item loop is the finding, and it never looks slow in isolation. Log or count invocations per request, not just duration per invocation.
- **Measure once more after the fix.** The same harness, the same workload, before/after numbers in the report. An optimisation without a second measurement is a hope.

## Process
1. **Understand the symptom.** What is slow? How slow? What is acceptable?
2. **Identify the hot path.** Trace the execution from trigger to completion. What are the major steps?
3. **Classify each step.** For each step on the hot path:
   - **CPU-bound:** Computation, serialization, regex, encryption.
   - **I/O-bound:** Database, file system, network, external API.
   - **Memory-bound:** Large allocations, GC pressure, collection growth.
   - **Concurrency:** Lock contention, thread pool starvation, async-over-sync.
4. **Estimate impact.** For each potential bottleneck:
   - Is this measured or suspected?
   - What is the estimated time spent here?
   - What is the frequency (once per request? once per item in a loop?)?
5. **Rank bottlenecks.** Order by estimated impact. Focus on the biggest contributors first.
6. **Recommend fixes.** For each bottleneck:
   - What to change.
   - Expected improvement.
   - Trade-offs (complexity, memory, correctness).
   - How to measure the improvement.

## Output Format

### Performance Analysis: [What is slow]

**Symptom:** [What the user observed]
**Acceptable Target:** [What would be acceptable, e.g., "< 200ms response time"]

#### Hot Path
```
Request -> Controller -> Service -> Database Query -> Mapping -> Response
            1ms          2ms        850ms             50ms       1ms
```
[Approximate timing. Mark measured vs estimated.]

#### Bottlenecks

| # | Location | Type | Impact | Measured? | Fix | Expected Improvement |
|---|----------|------|--------|-----------|-----|---------------------|
| 1 | `OrderRepo.cs:34` | I/O (DB) | 850ms / request | Suspected | Add index on `CustomerId` | ~100ms |
| 2 | `OrderMapper.cs:12` | CPU | 50ms / request | Suspected | Avoid re-mapping unchanged items | ~40ms |
| 3 | `Startup.cs:45` | Concurrency | Variable | Suspected | Register HttpClient as singleton | Reduces socket exhaustion |

#### Recommendations (Priority Order)
1. **[Highest impact fix]** — [Details and how to measure]
2. **[Second fix]** — [Details]

#### How To Measure
- [Tool or technique to measure before/after]
- [Specific metric to watch]

## Quality Bar
- Every bottleneck is classified as measured or suspected — and every "suspected" states why measurement was not possible, not just that it was not done.
- Database findings cite an actual execution plan, or are explicitly marked plan-not-obtained.
- Bottlenecks are ranked by estimated impact, not by ease of fixing.
- Recommendations include expected improvement and how to verify.
- The hot path is traced, not guessed.
- Premature optimisation is avoided — only address actual bottlenecks.
- Trade-offs of each fix are stated.

## Failure Modes To Avoid
- Optimising code that is not on the hot path.
- Recommending micro-optimisations when the bottleneck is a database query.
- Presenting suspected bottlenecks as confirmed without measurement.
- Recommending caching without considering cache invalidation.
- Ignoring the actual user complaint and profiling something unrelated.
- Over-optimising at the cost of code readability when the gain is negligible.
- Forgetting to suggest how to measure improvement.
