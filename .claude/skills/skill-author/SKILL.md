---
name: skill-author
description: Use this skill when you need to create, restructure, or quality-review a Claude Code skill or agent in this repository. The skill-author encodes how skills actually work (description-as-trigger, progressive disclosure), the house template, overlap rules, and the bookkeeping every addition requires (README, quickref, counts, validation). It does not write project documentation (use docs-writer) or general markdown content.
license: MIT
metadata:
  stack: agnostic
  version: 1.2
  last-reviewed: 2026-06-29
---

# Skill Author

## Use When

- Creating a new skill or agent in this repository.
- Restructuring, renaming, splitting, or merging an existing skill.
- Reviewing a skill for quality before it is committed.
- Deciding whether something should be a skill, an agent, or a CLAUDE.md rule.

## Do Not Use When

- Writing README, setup, or architecture documentation — use **docs-writer**.
- Preparing the repo for public visibility — use **public-repo-polisher**.
- Maintaining repo structure generally — use **project-maintainer**.

## How Skills Actually Work (write for the mechanism, not the reader)

A skill is not documentation — it is a behavioural payload with a trigger. Three facts drive every authoring decision:

1. **The description is the trigger.** Claude Code reads only the frontmatter `description` when deciding whether to invoke a skill. If the description does not contain the phrases a user would actually say ("review this PR", "is this query slow?"), the skill never fires no matter how good its body is. Spend more effort on the description than on any other paragraph.
2. **The body loads on invocation.** Everything in `SKILL.md` enters context when the skill fires. Every sentence that does not change Claude's behaviour is paying context cost for nothing. Cut throat-clearing, motivation, and anything Claude already does by default.
3. **Reference files load on demand.** Deep material (pattern catalogues, recipes, long checklists) belongs in `references/`, `checklists/`, or `templates/` subfolders, loaded only when the task needs them. This is progressive disclosure: a lean SKILL.md that knows where its depth lives beats a 600-line monolith.

## Skill vs Agent vs CLAUDE.md Rule

| It is a... | When |
|---|---|
| **Skill** | A reusable workflow, decision procedure, or domain reference that should fire on matching tasks. |
| **Agent** | A narrow role that benefits from isolation and a restricted tool set — almost always a read-only reviewer. One file under `.claude/agents/`. |
| **CLAUDE.md rule** | A short, always-on constraint ("never do X in this repo"). If it fits in two lines and applies to every task, it is not a skill. |

If a proposed skill is mostly "be careful and thorough", it is none of these — do not create it.

## House Template

Every skill in this repo follows this structure. Deviate only with a reason (see low-noise-mode for an accepted deviation).

```markdown
---
name: kebab-case-matching-folder
description: Use this skill when [trigger phrases a user would say]. [What it produces.]
  [What it does NOT do, naming the skill that does.]
license: MIT
metadata:
  stack: agnostic | dotnet | dotnet-primary | dotnet-and-web | web
  version: 1.0
  last-reviewed: YYYY-MM-DD
---

# Skill Name

## Use When          — bullet list of concrete trigger situations
## Do Not Use When   — boundaries, each naming the correct alternative skill/agent
## Inputs To Look For — what to gather before starting
## Process            — numbered, verifiable steps (the core)
## Output Format      — the exact deliverable shape, with a realistic example
## Quality Bar        — what "done well" means, checkable
## Failure Modes To Avoid — the specific ways this task goes wrong
## Related Skills     — handoffs in and out (optional but preferred)
```

Agents use: Role / Scope / Out Of Scope / Review Method / Output Format / Quality Bar / Failure Modes, with `tools: Read, Grep, Glob` unless writing is the agent's purpose.

## Writing Rules That Separate Superpowers From Essays

- **Verifiable steps over advice.** "Run `dotnet build --no-restore` and parse errors" is a step. "Ensure the code builds" is advice. Every process step should be checkable from its output.
- **Force evidence for claims.** If the skill's domain allows claims like "tests pass" or "compiles cleanly", the skill must require the tool output that proves it. This repo's strongest skills (dotnet-quality-gate, the unity set) all do this.
- **Make decisions, don't list options.** Where a default is defensible, state it and the exception ("Default to JWT; use opaque tokens when immediate revocation is a hard requirement"). A skill that presents three options without a recommendation has not done its job.
- **Concrete examples in output formats.** A table with realistic fake data (`OrderService.cs:42`, obviously generic names) teaches the shape faster than placeholder text.
- **Boundaries name the alternative.** Every "Do Not Use When" bullet routes to the skill or agent that should be used instead. Unrouted boundaries create dead ends.
- **Compose by name, never by include.** When a skill should hand off to or lean on another, reference it by **bold-name** in prose ("then use **planner**"). Never wire composition with an @-link or file-include that force-loads the other skill's body — that is push-based and pays the full context cost whether or not the branch is taken. Naming keeps composition pull-based: Claude loads the referenced skill only if and when it actually fires.
- **No hype, no personas beyond function.** "You are a meticulous reviewer" adds nothing; specific behavioural rules do.

## TDD For Skills

Write skill guidance the way you write code under test: red first, then the minimum that turns it green. Guidance you cannot tie to an observed failure is speculation — do not add it.

1. **Baseline the failure.** Pick a representative prompt the skill is meant to handle. Run it with the skill absent (or with the section you are about to write removed) and record what Claude actually does. The wrong behaviour you observe — skips a step, fires at the wrong time, omits evidence — is the failing test.
2. **Write the minimum guidance that flips it.** Add the smallest, most specific instruction that would have changed that behaviour. One sentence aimed at the exact failure beats a paragraph of general advice.
3. **Re-test on the same prompt.** Confirm the behaviour is now correct. If it is not, the guidance is wrong or too vague — fix the guidance, not the test.
4. **Iterate on loopholes.** Try variant prompts that could route around the new instruction. Each new wrong behaviour is the next failing test; close it with the next minimum addition. Stop when realistic variants behave.

