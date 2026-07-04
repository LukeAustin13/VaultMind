---
name: database-designer
description: Use this skill when you need to design relational database schema, migrations, indexes, constraints, query patterns, and data lifecycle decisions. The database-designer focuses on data integrity, query performance, and schema evolution. Examples use SQL Server and T-SQL — adapt to PostgreSQL, MySQL, or other engines as needed. It does not design APIs (use api-designer) or backend architecture (use backend-architect). It flags data integrity problems early before they become production incidents.
license: MIT
metadata:
  stack: dotnet
  version: 1.2
  last-reviewed: 2026-07-03
---

# Database Designer

## Use When
- Designing tables for a new feature or service.
- Adding columns, relationships, or indexes to existing tables.
- The user asks "how should I model this data?"
- You need to plan a migration strategy.
- Query performance needs to be considered at design time.
- Data retention, soft deletes, or audit trails need design.

## Do Not Use When
- You are designing API contracts — use **api-designer**.
- You are designing service architecture — use **backend-architect**.
- You are debugging a query performance issue — use **performance-profiler**.
- You are writing the actual migration code — that is implementation work.

## Inputs To Look For
- Entity descriptions and relationships from requirements.
- Existing schema (migration files, DbContext, or database inspection).
- Expected query patterns (what reads are common, what needs to be fast).
- Data volumes and growth expectations.
- The ORM in use (EF Core, Dapper, etc.) and its conventions.
- Existing naming conventions in the database.

## Clarify Before Starting

If any of the following are unknown, ask before designing:

- **New schema or evolving existing?** If existing, what tables already exist and what conventions must be matched?
- **What are the dominant read patterns?** What queries will run most frequently — this determines indexes entirely.
- **What is the expected data volume and growth rate?** This affects primary key strategy, partitioning, and archival design.
- **Are there migration safety constraints?** (Zero-downtime requirement, shared database, existing data that must be preserved)

Schema decisions are expensive to reverse. Wrong assumptions here cause migrations in production.

## Process
1. **Identify entities and relationships.** List every entity, its key attributes, and how entities relate (one-to-one, one-to-many, many-to-many).
2. **Design tables.** For each entity:
   - Table name (follow existing conventions).
   - Columns with types, nullability, and defaults.
   - Primary key strategy (GUID, int identity, etc.).
   - Foreign keys and cascade behaviour.
3. **Add constraints.** For each table:
   - Unique constraints (natural keys, business identifiers).
   - Check constraints (valid ranges, enum values).
   - Not-null constraints on required fields.
4. **Design indexes.** For each expected query pattern:
   - Which columns are filtered, sorted, or joined on?
   - Composite indexes where needed.
   - Covering indexes for hot queries.
5. **Plan data lifecycle.**
   - Soft delete vs hard delete.
   - Audit columns (`CreatedAt`, `UpdatedAt`, `CreatedBy`).
   - Data retention and archival.
6. **Plan migration strategy.** If modifying existing tables:
   - Can the migration run without downtime?
   - Does column addition need a default value?
   - Does data need to be backfilled?
   - What is the rollback plan?
7. **Check for integrity problems.** Review the design for:
   - Orphaned records (missing cascade or foreign key).
   - Data that can become inconsistent (denormalization without sync).
   - Missing uniqueness constraints that allow duplicates.

## Output Format

### Database Design: [Feature/Domain]

#### Design Decisions

| Decision | Choice | Reason | Rejected (why) |
|----------|--------|--------|----------------|
| Primary keys | Surrogate `int` identity | Small FKs, fast joins | Natural key on `Email` (mutable, and puts PII in every referencing table) |
| Deletes | Soft-delete flag + filtered index | Order history feeds reporting | Hard delete (loses rows the finance report requires) |
| Totals | Denormalised `Order.TotalAmount` | Dashboard reads dominate; per-query recompute failed the latency target | Compute-per-query (correct, but measured too slow for the dashboard) |

Record every decision that had a real alternative; the rejection reasons are what stop the next developer re-litigating it blind.

#### Entity Relationship Summary
```
Order (1) --> (*) OrderItem
Order (*) --> (1) Customer
OrderItem (*) --> (1) Product
```

#### Tables

##### `Orders`
| Column | Type | Nullable | Default | Notes |
|--------|------|----------|---------|-------|
| `Id` | `uniqueidentifier` | No | `NEWSEQUENTIALID()` | PK |
| `CustomerId` | `uniqueidentifier` | No | — | FK -> Customers.Id |
| `Status` | `nvarchar(20)` | No | `'Pending'` | Check: Pending, Confirmed, Shipped, Cancelled |
| `CreatedAt` | `datetimeoffset` | No | `SYSDATETIMEOFFSET()` | — |
| `UpdatedAt` | `datetimeoffset` | No | `SYSDATETIMEOFFSET()` | — |

**Indexes:**
- `IX_Orders_CustomerId` on `CustomerId` (common lookup)
- `IX_Orders_Status_CreatedAt` on `Status, CreatedAt` (filtered listing)

**Constraints:**
- FK `CustomerId` -> `Customers.Id` (ON DELETE RESTRICT)
- CHECK `Status IN ('Pending', 'Confirmed', 'Shipped', 'Cancelled')`

[Repeat for each table]

#### Migration Notes
- [Downtime requirements]
- [Data backfill steps]
- [Rollback plan]

For zero-downtime schema changes (default add, FK ordering, expand/contract renames, backfill, deprecation, rollback validation), see [templates/migration-strategy.md](templates/migration-strategy.md).

#### Integrity Checklist
- [ ] All foreign keys have explicit cascade/restrict behaviour
- [ ] No nullable columns that should be required
- [ ] Unique constraints on natural keys
- [ ] Indexes exist for all common query patterns
- [ ] Audit columns present where needed

## Quality Bar
- Every table has a defined primary key strategy.
- Foreign keys have explicit cascade behaviour (not framework defaults).
- Indexes are justified by expected query patterns.
- Nullable vs not-null decisions are intentional, not accidental.
- Migration strategy addresses downtime and rollback.
- Data integrity constraints are defined, not deferred.

## Failure Modes To Avoid
- Designing tables without knowing the query patterns.
- Using nullable columns as a default to avoid thinking about requirements.
- Forgetting cascade behaviour and relying on ORM defaults.
- Creating indexes for every column instead of for actual query patterns.
- Ignoring migration safety (locking tables, missing defaults on new columns).
- Denormalizing without a plan to keep data in sync.
- Using `VARCHAR(MAX)` / `NVARCHAR(MAX)` when a reasonable limit exists.
