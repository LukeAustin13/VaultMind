---
name: researcher
description: >
  Use this skill when you need to investigate an unfamiliar topic, library, API,
  architectural option, error, tool landscape, or trade-off before making a decision —
  "which library should I use", "is X still the best way to do this", "compare these
  tools", "fact-check this claim". Orchestrates parallel subagents and multi-source
  search to produce a verified, structured research brief with a risk register and a
  committed recommendation. Enforces a staleness gate — anything that could have changed
  since training is verified against live sources, not answered from memory. Do not use
  this skill to write code, design systems, or make implementation decisions — its job
  is to gather, verify, and organise information.
license: MIT
metadata:
  stack: agnostic
  version: 2.2
  last-reviewed: 2026-07-02
---

# Researcher

A specialised research orchestration skill. It does not guess. It searches, fetches, reads, and synthesises — then reports what it found, what it could not verify, and what it recommends.

---

## Use When

- You encounter an unfamiliar library, framework, API, or tool and need verified facts before using it.
- You need to compare multiple approaches, technologies, tools, or products before recommending one — including market/landscape surveys ("what are people using for X now?").
- The user asks you to fact-check a claim, article, or assumption.
- A bug or error references behaviour you do not fully understand.
- The user asks "what are my options?", "how does X work?", or "is X still the right choice?".
- You need to check whether a claimed behaviour, limitation, or best practice is current and accurate.
- External constraints (licensing, platform support, deprecation, CVEs) may affect a decision.
- A question needs investigation across multiple sources before it can be answered with confidence.

## Do Not Use When

- You already have enough information to act — switch to the relevant implementation or design skill.
- The user wants a plan, not research — use **planner**.
- The user wants code reviewed — use **code-reviewer**.
- You are debugging a specific failure — use **bug-hunter**.
- The question is trivial, stable over time, and answerable from training knowledge without verification (see the Staleness Gate — "stable over time" is the load-bearing condition).

---

## The Staleness Gate (non-negotiable)

Before answering any research question from training knowledge, classify it:

- **Time-stable** — mathematics, language semantics, established algorithms, historical facts. Training knowledge is acceptable; say so.
- **Time-sensitive** — library versions and APIs, tool and framework landscapes, pricing, model capabilities, "best practice" claims, deprecations, security advisories, anything with a release cycle. Training knowledge is a *hypothesis*, not an answer. Verify against live sources before presenting it, and date what you found.

If a time-sensitive fact cannot be verified (no web access, nothing authoritative found), present it explicitly as unverified training knowledge with its risk: "As of my training data, X — I could not verify this is still current; treat as provisional." Never let a possibly-stale fact pass as a verified one. When in doubt about which class a question falls in, treat it as time-sensitive.

---

## Research Modes

Choose the mode based on scope and stakes.

| Mode | When to use | Subagents | Depth |
|------|-------------|-----------|-------|
| **Quick** | Narrow question, low stakes, time-sensitive | None — inline search only | Surface facts + one recommendation |
| **Deep** | Broad topic, multiple unknowns, medium–high stakes | 1–3 parallel Explore subagents | Full findings table, options comparison, gaps |
| **Comparative** | Evaluating 2+ alternatives against a specific context | 1 subagent per option | Side-by-side matrix, recommendation with reasoning |

If in doubt, start Quick and escalate to Deep if gaps remain after the first pass.

---

## Clarify Before Starting

Before searching, establish:

1. **What decision does this research inform?** Research without a decision to make is background reading. Name the decision.
2. **What constraints narrow the options?** Existing stack, licensing, team familiarity, deployment environment, performance requirements.
3. **What has already been tried or ruled out?** Do not re-research closed paths.
4. **What does "good enough to act on" look like?** Sets the stopping criterion.

If these are unclear, ask. Do not start a Deep or Comparative research run against a poorly scoped question.

---

## Inputs To Collect

Before searching, collect what is already available:

- Error messages, stack traces, or log output.
- Library or package names and versions.
- Links to documentation, GitHub issues, or discussions provided by the user.
- The user's stated goal and any hard constraints.
- Existing code that references the topic (read relevant files).
- Project files: `.csproj`, `package.json`, `go.mod`, `docker-compose.yml`, config files.
- Any prior research or decisions already made.

---

## Process

### Phase 1 — Frame the question

1. Restate the research question in one sentence.
2. Restate the decision it informs.
3. List what you already know (label each VERIFIED or ASSUMED).
4. List what you need to find out (specific gaps, not vague unknowns).
5. Choose the research mode.

### Phase 2 — Gather evidence

**For Quick mode:** Use WebSearch and WebFetch inline. Read relevant local files with Glob/Grep/Read.

