# Destructive Migration Warning Example

### EF Migration Review: AppDbContext

**Migration Needed:** Yes
**Evidence:** `Customer.LegacyId` column removed from entity class; `Product.Price` changed from `decimal(18,2)` to `decimal(10,4)`
**Risk Level:** High

#### Model Changes Detected

| # | Change | Entity | Detail | Migration Impact |
|---|--------|--------|--------|-----------------|
| 1 | Removed column | Customer | `LegacyId` (int) removed from entity class | `DropColumn` — data loss |
| 2 | Type change | Product | `Price` decimal(18,2) to decimal(10,4) | `AlterColumn` — potential truncation |
| 3 | New column | Order | `ShippingDate` (DateTime?) added | `AddColumn` — safe |

#### Destructive Operations

| # | Operation | Table/Column | Risk | Mitigation |
|---|-----------|-------------|------|------------|
| 1 | DropColumn | Customer.LegacyId | Permanent data loss — 45,000 rows have values | Export `LegacyId` data before applying. Consider keeping as deprecated rather than removing. |
| 2 | AlterColumn | Product.Price | Values exceeding decimal(10,4) will be truncated | Run `SELECT * FROM Products WHERE Price >= 1000000` to check. Current max is 999.99 so this is likely safe, but verify. |

#### Recommended Next Steps

1. Generate SQL script: `dotnet ef migrations script --idempotent -o migration-review.sql`
2. Back up `Customer.LegacyId` data: `SELECT Id, LegacyId FROM Customers WHERE LegacyId IS NOT NULL`
3. Verify no `Product.Price` values will be truncated
4. Apply to local database first and run integration tests
5. Do not apply to staging or production without team review
