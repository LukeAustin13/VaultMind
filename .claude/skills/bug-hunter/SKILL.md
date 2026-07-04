---
name: bug-hunter
description: Use this skill when you need to diagnose a runtime error, failing test, unexpected behaviour, stack trace, log anomaly, or bad configuration. The bug-hunter follows a hypothesis-driven debugging process — it does not guess randomly or shotgun fixes. It produces a diagnosis with evidence before recommending a fix. It does not review code quality (use code-reviewer) or rewrite code (use refactorer).
license: MIT
metadata:
  stack: agnostic
  version: 1.3
  last-reviewed: 2026-06-29
---

# Bug Hunter

## Use When
- A test is failing and the cause is not obvious.
- The user reports unexpected runtime behaviour.
- You see a stack trace, error message, or exception.
- Logs show anomalies, warnings, or unexpected values.
- A configuration change broke something.
- The user says "this doesn't work" or "why is this happening?"

## Do Not Use When
- The code works but needs quality improvements — use **code-reviewer**.
- The user wants code restructured — use **refactorer**.
- You are investigating a security vulnerability — use **security-reviewer**.
- The problem is a design question, not a bug — use **planner** or **backend-architect**.

## Inputs To Look For
- Error messages and stack traces (exact text, not paraphrased).
- Log output around the time of failure.
- The code under suspicion.
- Recent changes (git diff, recent commits).
- Configuration files, environment variables.
- Steps to reproduce.
- Expected vs actual behaviour.

## Process
1. **Reconstruct the context.** Understand what was happening when the bug appeared. Read the error, trace, or log carefully. Do not skim.
2. **State the symptom.** One sentence: what is going wrong?
3. **Gather evidence.** Read the relevant code. Check recent changes with git. Inspect config files. Look at test output.
4. **Form hypotheses.** List 2-4 plausible explanations, ranked by likelihood. Each hypothesis must be falsifiable. If the user named a suspect component ("it's the cache layer"), record that as one hypothesis among the others — it earns rank from evidence, not from being said first, and it never narrows where you look.
5. **Test each hypothesis.** For each one:
   - What evidence supports it?
   - What evidence contradicts it?
   - What would confirm it? (A specific check, log line, or test.)
   - **After 3 failed fix attempts, STOP and question the assumptions/architecture — do not keep shotgunning.** Three misses means a premise is wrong: the symptom is mislocated, the reproduction is unreliable, or the mental model of the system is off. Re-derive from evidence instead of trying a fourth variation of the same guess.
6. **Narrow to root cause.** Identify the actual cause, not just the symptom. If you cannot determine root cause, say so and list what additional information is needed.
7. **Propose a fix.** The fix must address the root cause, not just suppress the symptom. If multiple fixes are possible, list them with trade-offs.
8. **Verify the fix.** Suggest how to confirm the fix works (run a test, check a log, reproduce the scenario).
9. **Lock it in with a regression test.** Any bug worth diagnosing is worth a test that fails without the fix and passes with it. Name the test case; hand off to **test-writer** if the test needs writing.
10. **Defense in depth.** Once the root cause is confirmed, validate at every layer the bad value crosses — entry-point, business logic, and the boundary guard nearest the failure — so the bug *class* becomes impossible, not just this instance patched. A value that was null at the entry point and exploded three layers down should be rejected at the entry point AND defended where it is consumed. The goal is that no future caller can reintroduce the same shape of bug through a different path.

## Output Format

### Bug Diagnosis

**Symptom:** [One sentence]

**Root Cause:** [One paragraph, or "Undetermined — see hypotheses below"]

#### Hypotheses

| # | Hypothesis | Likelihood | Evidence For | Evidence Against | Status |
|---|-----------|------------|-------------|-----------------|--------|
| 1 | ...       | High       | ...         | ...             | Confirmed / Eliminated / Untested |

#### Evidence Collected
- [File:line] — [What you observed]
- [Log output] — [What it indicates]
- [Git diff] — [What changed]

#### Recommended Fix
```
[Code or config change]
```
**Why this fixes it:** [One sentence]

#### Verification
- [ ] [How to confirm the fix works]
- [ ] [Regression test added or named — or why one is not feasible]

For two worked diagnoses (a null-reference case and a timezone-filter case), see [examples/bug-diagnosis-examples.md](examples/bug-diagnosis-examples.md).

## Quality Bar
- The symptom is stated precisely, not vaguely.
- At least two hypotheses were considered before settling on a cause.
- Evidence is cited with specific file locations, line numbers, or log excerpts.
- The fix addresses root cause, not just the symptom.
- A verification step is included.

## Common Rationalizations

These are the excuses that lead to patching the symptom instead of fixing the cause. Recognise them and reject them.

| Rationalization | Rebuttal |
|-----------------|----------|
| "The error is thrown on this line, so fix it here." | The throw site is where the bad value surfaced, not where it originated. Trace it back to where the value first went wrong. |
| "A try/catch will make it go away." | Swallowing the exception hides the symptom and leaves the cause live. The bad state still propagates — you have only muted the alarm. |
| "It works on retry, so it is fine." | Intermittent success means a race, ordering, or state dependency you have not characterised. It will fail again under load or in CI. |
| "It is probably a fluke / cosmic ray." | Software is deterministic given its inputs. "Fluke" is the name for a cause you have not found yet. Find the input that differs. |
| "The library/framework is buggy." | Almost always it is your usage, not the library. Reproduce against the library in isolation before blaming it; assume your code first. |
| "The fix worked locally, so we are done." | Local success proves nothing about the environment that failed. Reproduce the original failing condition, then confirm the fix there. |
| "The user already told me where the bug is." | The user reported where the pain surfaced, which is rarely where the defect lives. Their localisation is hypothesis #1, not a search boundary — if the evidence points elsewhere, follow the evidence and say so. |

## Failure Modes To Avoid
- Jumping to a fix without understanding the cause.
- Changing multiple things at once so you cannot tell what fixed it.
- Suppressing errors (try/catch with empty catch, ignoring return codes).
- Blaming the framework or library without evidence.
- Treating correlation as causation in logs.
- Giving up after one hypothesis fails instead of forming another.
- Declaring a bug fixed without a regression test, so it silently returns later.

## Related Skills

- **test-writer** — to write the regression test once the root cause is confirmed.
- **dotnet-quality-gate** — to validate the fix compiles and the full test suite still passes (.NET projects).
- **code-reviewer** — if the fix touches more than a few lines and deserves a review pass.
