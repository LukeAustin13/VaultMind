---
name: dependency-auditor
description: Use this skill when you need to review the state of project dependencies — outdated packages, version gap risk, known vulnerability patterns, deprecated packages, and safe upgrade sequencing. Works with NuGet (.csproj, packages.lock.json) and npm (package.json, package-lock.json). Establishes current versions by running dotnet list package --outdated / npm outdated, and includes a Verified-Upgrade Loop for actually applying upgrades one package at a time with build/test proof per bump. It does not run live vulnerability scanners. Use security-reviewer for broader security analysis and dotnet-upgrade for framework version bumps.
license: MIT
metadata:
  stack: dotnet-primary
  version: 1.2
  last-reviewed: 2026-07-03
---

# Dependency Auditor

## Use When
- The user says "audit dependencies", "check our packages", or "are our packages up to date?"
- A package upgrade is planned and the user wants to understand the risk.
- The user asks "is it safe to upgrade X to version Y?"
- A package has been flagged as vulnerable and a replacement or upgrade path is needed.
- A new project is starting and a package selection decision is needed.
- Periodic dependency health checks before a release.

## Do Not Use When
- The task is designing backend architecture — use **backend-architect**.
- The task is a broad security review — use **security-reviewer** (this skill focuses specifically on dependency state).
- The task is reviewing NuGet or npm configuration structure — use **project-maintainer**.

## Inputs To Look For
- `.csproj` files (NuGet package references).
- `packages.lock.json` (NuGet lock file, if present).
- `package.json` and `package-lock.json` or `yarn.lock` (npm/yarn).
- The current runtime version (e.g., .NET 8, Node 20).
- Any specific packages the user is concerned about.

## Process

### Step 1 — Read dependency files
1. Read all `.csproj` files for `<PackageReference>` entries.
2. Read `package.json` for `dependencies` and `devDependencies`.
3. Note the declared version (pinned `1.2.3`, range `^1.2.3`, or floating `*`).
4. Note the target framework / runtime version.

### Step 2 — Classify each dependency
Assign each package to a category:

| Category | Description |
|---|---|
| Core runtime | Framework or platform package (e.g., `Microsoft.AspNetCore.*`, `react`) |
| Data access | ORM, database driver (e.g., `Microsoft.EntityFrameworkCore`, `pg`) |
| Auth/security | Auth libraries (e.g., `Microsoft.AspNetCore.Authentication.*`, `jsonwebtoken`) |
| Testing | Test frameworks and utilities (e.g., `xunit`, `jest`, `Moq`) |
| Utilities | General helpers (e.g., `Newtonsoft.Json`, `lodash`) |
| Dev tooling | Build, lint, formatting tools |

### Step 3 — Assess version currency
Establish the current stable release with a command, never from memory: run `dotnet list package --outdated` for NuGet (add `--include-transitive` when auth/security packages are involved) and `npm outdated` for npm. If neither can run (no network, restore fails), say so and mark every currency assessment as UNVERIFIED — training-data version knowledge is stale by definition. Then assess the gap between the declared version and the current stable release:

| Gap | Risk Label | Action |
|---|---|---|
| Patch behind (e.g., 1.2.0 → 1.2.5) | Low | Upgrade freely |
| Minor behind (e.g., 1.2.0 → 1.5.0) | Medium | Review changelog before upgrading |
| Major behind (e.g., 1.x → 2.x) | High | Read migration guide; plan upgrade separately |
| Using pre-release in production | High | Evaluate if stable is available |
| No version pinned (floating `*`) | Medium | Pin to a specific version |

### Step 4 — Flag vulnerability patterns
Without a live scanner, apply these heuristics:

- Packages more than 2 major versions behind are statistically more likely to contain unpatched CVEs.
- Auth/security category packages should be treated as High risk at any version gap.
- Packages with known historical issues (e.g., `Newtonsoft.Json` < 13, `log4net` < 2.0.12) should be called out specifically when recognised.
- Packages no longer maintained (final release >3 years ago, repository archived) should be flagged as deprecated.

### Step 5 — Identify deprecated packages
Flag packages where:
- The package owner has published a deprecation notice (common with older Microsoft packages).
- A well-known successor exists (e.g., `Newtonsoft.Json` → `System.Text.Json` for most use cases, `Microsoft.Extensions.Logging.AzureAppServices` → built-in providers).
- The NuGet or npm listing shows the package as deprecated.

### Step 6 — Recommend upgrade sequencing
When multiple packages need upgrading, order by:
1. Security-critical packages first (auth, encryption, networking).
2. Framework/platform packages before packages that depend on them.
3. Packages with no inter-dependencies can be batched.
4. Major version upgrades last (they require the most testing).

