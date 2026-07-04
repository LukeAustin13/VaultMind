---
name: pull-request-review-swarm
description: Use this skill when you need a thorough, multi-perspective PR review — "full review", "comprehensive review", "review this properly before merge". It classifies PR size, dispatches up to six review passes (correctness, test gaps, security/config, performance, docs consistency, principles) as parallel subagents where available, adversarially verifies every blocking finding against the code, and produces a consolidated report with a GREEN/AMBER/RED merge verdict. Small PRs use a fast-track path (passes 1 and 3 only). For single-focus reviews, use the individual reviewer agents or skills directly.
license: MIT
metadata:
  stack: agnostic
  version: 2.1
  last-reviewed: 2026-07-03
---

# Pull Request Review Swarm

## Purpose

Coordinate a multi-perspective PR review by running six structured review passes and consolidating findings into a single actionable report. Each pass focuses on a distinct concern, preventing the common failure mode of a single review that tries to catch everything and misses important details.

## Use When

- A PR needs thorough review before merge.
- The user asks for a "full review" or "comprehensive review".
- A PR touches critical code paths (auth, payments, data access).
- Multiple areas of concern exist (logic, tests, security, performance).
- The user wants a structured review report, not scattered comments.

## Do Not Use When

- The PR is trivial (typo fix, comment update) — just review it directly.
- A general single-pass review is sufficient — use **code-reviewer**.
- Only one concern exists — use the specific reviewer agent instead.
- You are reviewing architecture, not a PR — use **architecture-reviewer** agent.
- You are triaging a CI failure — use **ci-triage**.

## Inputs To Inspect

- The full PR diff.
- Changed files and their context.
- PR description and linked issues.
- Existing test coverage for changed areas.
- Configuration file changes.
- CI/CD pipeline changes.
- Documentation changes or lack thereof.

## Process

0. **Confirm the review inputs exist.** Verify the five referenced agent files are present (pr-correctness-reviewer, test-gap-reviewer, security-config-reviewer, performance-reviewer, docs-maintainer) and the principles-reviewer skill. If any is missing, fall back to the **code-reviewer** skill for the affected concern and note the substitution in the report.
1. **Classify PR size.** Before running any passes, assess the PR:
   - **Fast-track:** Single concern (only one of logic, tests, security, performance, docs, or architecture plausibly needs review; the others are clearly out of scope for this diff), fewer than 100 lines changed, no auth/data/infrastructure changes. Run Pass 1 (Correctness) and Pass 3 (Security and Config) only.
   - **Standard:** Everything else. Run all six passes.
   - Record the classification and reason before proceeding. Do not skip the classification step.
1b. **Add conditional specialist passes.** Scan the diff's file list and add a pass when its trigger matches — these specialists exist but a fixed six-pass run never reaches them:
   - Diff touches `DbContext`, repositories, EF queries, or migrations → add the **data-access-reviewer** agent. When it runs, EF/N+1/tracking findings are owned by it, not by Pass 4.
   - Diff touches UI files (XAML, Razor, HTML/JSX components, forms) → add the **accessibility-reviewer** agent.
   - Diff changes public C# surface in a library or shared assembly → add the **breaking-change-detector** agent.
   Conditional passes run in the same parallel wave and report in the same table format. Record which conditional passes fired (or "none matched").
