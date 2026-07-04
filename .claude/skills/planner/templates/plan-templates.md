# Plan Templates

Three copy-paste plan templates, ordered by weight. Pick the lightest one that fits.

- **Lightweight** — one PR, one day, or one file. Low uncertainty.
- **Medium** — multiple files or a few phases, moderate uncertainty.
- **Full** — multiple systems, high uncertainty, or irreversible changes.

When in doubt, start lighter. A Lightweight plan that reveals hidden complexity can be expanded; a Full plan for a two-line change is friction without value.

Inline guidance is written as HTML comments (`<!-- ... -->`). Delete the comments before sharing the finished plan.

---

## Lightweight

```markdown
### Plan: [Title]

**Goal:** [One sentence — what will be observably true when this is done. Not "improve X"; state the visible end state.]

**Steps:**
1. [Concrete step — names a file, function, or command, not "set up the thing"]
2. [Concrete step]
3. [Concrete step]
<!-- 3 to 7 steps. Fewer than 3 and it is not worth a plan; more than 7 and it probably wants the Medium template. -->

**Risks:**
- [Risk or assumption that could cause rework — 2 to 3 items]
<!-- A real risk names what could go wrong and why it matters, not "might be tricky". -->

**First Action:** [One sentence. Specific enough to start immediately with no further conversation.]
```

---

## Medium

```markdown
### Plan: [Title]

**Problem:** [What is broken, missing, or sub-optimal today — in user or business terms, not technical implementation.]

**Outcome:** [What will be observably true when this is done that is not true today.]

---

#### Phase [N]: [Name]
<!-- A good phase is independently deliverable or testable, ordered so earlier phases unblock later ones, and small enough to verify in one review. If a phase ends with "work in progress", it is not a phase — split or resequence it. Put spikes and risky work first, while the cost of changing course is low. -->

**Delivers:** [Observable end state when this phase is complete — what demonstrably exists.]

**Tasks:**
1. [Specific, concrete task — "Add `CreateOrderAsync` to `IOrderService`", not "implement the service"]
2. [Specific, concrete task]

**Acceptance:**
- [ ] [Observable, testable criterion]
<!-- A good acceptance criterion is verifiable: observable, demonstrable, or measurable. Bad: "users can log in". Good: "valid credentials navigate to the dashboard; invalid credentials show 'Invalid email or password' and do not navigate." If you cannot test it, rewrite it. -->

**Risks:**
- [What could go wrong or is unknown, and its impact. If a risk is high enough, it becomes a spike in an earlier phase.]

---

<!-- Repeat for each phase — 3 to 4 phases for a Medium plan. More than that suggests the Full template. -->

**First Action:** [One sentence. Specific enough to start immediately.]
```

---

## Full

```markdown
### Plan: [Title]

**Problem:** [What is broken, missing, or sub-optimal today — in user or business terms, not technical implementation.]

**Outcome:** [What will be observably true when this is done.]

**Success Indicators:** [How you will know the plan worked — observable signals.]

---

#### Scope

**In scope:**
- [Item this plan will deliver]

**Out of scope:**
- [Item explicitly excluded — naming these prevents scope creep from unspoken assumptions]

**Integration points:**
- [Existing system, file, or API that will be touched, and how]

---

#### Phase [N]: [Name]
<!-- A good phase is independently deliverable or testable, ordered so earlier phases unblock later ones, and sized to verify in one review session. Order: spikes first, then schema/data before code that consumes it, then API contracts before frontend that binds to them, then infra/config before the code that uses it. -->

**Delivers:** [Observable end state when complete.]
**Skill:** [Skill that should execute this phase, or "direct implementation"]
**Effort:** [S (hours) / M (one to two days) / L (several days) — if L, consider splitting]
**Checkpoint:** [Required / Optional — required for irreversible changes: schema migrations, contract changes, external API calls]

**Tasks:**
1. [Specific, concrete task]
2. [Specific, concrete task]

**Dependencies:** [None / Phase N / External: ...]

**Acceptance Criteria:**
- [ ] [Observable, testable criterion]
<!-- Verifiable means observable, demonstrable, or measurable — never "it works" or "it looks good". State what you will see, test, or measure. -->

**Risks:**
- [Risk or unknown and its impact. A high-enough risk becomes a spike in an earlier phase.]

---

<!-- Repeat for each phase. -->

---

#### Cross-Cutting Concerns
<!-- Concerns that span phases do not appear by magic. Name each, state the approach, and assign the phase where it is first addressed. -->

| Concern | Approach | First Addressed In |
|---------|----------|--------------------|
| Auth | [How] | Phase N |
| Error handling | [How] | Phase N |
| Logging | [How] | Phase N |
| Input validation | [How] | Phase N |
| Migrations | [How] | Phase N |
| Config and secrets | [How] | Phase N |

---

#### Assumptions
<!-- An unchecked assumption is a hidden risk. For each, state what breaks if it is wrong and the phase that verifies it. -->

| Assumption | Impact If Wrong | Verified In |
|------------|-----------------|-------------|
| [Assumption] | [What breaks] | Phase N / Spike N |

---

#### Decision Log
<!-- Records why choices were made so they are not re-debated in implementation. Record even single-option decisions: "we chose X because Y" is the context that prevents future revisits. -->

| Decision | Options Considered | Choice | Reason |
|----------|--------------------|--------|--------|
| [Decision] | [Options] | [Chosen] | [Why] |

---

#### Pre-Mortem
<!-- Imagine the plan executed and failed. Concrete causes only — "the sandbox API lacks the webhook we depend on", not "integration issues". Every cause must change the plan; a pre-mortem that changes nothing was not performed honestly. -->

| Failure cause (imagined) | Likelihood | What the plan now does about it |
|--------------------------|-----------|--------------------------------|
| [Concrete cause] | Low / Med / High | [Spike in Phase N / Checkpoint before Phase N / Named risk + early-warning signal] |

---

#### First Action

[One sentence. Specific enough to execute immediately.]
```