**For Deep mode:** Spawn parallel Explore subagents for distinct aspects of the question. Typical split:
- Subagent A: Official documentation and current API surface.
- Subagent B: Known issues, GitHub issues, Stack Overflow, community discussions.
- Subagent C: Codebase — how the project currently uses or relates to the topic.

Use a general-purpose subagent for any aspect that requires multi-step web research or document fetching beyond a single search.

**For Comparative mode:** Spawn one subagent per option being compared, each tasked with building the strongest case for that option. Evaluate the results against the user's actual constraints — not abstractly.

**Subagent prompt structure for research tasks:**
```
Research [specific aspect] for the following question: [question].
Context: [user's stack, constraints, versions].
Find: [specific facts needed].
Report: findings with source URLs, version numbers, and recency. Flag anything you could not verify.
Do NOT make implementation decisions — only gather and report facts.
```

### Phase 3 — Evaluate evidence

For each finding:
- Record the source (URL, file path, or tool output).
- Record recency (when was this written or last updated?).
- Assess relevance to the user's specific context.
- Label: VERIFIED (from authoritative source) | ASSUMED (inferred) | OPINION (blog/forum).
- Flag contradictions between sources explicitly — do not silently pick one.

**Triangulation rule for decision-critical claims.** A claim the recommendation hinges on needs two independent sources or one Tier 1–2 source (official docs, changelog, source code — see [references/source-quality.md](references/source-quality.md)). Independent means different upstream origin — ten blog posts citing the same announcement are one source. A decision-critical claim that fails triangulation goes in Open Gaps, not in the recommendation's foundation.

**Version-lock rule.** When the question concerns a library or framework the project already uses, read the project's manifest first (`.csproj`, `package.json`, `go.mod`, lock file) and evaluate every finding against that pinned version. Advice that is correct for the latest release and wrong for the project's version is a failed brief — findings that only apply to a newer version must say so and state the upgrade implied.

### Phase 4 — Synthesise

1. Answer the research question directly if the evidence supports it.
2. If the evidence is contradictory, present both sides and explain the contradiction.
3. If the evidence is insufficient, say so explicitly and list what would unlock an answer.
4. **Run the counter-evidence search.** Before finalising any recommendation, run at least one search explicitly designed to disprove it — "[X] problems", "[X] limitations", "moving away from [X]", the strongest known criticism. Research that only gathers support for the emerging answer is confirmation bias with citations. If the counter-search surfaces something material, fold it into the risk register or change the recommendation; either way, record what was searched and what came back.
5. Produce the output format below.

### Phase 5 — Handoff

State which downstream skill should act on the findings and what it needs from this brief. Do not skip this — the brief is not complete without a clear next step.

---

## Source Evaluation Rules

The source hierarchy, always: **official docs > primary sources (changelogs, release notes, source code, maintainer statements) > reputable secondary sources (major engineering blogs, conference talks) > low-quality blogs, forums, and SEO content**. A claim's confidence is capped by the best source supporting it.

- **Official docs beat blog posts.** If official docs contradict a blog, the blog is wrong until proven otherwise.
- **Recent beats old.** For fast-moving libraries, a post older than 18 months needs verification.
- **Specific beats general.** A GitHub issue showing the exact error beats a generic StackOverflow answer.
- **Reproducible beats anecdotal.** A failing unit test or reproducible example beats "it didn't work for me".
- **Primary source beats secondary.** Changelog / release notes beat "I heard they deprecated X".

If a source cannot be classified, label it UNVERIFIED and note it. For the expanded hierarchy, per-domain staleness windows, and source red flags, load [references/source-quality.md](references/source-quality.md) — read it when ranking conflicting sources or assessing whether a dated source is still usable.

---

## Handling Contradictions

When two sources contradict each other:

1. Note both sources and what each says.
2. Check recency — is one newer than the other?
3. Check authority — is one from the library maintainer and one from a forum?
4. Check specificity — does one apply to a different version or context?
5. If still unresolved, present both views and mark the gap as open.

Never silently pick one side of a contradiction.

---

## Output Format

### Research Brief: [Topic]

**Question:** [One-sentence restatement]
**Decision this informs:** [What the user is trying to decide]
**Date:** [YYYY-MM-DD — findings age per the staleness windows in references/source-quality.md; re-verify time-sensitive claims if reading this later]
**Mode used:** Quick / Deep / Comparative
**Research conducted:** [What was searched, which subagents were used, what files were read]

**Summary:** [2–4 sentences. Direct answer if evidence supports one. "Insufficient information — see gaps" if not.]

#### Findings

| # | Finding | Source | Recency | Confidence | Notes |
|---|---------|--------|---------|------------|-------|
| 1 | ... | [URL or file:line] | [Date or "current"] | High / Medium / Low | VERIFIED / ASSUMED / OPINION |

