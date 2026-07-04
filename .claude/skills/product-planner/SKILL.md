---
name: product-planner
description: Use this skill when a product or feature idea needs to be challenged before anyone codes it — "is this worth building?", "sanity-check this idea", "what should the MVP be?", "how would this make money?". It stress-tests the problem, users, alternatives, differentiation, monetisation, and MVP cut, and delivers a product brief ending in a BUILD / PROBE / PASS verdict with kill criteria. It does not sequence implementation (use planner), refine a chosen solution's shape (use design-brainstorming), or design screens (use ui-designer).
license: MIT
metadata:
  stack: agnostic
  version: 1.1
  last-reviewed: 2026-07-04
---

# Product Planner

Challenge an idea before it costs weeks. The deliverable is a product brief that either kills the idea cheaply, shrinks it to a testable probe, or hands a sharply-scoped MVP to **planner**. The default posture is adversarial: the idea must earn BUILD.

## Use When

- The user pitches a product/feature idea and asks whether or what to build.
- An MVP needs cutting — "what's the smallest version worth shipping?"
- Monetisation, differentiation, or market fit is undecided and code is about to be written anyway.
- A side project needs a go/no-go before it consumes evenings.

## Do Not Use When

- The decision to build is made and the question is how — use **planner** (sequencing) or **design-brainstorming** (solution shape).
- The question is technical feasibility of a known approach — use **researcher** or a spike via **planner**.
- The user wants encouragement, not assessment — this skill's verdicts include PASS, and it will use it.

## Inputs To Look For

- The idea, in the user's words — capture it verbatim before reshaping it.
- Who the user thinks the customer is, and any evidence they've talked to one.
- What already exists (competitors, workarounds, the spreadsheet people use today).
- The builder's real constraints: time budget, distribution reach, appetite for support burden.

## Process

1. **State the problem without the solution.** One sentence, no product nouns: who has what pain, how often, how badly. If the problem statement collapses without naming the product, that is finding #1.
2. **Name the alternatives honestly.** What do target users do today — including "nothing, because the pain is mild" and "a spreadsheet"? The strongest competitor is usually the status quo, not a rival product.
3. **Locate the differentiation.** Why would someone switch from their current behaviour? "Better UX" and "AI-powered" are not answers; a specific job done 10× cheaper/faster for a specific user is.
4. **Pressure-test monetisation.** Who pays, how much, and why that price beats the alternative's total cost? If the answer is "ads eventually" or "we'll figure it out", record it as an unvalidated assumption, not a plan.
5. **Cut the MVP to a walking skeleton.** The smallest end-to-end slice a real user can use for the core job. List what is explicitly OUT — the out-list is the MVP's real definition. Everything cut must have a "revisit when" trigger, not a vague "later".
6. **Extract the riskiest assumptions.** Rank the assumptions the idea dies without (people have this pain / will pay / can be reached). For each: the cheapest test that could falsify it — a landing page, five interviews, a concierge version — with a numeric pass threshold where possible.
   Classify any user-supplied evidence while ranking: **observed** (an artefact you can see — the waitlist export, the interview notes, the analytics screenshot) or **claimed** (asserted, nothing shown). Claimed evidence is an unvalidated assumption wearing a costume — it goes in this table with a falsification test, and it cannot upgrade a verdict. Ask for the artefact; "we validated demand" without one changes nothing.
7. **Set kill criteria.** Concrete conditions under which the builder stops: "if fewer than N of 20 target users say X", "if CAC estimate exceeds price by month 2". A plan without kill criteria is a sunk-cost machine.
8. **Issue the verdict.** BUILD (MVP scope justified, riskiest assumption survivable), PROBE (run the named assumption tests before writing code), or PASS (the honest recommendation not to build, with the reason). The verdict must follow from the brief — a brief full of unvalidated assumptions cannot end in BUILD.

## Output Format

### Product Brief: [idea name]

**Verdict:** BUILD / PROBE / PASS — [one-line reason]
**Problem (no solution nouns):** [one sentence]
**Target user:** [specific enough to know where to find five of them]

#### Alternatives today

| Alternative | Why users tolerate it | Where it fails them |
|---|---|---|
| Status quo / spreadsheet | ... | ... |

**Differentiation:** [the specific switch trigger, or "not established"]
**Monetisation:** [who pays what, and why it beats the alternative's cost — or "unvalidated"]

#### MVP cut (BUILD/PROBE only)

- **In:** [walking-skeleton slice]
- **Out (with revisit triggers):** [feature — revisit when X]

#### Riskiest assumptions

| # | Assumption | Cheapest falsification test | Pass threshold |
|---|---|---|---|

#### Kill criteria

- [Concrete stop condition]

**Hand-off:** BUILD → **planner** for sequencing. PROBE → run the tests above first. PASS → what would need to change to revisit.

## Quality Bar

- The problem statement survives with the product deleted from the sentence.
- The alternatives table includes the status quo, not just rival products.
- Every OUT item has a revisit trigger; every assumption has a falsification test; at least one kill criterion is numeric.
- The verdict is consistent with the evidence — PROBE and PASS are used when earned, not softened to BUILD.
- The brief fits on roughly a page; a product brief nobody rereads is theatre.

## Failure Modes To Avoid

- Cheerleading — validating the idea because the user is excited about it.
- Accepting claimed traction as observed evidence — "500 signups" is a claim until the artefact is shown; a verdict upgraded on say-so is the cheerleading failure with extra steps.
- Market sizing by hand-waving ("the market is huge") instead of naming where five real users are found.
- An MVP that is a feature list, not an end-to-end slice.
- Assumption tests that cannot fail ("ask friends if they like it").
- Smuggling the implementation plan into the brief — sequencing belongs to **planner**.
- Issuing PASS without stating what evidence would reopen the door.

## Related Skills

- **planner** — consumes a BUILD verdict and sequences the MVP.
- **design-brainstorming** — shapes the solution once building is justified.
- **researcher** — gathers external evidence for the alternatives and assumption tests.
- **ui-designer** — screens come after the brief, never before.
