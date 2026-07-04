---
name: qa-strategist
description: Use this skill when a feature, fix, or change needs a testing plan — "what should we test?", "write acceptance criteria", "create a test strategy", "what could this break?". It produces acceptance criteria, prioritised test cases across levels (unit, integration, end-to-end, manual), regression checks, and a risk-based coverage plan. It does not write test code (use test-writer), review existing test quality (use the test-gap-reviewer agent), or design integration test infrastructure (use integration-test-designer).
license: MIT
metadata:
  stack: agnostic
  version: 2.0
  last-reviewed: 2026-07-03
---

# QA Strategist

Produce a practical test strategy for a feature, fix, or change. Define what to test, how to test it, what to prioritise, and what risks to watch for. This skill plans testing — it does not write test code.

Formerly an agent (`.claude/agents/qa-strategist.md`); demoted to a skill because it is a repeatable procedure that generates a deliverable, not an isolated review perspective — and as a skill it triggers automatically on testing-plan phrasing.

## Use When

- A feature needs acceptance criteria before implementation starts.
- Implementation is done and the user asks "what should we test?" or "what could this break?"
- A risky change needs regression checks identified.
- Manual test scenarios are needed for UI or workflow features.

## Do Not Use When

- Writing test code — use **test-writer**.
- Reviewing existing test quality — use the **test-gap-reviewer** agent.
- Designing test containers, fixtures, or isolation strategy — use **integration-test-designer**.
- Performance test analysis — use **performance-profiler**.
- Security testing — use **security-reviewer**.

## Inputs To Look For

- The feature description, fix, or diff being tested.
- Existing test projects and their conventions (what levels already exist).
- The riskiest paths: money, auth, data mutation, external integrations.
- What already broke before (bug history for the touched area).

## Process

1. **Understand the feature, fix, or change being tested.** Read the actual diff or spec — the strategy must reference the real code paths, not a generic feature shape.
2. **Define acceptance criteria** — what must be true for this to be "done". Each criterion must be specific enough to verify pass/fail.
3. **Identify test levels needed:**
   - Unit tests (isolated logic).
   - Integration tests (component interaction, database, API).
   - End-to-end tests (full user workflow).
   - Manual tests (UI behaviour, visual, accessibility).
4. **For each level, design specific test cases:**
   - Happy path (expected success).
   - Edge cases (boundaries, empty, maximum).
   - Error cases (invalid input, failures, timeouts).
   - Regression cases (things that must not break).
5. **Prioritise by risk** — test the most dangerous paths first.
6. **Identify what is not worth testing** (trivial code, generated boilerplate) and say so explicitly.

## Output Format

### Test Strategy: [Feature/Task Name]

**Scope:** [What is being tested]
**Risk Level:** Low / Medium / High

#### Acceptance Criteria

- [ ] [Criterion 1 — specific and verifiable]
- [ ] [Criterion 2]

#### Test Cases

| # | Level | Scenario | Input | Expected Result | Priority |
|---|-------|----------|-------|----------------|----------|
| 1 | Unit | Export orders to CSV — happy path | 3 orders with valid data | CSV with 3 rows + header | High |
| 2 | Unit | Export orders — empty list | Empty list | Empty CSV with header only | High |
| 3 | Integration | Export endpoint returns CSV | GET /api/orders/export | 200 with CSV content-type | High |
| 4 | Manual | Download button triggers CSV download | Click "Export" on order list page | Browser downloads .csv file | Medium |

#### Regression Checks

- [ ] Existing order list endpoint still works
- [ ] Pagination on order list unchanged

#### Manual Test Scenarios (if applicable)

| # | Scenario | Steps | Expected |
|---|----------|-------|----------|
| 1 | [Scenario name] | 1. [Step] 2. [Step] | [Expected outcome] |

#### Not Worth Testing

- [Trivial code or generated boilerplate that does not need test coverage]

#### Risk Areas

| # | Risk | Why | Mitigation |
|---|------|-----|------------|
| 1 | Unicode characters in CSV | Encoding issues possible | Test with emoji and CJK characters |

## Quality Bar

- Acceptance criteria are specific enough to verify pass/fail.
- Test cases cover happy path, edge cases, and error cases.
- Priorities are based on risk, not completeness.
- Regression checks protect existing functionality.
- The strategy is proportional to the change — not a 50-row table for a one-line fix.

## Failure Modes To Avoid

- Writing acceptance criteria so vague they cannot be verified.
- Designing 100 test cases for a trivial change.
- Ignoring regression risk in favour of new-feature testing only.
- Treating all test cases as equal priority.
- Planning manual tests for things that should be automated.

## Related Skills And Agents

- **test-writer** — implements the test cases this strategy defines.
- **integration-test-designer** — designs the fixtures and isolation the integration cases run on.
- **test-gap-reviewer** agent — reviews existing coverage against a diff.
- **verification-gate** — proves the implemented tests actually ran.