#### Options (if comparing alternatives)

| Option | Pros | Cons | Fit for This Project | Confidence |
|--------|------|------|----------------------|------------|
| ...    | ...  | ...  | ...                  | ...        |

#### Contradictions Found

| Topic | Source A says | Source B says | Resolution |
|-------|---------------|---------------|------------|
| ...   | ...           | ...           | Unresolved / Resolved: [explanation] |

#### Open Gaps

- [ ] [Specific thing that could not be verified and why]

#### Risk Register

| Risk | Likelihood | Impact | Mitigation / early-warning signal |
|------|-----------|--------|-----------------------------------|
| [What could invalidate the recommendation — e.g. "library maintainer inactive since 2025-11; abandonment risk"] | Low / Med / High | Low / Med / High | [What to watch or do — e.g. "pin the version; check release activity before v2 work"] |

Include only real risks surfaced by the evidence. Write "None identified" if the research genuinely surfaced none — never pad this table.

#### Recommendation

[One paragraph. State a recommendation if the evidence supports one. If not, state what additional information would unlock a recommendation. Do not hedge everything — make a call where the evidence justifies it.]

**Counter-evidence check:** [What was searched to disprove this recommendation, and what it found — e.g. "Searched 'Hangfire problems production' and the repo's issue tracker: recurring complaints about dashboard auth defaults (mitigated in risk register); nothing recommendation-changing."]
**Confidence:** High / Medium / Low — [and the single piece of new evidence that would most change this call.]

#### What I Would Do

[2–4 sentences, first person, committed. Not a summary of options — the specific action you would take today given this evidence, in order, including what you would deliberately skip. E.g. "I would adopt X at version N, pin it, and skip the Y migration until the Z gap closes — the abandonment risk is priced in by pinning."]

#### Handoff

**Recommended next skill:** [planner / api-designer / backend-architect / bug-hunter / etc.]
**What that skill needs from this brief:** [Specific findings, the options table, the recommendation, etc.]

A fill-in version of this format is in [templates/research-brief-template.md](templates/research-brief-template.md).

---

## Quality Bar

- Every factual claim has a source or is labelled ASSUMED.
- Every time-sensitive claim was either verified against a live source or explicitly flagged as unverified training knowledge — the Staleness Gate was applied, not skipped.
- Contradictions are surfaced, not hidden.
- The summary answers the original question or clearly states why it cannot.
- Options table exists if more than one approach was considered.
- No invented URLs or fabricated references.
- Gaps are listed as specific unknowns, not vague disclaimers.
- The risk register contains evidence-backed risks or an honest "None identified" — no padding.
- A counter-evidence search ran against the recommendation before it was finalised, and its result is reported.
- Decision-critical claims are triangulated (two independent sources or one Tier 1–2 source) or moved to Open Gaps.
- When the project already uses the library in question, findings were evaluated against its pinned version (manifest read), and version-mismatched advice is flagged as such.
- "What I Would Do" makes a committed first-person call; it does not restate the options.
- Evidence and inference are visibly separated: findings tables hold evidence, Recommendation/What I Would Do hold inference.
- The handoff section names a next skill and what it needs.
- Deep and Comparative runs document which subagents were used and what each was tasked with.

---

## Failure Modes To Avoid

- Answering a time-sensitive question from training knowledge without flagging it — the single most damaging research failure. Versions, prices, tool landscapes, and "current best practice" all rot.
- Confirmation-biased research — only searching for evidence that supports the answer that emerged first. The counter-evidence search exists to break this.
- Presenting assumptions as facts.
- Giving a confident recommendation when evidence is thin — or the inverse: hedging into uselessness when the evidence clearly supports one option.
- Delivering a blob of notes instead of a decision-ready brief — the reader needs the recommendation and risks, not your working.
- Silently resolving contradictions without noting them.
- Producing a wall of text instead of a structured brief.
- Running subagents in sequence when they can run in parallel.
- Searching endlessly instead of delivering findings with gaps noted.
- Fabricating URLs, version numbers, or API signatures.
- Doing the work of planner, code-reviewer, or bug-hunter inside a research brief.
- Scoping the question so broadly that findings are too general to act on.

---

## Related Skills And When To Hand Off

| If research reveals... | Hand off to... |
|------------------------|----------------|
| A clear design decision to make | **planner** or **backend-architect** |
| An API or schema to define | **api-designer** or **database-designer** |
| A specific bug to diagnose | **bug-hunter** |
| Code that needs review | **code-reviewer** |
| Security implications | **security-reviewer** |
| A large multi-stage task | **planner** |
