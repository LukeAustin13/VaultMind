---
name: engineering-guardrails
description: Use this skill while doing any coding or change work to run the disciplined implementation loop — discover the existing architecture, plan before editing, state success criteria, make small surgical patches that follow repo conventions, verify, and hand off honestly. Triggers on phrases like "before you start coding", "keep it minimal", "don't over-engineer", "implement this properly", or any build/change request. It augments CLAUDE.md rather than replacing it. It does not review finished code against design principles (use **principles-reviewer**) or run a verification/quality gate itself (it routes to **verification-gate** or **dotnet-quality-gate**).
license: MIT
metadata:
  stack: agnostic
  version: 2.2
  last-reviewed: 2026-07-02
---

# Engineering Guardrails

A behavioural layer applied *while* building, not a review done afterwards. It encodes six guardrails as defaults that run on every change, wrapped in a discover → plan → implement → verify → handoff loop. It augments the standing rules in CLAUDE.md and does not override them.

## Use When

- You are about to write or modify code in response to any request.
- The user says "before you start coding", "keep it minimal", "don't over-engineer", or similar.
- A task has unknowns that could be guessed at instead of confirmed.

## Do Not Use When

- The code is already written and you want it checked against SOLID/DRY/KISS/YAGNI — use **principles-reviewer**.
- You need to confirm the change compiles, passes tests, and meets the bar — use **verification-gate** or **dotnet-quality-gate**.
- The task is to turn a feature request into a staged plan — use **planner**.

## Inputs To Look For

- The requirement: what the change must accomplish.
- The unknowns: paths, signatures, schema, framework version, existing patterns the change depends on.
- The blast radius: which files the task actually requires touching.

## The Six Guardrails

Each is a verifiable behaviour, not advice.

1. **Verify assumptions before coding.** List the unknowns the change depends on (a method signature, a config key, where a type lives, the framework version). Confirm each by reading the file or running a search before writing code. *Signal:* no edit rests on an unconfirmed guess; every assumption was checked against the repo.
2. **Prefer the minimal solution that satisfies the requirement.** Implement what the requirement asks and nothing more. No speculative generality — no extra parameters, interfaces, config, or abstraction for futures nobody requested. *Signal:* every element added traces to a stated requirement.
3. **Make surgical edits.** Change only what the task needs. Do not bundle orthogonal refactors, renames, formatting sweeps, or cleanups the user did not ask for. No broad rewrites without explicit justification and agreement. *Signal:* the diff contains only changes the task requires; unrelated lines are untouched.
4. **State verifiable success criteria up front.** Before writing code, define how you will know it is done — the concrete, checkable conditions (builds, the named test passes, the endpoint returns X). *Signal:* success criteria were stated before the first edit and are checkable, not vague.
5. **Follow the repo's conventions, not your defaults.** Before writing, find how the repo already does this kind of thing — error shape, DI style, naming, test structure — and match it. Inventing a second pattern needs a stated reason. *Signal:* the new code is indistinguishable in style from the file it lives in.
6. **Complete honestly.** No placeholder TODOs or stubbed branches unless the user explicitly accepted them. No claiming "done" on unverified work — "done" means the success criteria were checked with fresh output; anything unchecked is reported as unverified, and failing tests are reported as failing, never glossed. *Signal:* every completion claim is either backed by command output or explicitly labelled unverified.

## Process

1. **Discover.** Read the code the change touches and its callers; find the existing conventions (guardrail 5). In an unfamiliar codebase, orient first — **codebase-visualiser**'s Internal Orientation Mode if the area is large.
2. **Plan.** Restate the requirement in one line. List the unknowns the change depends on; resolve each by reading or searching before editing (guardrail 1). Then size the change by blast radius, not diff length — **SIMPLE** (one or two files, local behaviour, reversible by revert: state success criteria and go), **STRUCTURED** (several files or one subsystem, no contract changes: write an ordered change list with a verification step per item), or **ARCHITECTURAL** (public contracts, schemas, persisted data, cross-system coupling, anything hard to reverse: route to **planner** and get the plan approved before editing). State the sizing in the preamble. The dangerous failure is the silent downgrade — starting architectural work as simple because planning feels slow; a one-line schema default change is ARCHITECTURAL no matter how small the diff.
3. **State the success criteria** — the checkable conditions that mean "done" (guardrail 4), including which tests must pass.
4. **Implement in small coherent patches.** The minimal change, matching repo conventions, touching only the files the task needs (guardrails 2, 3, 5). If the repo has tests around the touched behaviour, run them as you go; write or update tests for changed behaviour rather than leaving it dark (**test-writer** for substantial coverage work, **tdd-loop** if building new code test-first).
5. **Verify.** Check the result against the stated success criteria with real command output — build, tests, or a manual check. For .NET, route to **dotnet-quality-gate**; for a formal done-claim, **verification-gate**. What could not be run gets named, not implied.
6. **Self-review the diff.** Before handing off, re-read the full diff top to bottom as if it were a stranger's PR: leftover debug output, dead code, accidental file touches, naming drift, a change that no longer matches the stated requirement. Fix what you find — it is the cheapest defect filter that exists, and it runs before anyone else's time is spent.
7. **Hand off.** Close with the handoff note below.

