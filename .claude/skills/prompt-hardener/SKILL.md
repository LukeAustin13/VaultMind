---
name: prompt-hardener
description: Use this skill when a task brief is vague, underspecified, or likely to produce generic output — "harden this prompt", "make this a proper brief", "write a prompt for the agent to do X" — or before dispatching a subagent with loose instructions. It converts a rough ask into an agent-ready brief with scope, constraints, deliverable, stop conditions, and verification, pulling answers from the codebase before asking the user. It does not execute the hardened task itself, and it does not harden prompts that are already specific — a clear ask should just be done.
license: MIT
metadata:
  stack: agnostic
  version: 1.1
  last-reviewed: 2026-07-04
---

# Prompt Hardener

Turn a rough ask into a brief an agent can execute without guessing. The output is a prompt, not the work — the value is that the hardened brief closes the gaps that make agents produce generic output: undefined scope, unnamed deliverable, missing constraints, no stop condition, no way to verify.

## Use When

- The user hands you a vague ask to pass on: "get the agent to clean up the auth code", "write a prompt for reviewing this".
- You are about to dispatch a subagent and your draft prompt lacks scope, deliverable shape, or a stop condition.
- A previous agent run produced generic output and the user wants the ask fixed rather than retried.

## Do Not Use When

- The ask is already specific — execute it; hardening a clear task is ceremony.
- The user wants the task *done*, not a prompt for it — do the task (route to the matching skill).
- The gap is a product/scope decision, not prompt wording — use **product-planner** or **design-brainstorming**.

## Inputs To Look For

- The rough ask, verbatim.
- The codebase or files the task concerns (most missing specifics live here, not in the user's head).
- Who or what will execute the brief (subagent, future session, another model) — weaker executors need tighter briefs.

## Process

1. **Extract the intent.** One sentence: what outcome does the requester actually want? If two readings are plausible, the ambiguity is finding #1.
2. **Run the five-gap check.** A brief is soft wherever one of these is missing:
   - **Scope** — which files/systems are in, and which are explicitly out.
   - **Deliverable** — the exact artifact and its shape (diff, report with named sections, table, plan).
   - **Constraints** — conventions to follow, things not to touch, size/effort budget.
   - **Stop conditions** — what "done" is, and when to halt and report instead of pushing on (ambiguity, failing tests, missing access).
   - **Verification** — the command or check that proves the result, and the evidence to include.
3. **Fill gaps from evidence before asking.** Grep/read the repo for the answers (the test command, the naming convention, the affected files). Only gaps the codebase cannot answer go to the user — as specific questions, not "any preferences?"
   While inspecting, also **verify the ask's embedded premises**. A rough ask often smuggles in factual claims ("migrate the tests from NUnit", "the auth code in `src/Auth`", "the flaky suite") — check each against the repo before writing it into the brief. A false premise is a blocking finding reported back to the requester, not a fact to enshrine: a hardened brief built on a wrong claim is worse than the vague ask, because it carries authority.
4. **Write the hardened brief.** Imperative, concrete, no personas, no "be thorough" — every vague adjective converted into a checkable instruction. Name the skill(s) the executor should apply if this repo's skill system is available to it.
5. **State assumptions.** Every gap you filled by inference is listed so the requester can veto it cheaply.
6. **Size it honestly.** If the hardened brief reveals the task is really three tasks, say so and split it — one brief per independently verifiable outcome.

## Output Format

### Hardened Brief: [one-line intent]

```
[The hardened prompt, ready to paste. Structure: goal → scope (in/out) →
constraints → deliverable shape → stop conditions → verification with evidence.]
```

**Assumptions made:** [each inference from the codebase, one line, vetoable]
**Open questions (blocking):** [only what neither codebase nor context answers — or "none"]
**Split recommended:** [yes: the pieces / no]

## Quality Bar

- Every vague adjective in the original ("clean", "thorough", "better") is either converted to a checkable instruction or deleted.
- Scope names real files/directories from this repo, not placeholders.
- Every factual claim embedded in the original ask was checked against the repo; any that failed the check is reported as a premise finding, not written into the brief.
- The stop conditions include at least one halt-and-report case, not only a success case.
- Verification names a runnable command or concrete check, not "make sure it works".
- Assumptions are listed — a silent guess baked into a brief is worse than a question.

## Failure Modes To Avoid

- Inventing fake specificity (naming files that don't exist, quoting conventions you didn't check).
- Hardening a false premise — converting the requester's unchecked claim into confident instructions an executor will follow without question.
- Inflating a one-line fix into a ceremony brief — hardening has a floor; below it, just do the task.
- Asking the user questions the repo answers in thirty seconds of grep.
- Writing role-play framing ("you are a world-class engineer") instead of behavioural instructions.
- Producing a brief with no stop conditions — that is how agents over-edit.

## Related Skills

- **planner** — when the ask needs sequencing and phases, not just sharpening; a hardened brief is often planner's input.
- **product-planner** — when the real gap is whether/what to build, not how to phrase it.
- **design-brainstorming** — when the requester needs to decide between approaches before any brief can be written.
- **skill-author** — hardening that should become permanent lives in a skill, not a one-off prompt.
