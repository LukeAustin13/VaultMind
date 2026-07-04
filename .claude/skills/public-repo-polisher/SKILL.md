---
name: public-repo-polisher
description: Use this skill when preparing a repository for public GitHub visibility. It checks README quality, repo structure, naming consistency, example safety, absence of secrets, broken links, and overall presentation. It does not write documentation (use docs-writer) or design repo structure (use planner).
license: MIT
metadata:
  stack: agnostic
  version: 1.1
  last-reviewed: 2026-06-29
---

# Public Repo Polisher

## Purpose

Make a repository look professional and safe for public GitHub. Identify issues that would make the repo look unfinished, unprofessional, or unsafe — secrets, broken links, empty files, poor README, inconsistent naming — and produce a prioritised fix list.

## Use When

- Preparing to make a private repo public.
- The user asks "is this ready to publish?" or "how does this repo look?".
- Before sharing the repo link with others.
- After a batch of changes to verify public presentation has not regressed.

## Do Not Use When

- You are writing documentation — use **docs-writer**.
- You are reviewing code quality — use **code-reviewer**.
- You are reviewing security in depth — use **security-reviewer**.
- You are maintaining internal repo conventions — use **project-maintainer**.

## Inputs To Inspect

- `README.md` and other root documentation.
- Repository file and folder structure.
- Naming patterns across files and folders.
- Example files and sample code.
- `.gitignore` and what is/is not tracked.
- Any files that might contain secrets, tokens, or private details.
- Internal references (company names, private URLs, internal tool names).
- Empty or placeholder files.
- Broken internal links in markdown files.
- License file (if applicable).

## Process

1. **Check README quality.** Work through `checklists/public-readme-checklist.md` item by item — every unchecked item appears in the findings. Does the README explain what the repo is, how to use it, and who it is for? Is it scannable?
2. **Check for secrets and private data.** Work through `checklists/public-repo-safety-checklist.md` item by item. Grep for secret-shaped tokens, internal URLs, company names, employee names, and private project references — do not scan by eye. Grep the README and docs for internal references — company domains, private GitHub URLs, internal tool names, ticket numbers — and genericise or remove them.
3. **Check repo structure.** Is the folder structure logical? Are there orphaned or empty directories? Do folder names follow a consistent convention?
4. **Check naming consistency.** Are files and folders named consistently (all kebab-case, all PascalCase, etc.)? Are there outliers?
5. **Check examples and samples.** Are example files realistic but safe? Do they use placeholder data that is obviously fake?
6. **Check for broken links.** Do internal markdown links point to files that exist? Do relative paths resolve correctly? Also verify referenced assets (images, diagrams) exist and resolve via their relative path.
7. **Check for empty files.** Are there placeholder files with no content that should be filled or removed?
8. **Check for presentation issues.** Typos in visible docs, inconsistent formatting, missing sections in repeated file patterns.
9. **Score and prioritise.** Assign a readiness score and prioritise fixes.

## Output Format

### Public Repo Review: [Repo Name]

**Readiness:** [Ready / Almost Ready / Needs Work / Not Ready]
**Score:** [X/10]

#### Safety Issues (Fix Before Publishing)

| # | File | Issue | Action |
|---|------|-------|--------|
| 1 | `.env.example` | Contains real API key | Replace with placeholder |
| 2 | `docs/setup.md` | References internal VPN URL | Remove or genericise |

#### Presentation Issues

| # | File | Issue | Action |
|---|------|-------|--------|
| 1 | `README.md` | No usage examples | Add 2-3 practical examples |
| 2 | `src/helpers/` | Inconsistent with `src/Services/` casing | Align to one convention |

#### Broken Links

| # | Source File | Link | Status |
|---|-----------|------|--------|
| 1 | `README.md` | `./docs/api.md` | File does not exist |

#### Empty / Placeholder Files

| # | File | Action |
|---|------|--------|
| 1 | `docs/contributing.md` | Add content or remove |

#### README Improvements

- [Specific suggestions for the README]

#### Intentionally Skipped

- [Things that look odd but are fine, with explanation]

## Quality Bar

- Safety issues (secrets, private data) are always the top priority.
- Every finding has a specific file and concrete action.
- The readiness score reflects reality — not inflated to be encouraging.
- The review covers structure, content, and safety — not just one category.
- Broken links are checked: internal markdown links are verified by confirming the target file exists, and external URLs are spot-checked for obvious 404s — not assumed to be fine.

## Failure Modes To Avoid

- Missing actual secrets while flagging harmless placeholder values.
- Being too harsh on repos that are genuinely well-structured.
- Ignoring the `.git` history where secrets may exist in past commits.
- Suggesting changes that conflict with the repo's established conventions.
- Treating every cosmetic issue as a blocker.
- Producing a report longer than the repo itself.

## Related Skills And Agents

- **project-maintainer** — for internal convention drift and ongoing maintenance after publishing (different focus than public presentation).
- **security-reviewer** — for deeper security analysis.
- **docs-writer** — for writing the documentation identified as missing.
