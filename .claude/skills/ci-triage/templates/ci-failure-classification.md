# CI Failure Classification Template

Map a failing run across five columns to land on the right fix without guessing. Fill one row per failing step. Use the pre-populated rows below as a starting reference — they cover the most common patterns. Match your error against them first, then adjust the root cause location and minimal fix to your repo.

## Columns

- **Failing step** — the first step that failed (ignore cascading later failures).
- **Error type** — restore / build / test / lint / timeout / flaky / runner.
- **Root cause location** — where the actual cause lives: a file, a commit, a workflow step, an external service.
- **Code vs pipeline** — is your code wrong, or is the CI config/environment wrong? Also note infrastructure where it applies.
- **Minimal fix** — the smallest change that makes CI pass.

## Common Patterns

| Failing step | Error type | Root cause location | Code vs pipeline | Minimal fix |
|--------------|------------|---------------------|------------------|-------------|
| `dotnet restore` | Restore | NuGet feed / package source config | Pipeline | Pin or correct the feed URL; add missing credentials/source; check `nuget.config` path |
| `dotnet restore` | Restore | `packages.lock.json` out of date | Code | Restore with `--locked-mode` locally, commit the regenerated lock file |
| `dotnet build` | Build | Specific file + line in a recent commit | Code | Fix the compilation error in the named file; verify with a local build |
| `dotnet build` | Build | SDK version mismatch (`global.json` vs runner) | Pipeline | Align the runner SDK version with `global.json`, or update `global.json` |
| `dotnet test` | Test | Failing assertion in a named test | Code | Fix the code or the test expectation; reproduce locally first |
| `dotnet test` | Test | Missing env var / connection string in CI | Pipeline | Add the secret or variable to the CI environment; do not hardcode it |
| Any step | Timeout | Step exceeded the job/step time limit | Pipeline (or Code if a hang) | Raise the timeout only if work is legitimately long; otherwise fix the hang or slow query in code |
| `dotnet test` | Flaky | Non-deterministic dependency (time, ordering, shared state) | Code | Stabilise the test (use **flaky-test-stabiliser**); do not just re-run |
| Job setup | Runner | Runner unavailable, disk full, network error, rate limit | Infrastructure | Re-run after confirming runner/service health; no code change; escalate if persistent |
| `dotnet format` / lint | Lint | Style violation in a named file | Code | Apply the formatter locally and commit; verify with the same lint command |

## How To Use

1. Identify the first failing step and copy the matching row.
2. Replace the placeholder root cause with the specific file, commit, step, or service.
3. Confirm the code-vs-pipeline call — this decides who owns the fix.
4. Record the minimal fix and a local verification command.
5. If the row says infrastructure, do not change code; confirm environment health instead.

For flaky failures, hand off to **flaky-test-stabiliser** rather than fixing inline. For pipeline design changes beyond a minimal fix, use **devops-deploy**.
