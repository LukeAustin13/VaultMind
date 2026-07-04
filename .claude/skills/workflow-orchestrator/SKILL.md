---
name: workflow-orchestrator
description: Use this skill when you want to "take this from idea to PR", "orchestrate this build", or "run the full workflow end to end" — driving a feature through every stage by routing to the right skill at each step. It delivers a gated, sequenced run that hands off to existing skills in order and stops on the first failed gate. It does NOT produce the plan itself (use **planner**) or run the review passes itself (use **pull-request-review-swarm**) — it CALLS them. It owns sequencing, handoffs, and gates only; it re-implements no skill's work.
license: MIT
metadata:
  stack: agnostic
  version: 1.1
  last-reviewed: 2026-06-29
---

# Workflow Orchestrator

A thin top-level conductor that runs a feature end to end by routing to existing skills through enforced gates. It does no design, no implementation, no review, and no PR authoring of its own — it sequences the skills that do, checks each exit gate, and halts the moment a gate fails.

## The No-Duplicate Boundary

This is the load-bearing rule. Read it before anything else.

- **planner** PRODUCES a static plan: a staged breakdown of the work, written once, then handed off.
- **workflow-orchestrator** EXECUTES the cross-skill sequence described by that plan: it invokes each skill in order, enforces stop-on-failure gates between stages, and manages the handoffs.

The orchestrator owns sequencing, handoffs, and gates — nothing else. If you find yourself writing the plan, writing code, running a review checklist, or composing a PR body inside this skill, you have crossed the boundary. Stop and route to the named skill instead.

## Use When

- The user says "take this from idea to PR", "orchestrate this build", or "run the full workflow end to end".
- A feature needs to move through plan → implement → verify → review → PR and you want the stages sequenced with gates rather than run ad hoc.
- A previous run halted at a gate and you are resuming the pipeline from that stage.

## Do Not Use When

- You need the plan itself written — use **planner**; this skill consumes a plan, it does not create one.
- You need the multi-pass review run — use **pull-request-review-swarm**; this skill calls it, it does not perform the passes.
- You need code implemented — use **frontend-implementer**, **refactorer**, or **test-writer**; this skill routes to them.
- You need the .NET validation run — use **dotnet-quality-gate** directly for a one-off check.
- You only want a single stage (just the PR text, just a review) — call that skill directly; orchestration adds overhead with no benefit for one stage.

## Inputs To Look For

- A feature request or idea, and whether a **planner** plan already exists for it.
- The implementation stack (UI vs refactor vs new logic) — this decides which implementation skill the implement stage routes to.
- Whether the project is .NET (determines whether **dotnet-quality-gate** is in the pipeline).
- Any stage the user wants skipped or any gate they say is already cleared — treated per Gate Overrides below, not taken on faith.
- Resume point: if a prior run stopped at a gate, which stage to re-enter at.

## Process

Run the pipeline below in order. Each stage names the skill it routes to and the exit gate that must be satisfied to advance. **If any gate fails, STOP immediately, report the failing stage and gate, and do not proceed.** Do not perform a stage's work yourself — invoke the named skill.

1. **(Optional) Frame the idea — routes to `design-brainstorming`.** Only when the request is vague or under-explored. Exit gate: a clear problem statement and approach exist. Skip when the request is already concrete.
2. **Plan — routes to `planner`.** Produce the staged plan. Exit gate: the plan is explicitly approved by the user. No approval, no advance.
3. **Implement — routes to `frontend-implementer` / `refactorer` / `test-writer` / `tdd-loop`.** Pick the implementation skill by the kind of work: UI from a spec → **frontend-implementer**; behaviour-preserving cleanup → **refactorer**; tests for code → **test-writer**; red-green-refactor cycles → **tdd-loop**. Exit gate: each planned change is implemented and the working tree builds locally.
4. **Validate — routes to `dotnet-quality-gate` then `verification-gate`.** Run **dotnet-quality-gate** (format, build, test in sequence) on .NET projects, then **verification-gate** to confirm the change actually does what was asked. Exit gate: BOTH pass. A failure here halts the pipeline — fix and re-enter at stage 3, do not skip ahead to review.
5. **Review — routes to `code-reviewer` / `pull-request-review-swarm`.** Single small change → **code-reviewer**; substantial or multi-concern change → **pull-request-review-swarm**. Exit gate: no blocking findings remain (resolve or explicitly accept each before advancing).
6. **Open the PR — routes to `git-pr-assistant`.** Produce branch name, commit message, and PR description. Exit gate: PR text is ready to post. This is the terminal stage.

