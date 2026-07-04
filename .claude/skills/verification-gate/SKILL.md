---
name: verification-gate
description: Use this skill when you are about to claim work is done, fixed, passing, building, or working — before saying "done", "fixed", "passing", "it works", or when the user asks "verify this" or "is it actually done?". It produces a verified claim backed by fresh command output and an exit code. It does not run the .NET format/build/test sequence (use dotnet-quality-gate) or review code quality (use code-reviewer).
license: MIT
metadata:
  stack: agnostic
  version: 1.0
  last-reviewed: 2026-06-29
---

# Verification Gate

## Purpose

A universal evidence gate that must run before any claim that work is done, passing, fixed, built, or working. It forces a fresh observation that proves the claim, instead of relying on memory, earlier output, or optimism. The deliverable is a verified claim that cites the exact command run and its result.

## Use When

- You are about to say "done", "fixed", "passing", "it works", "all set", or any equivalent completion claim.
- The user asks "verify this", "is it actually done?", "did that work?", or "are you sure?".
- You are closing out any task and need evidence the work holds.
- Another skill has finished its work and needs a final evidence check before reporting success.

## Do Not Use When

- You need to run the .NET format, build, and test sequence end to end — use **dotnet-quality-gate**.
- You are reviewing code for correctness, design, or maintainability — use **code-reviewer**.
- You are diagnosing why something is broken rather than confirming it is fixed — use **bug-hunter**.
- You are triaging a failed CI pipeline — use **ci-triage**.

## Inputs To Look For

- The exact claim about to be made (e.g. "tests pass", "the bug is fixed", "the endpoint works").
- The command, request, or observation that would directly prove or disprove that claim.
- Any earlier output that is being used as a substitute for a fresh run — treat it as stale.
- The expected result: exit code, status code, response body, log line, or test count.

## Process

1. **Identify the proof.** State the single command or observation whose result would PROVE the claim. If you cannot name one, the claim is not verifiable yet — do not make it.
2. **Run it fresh.** Execute that command or make that observation now. Do not trust earlier runs, cached output, or memory of "it worked before". Code, environment, and state may have changed.
3. **Read the full output and the exit code.** Read all of it, not just the last line. Check the exit code explicitly (`echo $?` or the tool's reported code). A zero exit with error text in the body still fails.
4. **Confirm the output supports the claim.** Match the result against the expected result. Watch for partial passes, skipped steps, "0 tests ran", cached/no-op output, or success on the wrong target. A green line that does not actually exercise the claim does not count.
5. **State the claim with evidence.** Only now make the claim, and cite the command and its exit code or key output. If the evidence does not support the claim, report what actually happened instead — do not soften it with hedge words.

## Claim to Required Evidence

| Claim | Required evidence |
|-------|-------------------|
| Tests pass | Test runner output shows the expected count with 0 failures, exit code 0 |
| Build succeeds | Build command completes with exit code 0 and no errors |
| Bug is fixed | The original failing repro now runs and produces the correct result |
| Endpoint works | An actual request returns the expected status code and body |
| Lint/format clean | The checker reports no violations, exit code 0 |
| File was changed | A fresh read of the file shows the new content |
| Service is up | A health check or request against the running service responds as expected |

## Banned Phrases

These are forbidden as substitutes for evidence. They are tells of an unverified claim.

- **Hedge words:** "should", "probably", "seems to", "I think", "likely", "ought to", "in theory". If you are hedging, you have not verified — go run the check.
- **Premature celebration:** "Great!", "Perfect!", "Done!", "All set!", "Fixed!" stated before the evidence. Celebration is not proof.

State the command and its result instead. "Tests pass — `dotnet test` exited 0, 142 passed, 0 failed" is allowed. "Should be working now!" is not.

## Output Format

State the claim, the evidence, and the verdict.

### Verification: tests pass after the null-check fix

**Claim:** All unit tests pass after fixing the null reference in `OrderService`.
**Proof command:** `dotnet test --no-build`
**Exit code:** 0
**Key output:** `Passed! - Failed: 0, Passed: 142, Skipped: 0, Total: 142`
**Verdict:** VERIFIED — the claim holds.

If verification fails, report it plainly:

### Verification: endpoint works

**Claim:** `GET /api/orders/42` returns the order.
**Proof command:** `curl -i http://localhost:5000/api/orders/42`
**Exit code:** 0
**Key output:** `HTTP/1.1 500 Internal Server Error`
**Verdict:** NOT VERIFIED — the endpoint returns 500, not the expected 200. Claim withdrawn. Next: investigate the server log for the stack trace.

## Quality Bar

- Every "done" claim cites the exact command run and its exit code or key output.
- The proof command was run fresh during this gate, not recalled from an earlier turn.
- The full output and the exit code were read, not just the final line.
- No banned hedge or celebration phrase stands in for evidence.
- When evidence contradicts the claim, the claim is withdrawn and the real result is reported.

## Failure Modes To Avoid

- Claiming success from memory of an earlier run instead of running it again.
- Reading only the last line and missing an error or warning earlier in the output.
- Treating a zero exit code as success when the body contains an error.
- Accepting a partial or no-op pass ("0 tests ran", "nothing to build") as proof.
- Verifying the wrong target — a different branch, project, or stale build artifact.
- Substituting "should work" or "Done!" for an actual command result.

## Related Skills

- **dotnet-quality-gate** — runs the .NET format, build, and test sequence; reference this gate as its closing step.
- **code-reviewer** — for correctness and design review, separate from evidence verification.
- **bug-hunter** — when verification fails and you need to diagnose why.
- **ci-triage** — when the failing evidence comes from a CI pipeline.

Other skills should reference this skill as their CLOSING step: before any skill reports its work as done, passing, or fixed, it runs this gate to back the claim with fresh evidence.
