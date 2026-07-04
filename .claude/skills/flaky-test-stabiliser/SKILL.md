---
name: flaky-test-stabiliser
description: Use this skill when a test passes sometimes and fails other times and you need to make it deterministic — "this test is flaky", "passes sometimes", "intermittent test failure", "test is non-deterministic", "test fails randomly in CI". It classifies the flakiness cause and applies the matching deterministic fix, then proves it with repeated green runs. It does not design the test suite, fixtures, or isolation strategy (use integration-test-designer) and does not write new tests for uncovered code (use test-writer).
license: MIT
metadata:
  stack: agnostic
  version: 1.0
  last-reviewed: 2026-06-29
---

# Flaky Test Stabiliser

## Use When

- A specific test passes on some runs and fails on others with no code change.
- A test fails only in CI, only under parallel execution, or only at certain times of day.
- A test fails when the suite runs in a different order.
- A test depends on real time, real network, real timezone, or real culture.
- Someone has wrapped a test in a retry/rerun attribute to "fix" the flakiness.

## Do Not Use When

- You are designing fixtures, isolation, or the overall integration test setup — use **integration-test-designer**.
- You are writing tests for code that has none — use **test-writer**.
- The test fails every single run for the same reason — that is a deterministic failure, use **bug-hunter**.
- The CI pipeline itself (not the test) is the problem — use **ci-triage**.

## Inputs To Look For

- The exact test name and its source file.
- The command and runner used to execute it (e.g. `dotnet test --filter`, xUnit, NUnit).
- Whether it fails in isolation, only in parallel, or only in a specific order.
- Use of `Task.Delay`, `Thread.Sleep`, `DateTime.Now`, `CultureInfo.CurrentCulture`, static/shared fields, or real HTTP calls inside the test or its setup.
- The failure message and stack trace from a failing run.

## Process

1. **Reproduce.** Run the single test in a loop (e.g. 20 times) until it fails at least once. If it never fails in isolation, run it under the same parallelism and ordering as the failing run. A flake you cannot reproduce cannot be confirmed fixed.
2. **Classify the cause** against the table below, using the failure message and the inputs you found. Name one primary cause.
3. **Apply the matching deterministic fix** from the table. Change the cause, not the symptom.
4. **Verify.** Run the test many times (at least 20) under the conditions that previously failed. The fix is accepted only at 100% pass across those runs. If any run fails, the classification was wrong — return to step 2.

### Causes and fixes

| # | Flakiness cause | Deterministic fix |
|---|-----------------|-------------------|
| 1 | Arbitrary sleeps / fixed timeouts waiting for work to finish | Poll the actual condition with a timeout; never sleep a guessed duration |
| 2 | Shared mutable state between tests (static fields, shared DB rows, singletons) | Isolate or reset state per test; give each test its own data |
| 3 | Test-order dependence (test B only passes after test A ran) | Make each test self-contained — arrange its own preconditions |
| 4 | Real time, timezone, or culture | Inject a clock; pin culture explicitly in the test |
| 5 | Async races / unawaited tasks | Await the operation deterministically; do not fire-and-forget |
| 6 | Real external network | Stub the boundary (HTTP handler / fake) so the test does not hit the network |

## Output Format

State the test, the reproduced failure rate, the classified cause, the before/after change, and the verified pass rate.

### Flaky Test Fix: `OrderProcessor_CompletesWithinWindow`

**File:** `tests/Orders.Tests/OrderProcessorTests.cs`
**Reproduced:** 6 failures in 20 isolated runs (30%)
**Cause:** (1) Arbitrary sleep — the test slept 100ms then asserted the order was processed, but processing occasionally took longer under load.

**Before**

```csharp
processor.Enqueue(order);
await Task.Delay(100);
Assert.Equal(OrderState.Processed, store.Get(order.Id).State);
```

**After**

```csharp
processor.Enqueue(order);
await WaitUntil(
    () => store.Get(order.Id).State == OrderState.Processed,
    timeout: TimeSpan.FromSeconds(5));
Assert.Equal(OrderState.Processed, store.Get(order.Id).State);
```

`WaitUntil` polls the condition on a short interval and throws on timeout — the test now waits exactly as long as the work takes, no longer, no less.

**Verified:** 20/20 passes in isolation and 20/20 under parallel execution. Failure rate 0%.

## Quality Bar

- The fix targets the classified cause, supported by evidence: the test was reproduced failing, then ran green many times under the failing conditions.
- The before/after shows a real change to the cause, not a wider timeout or a retry/rerun wrapper.
- No `Task.Delay`, `Thread.Sleep`, or fixed sleep remains as the synchronisation mechanism.
- The verification run count and conditions (isolation, parallelism, order) are stated, not assumed.

## Failure Modes To Avoid

- Wrapping the test in a retry/rerun attribute and declaring it fixed — this hides the flake, it does not remove it.
- Increasing a timeout instead of polling the actual condition.
- Declaring success after one green run — a flake needs repeated green runs to confirm.
- Fixing a symptom in the wrong test because the real cause was shared state set up by another test.
- Changing the assertion to be looser so it stops failing.

## Related Skills

- **integration-test-designer** — for designing fixtures, isolation, and the integration test harness that prevents flakiness by construction.
- **test-writer** — for writing new tests once the existing one is stable.
- **bug-hunter** — when the test fails every run for the same reason (a real bug, not a flake).
- **ci-triage** — when the flakiness is in the pipeline or environment rather than the test.