At every stage: confirm the exit gate is met before moving on, and record which skill handled the stage so the run is auditable. Record one line of the downstream skill's evidence (failing test names, reviewer verdict, approval quote) in the gate-result cell — a bare Pass is not auditable.

**Gate overrides and claimed gates.** A gate result can only be **Pass** (evidence recorded), **FAIL** (halt), **Skipped** (stage not applicable), or **WAIVED** (explicit user instruction to proceed without the gate). A user *claiming* a gate is cleared ("validation passed on my machine") is not evidence — either re-run the gate cheaply or record the stage as WAIVED with the claim quoted and the risk named. A red gate is never rewritten as Pass under any instruction; the user may WAIVE it, and the waiver — with what was red — carries forward into the run log and the PR description so the reviewer sees it. If the user insists a failed gate be recorded as passed, halt: that is a falsified audit trail, and the orchestrator's log is worthless if it can be dictated.

## Output Format

A pipeline run log: one row per stage, the skill it routed to, the gate result, and the current halt/continue state. Show the realistic example below.

### Workflow Run: Add CSV export to the reports page

**Plan source:** approved plan from **planner** (2026-06-29 run)
**Stack:** .NET 8 + Blazor → dotnet-quality-gate in pipeline

| # | Stage | Routed to | Exit gate | Result |
|---|-------|-----------|-----------|--------|
| 1 | Frame idea | design-brainstorming | Problem statement clear | Skipped — request already concrete |
| 2 | Plan | planner | Plan approved by user | Pass — 3-phase plan approved |
| 3 | Implement | frontend-implementer | Builds locally | Pass — export button + handler added |
| 4 | Validate | dotnet-quality-gate → verification-gate | Both pass | **FAIL — 2 tests red in `ReportExportTests`** |
| 5 | Review | code-reviewer / pull-request-review-swarm | No blocking findings | Not started |
| 6 | Open PR | git-pr-assistant | PR text ready | Not started |

**Status:** HALTED at stage 4 (Validate).
**Reason:** `dotnet-quality-gate` reported 2 failing tests in `ReportExportTests` (empty-dataset and quoted-field cases). Gate not satisfied.
**Next action:** Return to stage 3 with **bug-hunter** or **refactorer** to fix the failures, re-run stage 4, then continue. Stages 5 and 6 remain blocked until the validate gate passes.

## Quality Bar

- Every stage hands off to a named skill — the orchestrator performs no design, implementation, review, or PR authoring itself.
- No skill's work is re-implemented or duplicated inside this skill.
- Each stage states its exit gate, and the gate result is recorded before advancing.
- A failed gate halts the pipeline: no later stage runs while an earlier gate is unmet.
- The run log is auditable — every stage shows which skill handled it and whether its gate passed.
- The plan is consumed from **planner**, never authored here.

## Failure Modes To Avoid

- Doing a skill's job instead of routing to it — writing the plan, the code, the review, or the PR body inside this skill. This is the cardinal failure; route instead.
- Skipping a gate, or advancing while a gate is merely "probably fine" rather than confirmed met.
- Recording a user's claim that a gate passed as a Pass — a claimed gate is re-verified or logged WAIVED with the risk; it never silently becomes evidence.
- Proceeding past a failure — running review or opening a PR after a red validate gate.
- Re-implementing **planner**'s plan or **pull-request-review-swarm**'s passes inline instead of calling them.
- Collapsing stages so the handoff and gate are invisible, making the run impossible to audit or resume.
- Treating the pipeline as advisory — the stop-on-failure gate is mandatory, not a suggestion.

## Related Skills

- **planner** — produces the static plan this skill executes against; the orchestrator never writes it.
- **pull-request-review-swarm** / **code-reviewer** — the review stage; called, never performed inline.
- **frontend-implementer** / **refactorer** / **test-writer** — the implementation skills the implement stage routes to.
- **dotnet-quality-gate** — the .NET validate stage; run it directly for a standalone check.
- **git-pr-assistant** — the terminal PR-authoring stage.
- **bug-hunter** — where to route when a validate gate fails and the cause needs diagnosis.
