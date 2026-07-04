---
name: dotnet-upgrade
description: Use this skill when upgrading a .NET solution to a new framework version (e.g. .NET 6 to 8, 8 to 9) — target framework bumps, SDK and global.json changes, breaking-change review, package compatibility, multi-project sequencing, and post-upgrade verification. It does not audit package health generally (use dependency-auditor) or fix unrelated build failures (use bug-hunter).
license: MIT
metadata:
  stack: dotnet
  version: 1.0
  last-reviewed: 2026-06-10
---

# .NET Upgrade

## Use When

- The user says "upgrade to .NET 9", "move off .NET 6", or "bump the target framework".
- A .NET version is approaching end of support and the solution must move.
- A needed package or language feature requires a newer TFM.
- An upgrade was attempted and the solution no longer builds or behaves the same.

## Do Not Use When

- Updating NuGet packages within the same framework version — use **dependency-auditor**.
- Diagnosing a runtime failure unrelated to the upgrade — use **bug-hunter**.
- Validating a finished upgrade builds and tests green — this skill ends by invoking **dotnet-quality-gate**.

## Inputs To Inspect

- Every `.csproj` / `.fsproj`: current `<TargetFramework(s)>`, `<LangVersion>`, `<Nullable>`, conditional compilation symbols.
- `global.json` (SDK pin), `Directory.Build.props`, `Directory.Packages.props` (central package management).
- CI configuration: SDK versions in workflows, container base images (`mcr.microsoft.com/dotnet/...` tags in Dockerfiles).
- Deployment targets: hosting runtime versions, self-contained vs framework-dependent publish.
- Package references that are version-bound to the old TFM.

## Process

1. **Inventory the upgrade surface.** List every project with its current TFM, every SDK pin, every CI image, every Dockerfile base image, and the deployment runtime. An upgrade that misses one of these "builds locally, fails in CI/production" — find them all before changing anything.
2. **Decide the target and the path.** Default to the latest LTS unless the user needs STS features. For multi-version hops (6 → 9), upgrade in one step but review the breaking changes for *every* intermediate major version — breaking changes accumulate per version, they are not summarised at the destination.
3. **Review breaking changes for each version crossed.** Check Microsoft's official breaking-changes lists (`learn.microsoft.com/dotnet/core/compatibility/`) for each major version in the path. For any target version released after your knowledge cutoff, fetching the list via **researcher** is mandatory, not optional — do not reconstruct breaking changes from memory. Use **researcher** also when behaviour needs verification beyond the lists. Record every entry that touches APIs the solution actually uses — grep the codebase for each suspect API rather than guessing. High-recurrence areas to check explicitly:
   - Serialization defaults (`System.Text.Json` behaviour changes between majors).
   - ASP.NET Core middleware, hosting, and minimal API changes.
   - EF Core major version coupling (EF Core majors track .NET majors; an upgrade usually forces an EF Core upgrade with its own breaking list — involve **ef-migration-guardian** if the EF jump changes model behaviour).
   - Obsoleted APIs promoted to errors, and new analyzers that fail the build on `TreatWarningsAsErrors`.
4. **Check package compatibility.** Run `dotnet list package --outdated` and check each direct dependency supports the target TFM. Classify: compatible as-is / needs version bump / no compatible version (blocker — resolve before starting, not mid-upgrade).
5. **Sequence the change.** For a single solution, bump everything in one change set — mixed-TFM intermediate states create reference errors that waste time. Exception: published library projects that external consumers depend on may need multi-targeting (`<TargetFrameworks>net8.0;net9.0</TargetFrameworks>`) instead of a hard bump.
6. **Apply the upgrade.** Update in this order: `global.json` → `Directory.Build.props` (if TFM is centralised) → each project's TFM → package versions → CI SDK versions → Dockerfile base images (both `sdk` and `aspnet`/`runtime` tags). Keep it one reviewable commit per concern where practical.
7. **Verify.** Run **dotnet-quality-gate** (restore → format → build → full test suite, not a filtered run — upgrades are exactly the broad change its filter rules reserve the full suite for). Then check the runtime-behaviour risks that compile clean: serialization round-trips, date/number formatting under culture, and any reflection-heavy code.
8. **Sweep for leftovers.** Grep for the old TFM string (`net6.0`) across the repo — docs, scripts, launch profiles, and test fixtures routinely keep stale copies.

## Output Format

### .NET Upgrade Plan: [Solution] — [current] → [target]

**Path:** [e.g. net6.0 → net9.0, crossing 7, 8, 9 breaking-change sets]
**SDK:** [global.json change] | **CI:** [workflow/image changes] | **Docker:** [base image changes]

#### Breaking Changes That Apply

| # | Version | Change | Where It Hits | Action |
|---|---------|--------|---------------|--------|
| 1 | 8.0 | `System.Text.Json` — [specific change] | `OrderSerializer.cs:30` | [specific fix] |

#### Package Compatibility

| Package | Current | Target-Compatible Version | Notes |
|---------|---------|--------------------------|-------|
| ...     | ...     | ... / **BLOCKER**         | ...   |

#### Execution Sequence

1. [Ordered steps from Process step 6, specific to this solution]

#### Verification

- [ ] dotnet-quality-gate: full suite, result cited
- [ ] Runtime behaviour checks: [the specific risks identified in step 7]
- [ ] Old-TFM sweep: zero remaining references to `[old TFM]`

## Quality Bar

- Every breaking-change entry cites where it hits this codebase (file:line from an actual search), or the section states "none of the listed changes touch used APIs — verified by grep for [APIs checked]".
- Package blockers are identified before any TFM is changed.
- CI images and Dockerfiles are in the inventory — not discovered by a red pipeline.
- Verification runs the full test suite and the result is cited, not assumed.

## Failure Modes To Avoid

- Bumping the TFM first and discovering an incompatible package mid-upgrade.
- Reading only the destination version's breaking changes on a multi-version hop.
- Upgrading projects one at a time and fighting mixed-TFM reference errors.
- Declaring success on a green build while serialization or culture behaviour silently changed.
- Leaving CI or Docker images on the old SDK so the upgrade "works on my machine" only.
- Letting the upgrade sprawl into refactoring — the upgrade commit changes versions and compatibility fixes, nothing else.

## Related Skills

- **dependency-auditor** — package-level health and upgrade sequencing within a TFM.
- **dotnet-quality-gate** — the verification step.
- **ef-migration-guardian** — when the forced EF Core jump affects models or migrations.
- **ci-triage** — if the pipeline fails after the upgrade despite local green.
- **researcher** — to verify breaking-change behaviour beyond the official lists.
