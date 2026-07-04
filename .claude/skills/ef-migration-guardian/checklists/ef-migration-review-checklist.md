# EF Migration Review Checklist

Use this checklist when reviewing a generated EF Core migration before applying it.

## Model Change Detection

- [ ] Compared entity classes against the model snapshot
- [ ] Checked `OnModelCreating` / Fluent API for configuration changes
- [ ] Checked for new, removed, or renamed entities
- [ ] Checked for column type, nullability, or length changes
- [ ] Checked for relationship or FK changes
- [ ] Checked for index additions or removals

## Migration File Review

- [ ] Read the `Up()` method completely
- [ ] Read the `Down()` method completely
- [ ] No `DropTable` without explicit approval
- [ ] No `DropColumn` without data backup plan
- [ ] No `AlterColumn` that narrows types without data validation
- [ ] Renames use `RenameColumn`/`RenameTable`, not drop-and-add
- [ ] Cascade delete behaviour is intentional
- [ ] Index changes do not break existing query patterns

## SQL Script Review

- [ ] Generated SQL script via `dotnet ef migrations script --idempotent`
- [ ] SQL is reviewable and matches expected changes
- [ ] No unexpected `DROP` or `ALTER` statements
- [ ] Idempotent script can be re-run safely

## Application Safety

- [ ] Migration does not auto-apply to shared databases
- [ ] Migration has been tested on a local or disposable database first
- [ ] `Down()` method actually reverses the `Up()` method correctly
- [ ] Application code handles both old and new schema during rollout (if applicable)

## Final Check

- [ ] Risk level assessed (Low / Medium / High / Critical)
- [ ] Destructive operations documented and approved
- [ ] Rollback plan exists
