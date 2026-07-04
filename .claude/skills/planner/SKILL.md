---
name: planner
description: Use this skill when you need to turn a feature request, project, or complex task into a structured, staged plan — "plan this", "how should we approach this", "break this down". The planner discovers context before planning, frames the problem correctly, maps what already exists, identifies unknowns requiring spikes, sequences phases to minimise rework (walking-skeleton first where feasible), stress-tests the plan with a pre-mortem, routes each phase to the right skill, and produces a plan that other skills execute. Use it at the start of any non-trivial work. It does not write or review code.
license: MIT
metadata:
  stack: agnostic
  version: 2.0
  last-reviewed: 2026-07-02
---

# Planner

## Use When

- Starting a feature, project, or significant change — before writing a single line of code.
- A task touches multiple files, services, layers, or systems.
- The user says "plan this", "how should we approach this?", or "break this down".
- The right sequence of work is unclear, or work could block itself if ordered wrong.
- Constraints, scope, or design decisions need to be agreed before implementation begins.

## Do Not Use When

- The task is small enough to just do — a single-file edit or trivial bug fix.
- You need to research a topic or technology first — use **researcher**, then come back.
- You are executing a plan that already exists — use the appropriate skill for each phase.
- You are debugging a runtime failure — use **bug-hunter**.

## Inputs To Look For

- The user's description of what they want to build or change.
- Existing codebase structure (solution files, project layout, entry points, config).
- Known constraints: deadlines, tech stack decisions already made, team size, platform targets.
- Prior decisions or context already established in the conversation.
- Related files, endpoints, or systems that the change will touch.

## Clarify Before Starting

Do not produce a plan without answers to these. Wrong constraints produce wrong plans, and wrong plans produce rework.

- **What problem are we solving?** Not what to build — why. What user or business need does this address?
- **What does done look like?** Concrete, observable end state. Not "works well" — what can you see, test, or measure?
- **What constraints are fixed?** Deadline, tech stack decisions already made, team size, budget, regulatory requirements.
- **What already exists and cannot change?** Legacy systems, locked interfaces, previous architectural decisions.
- **What is explicitly out of scope?** If the user has not stated this, ask — scope creep starts with unspoken assumptions.
- **Who are the users of this feature?** Power users, occasional users, external integrations, other services?

If any of these are unknown, surface them as open questions in the plan rather than silently assuming answers.

## Process

### 1. Discover before planning

Before designing phases, read the relevant parts of the codebase. Do not plan in a vacuum.

- Identify which files, services, and patterns are most relevant to the feature.
- Find the existing naming conventions, architectural patterns, and abstractions already in use.
- Identify integration points: where will the new work connect to existing systems?
- Note what must not be broken — existing behaviour, contracts, APIs, test coverage.

If the codebase is completely unfamiliar, use the **codebase-visualiser** skill's Internal Orientation Mode first and fold the findings into the plan.

### 2. Frame the problem

State the problem in terms of user or business need, not technical implementation.

- **Problem statement:** What is broken, missing, or sub-optimal today?
- **Desired outcome:** What will be true when this is done that is not true today?
- **Success indicators:** How will you know the plan worked? What is observable?
- **Non-goals:** What are we explicitly not trying to achieve? Stating this prevents scope creep.

If you cannot write a problem statement without implementation details leaking in, ask one clarifying question to separate the problem from the proposed solution before continuing.

### 3. Map the scope

List everything the feature touches. Be explicit.

- Which existing systems, services, layers, or files will be modified?
- Which existing contracts, APIs, or interfaces will change — and what breaks if they do?
- What new components, tables, endpoints, or services will be created?
- What existing behaviour is preserved, extended, or replaced?

If something in scope requires design work before coding, name the design skill: **ui-designer**, **api-designer**, **database-designer**, **backend-architect**.

### 4. Identify unknowns requiring spikes

A spike is a short, time-boxed investigation that resolves a high-uncertainty question before committing to an approach. Spikes belong in the plan, not as a silent assumption.