This is the antidote to bloat: every line in the skill exists because a baseline run showed the model needed it. If you cannot point to the failure a sentence fixes, cut the sentence.

## Process

1. **Check for overlap first.** Read `docs/skills-quickref.md` and search existing skills/agents for the concept under any name. If an existing skill covers ≥70% of the idea, extend it instead of creating a sibling. Duplicate concepts under different names is this repo's named failure mode.
2. **Classify.** Skill (general or specialist), agent, or CLAUDE.md rule — using the table above. State the classification and why.
3. **Write the description.** Third person, starts "Use this skill when…", contains the literal phrases a user would type, states the deliverable, ends by naming what it does *not* do and what to use instead. Target 2–4 sentences.
4. **Draft the body** against the house template. Apply the writing rules. Hard size budget (aligned with the agentskills.io spec): SKILL.md body under 500 lines and roughly 5,000 tokens — the working target here is 100–150 lines; past ~200, move reference material into subfolder files and leave a load-on-demand index (see unity-ui-design for the pattern). Referenced files stay one level deep from SKILL.md — a reference must never point at another reference.
5. **Self-review against the Quality Bar below.** Fix before proceeding.
6. **Add evals for core-workflow and trigger-boundary skills.** Create `evals/evals.json` with at least one should-fire and one should-not-fire prompt (the should-not case names the skill that wins instead). Any skill whose trigger borders a sibling gets the boundary case. Print a case with `bash scripts/run-eval.sh <skill> <id>`; re-run after editing triggers. If a live baseline run is impossible, write the predicted failure as the eval case instead — never skip straight to guidance.
7. **Do the bookkeeping.** Every addition/rename/removal must update: `README.md` (structure listing and counts), `docs/skills-quickref.md` (one-line entry in the right workflow group), and `CLAUDE.md` repository map counts.
8. **Validate.** Run `bash scripts/validate-skills.sh` and fix every failure. Do not report the work complete with a red validator.

Steps 6–8 are not droppable under time pressure. "Just add it quickly, skip the bookkeeping/evals, we'll tidy later" produces a skill the router lists wrong, the validator flags red, and nobody ever tidies — the system's consistency is the product. The honest fast path is a *smaller skill* (leaner body, minimal evals: one should-fire, one should-not-fire), never a skipped checklist. If even that is too slow for the requester, decline the addition and record the idea as a follow-up instead.

## New-Skill Interview Mode

When the user wants a new skill but has not supplied the trigger, deliverable, boundary, and process up front, do not guess — interview them. Ask one focused question at a time, wait for the answer, then ask the next. This complements the validate/restructure path above: it front-loads the four facts the description and body cannot be written without.

Ask in this order, one at a time:

1. **Trigger phrases.** "What exact words would a user type when they want this? Give me two or three literal phrasings." These become the description's trigger and the **Use When** bullets.
2. **Deliverable.** "What is the single concrete thing this skill produces — a report, a spec, a passing command, a diff?" This becomes the description's deliverable clause and anchors **Output Format**.
3. **Boundary and alternative.** "What is the nearest existing skill that does something similar, and where exactly should the line be?" Run the overlap check (step 1 of Process) against the answer. This becomes the description's closing clause and the **Do Not Use When** routing.
4. **Core process.** "Walk me through the steps you'd want it to take, in order." Convert each into a verifiable step for **Process**.

Then emit the house template with these four answers pre-filled and the remaining sections (Inputs To Look For, Quality Bar, Failure Modes To Avoid, Related Skills) drafted from them, ready for the self-review and bookkeeping steps. Do not skip ahead and write the whole skill before all four answers are in — a missing answer produces a guessed trigger or a dead-end boundary.

## Output Format

For a new skill: the complete `SKILL.md` (plus any subfolder files), followed by:

### Skill Added: [name]

**Classification:** General skill / Specialist skill / Agent — [one-line reason]
**Overlap check:** [skills/agents inspected and why none covers this]
**Trigger phrases covered:** [the user phrasings the description matches]
**Bookkeeping updated:** README ✓ / quickref ✓ / CLAUDE.md ✓
**Validation:** `validate-skills.sh` — PASS / [failures and fixes]

## Quality Bar

- The description alone is enough for Claude Code to fire the skill at the right moment and skip it at the wrong one.
- Every process step is verifiable from its output; no step is "be thorough".
- Every claim the skill's domain permits ("passes", "compiles", "no regressions") is backed by a required evidence step.
- Output format includes a realistic example, not bare placeholders.
- All "Do Not Use When" bullets route to a named alternative.
- No content duplicates another skill — checked, not assumed.
- Bookkeeping is complete and the validation script passes.

## Failure Modes To Avoid

- Writing a great body behind a description that never triggers.
- Padding the skill with restatements of default Claude behaviour — context cost with no behaviour change.
- Creating a sibling skill because searching for the existing one was skipped.
- Personas and adjectives instead of behavioural rules.
- Skipping bookkeeping, leaving README counts and quickref silently wrong — including when the requester explicitly asks you to skip it; shrink the skill instead.
- Claiming validation passed without running the script.
- Creating a skill for something that should be a two-line CLAUDE.md rule.

## Related Skills

- **docs-writer** — for the public docs that describe skills, as opposed to the skills themselves.
- **public-repo-polisher** — before publishing changes to the public repo.
- **project-maintainer** — for detecting drift across the repo after many additions.
