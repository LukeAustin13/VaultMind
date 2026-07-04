---
name: project-maintainer
description: Use this skill when the user wants drift fixed — "clean up this repo", "fix the inconsistencies", "bring this back in line with conventions" — covering structure, naming conventions, configuration files, scripts, and documentation consistency. The project-maintainer detects drift from established patterns and suggests minimal corrective changes. It does not add features, redesign architecture, or perform deep security reviews — it maintains what already exists. Health-check and assessment questions ("is this repo healthy?", "repo health check") belong to repo-reviewer; package currency belongs to dependency-auditor.
license: MIT
metadata:
  stack: agnostic
  version: 1.3
  last-reviewed: 2026-07-03
---

# Project Maintainer

## Use When
- The user says "clean up this repo", "fix the inconsistencies", or "what needs cleaning up?"
- You notice inconsistencies in naming, structure, or config during another task.
- Build scripts, CI config, or project files have drifted from conventions.
- New files or folders were added and may not follow the project's existing patterns.

Health-check phrasings ("is this repo in good shape?", "health check before release/handover") route to **repo-reviewer** — that skill assesses; this one fixes drift.

## Do Not Use When
- You are adding new features — that is implementation work.
- You are redesigning the project structure — use **backend-architect** or **planner**.
- You are doing a security audit — use **security-reviewer**.
- You are debugging — use **bug-hunter**.
- You are preparing a repo for public visibility — use **public-repo-polisher**.
- You are auditing package versions and currency specifically — use **dependency-auditor**.
- You want a read-only, holistic health snapshot with risk hotspots rather than drift fixes — use **repo-reviewer**.

## Inputs To Look For
- The full project directory structure.
- Naming patterns in existing files and folders.
- Project files (`.csproj`, `package.json`, `Cargo.toml`, etc.) and their consistency.
- Configuration files across environments.
- Build and CI scripts.
- Existing documentation and its accuracy.
- Dependency versions and update status.

## Process
1. **Survey the project.** Scan the directory structure, project files, config files, and scripts.
2. **Identify the established conventions.** What patterns are already in use? (Naming, folder structure, config format, dependency management.) Scan at least 5 files in each major category (config files, scripts, class/module definitions) to extract the repeated choices before judging drift.
3. **Detect drift.** Find files, folders, configs, or dependencies that deviate from established conventions:
   - Inconsistent naming (PascalCase mixed with kebab-case).
   - Orphaned files (not referenced anywhere).
     - Orphaned references (config keys used nowhere, scripts not wired into CI, unused imports) — spot-check by grepping for where each is referenced.
   - Stale config (references to removed features or services).
   - Missing or outdated documentation.
   - Build scripts that no longer work.
4. **Prioritise findings.** Focus on drift that causes real problems (build failures, confusion, maintenance burden), not cosmetic issues.
5. **Triage each finding: FIX / DEFER / DELETE.** FIX — it causes real problems now and the correction is safe and small. DEFER — real but off-path or risky to change (externally referenced names, published scripts, anything consumers depend on); record it with location and severity rather than fixing it in this pass. DELETE — an intentional deviation or a taste difference; leave it alone and say why. DEFER and DELETE are first-class outcomes, not failures to fix.
6. **Suggest minimal fixes for the FIX items.** Each suggestion should be the smallest change that restores consistency. Do not over-correct. If the user asserts a convention the repo's own files contradict (a "new standard" that matches nothing established), challenge it before proposing mass churn: drift is measured against what the repo does, and a convention change is a decision to record and stage, not a cleanup to smuggle in.
7. **Identify things to leave alone.** Explicitly note deviations that are intentional or not worth fixing.

## Output Format

### Project Health: [Project Name]

**Date:** [Date]
**Overall:** [Clean / Minor drift / Needs attention]

#### Findings

| # | Category | Finding | Severity | Triage | Suggested Fix |
|---|----------|---------|----------|--------|---------------|
| 1 | Naming | `utils/` folder inconsistent with `Helpers/` convention | Low | DEFER | Rename touches 30+ imports — record, batch with next structural change |
| 2 | Config | `appsettings.Staging.json` references removed service | Medium | FIX | Remove stale section |
| 3 | Docs | README setup steps reference old CLI tool | High | FIX | Update to current tool |
| 4 | Build | `build.sh` not executable | Low | FIX | `chmod +x build.sh` |

**Severity:**
- **High:** Causes build failures, confusion, or incorrect behaviour.
- **Medium:** Maintenance burden or potential for mistakes.
- **Low:** Cosmetic inconsistency.

#### Intentionally Skipped
- [Deviation that is intentional or not worth fixing, and why]

Dependency currency is not assessed here — route it to **dependency-auditor**, which establishes latest versions by command rather than from memory.

## Quality Bar
- Findings are based on the project's own conventions, not external opinions.
- Each finding has a concrete, minimal fix.
- Severity reflects actual impact, not pedantry.
- Intentional deviations are noted and left alone.
- The review is actionable — someone can go through the list and fix things.

## Failure Modes To Avoid
- Imposing conventions from other projects onto this one — including a convention the user decrees mid-task that the repo's own files contradict; challenge it, do not mass-rename on say-so.
- Suggesting large renames or restructures that create churn without value.
- Flagging every cosmetic inconsistency as if it were critical.
- Ignoring dependency updates because "it works now".
- Suggesting fixes that break things (renaming a folder without updating references).
- Turning a maintenance review into an architecture redesign.
