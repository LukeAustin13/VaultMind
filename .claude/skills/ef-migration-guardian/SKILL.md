---
name: ef-migration-guardian
description: Use this skill when you need to check whether Entity Framework Core model changes require a migration, review generated migrations for destructive operations, or validate migration safety before applying. It does not design database schemas (use database-designer) or review general data access patterns (use data-access-reviewer agent).
license: MIT
metadata:
  stack: dotnet
  version: 1.0
  last-reviewed: 2026-05-18
---

# EF Migration Guardian

## Purpose

Detect Entity Framework Core model changes that require migrations, review generated migrations for destructive operations (data loss, type narrowing, dropped indexes), and prevent unsafe migration application to shared or production databases.

## Use When

- Entity classes, DbContext configuration, or Fluent API mappings have changed.
- The user asks "do I need a migration?" or "is this migration safe?".
- A migration has been generated and needs review before applying.
- You are about to apply a migration to a shared or production database.
- A PR includes migration files that need review.

## Do Not Use When

- You are designing a new schema from scratch — use **database-designer**.
- You are reviewing query patterns or data access code — use **data-access-reviewer** agent.
- You are debugging a runtime EF error — use **bug-hunter**.

## Inputs To Inspect

- Entity classes and their properties.
- `DbContext` and `OnModelCreating` configuration.
- Fluent API configuration files.
- Existing migration files in the `Migrations/` folder.
- The model snapshot file (`*ModelSnapshot.cs`).
- Any generated SQL scripts (`dotnet ef migrations script`).

## Process

1. **Detect model changes.** If the EF tooling is available, run `dotnet ef migrations has-pending-model-changes` (EF Core 8+) and cite its output as the Evidence line. Fall back to manually diffing entities against the model snapshot only when the command cannot run, and say so — eye-diffing Fluent API configs is unreliable. Look for:
   - New or removed entities.
   - Added, removed, or renamed columns.
   - Changed column types, nullability, or max length.
   - New or removed indexes, keys, or relationships.
   - Changed cascade delete behaviour.
2. **Determine if a migration is needed.** If model changes exist and no corresponding migration exists, a migration is needed.
3. **Review generated migration.** If a migration file exists, inspect the `Up()` and `Down()` methods for:
   - **Destructive operations:** `DropTable`, `DropColumn`, `AlterColumn` (type narrowing), `DropIndex`.
   - **Data loss risks:** Removing nullable, reducing string length, changing column types.
   - **Rename ambiguity:** EF sometimes generates drop+add instead of rename. Check if `RenameColumn` or `RenameTable` should be used.
   - **Index changes:** Dropped indexes that may be needed by existing queries.
4. **Generate SQL script for review.** Run `dotnet ef migrations script --idempotent` yourself when the project builds here, and quote the destructive statements from the actual script in the Destructive Operations table. If it cannot run, state that the review is based on the C# migration only.
5. **Assess risk.** Rate the migration based on destructive potential and data loss risk.
6. **Recommend next steps.** Never recommend auto-applying to shared databases.

## Output Format

### EF Migration Review: [Context Name]

**Migration Needed:** Yes / No / Unknown
**Evidence:** [What changed in the model]
**Risk Level:** Low / Medium / High / Critical

#### Model Changes Detected

| # | Change | Entity | Detail | Migration Impact |
|---|--------|--------|--------|-----------------|
| 1 | New column | Order | `ShippingDate` (DateTime?) | `AddColumn` — safe |
| 2 | Type change | Product | `Price` decimal(18,2) to decimal(10,4) | `AlterColumn` — potential truncation |
| 3 | Removed column | Customer | `LegacyId` | `DropColumn` — data loss |

#### Destructive Operations

| # | Operation | Table/Column | Risk | Mitigation |
|---|-----------|-------------|------|------------|
| 1 | DropColumn | Customer.LegacyId | Data loss | Back up column data before applying |
| 2 | AlterColumn | Product.Price | Truncation | Verify no values exceed new precision |

#### Recommended Next Steps

1. [Generate SQL script: `dotnet ef migrations script --idempotent`]
2. [Review SQL before applying]
3. [Apply to dev/test first, never directly to production]

## Quality Bar

- Every model change is identified with its migration impact.
- Destructive operations are explicitly called out.
- Risk level reflects actual data loss potential, not just schema change count.
- SQL script generation is always recommended for non-trivial migrations.
- Never recommends `dotnet ef database update` against shared or production databases without review.

## Failure Modes To Avoid

- Saying "no migration needed" when model changes exist but were not detected.
- Missing rename-vs-drop ambiguity in generated migrations.
- Recommending auto-apply to production or shared databases.
- Ignoring `Down()` method — rollback must also be safe.
- Treating all `AlterColumn` operations as safe when type narrowing can lose data.
- Overlooking cascade delete changes that affect referential integrity.

## Related Skills And Agents

- **database-designer** — for designing new schemas.
- **data-access-reviewer** agent — for reviewing query patterns and EF usage.
- **dotnet-quality-gate** — run the quality gate after applying migrations locally.
- **ci-triage** — when CI fails due to migration issues.
