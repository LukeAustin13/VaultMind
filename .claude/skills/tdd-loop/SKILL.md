---
name: tdd-loop
description: >
  Use this skill when you need to build NEW code under strict test-first discipline —
  "do this TDD", "write the test first", "red-green-refactor", "test-driven development".
  It enforces the red-green-refactor loop one small cycle at a time and produces
  production code that never exists without a failing test that demanded it. It does NOT
  write tests for code that already exists (use test-writer) or review test quality
  (use code-reviewer). Cross-route — if the production code is already written and you
  are adding coverage after the fact, that is test-writer, not this skill; tdd-loop only
  applies when the test is written BEFORE the code it tests.
license: MIT
metadata:
  stack: agnostic
  version: 1.0
  last-reviewed: 2026-06-29
---

# TDD Loop

## The Iron Law

**NO production code without a failing test first.** Every line of production code must be written to make a specific, already-failing test pass. If you cannot point to the failing test that demanded a line of code, that line should not exist yet. There are no exceptions in this skill — the moment you exempt a case is the moment the discipline stops being TDD.

## Use When
- The user asks you to "do this TDD", "write the test first", or work "red-green-refactor".
- You are building new behaviour and want each step locked in by a test before it is written.
- A specification or acceptance criterion exists and you want to drive implementation from it incrementally.
- You are adding a new method, branch, or rule to existing code and want the new behaviour test-driven.

## Do Not Use When
- The production code already exists and you are adding coverage afterward — use **test-writer**.
- You need to judge whether existing tests are well-named, well-structured, or meaningful — use **code-reviewer**.
- You need to design which test types apply (unit vs integration vs contract) before any code exists — use **test-writer** to categorise, then return here to drive each unit.
- You are diagnosing why an existing test fails unexpectedly — use **bug-hunter**.
- You need integration fixtures, containers, or `WebApplicationFactory` setup — use **integration-test-designer** for the harness, then drive the logic here.

## Inputs To Look For
- The behaviour to build, stated concretely enough to express as an assertion (a rule, a calculation, a transformation, a validation).
- The testing framework and runner in use (xUnit, NUnit, MSTest, Jest, pytest) and how to invoke it.
- Existing test project, naming conventions, and assertion style.
- Acceptance criteria or examples that name expected inputs and outputs.
- The smallest next slice of behaviour — not the whole feature, the next assertion.

## Process

Work one cycle at a time. A cycle is RED, then GREEN, then REFACTOR. Do not start the next cycle until the current one is green and clean.

1. **RED — write the smallest failing test.**
   - Pick the smallest slice of unbuilt behaviour that you can assert on.
   - Write one test that asserts that behaviour. No more than one new behaviour per test.
   - **Run the test.** Do not skip this. Reading the test is not running it.
   - **Confirm it fails for the expected reason.** The failure must be an assertion failure (the value is wrong / the method returns the wrong thing) OR a deliberate "not implemented yet" — the reason you predicted. Read the actual failure message.
   - If it fails for the wrong reason — a typo, a compile error you did not intend, a missing using, a wrong test-project reference — fix that first and re-run until the test fails for the *expected* reason. A test that fails to compile has not yet entered RED.
   - If it passes immediately, the behaviour already exists or the test asserts nothing. Strengthen the assertion or delete the test; do not proceed.

2. **GREEN — write the minimum code to pass.**
   - Write the least production code that makes the failing test pass. Hard-coding a return value is acceptable here if the current tests do not yet forbid it — the next RED cycle will force generalisation.
   - Do not add code for behaviour no test demands. No speculative branches, no extra parameters, no "while I'm here".
   - **Run the test.** Confirm it now passes.
   - **Run the whole test suite.** Confirm nothing else broke. Green means all tests green, not just the new one.