2. **Understand the PR.** Read the description, linked issues, and diff. Summarise the intent in one sentence.
2a. **Choose the dispatch mode.** If subagent dispatch is available (Claude Code Agent/Task tool with the reviewer agents registered), run the applicable passes as **parallel subagents** — one per reviewer agent, each given the diff scope and told to return findings in the table format below. Parallel dispatch is the default: it isolates each perspective (no pass anchors on another's findings) and cuts wall-clock time. Fall back to inline sequential passes only when dispatch is unavailable, and say which mode ran in the report. Subagent prompts must forbid file modification and git operations — reviewers read and report only.
3. **Pass 1 — Correctness.** Review for:
   - Logic errors, null handling, incorrect assumptions.
   - Edge cases and boundary conditions.
   - Broken control flow or early returns.
   - Regression risk.
   - (Apply the review method in [pr-correctness-reviewer](../../agents/pr-correctness-reviewer.md).)
4. **Pass 2 — Test Gaps.** (Standard track only.) Review for:
   - Missing tests for new code paths.
   - Weak assertions that pass trivially.
   - Missing edge case and error path coverage.
   - Test naming clarity.
   - (Apply the review method in [test-gap-reviewer](../../agents/test-gap-reviewer.md).)
5. **Pass 3 — Security and Config.** Review for:
   - Hardcoded secrets or credentials.
   - Auth/authorisation gaps.
   - Unsafe configuration defaults.
   - Injection risks.
   - (Apply the review method in [security-config-reviewer](../../agents/security-config-reviewer.md).)
6. **Pass 4 — Performance.** (Standard track only.) Review for:
   - Obvious performance regressions.
   - N+1 queries, unnecessary allocations.
   - Missing async/await or blocking calls.
   - (Apply the review method in [performance-reviewer](../../agents/performance-reviewer.md).)
7. **Pass 5 — Docs Consistency.** (Standard track only.) Review for:
   - README or docs that need updating.
   - Stale comments in changed code.
   - Missing or incorrect API documentation.
   - (Apply the review method in [docs-maintainer](../../agents/docs-maintainer.md).)
8. **Pass 6 — Principles.** (Standard track only.) Review for:
   - SRP violations — classes or methods doing more than one job.
   - DIP violations — concrete dependencies where abstractions should be injected.
   - DRY violations — duplicated logic or repeated magic values across the diff.
   - KISS violations — unnecessary abstraction layers or over-engineered solutions.
   - YAGNI violations — unused methods, dead config, interfaces with a single implementation.
   - (See `principles-reviewer` skill for full checklist. Findings from this pass are non-blocking by default unless the violation creates a real design problem today.)
9. **Consolidate findings.** Merge all findings, deduplicate (two passes flagging the same line report once, credited to both), and sort by severity. Principles findings appear in their own section.
10. **Verify blocking findings.** Before anything reaches the report as blocking, the orchestrator (not the pass that raised it) attempts to refute it against the actual code: re-read the path, look for the upstream guard or contract that would make it a false positive. Survivors are labelled **CONFIRMED**; plausible-but-untraceable ones **SUSPECTED** with the confirming check named; refuted ones are dropped and counted in the report footer ("N candidate findings refuted during verification"). This stage is what separates a swarm review from six opinions stapled together.
11. **Make the merge verdict.** GREEN (approve — no blockers), AMBER (request changes — merge after the named blockers are fixed), or RED (block — the PR is harmful as-is or does not do what it claims). The verdict must be justified by the verified findings, and it uses the same scale as **code-reviewer** so downstream tooling reads one vocabulary.

## Output Format

### PR Review: [PR Title]

**PR:** #[number]
**Author:** [author]
**Intent:** [One sentence summary]
**Verdict:** GREEN (approve) / AMBER (request changes) / RED (block)
**Dispatch mode:** Parallel subagents (passes 1,2,3,4,5,6) / Inline sequential
**Conditional passes:** [data-access-reviewer / accessibility-reviewer / breaking-change-detector, or "none matched"]
**Verification:** [N] blocking candidates checked, [N] confirmed, [N] suspected, [N] refuted and dropped

#### Executive Summary

[2-3 sentences on overall quality and key concerns]

#### Blocking Issues

| # | Pass | Status | File:Line | Issue | Severity | Suggested Fix |
|---|------|--------|-----------|-------|----------|---------------|
| 1 | Correctness | CONFIRMED | `OrderService.cs:42` | Null reference when order has no items — traced, no upstream guard | Critical | Add null check before `.First()` |

#### Non-Blocking Issues

| # | Pass | File:Line | Issue | Severity | Suggested Fix |
|---|------|-----------|-------|----------|---------------|
| 1 | Performance | `QueryHandler.cs:18` | N+1 query in loop | Medium | Use `.Include()` or batch query |

#### Test Gaps

- [ ] No test for empty order case in `OrderServiceTests`
- [ ] Missing edge case: negative quantity

#### Security / Config Concerns

- [ ] [Any security findings, or "None found"]

#### Principles Findings (non-blocking)

| # | Principle | File:Line | Violation | Suggested Fix |
|---|-----------|-----------|-----------|---------------|
| 1 | SRP | `OrderService.cs:18` | Class handles validation and persistence | Extract validation to `OrderValidator` |

#### Suggested PR Comment

```
[Ready-to-post summary for the PR, concise and actionable]
```

## Quality Bar

- The referenced agent files are confirmed present (or a documented fallback was used) before passes run.
- PR size is classified before any pass runs. The classification is stated explicitly.
- Fast-track PRs run passes 1 and 3 only. Standard PRs run all six passes.
- Conditional specialist triggers were checked against the diff's file list, and the report states which fired.
- The dispatch mode (parallel subagents vs inline sequential) is stated in the report; parallel is the default when dispatch is available.
- All applicable passes are completed, even if a pass finds nothing.
- Every blocking finding went through orchestrator verification and carries a CONFIRMED or SUSPECTED status; the refuted count is reported.
- Blocking issues are clearly separated from non-blocking and principles findings.
- Each finding has a file:line reference and a concrete fix.
- The GREEN/AMBER/RED verdict is justified by the verified findings.
- The suggested PR comment is ready to post without editing.

## Failure Modes To Avoid

- Skipping the size classification step.
- Running all six passes on a trivial single-concern fix — that is what fast-track prevents.
- Running passes sequentially inline when parallel dispatch was available — six opinions in sequence anchor on each other and take six times as long.
- Forwarding pass findings to the report unverified — the consolidation stage exists to kill false positives, not to format them.
- Letting the pass that raised a finding also verify it. The refutation attempt must be an independent read.
- Skipping passes because the PR "looks fine".
- Marking principles findings as blocking when they are non-blocking by default.
- Producing a review longer than the PR itself.
- Reviewing style preferences instead of real issues.
- Missing the forest for the trees — catching nitpicks but missing a logic bug.

## How Passes Relate To Agents

Each pass applies the review method defined in the corresponding agent file under `.claude/agents/`. **Default: dispatch each applicable pass as a parallel subagent** using the registered reviewer agents — each subagent gets the diff scope, applies its agent definition, and returns findings in this skill's table format; prompts must forbid file modification and git. The orchestrator consolidates, verifies, and issues the verdict.

**Fallback: inline sequential** — when subagent dispatch is unavailable, Claude reads each agent definition as a checklist and applies it within the current session. The agent files are the single source of truth for what each pass checks either way; this skill adds the classification, dispatch, verification, and consolidation around them.

This skill depends on the agent files being present. If you copy this skill to another project, copy the five referenced agents with it.

## Related Skills And Agents

- **pr-correctness-reviewer** agent — for standalone correctness review.
- **test-gap-reviewer** agent — for standalone test coverage review.
- **security-config-reviewer** agent — for standalone security review.
- **performance-reviewer** agent — for standalone performance review.
- **docs-maintainer** agent — for standalone docs review.
- **data-access-reviewer**, **accessibility-reviewer**, **breaking-change-detector** agents — conditional passes (step 1b) when the diff matches their territory.
- **code-reviewer** skill — the general-purpose single-pass code review skill.