For each unknown:
- State the question precisely.
- Define the spike goal: what will you know at the end?
- Define the output: a decision, a prototype, a data measurement, or a confirmed assumption.
- Assign it to an early phase so it unblocks later work.

Do not design a detailed plan around an unknown. Put a spike in Phase 1 and design the next phase after the spike concludes.

### 5. Break into phases

Each phase must be:
- **Independently deliverable or testable** — something demonstrable exists at the end.
- **Ordered to unblock** — earlier phases must not depend on later ones.
- **Sized for review** — not so large that completion cannot be verified in one review session.

Order phases so that:
- Spikes come first.
- Database schema and data model changes precede code that consumes them.
- Backend contracts (API design) precede frontend work that binds to them.
- Infrastructure and config precede the code that uses them.
- Risky or uncertain work comes early, when the cost of changing course is low.

**Walking-skeleton rule.** When the work spans multiple layers or systems (UI + API + data, or service + queue + consumer), the first build phase should deliver a thin end-to-end slice — the smallest path that touches every layer with real wiring, even with one hardcoded case — before any layer is built out in full. Integration risk is the risk that kills multi-system plans, and layer-by-layer construction discovers it last. If a walking skeleton is genuinely infeasible, say why in the decision log rather than silently building layer-by-layer.

### 6. Define each phase

For every phase, define all of the following:

**Delivers:** What is demonstrably true when this phase is complete.

**Skill:** Which Claude Code skill should execute this phase:
- Research/investigation → `researcher`
- Architecture decisions → `backend-architect`
- Database schema → `database-designer`
- API contract → `api-designer`
- UI design spec → `ui-designer`
- UI implementation → `frontend-implementer`
- Code implementation → (direct implementation)
- Test coverage → `test-writer`
- Security review → `security-reviewer`
- Code review → `code-reviewer`
- EF migrations → `ef-migration-guardian`
- CI/CD/infra → `devops-deploy`

**Effort:** S (hours), M (one to two days), L (several days). Be honest. If a phase is L, consider splitting it.

**Tasks:** Concrete, specific tasks. "Implement the service" is not a task. "Add `CreateOrderAsync` to `IOrderService` and its implementation in `OrderService`" is a task. Every phase's tasks must name at least one real file, type, or system found during discovery (step 1) — a phase with no reality anchor is a guess wearing a plan's clothes.

**Dependencies:** What must be done before this phase starts. Be explicit about:
- Other phases in this plan.
- External decisions or approvals.
- Third-party integrations or vendor responses.

**Acceptance Criteria:** One or more criteria that are verifiably testable — observable, demonstrable, or measurable. Not "it works" or "it looks good":
- Bad: "Users can log in."
- Good: "Submitting valid credentials navigates to the dashboard. Submitting invalid credentials shows 'Invalid email or password' and does not navigate."

**Risks:** What could go wrong, what is unknown, what could cause rework. If a risk is high enough, it should become a spike.

**Checkpoint:** Should the user review and approve before continuing to the next phase? Mark phases that introduce irreversible changes (schema migrations, contract changes, external API calls) as mandatory checkpoints.

### 7. Map cross-cutting concerns

Identify concerns that affect multiple phases and define how each will be handled and in which phase it is addressed:

- Authentication and authorisation
- Error handling and error response shape
- Logging and correlation IDs
- Input validation
- Database migrations and rollback strategy
- Configuration and secrets management
- Test coverage strategy
- Feature flags (if the change needs to be gated)
- Breaking changes and backwards compatibility

For each: name it, state the approach, and name the phase where it is first addressed.

### 8. Record assumptions

List everything the plan assumes to be true that was not explicitly confirmed. For each:

- State the assumption.
- State what breaks if the assumption is wrong.
- State how to verify it (and in which phase that verification happens).

An unchecked assumption is a hidden risk. Surface it.

### 9. Record the decision log

During planning, decisions are made. Record them here so they are not re-debated in implementation.

- What options were considered?
- Which was chosen?
- Why?

