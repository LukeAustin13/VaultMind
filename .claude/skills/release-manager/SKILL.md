---
name: release-manager
description: Use this skill when you need to manage a release — decide the version number, generate release notes from git history, produce a release checklist, or plan a hotfix workflow. The release-manager covers the full cycle from version decision to published release. It does not write code, design APIs, or manage deployment infrastructure — use devops-deploy for that.
license: MIT
metadata:
  stack: dotnet-primary
  version: 1.1
  last-reviewed: 2026-07-04
---

# Release Manager

## Use When
- The user says "time to release", "cut a release", or "prepare a release".
- A version number decision is needed (what to bump: major, minor, or patch).
- Release notes need to be written from commit history or PR descriptions.
- A hotfix needs to be released outside the normal release cycle.
- A pre-release (alpha, beta, RC) needs to be prepared.
- The user asks "what version should this be?"

## Do Not Use When
- The task is deploying the release to an environment — use **devops-deploy**.
- The task is writing a PR description — use **git-pr-assistant**.
- The task is designing a versioning strategy for an API contract — use **api-contract-guardian**.

## Inputs To Look For
- Git log since the last release tag (or since a specified commit range).
- The current version number.
- Whether any breaking changes are included.
- The target environment (production, staging, internal).
- Any known issues or blockers that should be noted.

## Semantic Versioning Rules

Given version `MAJOR.MINOR.PATCH`:

| Change Type | Bump | Example |
|---|---|---|
| Breaking change in public interface | MAJOR | `1.0.0 → 2.0.0` |
| New backwards-compatible feature | MINOR | `1.0.0 → 1.1.0` |
| Backwards-compatible bug fix | PATCH | `1.0.0 → 1.0.1` |

**Pre-release suffixes:** `-alpha.1`, `-beta.2`, `-rc.1`

**Rules:**
- When MAJOR is bumped, MINOR and PATCH reset to 0.
- When MINOR is bumped, PATCH resets to 0.
- `0.y.z` versions (initial development) may have breaking changes at MINOR level — document this clearly.
- A deprecation notice is not a breaking change. Removing the deprecated thing is.

## Process

### Step 1 — Assess the change set
1. Read the git log since the last release tag: `git log v<last> ..HEAD --oneline`.
2. Group commits into: breaking changes, new features, bug fixes, internal changes (refactoring, tests, docs).
3. Identify the highest-impact change — that determines the version bump.

### Step 2 — Decide the version
1. Apply SemVer rules from the table above.
2. If uncertain whether a change is breaking, err toward MAJOR.
3. State the version decision with a one-sentence rationale.

### Step 3 — Write release notes
Release notes for a tagged release are owned by this skill (per-PR changelog entries belong to **git-pr-assistant**).
1. Group entries into sections: Breaking Changes, New Features, Bug Fixes, Internal.
2. Omit internal entries (refactoring, test updates, CI changes) unless they affect consumers.
3. Write each entry from the consumer's perspective, not the implementation's.
4. Include migration notes for any breaking changes.
5. Credit contributors if the project is public and names are available.

### Step 4 — Run release checklist
Work through `checklists/release-checklist.md`. Flag any items that are not complete. For build/test rows, route to **dotnet-quality-gate** (or the project's own test command) rather than reporting Unknown when the commands are runnable here — Unknown is for genuinely unrunnable checks, not unattempted ones.

### Step 5 — Produce the tag command and release summary
Provide the exact git tag command to run and a formatted release summary ready to paste into GitHub Releases or a CHANGELOG.

## Release Checklist

The full item list lives in `checklists/release-checklist.md` — work through every item there. Report results using the Checklist Status summary table in the Output Format below.

## Hotfix Workflow

When a critical bug requires a release outside the normal cycle:

1. **Branch from the release tag,** not from `main` (unless `main` is always production-ready):
   ```
   git checkout -b hotfix/v1.2.1 v1.2.0
   ```
2. **Apply the minimal fix only.** Do not include unrelated changes.
3. **Test specifically.** Run the test suite and manually verify the bug is fixed.
4. **Bump the PATCH version** (hotfixes are always patch releases).
5. **Merge back to both the release branch and `main`** to prevent regression.
6. **Tag and release** using the same process as a normal release.

## Output Format

### Release: v[version]

**Version Decision:** v[previous] → v[new] ([bump type])
**Rationale:** [One sentence explaining why this bump level was chosen]

---

#### Release Notes

**Breaking Changes**
- [Description of change and migration path, or "None"]

**New Features**
- [Feature description from consumer perspective]

**Bug Fixes**
- [Bug description and what was fixed]

---

#### Checklist Status

| Item | Status |
|------|--------|
| Build passing | [Pass / Fail / Unknown] |
| Tests passing | [Pass / Fail / Unknown] |
| Version bumped | [Done / Pending] |
| CHANGELOG updated | [Done / Not used / Pending] |
| Smoke tested | [Done / Pending] |

**Blockers:** [Any checklist items that are not complete and must be resolved before release]

---

#### Tag Command

```bash
git tag -a v[version] -m "Release v[version]"
git push origin v[version]
```

## Quality Bar
- Version decision always cites a specific reason.
- Release notes are written from the consumer's perspective.
- Breaking changes always include migration notes.
- Checklist status is honest — unknown items are marked unknown, not assumed to pass.
- Hotfix steps always check both the release branch and `main` for the merge-back.

## Failure Modes To Avoid
- Bumping MINOR for a breaking change to avoid consumer disruption — SemVer exists to communicate truth.
- Writing release notes that describe implementation ("refactored service layer") instead of consumer impact.
- Skipping the checklist because the release feels small.
- Creating a tag from a dirty or unreviewed commit.
- Forgetting to merge a hotfix back to `main`, causing regression in the next release.