3. **REFACTOR — improve with tests staying green.**
   - With the bar green, improve names, remove duplication, simplify control flow, extract methods.
   - Change structure only, never behaviour. Add no new behaviour in this step — new behaviour needs a new RED cycle.
   - **Re-run the suite after each change.** If a refactor turns the bar red, revert it; the refactor was wrong, not the test.

4. **Repeat.** Return to step 1 with the next smallest slice. Keep cycles small — a cycle that takes many minutes of production code before going green is too big; split it.

5. **Stop when the behaviour is complete.** When every acceptance criterion is covered by a test that drove its implementation, the feature is done. Report the cycles run and the final suite state.

## Common Rationalizations

These are the excuses a model invents to skip the failing-test-first step. Each is a violation of the Iron Law. Reject all of them.

| Rationalization | Rebuttal |
|---|---|
| "This code is too trivial to test." | Trivial code still breaks and still regresses; a trivial test costs seconds and the regression it catches does not. |
| "I'll add the tests after." | Tests written after the code only confirm what you already wrote, including its bugs; they never had the chance to fail. |
| "It's just a refactor, no test needed." | Refactoring is only safe *because* tests already cover the behaviour; if they do not, write them first — that is **test-writer**, not licence to skip. |
| "The test is obvious, I'll just write the code." | If it is obvious, the test takes ten seconds; obviousness is an argument *for* writing it, not against. |
| "I already know what the code should be." | Knowing the answer is exactly when a test is cheap to write; write it and watch it fail to prove it was actually missing. |
| "Writing the test first slows me down." | Debugging untested code later is far slower; the loop trades a few seconds now for hours not spent in a debugger. |
| "I'll write all the tests up front, then all the code." | That is not the loop — untested code piles up between RED and GREEN; one test, one slice of code, then run. |
| "The framework/library makes this hard to test." | Hard-to-test usually means a design problem; let the difficulty of the test push the design, do not abandon the test. |
| "It's experimental / a spike, TDD doesn't apply." | A spike is fine, but it is throwaway — keep none of the spike code; production code re-enters under the Iron Law. |
| "Mocking this is too much setup for one test." | Heavy setup is a signal to shrink the unit or move the boundary, not to skip the test. |

## Red Flags

If any of these is true, you have left the loop. **Stop, delete the untested production code, and restart from a failing test.**

- You wrote production code and there is no test currently failing because it was missing.
- You wrote a test and never ran it before writing the code.
- You wrote the test and the code together and ran them for the first time after both existed.
- A new test passed on its very first run and you proceeded anyway.
- You added a branch, parameter, or method that no test exercises.
- You "fixed" a red test by changing the test's assertion to match whatever the code returned, rather than the other way round.
- You have several untested methods written and plan to "go back and cover them".

In every case the remedy is the same: discard the production code that no failing test demanded, write the test, watch it fail for the right reason, and only then write the code back.

## Output Format

Present each cycle explicitly, labelled RED / GREEN / REFACTOR, including the test run result at each step. End with the suite state.

### Worked Example: a `Discount` calculator

**Cycle 1 — RED**

```csharp
public class DiscountTests
{
    [Fact]
    public void Apply_NoDiscount_ReturnsOriginalPrice()
    {
        var calc = new DiscountCalculator();
        Assert.Equal(100m, calc.Apply(100m, percentOff: 0));
    }
}
```

Run: `dotnet test`
Result: **FAILS** — `DiscountCalculator` does not exist (compile error). That is not the expected *assertion* failure. Create the minimal type so the test compiles and fails on the assertion instead:

```csharp
public class DiscountCalculator
{
    public decimal Apply(decimal price, int percentOff) => 0m;
}
```

Run: `dotnet test`
Result: **FAILS** — `Assert.Equal() Expected: 100, Actual: 0`. Correct reason. RED confirmed.

**Cycle 1 — GREEN**

```csharp
public decimal Apply(decimal price, int percentOff) => price;
```

Run: `dotnet test` — **PASSES**. Full suite green.