Even if only one option was considered, record it — "we chose X because Y" is the context that prevents future revisits.

### 10. Run the pre-mortem

With the phases drafted, imagine the plan has been executed and failed. Write the three most likely causes of that failure — be concrete: "the third-party API's sandbox doesn't support the webhook we depend on", not "integration issues". Then convert every cause into a plan change:
- A cause rooted in an unknown → a spike in Phase 1.
- A cause rooted in an irreversible step → a mandatory checkpoint before it.
- A cause you can only watch for → a named risk with its early-warning signal.

A pre-mortem that changes nothing about the plan was not performed honestly. Record the causes and what each changed in the Pre-Mortem output section.

### 11. Name the first action

One sentence. What to do immediately after the plan is approved. It must be specific enough to start without further conversation.

## Template Selection

Choose the template before writing output.

- **Lightweight** — task fits in one PR, one day of work, or one file. Use the Lightweight Plan format below.
- **Full** — task touches multiple files, phases, or systems, or has high uncertainty. Use the Full Plan format below.

When in doubt, default to Lightweight. A Lightweight plan that reveals hidden complexity can always be expanded; a Full plan for a two-line change creates friction without value.

Copy-paste templates for all three weights (Lightweight, Medium, Full) with inline guidance on good phases and acceptance criteria live in [templates/plan-templates.md](templates/plan-templates.md).

## Lightweight Plan Format

### Plan: [Title]

**Goal:** [One sentence — what will be true when this is done, in observable terms]

**Steps:**
1. [Concrete step]
2. [Concrete step]
3. [Concrete step — 3 to 7 steps total]

**Risks:**
- [Risk or assumption — 2 to 3 items]

**First Action:** [One sentence. Specific enough to start immediately.]

---

## Full Plan Format

### Plan: [Title]

**Problem:** [What is broken, missing, or sub-optimal today — in user/business terms, not technical]

**Outcome:** [What will be observably true when this is done]

**Success Indicators:** [How you will know the plan worked]

---

#### Scope

**In scope:**
- [Item]

**Out of scope:**
- [Item]

**Integration points:**
- [Existing system / file / API that will be touched, and how]

---

#### Phase [N]: [Name]

**Delivers:** [Observable end state when complete]
**Skill:** [Skill name or "direct implementation"]
**Effort:** [S / M / L]
**Checkpoint:** [Required / Optional — required for irreversible changes]

**Tasks:**
1. [Specific, concrete task]
2. [Specific, concrete task]

**Dependencies:** [None / Phase N / External: ...]

**Acceptance Criteria:**
- [ ] [Observable, testable criterion]
- [ ] [Observable, testable criterion]

**Risks:**
- [Risk or unknown and its impact]

---

[Repeat for each phase]

---

#### Cross-Cutting Concerns

| Concern | Approach | First Addressed In |
|---------|----------|--------------------|
| Error handling | [How] | Phase N |
| Logging | [How] | Phase N |
| Auth | [How] | Phase N |
| Migrations | [How] | Phase N |

---

#### Assumptions

| Assumption | Impact If Wrong | Verified In |
|------------|-----------------|-------------|
| [Assumption] | [What breaks] | Phase N / Spike N |

---

#### Decision Log

| Decision | Options Considered | Choice | Reason |
|----------|--------------------|--------|--------|
| [Decision] | [Options] | [Chosen] | [Why] |

---

#### Pre-Mortem

| Failure cause (imagined) | Likelihood | What the plan now does about it |
|--------------------------|-----------|--------------------------------|
| [Concrete cause of failure] | Low / Med / High | Spike in Phase N / Checkpoint before Phase N / Risk with early-warning signal: [signal] |

---

#### First Action

[One sentence. Specific enough to start immediately.]

---

## Quality Bar

