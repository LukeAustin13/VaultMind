---
name: ci-triage
description: Use this skill when a CI pipeline has failed and you need to diagnose the root cause, distinguish code failures from pipeline/infrastructure failures, and produce a focused repair plan. It does not fix code bugs (use bug-hunter) or review CI pipeline design (use devops-deploy).
license: MIT
metadata:
  stack: agnostic
  version: 2.0
  last-reviewed: 2026-07-03
---

# CI Triage

## Purpose

Analyse failing CI runs and produce a root-cause-focused repair plan. Distinguish between code failures (your code is broken), pipeline failures (the CI config is broken), and infrastructure failures (the runner or service is broken). Avoid wasting time fixing the wrong thing.

## Use When

- A CI pipeline has failed and the cause is not immediately obvious.
- The user asks "why did CI fail?" or "what broke the build?".
- Tests pass locally but fail in CI.
- A CI failure looks like a flaky test or environment issue.
- Multiple CI steps failed and you need to find the root cause.

## Do Not Use When

- You are debugging application logic — use **bug-hunter**.
- You are designing or modifying the CI pipeline — use **devops-deploy**.
- You are running local quality checks — use **dotnet-quality-gate**.
- The failure message is obvious and needs no analysis.

## Inputs To Inspect

- CI pipeline logs (GitHub Actions, Azure DevOps, etc.).
- The failing step name and exit code.
- The error message and surrounding log context.
- The CI workflow file (`.github/workflows/*.yml`, `azure-pipelines.yml`, etc.).
- Recent commits that may have triggered the failure.
- Test results output (if available).
- The diff between the last passing and first failing run.

## Tool-Use Rules

- **Pull the real logs, not the summary.** For GitHub Actions: `gh run list --limit 5` to find the run, then `gh run view <run-id> --log-failed` for the failing steps' actual output. The web UI summary and the failure annotation routinely omit the line that matters.
- **Establish the green-to-red range.** Find the last passing run and the first failing run of the same workflow on the same branch, then `git log --oneline <green-sha>..<red-sha>` — the root cause is almost always in that commit range or in an environmental change between those timestamps.
- **Reproduce locally before proposing a fix.** Run the failing step's actual command (from the workflow file, not from memory) locally. Reproduces → code failure, debug it. Passes locally → the difference (env var, tool version, OS, service availability) IS the finding — diff the workflow's environment against yours instead of staring harder at the code.
- **Re-run is evidence, not a fix.** One re-run of the failing job is a legitimate flakiness probe when nondeterminism is suspected — state that it is a probe. Re-running until green and calling it fixed is banned.

## Process

1. **Identify the failing step.** Find the first step that failed. Later failures may be cascading.
2. **Extract the error.** Get the actual error message, not just the step name.
3. **Classify the failure.**
   - **Code failure:** Compilation error, test failure, lint violation. The code is wrong.
   - **Pipeline failure:** Misconfigured step, wrong tool version, missing secret, bad path. The pipeline config is wrong.
   - **Infrastructure failure:** Runner timeout, service unavailable, rate limit, disk full, network error. The environment is wrong.
   - **Flaky failure:** Test passes sometimes, depends on timing, ordering, or external state.
4. **Trace the root cause.** Follow the error back to its source:
   - For code failures: which commit, which file, which line.
   - For pipeline failures: which workflow file, which step, which config value.
   - For infrastructure failures: which service, what the symptoms indicate.
   - For flaky failures: what the test depends on that is non-deterministic.
5. **Determine the minimal fix.** What is the smallest change to make CI pass?
6. **Assess risk.** Could the fix introduce new problems?
7. **Suggest verification.** How to confirm the fix works.

## Output Format

To classify a failure across the five triage columns, use [templates/ci-failure-classification.md](templates/ci-failure-classification.md).

### CI Triage: [Pipeline / Workflow Name]

**Run:** [Link or run ID]
**Failing Step:** [Step name]
**Failure Category:** Code / Pipeline / Infrastructure / Flaky
**Root Cause Confidence:** High / Medium / Low
**Evidence basis:** [What was actually inspected — e.g. "`gh run view 8412 --log-failed`; failing step reproduced locally with `dotnet test`" — or "logs only; local reproduction not possible because X"]

#### Error

```
[Exact error message from logs]
```

#### Analysis

| Field | Value |
|-------|-------|
| First failing step | [Step name] |
| Error type | [Compilation / Test / Lint / Restore / Timeout / Config] |
| Root cause | [Specific explanation] |
| Introduced by | [Commit hash or "pre-existing"] |
| Affects | [What downstream steps also fail because of this] |

#### Minimal Fix

```
[The fix — code change, config change, or manual action]
```

**Risk:** [Low / Medium / High — could this fix break something else?]

#### Verification

```
[Command to verify the fix locally before pushing]
```

#### Additional Notes

- [Flaky test history, known CI issues, etc.]

## Quality Bar

- The root cause is identified, not just the symptom.
- Code failures are not confused with pipeline or infrastructure failures.
- The fix is minimal — not a rewrite of the CI config.
- Cascading failures are traced back to the first failure.
- Flaky tests are identified as such, not treated as deterministic failures.

## Failure Modes To Avoid

- Treating a pipeline config error as a code bug.
- Treating an infrastructure issue as a code bug.
- Suggesting "re-run the pipeline" as a fix for a deterministic failure.
- Missing that a test passes locally because of environment differences.
- Fixing a downstream cascading failure instead of the root cause.
- Ignoring the CI workflow file when the failure is clearly config-related.

## Related Skills And Agents

- **bug-hunter** — for debugging code logic after CI identifies the failing area.
- **devops-deploy** — for CI pipeline design and modification.
- **dotnet-quality-gate** — for running the same checks locally.
- **test-writer** — when the fix reveals missing test coverage.
