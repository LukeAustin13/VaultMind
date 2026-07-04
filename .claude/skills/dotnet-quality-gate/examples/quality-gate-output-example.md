# Quality Gate Output Example

### Quality Gate: OrderService.sln

**Date:** 2026-04-25
**Result:** FAIL

| Step | Command | Result | Duration |
|------|---------|--------|----------|
| Restore | `dotnet restore` | Pass | 3s |
| Format | `dotnet format --verify-no-changes` | Pass | 2s |
| Build | `dotnet build --no-restore` | Pass | 8s |
| Test | `dotnet test --no-build --filter "FullyQualifiedName~OrderService"` | Fail (12 passed, 1 failed) | 5s |

#### Failures

| # | Step | Error | Likely Root Cause | Suggested Fix |
|---|------|-------|-------------------|---------------|
| 1 | Test | `OrderExportTests.Should_Export_Empty_Order` — Assert.NotNull failed | `ExportOrder` returns null when order has no line items instead of an empty CSV | Add empty-collection guard in `CsvExporter.Export()` at line 34 |

#### Test Filter Used

- **Filter:** `FullyQualifiedName~OrderService`
- **Reason:** Only `OrderService` project files were changed

#### Next Action

- Fix null return in `CsvExporter.Export()` for empty orders, then re-run the quality gate.