## Output Format

A short preamble before editing, then the change itself, then a handoff note.

Preamble:

> **Requirement:** Add a `status` filter to `GET /orders`.
> **Sizing:** SIMPLE — two files, no contract change (the new query param is optional), reversible by revert.
> **Assumptions to confirm:** the existing query lives in `OrderQueryService.cs`; `OrderStatus` is an enum; the controller already binds query params. — confirmed by reading both files.
> **Success criteria:** `GET /orders?status=Shipped` returns only shipped orders; existing unfiltered call is unchanged; `OrderQueryTests` still pass.
> **Scope:** edit `OrderQueryService.cs` and `OrdersController.cs` only — no changes to the DTO or repository.

Handoff note after the work:

> **Changed:** `OrderQueryService.cs` (filter clause in `BuildQuery`), `OrdersController.cs` (optional `status` query param).
> **Verified:** `dotnet test --filter OrderQueryTests` — 14 passed, 0 failed. Build clean.
> **Not verified:** behaviour under the legacy v1 route — no test exists and it was out of scope.
> **Risks:** none known beyond the unverified v1 route.
> **Next:** if v1 must support the filter too, that is a separate small change to `LegacyOrdersController.cs`.

For trivial changes both blocks collapse to a single line each — criteria/scope before, verification result after.

## Quality Bar

- Every assumption the change depends on was confirmed against the repo, not guessed.
- Nothing was added that does not trace to the stated requirement.
- The diff touches only what the task requires; no unrequested refactors rode along.
- Success criteria were stated before editing and are concrete enough to check.
- The new code matches the surrounding repo's conventions; any deliberate deviation has a stated reason.
- The full diff was re-read as a reviewer before handoff; no debug leftovers, dead code, or unintended file touches remain.
- The handoff note exists: touched files, what was verified with output, what was not verified, remaining risks, next action.
- No completion claim outruns the evidence — failing or unrun tests are reported as exactly that.

## Failure Modes To Avoid

- Writing code against a guessed signature or path instead of reading the file first.
- Adding "while I'm here" abstraction, options, or interfaces for hypothetical futures.
- Folding an unrelated cleanup or rename into the change so the diff no longer maps to the task.
- Starting to edit with only a vague sense of "done" and no checkable criteria.
- Sizing by diff length instead of blast radius — under-engineering a small-looking change to a contract, schema, or default is how half-migrated systems happen.
- Introducing a new pattern (error shape, DI style, naming scheme) when the repo already has one for the same job.
- Leaving a `TODO`, `NotImplementedException`, or stubbed branch behind without the user's explicit acceptance.
- Saying "done" or "tests pass" without fresh command output — or quietly omitting a failing test from the summary.
- Rewriting a working file wholesale when a targeted patch would do.
- Treating this as a post-hoc review — it runs during authoring; **principles-reviewer** runs after.

## Related Skills

- **principles-reviewer** — use after the code exists to check it against SOLID, DRY, KISS, and YAGNI; this skill is the proactive counterpart applied while writing.
- **planner** — use first when the work is large enough to need staged sequencing before any guardrails apply.
- **codebase-visualiser** — Internal Orientation Mode for the discover step in unfamiliar code.
- **test-writer** / **tdd-loop** — for substantial test coverage work during step 4, or building new code test-first.
- **verification-gate** / **dotnet-quality-gate** — the verify step routes here; this skill never claims a gate passed without them.
- **code-reviewer** — the natural next step after handoff for non-trivial changes.

The portable work loop, decision gates, and evidence discipline these guardrails enforce are stated in full in `docs/OPERATING_LAYER.md` (when that document travels with the skills); this skill remains self-contained without it.
