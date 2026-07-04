# Zero-Downtime Migration Strategy

A template for evolving a schema while the application stays online. Examples use SQL Server and T-SQL — adapt to your engine. The core rule: every schema change must be safe with both the old and new application versions running at the same time.

## Why Zero-Downtime

During a rolling deploy, old and new code run against the same database for a window of time. A migration is safe only if:

- The old code keeps working after the migration runs.
- The new code keeps working before all old instances are gone.

This forces most changes into two or three deploys instead of one.

## Adding A Column With A Default

Adding a non-nullable column with a default can lock the table while every row is rewritten, depending on the engine. Split it into safe steps.

1. Add the column as **nullable** with no default.
   ```sql
   ALTER TABLE Orders ADD DiscountCode nvarchar(20) NULL;
   ```
2. Deploy code that **writes** the column but does not yet require it on read.
3. Backfill existing rows in batches (see Backfill Strategy).
4. Once backfilled, add the default and/or the NOT NULL constraint.
   ```sql
   ALTER TABLE Orders ADD CONSTRAINT DF_Orders_DiscountCode DEFAULT '' FOR DiscountCode;
   ALTER TABLE Orders ALTER COLUMN DiscountCode nvarchar(20) NOT NULL;
   ```

Adding the constraint after backfill avoids a long blocking rewrite on a populated table.

## Deploying An FK Constraint In The Right Order

A foreign key cannot point at rows that do not yet exist, and old code may still write rows that violate it.

1. Add the referencing column as nullable.
   ```sql
   ALTER TABLE OrderItems ADD ProductId uniqueidentifier NULL;
   ```
2. Deploy code that populates the column for new rows.
3. Backfill the column for existing rows; verify there are no orphans.
   ```sql
   SELECT COUNT(*) FROM OrderItems oi
   LEFT JOIN Products p ON p.Id = oi.ProductId
   WHERE oi.ProductId IS NOT NULL AND p.Id IS NULL;
   -- must return 0 before adding the constraint
   ```
4. Add the FK with `WITH NOCHECK` first to avoid validating all rows under lock, then validate.
   ```sql
   ALTER TABLE OrderItems WITH NOCHECK
     ADD CONSTRAINT FK_OrderItems_Products FOREIGN KEY (ProductId) REFERENCES Products(Id);
   ALTER TABLE OrderItems WITH CHECK CHECK CONSTRAINT FK_OrderItems_Products;
   ```
5. Once all data is valid and code requires it, tighten the column to NOT NULL.

Always create the parent table/rows and the index on the referencing column before the constraint.

## Expand / Contract For Renames

Never rename a column or table in one step — old code still references the old name. Use expand/contract (also called parallel change).

**Expand** (deploy 1):
- Add the new column alongside the old one.
  ```sql
  ALTER TABLE Customers ADD FullName nvarchar(200) NULL;
  ```
- Deploy code that writes **both** the old and new columns and reads the old one.

**Migrate** (deploy 2):
- Backfill the new column from the old.
- Deploy code that reads the **new** column and still writes both.

**Contract** (deploy 3):
- Stop writing the old column.
- After the deprecation period, drop the old column.
  ```sql
  ALTER TABLE Customers DROP COLUMN Name;
  ```

The same three phases apply to renaming a table (add new table, dual-write, switch reads, drop old) and to changing a column type (add new column of the new type, backfill, switch, drop).

## Backfill Strategy

A single `UPDATE` over a large table holds locks and bloats the transaction log. Backfill in bounded batches.

```sql
DECLARE @BatchSize int = 5000;
WHILE 1 = 1
BEGIN
    UPDATE TOP (@BatchSize) Customers
    SET FullName = Name
    WHERE FullName IS NULL;

    IF @@ROWCOUNT < @BatchSize BREAK;
    WAITFOR DELAY '00:00:01';  -- let other transactions through
END
```

Guidelines:

- Make the backfill **idempotent** — safe to re-run if it fails midway (filter on `WHERE FullName IS NULL`).
- Keep batches small enough to avoid lock escalation; tune size to row width.
- Run during lower-traffic windows for very large tables.
- Backfill is a data operation, not a schema operation — run it between deploys, not inside the migration that adds the column.

## Deprecation Period

Old columns, tables, and code paths are removed only after a deliberate wait, not in the same release that replaces them.

- Keep the deprecated object in place across at least one full deploy cycle so every old instance is gone.
- Mark deprecated objects clearly (naming, extended properties, or a tracked ticket) so they are not mistaken for live schema.
- Confirm via logs or query stats that the old column/path has zero reads and writes before dropping it.
- Record the planned removal date and the deploy that introduced the replacement.

## Rollback Validation Checklist

Before running the migration, confirm each item. A migration is not ready if any answer is no.

- [ ] The migration runs without taking a long blocking lock on a populated table.
- [ ] Old application code still works after the migration is applied (no removed/renamed columns it depends on).
- [ ] New application code still works before the migration is applied (additive-only for this deploy).
- [ ] The backfill is idempotent and batched.
- [ ] Orphan/integrity check returns zero before any FK or NOT NULL constraint is added.
- [ ] A reverse migration exists, or the change is provably forward-safe and the rollback is "deploy old code only".
- [ ] The rollback was tested against a copy of production-shaped data, not just an empty schema.
- [ ] A backup or point-in-time restore target exists for any irreversible step (e.g. a column drop).
- [ ] Drops of old objects are scheduled for a later deploy, never the same one that adds the replacement.
- [ ] Monitoring is in place to detect errors from the old/new code overlap window.

## Sequencing Summary

| Change | Deploys | Rollback |
|--------|---------|----------|
| Add nullable column | 1 | Drop column (or leave; harmless) |
| Add column, default, NOT NULL | 2+ (add → backfill → constrain) | Drop constraint, then column |
| Add FK constraint | 2+ (column → backfill → constraint) | Drop constraint |
| Rename column/table | 3 (expand → migrate → contract) | Stop at current phase; old name still present until contract |
| Change column type | 3 (add new → backfill → switch → drop) | Old column present until contract |
| Drop a column | 1, after deprecation period | Restore from backup (irreversible) |