**Cycle 1 — REFACTOR**

Nothing to improve yet. Move on.

---

**Cycle 2 — RED**

```csharp
[Fact]
public void Apply_TenPercentOff_ReducesPrice()
{
    var calc = new DiscountCalculator();
    Assert.Equal(90m, calc.Apply(100m, percentOff: 10));
}
```

Run: `dotnet test`
Result: **FAILS** — `Expected: 90, Actual: 100`. Correct reason (the hard-coded `price` is now insufficient). RED confirmed.

**Cycle 2 — GREEN**

```csharp
public decimal Apply(decimal price, int percentOff)
    => price - (price * percentOff / 100m);
```

Run: `dotnet test` — both tests **PASS**. Full suite green.

**Cycle 2 — REFACTOR**

Extract the rate for readability; behaviour unchanged:

```csharp
public decimal Apply(decimal price, int percentOff)
{
    var rate = percentOff / 100m;
    return price - (price * rate);
}
```

Run: `dotnet test` — both tests still **PASS**. Refactor safe.

---

**Cycle 3 — RED**

```csharp
[Fact]
public void Apply_NegativePercent_ThrowsArgumentOutOfRange()
{
    var calc = new DiscountCalculator();
    Assert.Throws<ArgumentOutOfRangeException>(() => calc.Apply(100m, percentOff: -5));
}
```

Run: `dotnet test`
Result: **FAILS** — no exception thrown (`Actual: 105`). Correct reason. RED confirmed.

**Cycle 3 — GREEN**

```csharp
public decimal Apply(decimal price, int percentOff)
{
    if (percentOff < 0)
        throw new ArgumentOutOfRangeException(nameof(percentOff));

    var rate = percentOff / 100m;
    return price - (price * rate);
}
```

Run: `dotnet test` — all three tests **PASS**. Full suite green.

**Cycle 3 — REFACTOR**

Nothing to improve. Behaviour complete for the current acceptance criteria.

**Final state:** 3 cycles, 3 tests, suite green. Each line of production code was written to satisfy a test that failed first.

## Quality Bar
- Every production line traces to a test that was failing before it was written.
- Every test was run and observed to fail for the expected reason before its production code existed.
- No test passed on its first run without being strengthened or deleted.
- GREEN steps added only the minimum code; no speculative branches or parameters.
- The full suite was run and green after every GREEN and every REFACTOR.
- REFACTOR steps changed structure only, never behaviour, and never introduced new tests.
- Cycles are small — each shows a single new assertion driving a single small change.

## Failure Modes To Avoid
- Writing the test and the production code in one go, then running them together for the first time — the test never had a chance to fail.
- Skipping the run in RED and assuming the test fails.
- Accepting a compile-error failure as RED — the test must fail on its assertion (or a deliberate not-implemented), not on a typo.
- Editing a test's expected value to match buggy output to "make it green".
- Writing more production code than the current failing test requires.
- Letting cycles grow large — pages of code between RED and GREEN means the slice was too big.
- Adding new behaviour during REFACTOR instead of opening a new RED cycle.
- Using this skill to retrofit tests onto code that already exists — that is **test-writer**.

## Related Skills
- **test-writer** — Use when the production code already exists and you need coverage after the fact, or to categorise which test types a feature needs before driving each unit here.
- **code-reviewer** — Use to judge whether the tests this loop produced are well-named, meaningful, and not coupled to implementation detail.
- **bug-hunter** — Use when an existing test fails in a way you did not predict and you need to diagnose the cause.
- **integration-test-designer** — Use to build the integration harness (containers, `WebApplicationFactory`, fixtures) when the behaviour under test is infrastructure, then drive the logic with this loop.

## Coding Standards Reference

For C# test and production code, apply the conventions in [docs/csharp-coding-standards.md](../../../docs/csharp-coding-standards.md). Test code is production code — the same naming, layout, and language-feature rules apply.