- The problem is stated in user/business terms before any implementation detail appears.
- Pre-planning discovery happened — the plan references specific files, patterns, or systems found in the codebase, not generic placeholders, and every phase's tasks carry at least one such reality anchor.
- Every high-uncertainty area has a spike in an early phase, not a silent assumption.
- Multi-layer work delivers a walking skeleton in the first build phase, or the decision log says why not.
- The pre-mortem ran and changed the plan — at least one spike, checkpoint, or monitored risk traces to it.
- Each phase is independently demonstrable or testable. No phase ends with "work in progress".
- Every phase names the skill that should execute it.
- Every acceptance criterion is verifiably testable — observable or demonstrable, not descriptive.
- Dependencies between phases are explicit. No phase can start before its dependencies are listed.
- Cross-cutting concerns are named and assigned to a phase.
- Assumptions are listed with their verification phase. "We assumed X" is not a plan.
- The decision log captures why choices were made, not just what was chosen.
- Out-of-scope items are listed to prevent scope creep.
- Mandatory checkpoints mark irreversible changes.
- The first action is specific enough to execute immediately.

## Plan Update Mode

A plan written at project start will not survive contact with real implementation. When something changes mid-execution, use Plan Update Mode rather than silently continuing or abandoning the plan.

**Trigger conditions:**
- A phase completes but reveals a blocker that makes the next phase infeasible as designed.
- New information invalidates a recorded assumption.
- The user changes scope — adding, removing, or deferring work.
- A phase's acceptance criteria cannot be met with the current approach.
- A spike resolves with a different answer than assumed.

**Process:**
1. Name the change event precisely — what happened, in which phase.
2. Identify which subsequent phases are affected and how.
3. Classify the resolution: minor adjustment, new spike needed, or phase redesign.
4. Update the plan — do not produce a whole new document. State what changed and why, then restate the new first action.
5. Record the decision in the decision log (what was the old approach, what is the new approach, why it changed).

### Plan Update Output

**Change Event:** [What happened — one sentence]
**Triggered By:** Blocker / New information / Scope change / Failed assumption
**Phases Affected:** [Phase numbers and names]
**Resolution:** Minor adjustment / New spike required / Phase redesign

**What Changed:**
- Phase N: [Specific change — what the phase now does differently]
- Assumption "[assumption text]" invalidated — updated to "[new understanding]"

**Decision Log Addition:**
| Decision | Old Approach | New Approach | Reason |
|----------|-------------|-------------|--------|
| [What] | [Old] | [New] | [Why changed] |

**New First Action:** [Specific next step, same format as original plan's First Action]

## Failure Modes To Avoid

- **Planning in a vacuum.** A plan that does not reference the actual codebase, existing patterns, or existing constraints is not a plan — it is a guess. Discover first.
- **Burying assumptions.** "We'll figure out auth later" is not a plan. Surface every assumption, assign it a verification step, and fail loudly if verification is missing.
- **Vague phases.** "Phase 2: Implement the backend" is not a phase. A phase is done when something specific and demonstrable exists.
- **No acceptance criteria.** "It works" is not an acceptance criterion. State what you will observe, test, or measure.
- **Missing spikes.** If you do not know how something works, a spike comes first. Never design a phase around an unresolved unknown.
- **Layer-by-layer construction.** Building the whole data layer, then the whole API, then the UI — integration risk surfaces in the final phase, when it is most expensive. Walking skeleton first.
- **Ritual pre-mortem.** Writing three vague failure causes ("scope creep", "delays") that change nothing. Each cause must be concrete and must alter the plan.
- **Ignoring dependencies.** Letting the user discover that Phase 3 depended on Phase 2 being done differently is avoidable. Dependencies must be stated.
- **Over-planning small tasks.** A one-file change does not need five phases. Match plan depth to task complexity.
- **Treating the plan as a specification.** The plan defines what, when, and why — not how at the implementation level. Implementation detail belongs in the skill that executes the phase.
- **Skipping cross-cutting concerns.** Auth, error handling, logging, and migrations do not appear by magic. If they are not in the plan, they will be missing from the implementation.
- **Not naming the first action.** A plan with no clear starting point is a conversation, not a plan.