## Verified-Upgrade Loop

This is opt-in. The default behaviour of this skill is to analyse and report only (Steps 1–6 above). Enter this loop **only** when the user explicitly asks you to perform the upgrades — phrases like "actually upgrade these", "apply the upgrades", or "fix the dependencies", not just "audit" or "report".

Upgrade one risky package per iteration so that any failure is attributable to a single bump. Cap the loop at roughly 3 iterations per package; if a package still fails after rollback and retry, stop and report it as blocked rather than forcing it through.

For each package, in the recommended upgrade sequence from Step 6:

1. **Record the current state.** Note the declared version before changing it. Confirm a clean baseline: restore/install succeeds, build is green, tests pass. If the baseline is already broken, stop — a broken baseline makes every result unattributable.
2. **Bump one package.** Edit the single `<PackageReference>` or `package.json` entry. Do not bump anything else in the same iteration.
3. **Re-resolve.** Run the restore/install step: `dotnet restore` (or `dotnet build` which restores) for NuGet, `npm install` for npm. Watch for resolution conflicts and downgrade warnings introduced by the bump.
4. **Build.** Run the build (`dotnet build -c Release` or the project's `npm run build`). A failed build means the bump is incompatible — go to step 6.
5. **Test.** Run the test suite (`dotnet test` or `npm test`). Compare against the baseline from step 1 — a test that passed before and fails now is a regression caused by this bump.
6. **Verdict.**
   - If restore, build, and tests all pass: keep the bump, record the new version, move to the next package.
   - If any step fails: roll back this single bump (restore the recorded version), confirm the baseline is green again, and record the package as blocked with the failing step and the error. Do not leave a broken bump in place to chase the next package.
7. **Repeat** for the next package in sequence. Re-establish the green baseline at the start of each package so each result stays attributable.

Report the outcome per package: upgraded (old → new), or blocked (attempted version, failing step, error). Never claim an upgrade succeeded without showing the build and test commands that confirmed it.

## Output Format

### Dependency Audit: [Project name]

**Packages Reviewed:** [count NuGet] / [count npm]
**Target Runtime:** [e.g., .NET 8 / Node 20]
**Audit Date:** [today's date]

---

#### Summary

| Risk Level | Count |
|---|---|
| High | [n] |
| Medium | [n] |
| Low | [n] |
| Deprecated | [n] |
| Up to date | [n] |

---

#### High Priority

| Package | Declared | Latest Stable | Gap | Concern | Action |
|---------|---------|--------------|-----|---------|--------|
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 6.0.0 | 8.0.5 | Major ×2 | Auth package; security-critical | Upgrade with migration guide |

#### Medium Priority

| Package | Declared | Gap | Notes |
|---------|---------|-----|-------|
| `Newtonsoft.Json` | 12.0.3 | Minor | Consider migrating to `System.Text.Json` |

#### Deprecated Packages

| Package | Status | Replacement |
|---------|--------|------------|
| `Microsoft.AspNetCore.Mvc.Versioning` | Deprecated by owner | `Asp.Versioning.Mvc` |

#### Recommended Upgrade Sequence

1. [Package] — [Reason for priority]
2. [Package] — [Reason for priority]
3. [Package group] — [Can be batched]

#### Floating / Unpinned Versions

| Package | Declared | Risk |
|---------|---------|------|
| `SomePackage` | `*` | Unpredictable resolution; pin to a specific version |

#### Notes
- [Any project-specific observations, e.g., transitive dependency conflicts, multi-target frameworks]

## Quality Bar
- Every High risk finding has a specific, actionable recommendation.
- Upgrade sequencing accounts for framework dependencies (upgrade the framework before its extensions).
- Deprecated packages always include a known replacement or a note that no replacement exists.
- The audit does not recommend downgrading without a specific reason.
- Floating versions are always flagged — they undermine reproducible builds.
- In the Verified-Upgrade Loop, every kept upgrade is backed by a green restore, build, and test run; every failed bump is rolled back to a confirmed-green baseline and reported as blocked, never left in place — and packages are bumped one at a time so each result is attributable.

## Failure Modes To Avoid
- Recommending upgrading everything at once — batch by risk, not alphabetically.
- Flagging every minor version gap as High risk — calibrate to actual impact.
- Suggesting `System.Text.Json` as a drop-in for `Newtonsoft.Json` without noting the API differences.
- Missing auth/security packages hidden inside transitive dependencies.
- Treating `devDependencies` the same as production dependencies for risk purposes.
- Claiming specific CVE numbers without a live scanner — only note known historical patterns.
