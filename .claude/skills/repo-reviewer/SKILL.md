---
name: repo-reviewer
description: Use this skill when you need a holistic, read-only health review of an ENTIRE repository — "review this repo", "is this codebase healthy?", "what are the risky areas?", "is this ready to onboard onto?", "give me a repo health check". It produces a one-page health snapshot — structure, risk hotspots (churn × size × thin tests), dependency and security posture, test/CI state, and a prioritised action list — routing deep analysis to specialists. It does NOT fix drift or enforce conventions (use **project-maintainer**), prepare a repo for public release (use **public-repo-polisher**), explain how the code works (use **codebase-visualiser**), or review a single diff/PR (use **code-reviewer** / **pull-request-review-swarm**).
license: MIT
metadata:
  stack: agnostic
  version: 1.0
  last-reviewed: 2026-06-29
---

# Repo Reviewer

A read-only, whole-repository health assessment. It assesses and prioritises; it does not change files. It is an orchestrator — it routes deep dives to the specialist skills and aggregates their signal into one page.

## Use When

- Inheriting or auditing an unfamiliar repository and you need a risk-and-health overview.
- Deciding whether a codebase is ready to build on, onboard a team onto, or invest in.
- The user asks "where are the risky parts?", "what's the state of this repo?", or "what should we fix first?".

## Do Not Use When

- You need to fix drift, stale config, or convention violations — use **project-maintainer**.
- You are preparing the repo for public GitHub (secrets, README, presentation) — use **public-repo-polisher**.
- You need to understand how the system works or see its architecture — use **codebase-visualiser**.
- You are reviewing one change, diff, or PR — use **code-reviewer** or **pull-request-review-swarm**.
- You only need package currency/vulnerability detail — use **dependency-auditor** directly.

## Inputs To Look For

- Repo root: solution/project files, `package.json`/`.csproj`, lockfiles, CI config, README, test projects.
- Git history availability (needed for churn/hotspot analysis).
- Any stated concern that should weight the review (reliability, onboarding, security).

## Process

1. **Map structure and stack.** Identify languages, projects, entry points, and layout. If the repo is unfamiliar, run **codebase-visualiser** (Internal Orientation Mode) and fold in its map rather than re-deriving it.
2. **Risk hotspot analysis.** This is the distinctive value — find files most likely to cause problems:
   - **Churn:** `git log --pretty=format: --name-only --since="12 months ago" | sort | uniq -c | sort -rn | head -20` for most-changed files.
   - **Size/complexity:** flag unusually large files/methods (line counts, deep nesting).
   - **Thin tests:** map hotspots to test presence — high-churn files with no nearby test are top risk.
   - **Repeat-bug signal:** `git log --oneline --grep="fix\|bug" -- <file>` to spot files that attract fixes.
   - Rank: high churn × high complexity × low test coverage = highest risk.
   - If git history is shallow or absent, say so in the snapshot and grade Risk Hotspots as "insufficient data" — do not substitute guesses for churn evidence.
3. **Dependency posture.** Route to **dependency-auditor** for outdated/deprecated/vulnerable packages. Summarise its headline, do not re-derive it.
4. **Security posture.** Route to **security-reviewer** for a high-level pass (secrets, auth, unsafe config). Summarise headline findings.
5. **Test and CI state.** Note test project presence, rough coverage signal, and whether CI builds/tests on every change.
6. **Documentation and onboarding readiness.** Is there a README that gets a newcomer running? Setup steps, contributing notes, architecture docs?
7. **Aggregate** into the one-page snapshot below with a grade per dimension and a prioritised action list.

## Output Format

### Repo Health Snapshot: [repo name]

**Overall:** [Healthy / Needs attention / At risk] — one-sentence verdict.

| Dimension | Grade | Evidence |
|---|---|---|
| Structure & clarity | B | 4 projects, clear layering; `Utils` is a catch-all |
| Risk hotspots | C | see table |
| Dependencies | C | 6 majors behind; 1 deprecated (via dependency-auditor) |
| Security posture | B | no secrets found; 1 broad CORS policy (via security-reviewer) |
| Tests & CI | D | 12% of projects have tests; CI builds but does not run tests |
| Docs & onboarding | C | README exists; setup steps are stale |

**Risk Hotspots**

| File | Churn (12mo) | Size | Tests | Risk | Why |
|---|---|---|---|---|---|
| OrderService.cs | 47 commits | 820 LOC | none | High | Most-changed file, large, untested, 9 "fix" commits |
| PricingEngine.cs | 31 commits | 540 LOC | thin | High | High churn, core logic, only happy-path tests |

**Top Actions (prioritised)**
1. Add characterisation tests around `OrderService` before further change (highest risk).
2. Wire test execution into CI — it currently only builds.
3. Upgrade the deprecated package (see dependency-auditor output).

**Routed to specialists:** dependency-auditor ✓ · security-reviewer ✓ · codebase-visualiser ✓ (or "not run — reason")

## Quality Bar

- Every hotspot is backed by concrete evidence (churn count, LOC, missing test path) — not a hunch.
- Deep analysis is routed to **dependency-auditor** and **security-reviewer**, whose findings are summarised, not re-derived or duplicated.
- Each dimension has a grade and a one-line justification.
- The action list is prioritised by risk and is concrete enough to start.
- The review changes nothing — it is read-only assessment.

## Failure Modes To Avoid

- Duplicating **dependency-auditor** or **security-reviewer** instead of routing to them.
- Grading hotspots without git evidence (churn/size/test data).
- Drifting into fixing things — this skill assesses; **project-maintainer** fixes.
- Producing a multi-page essay instead of a one-page, prioritised snapshot.
- Claiming a clean security/dependency bill without actually running (or routing to) the specialist.

## Related Skills

- **project-maintainer** — fix the drift and convention issues this review surfaces.
- **dependency-auditor** / **security-reviewer** — the specialists this skill routes deep dives to.
- **codebase-visualiser** — for the structural map and architecture explanation.
- **public-repo-polisher** — when the goal is public-release readiness specifically.
