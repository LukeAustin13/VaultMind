---
name: design-brainstorming
description: Use this skill when the user wants to think through a design before any planning or coding — "help me think through this", "brainstorm this", "let's design this together", "what are the options here?", or "what are the trade-offs?". It runs an interactive Socratic refinement loop — one question at a time, proposes 2-3 approaches with a clear recommendation, and presents the design in approval-gated sections. The deliverable is an approved, dated design summary. It does not produce the staged build plan (use planner) and does not gather external facts about libraries or APIs (use researcher).
license: MIT
metadata:
  stack: agnostic
  version: 1.0
  last-reviewed: 2026-06-29
---

# Design Brainstorming

Interactive design refinement that happens *before* planning or coding. It pulls the design out of the user's head through focused questions, weighs approaches, and lands on a design the user has explicitly approved. It never starts building.

---

## Use When

- The user says "help me think through this", "brainstorm this", "let's design this together", or "talk me through the options".
- The user asks "what are the trade-offs?" or "what are my options?" for a feature or change.
- The shape of a solution is genuinely undecided and needs to be refined through conversation before it can be planned.
- The user has signalled they want to *decide between approaches* before work is sequenced. If they asked "plan this" or "how should we approach this", that is **planner** — even when the request is vague; planner runs its own discovery.

## Do Not Use When

- The design is already decided and you need to sequence the work into phases — use **planner**.
- You need external facts first (how a library behaves, which API to use, whether an approach is still current) — use **researcher**, then return here.
- The decision is about system structure with no real ambiguity to explore — use **backend-architect**.
- The decision is a specific contract or schema, not an open design — use **api-designer** or **database-designer**.
- The user wants the UI/UX of a screen specified — use **ui-designer**.
- The task is small enough to just do — skip brainstorming and implement it.

## Inputs To Look For

- The user's description of what they want, however rough.
- Constraints already stated: stack, deadline, team size, platform, integrations.
- The existing codebase — patterns, conventions, and abstractions the design must fit.
- Decisions already made earlier in the conversation that the design must respect.
- Anything the user has explicitly ruled in or out.

Before asking the user anything, check whether the inputs or the codebase already answer it. Only ask what they genuinely cannot.

## Process

The loop is: ask one question, refine, propose approaches, present a section, gate on approval, repeat. Move fast — every question carries a recommended default so the user can confirm in one word.

### 1. Frame the problem in one sentence

State what you understand the goal to be, in user or business terms, and ask the user to confirm or correct it. Do not proceed on a goal the user has not confirmed.

### 2. Ask one question per message

- Ask exactly one question at a time. Never send a numbered list of questions.
- Prefer multiple-choice. Offer 2-4 concrete options the user can pick by letter.
- Name a recommended default and say why, so the user can reply "default" or "A" and move on.
- Only ask what the inputs and codebase cannot answer. If you can derive it, state your derivation and ask only for confirmation.
- Stop asking once you have enough to design. Do not gather more than the design needs.

### 3. Propose 2-3 approaches with a recommendation

Once the key unknowns are resolved, lay out 2-3 viable approaches. For each: a one-line description, its main trade-off, and when it is the right choice. End with a clear recommendation and the reason. Never present options without recommending one.

### 4. Present the design in approval-gated sections

Break the design into sections (for example: overall approach, data shape, key flows, edge cases, open risks). After each section, stop and ask the user to approve it or request changes before moving to the next. Do not present the whole design as one wall and ask for blanket sign-off.

### 5. Write a dated design summary

Once every section is approved, write a single dated summary capturing the agreed design, the approaches considered, the decisions made and why, and the open questions that remain. This is the deliverable.

### 6. Hand off — do not build

State explicitly that the design is approved and that the only next step is the **planner** skill, which turns it into a staged build plan. Do not write code, scaffold files, or produce a phased plan yourself.

## Hard Gate

No implementation action and no planning action happen inside this skill. You do not write code, create files, scaffold structure, or produce a staged plan. The single permitted next step after approval is to hand the design summary to the **planner** skill. State this gate to the user before you begin and again when you hand off. If the user asks you to start building mid-conversation, confirm the design is approved, then route to **planner** rather than coding directly.

## Output Format

Two kinds of output. During the conversation, single questions and section proposals. At the end, one dated design summary.

A single question during the loop:

> **Question — storage for draft autosave**
> Where should unsaved drafts live between autosaves?
>
> - **A. Server-side, per user** (recommended) — drafts survive device switches; costs one table and a periodic write. Best when users edit across devices.
> - **B. Browser localStorage** — zero server cost; drafts are lost if the user switches device or clears storage.
> - **C. In-memory only** — simplest; drafts are lost on refresh.
>
> I recommend **A** because the brief mentions users editing on both desktop and mobile. Reply with a letter, or "A" to take the default.

The final deliverable:

> ## Design Summary — Draft Autosave
> **Date:** 2026-06-29
> **Status:** Approved
>
> **Goal:** Let users leave and return to an in-progress document without losing edits, across devices.
>
> **Approach chosen:** Server-side draft store, one row per (user, document), overwritten on a 5-second debounced autosave. Chosen over localStorage because drafts must survive a device switch.
>
> **Approaches considered:**
> - Server-side per-user store — chosen. Survives device switches; one table, periodic write.
> - Browser localStorage — rejected. No cross-device survival.
> - In-memory only — rejected. Lost on refresh.
>
> **Key decisions:**
> - Autosave debounce: 5s (balances write volume against data-loss window).
> - Conflict handling: last-write-wins for v1; no multi-tab merge.
> - Retention: drafts older than 30 days are purged.
>
> **Open questions for planning:**
> - Does the existing `documents` table have room for a `draft_json` column, or is a separate table cleaner? (Resolve in **planner** discovery.)
>
> **Next step:** Hand this summary to the **planner** skill to produce a staged build plan. No implementation until then.

## Quality Bar

- The goal was confirmed by the user before any design work began.
- Every message in the loop asked at most one question.
- Questions were multiple-choice with a recommended default wherever the choice was bounded.
- No question was asked that the inputs or codebase already answered.
- At least one decision point presented 2-3 approaches with an explicit recommendation, not a bare list.
- The design was presented in sections, each approved before the next was shown.
- The final summary is dated, marked Approved, and records decisions with their reasons.
- The summary ends by naming **planner** as the only next step, and no code or plan was produced inside this skill.

## Failure Modes To Avoid

- **Wall of questions.** Sending a numbered list of five questions at once. Ask one, wait, then ask the next.
- **Options with no recommendation.** Listing approaches and asking "which do you prefer?" without a recommended default forces the user to do the deciding the skill exists to help with. Always recommend.
- **Drifting into code.** Writing implementation, scaffolding files, or producing a phased plan before the design is approved. The gate is hard — stop and route to **planner**.
- **Blanket sign-off.** Presenting the entire design as one block and asking "looks good?" — the user cannot meaningfully approve what they cannot review in pieces. Gate each section.
- **Asking what you can derive.** Querying the user for facts already in the inputs or the codebase. Derive it, state the derivation, ask only to confirm.
- **Over-questioning.** Continuing to ask after you have enough to design. Stop when the unknowns that block the design are resolved.

## Related Skills

- **planner** — the only permitted next step; turns the approved design summary into a staged, risk-aware build plan.
- **researcher** — use first when the design depends on external facts (library behaviour, API choice, current best practice) that must be verified before deciding.
- **backend-architect** — use instead when the structure of the system, not an open design question, is what needs deciding.
- **ui-designer** — use when the brainstorm lands on UI work and a screen-level design specification is needed.
