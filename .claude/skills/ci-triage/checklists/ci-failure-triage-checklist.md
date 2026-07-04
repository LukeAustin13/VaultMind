# CI Failure Triage Checklist

Use this checklist to systematically diagnose a CI failure.

## 1. Identify the Failure

- [ ] Found the first failing step (not a cascading failure)
- [ ] Extracted the actual error message
- [ ] Noted the exit code
- [ ] Checked if this is a new failure or a pre-existing one

## 2. Classify the Failure

- [ ] **Code failure?** Compilation error, test failure, lint violation
- [ ] **Pipeline failure?** Wrong tool version, missing secret, bad config path, misconfigured step
- [ ] **Infrastructure failure?** Runner timeout, disk full, network error, service unavailable, rate limit
- [ ] **Flaky failure?** Passes sometimes, depends on timing or external state

## 3. Trace the Root Cause

For code failures:
- [ ] Identified the commit that introduced the failure
- [ ] Identified the specific file and line
- [ ] Verified the error reproduces locally

For pipeline failures:
- [ ] Identified the workflow file and step
- [ ] Checked tool/SDK version matches local
- [ ] Checked for missing or expired secrets
- [ ] Checked for path or working directory issues

For infrastructure failures:
- [ ] Checked runner health and availability
- [ ] Checked external service status
- [ ] Checked for rate limiting or quota issues
- [ ] Checked disk and memory usage

For flaky failures:
- [ ] Identified the non-deterministic dependency (time, ordering, external state)
- [ ] Checked if the test has failed before (search CI history)
- [ ] Determined if the test needs fixing or quarantining

## 4. Determine the Fix

- [ ] Fix is minimal — addresses root cause only
- [ ] Fix does not introduce new risks
- [ ] Fix can be verified locally before pushing

## 5. Verify

- [ ] Re-ran the failing step locally (if possible)
- [ ] Confirmed downstream steps would also pass
- [ ] No other failures were masked by this one
