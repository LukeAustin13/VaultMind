---
name: dotnet-quality-gate
description: Use this skill when you need to validate .NET code changes by running formatting, build, and test commands in sequence. The dotnet-quality-gate ensures code compiles, passes tests, and follows formatting conventions before claiming work is complete. This is the .NET-specific gate — for non-.NET or mixed repos use verification-gate. It does not review code logic (use code-reviewer) or design architecture (use backend-architect).
license: MIT
metadata:
  stack: dotnet
  version: 1.2
  last-reviewed: 2026-07-03
---

# .NET Quality Gate

## Purpose

A practical .NET validation workflow that runs formatting, build, and test checks against code changes. It produces a clear pass/fail report with failure details and next actions.

## Use When

- You have made code changes in a .NET project and need to verify they work.
- The user asks "does it build?" or "do the tests pass?".
- You are about to claim implementation work is done.
- The user wants a pre-commit or pre-PR quality check.
- You need to validate a fix before moving on.

## Do Not Use When

- You are reviewing code for logic or design issues — use **code-reviewer**.
- You are debugging a runtime failure — use **bug-hunter**.
- You are triaging a CI failure — use **ci-triage**.
- You are running tests as part of writing them — use **test-writer**.

## Inputs To Inspect

- The solution or project file (`.sln`, `.csproj`).
- Changed files and their project membership.
- Existing test projects and their relationship to changed code.
- `.editorconfig` or format settings if present.
- Any `Directory.Build.props` or `Directory.Packages.props` files.

## Process

1. **Identify the scope.** Determine which projects were affected by the changes.
2. **Restore dependencies.** Run `dotnet restore` on the solution or affected projects. If restore fails, stop and report — nothing else will work.
3. **Check formatting.** Run `dotnet format --verify-no-changes` on affected projects. Report any formatting violations.
4. **Build.** Run `dotnet build --no-restore` on the solution. Capture and parse any errors or warnings.
5. **Select tests.** Identify test projects that cover the changed code. Prefer targeted test runs (`dotnet test --filter`) over running the entire suite when possible. Use the full suite only when changes are broad or the mapping is unclear.
6. **Run tests.** Run `dotnet test --no-build` on selected test projects. Capture results.
7. **Analyse failures.** For any failures:
   - Identify the failing step (restore, format, build, test).
   - Extract the specific error message.
   - Determine the likely root cause.
   - Suggest the minimal fix.
8. **Report results.** Produce the output format below.

## Output Format

### Quality Gate: [Solution/Project Name]

**Date:** [Date]
**Result:** [PASS / FAIL]

| Step | Command | Result | Duration |
|------|---------|--------|----------|
| Restore | `dotnet restore` | Pass/Fail | Xs |
| Format | `dotnet format --verify-no-changes` | Pass/Fail/Skipped | Xs |
| Build | `dotnet build --no-restore` | Pass/Fail | Xs |
| Test | `dotnet test --no-build [filter]` | Pass (N passed) / Fail (N passed, M failed) | Xs |

#### Failures (if any)

| # | Step | Error | Likely Root Cause | Suggested Fix |
|---|------|-------|-------------------|---------------|
| 1 | Build | CS0246: Type 'Foo' not found | Missing using directive or package | Add `using Foo.Namespace;` |

#### Test Filter Used

- **Filter:** `[filter expression or "full suite"]`
- **Reason:** `[why this filter was chosen]`

#### Next Action

- [What to do next — fix the failure, proceed to PR, run additional checks]

## Quality Bar

- Every step that was run has its actual command and result recorded.
- Failures include the specific error, not just "build failed".
- Test selection is justified — not always the full suite.
- The report never claims "done" when any step is red.
- A gate is never made to pass by editing tests, lowering thresholds, adding skips, or relaxing analyzer rules — only by fixing the underlying code.
- Next action is concrete and actionable.

## Anti-Cheating Rules

- Never modify a test, lower a coverage threshold, add a skip or ignore, or relax an analyzer rule just to make a gate pass.
- If a gate fails, STOP and report the failure with its output. Fixing the underlying code is the only acceptable way to turn a gate green.
- Gates run in order. A failure halts the sequence — do not run later gates, and do not claim success after an earlier gate failed.

## Failure Modes To Avoid

- Claiming work is complete when the build is broken.
- Running the entire test suite when only a few tests are relevant.
- Reporting "tests passed" without actually running them.
- Ignoring format violations as unimportant.
- Swallowing warnings that indicate real problems.
- Running `dotnet test` without building first and misinterpreting compile errors as test failures.

## Related Skills And Agents

- **code-reviewer** — for logic and design review after the quality gate passes.
- **ci-triage** — when CI fails and you need to diagnose pipeline vs code issues.
- **test-writer** — when failing tests reveal missing coverage.
- **pr-correctness-reviewer** agent — for deeper correctness review after the gate passes.
