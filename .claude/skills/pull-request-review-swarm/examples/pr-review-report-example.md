# PR Review Report Example

### PR Review: Add order export to CSV

**PR:** #142
**Author:** luke
**Intent:** Allow customers to export their order history as a CSV file.
**Merge Recommendation:** Request changes

#### Executive Summary

The feature works for the happy path but has a null reference bug when orders have no line items, a missing test for Unicode characters in order descriptions, and an N+1 query that will be slow for customers with many orders. Two blocking issues, three non-blocking.

#### Blocking Issues

| # | Pass | File:Line | Issue | Severity | Suggested Fix |
|---|------|-----------|-------|----------|---------------|
| 1 | Correctness | `CsvExporter.cs:34` | `orders.First()` throws when collection is empty | Critical | Add empty check: `if (!orders.Any()) return EmptyCsv();` |
| 2 | Correctness | `CsvExporter.cs:47` | ASCII encoding fails on Unicode characters | Major | Change `Encoding.ASCII` to `Encoding.UTF8` |

#### Non-Blocking Issues

| # | Pass | File:Line | Issue | Severity | Suggested Fix |
|---|------|-----------|-------|----------|---------------|
| 1 | Performance | `OrderQueryHandler.cs:18` | N+1: loads line items per order in loop | Medium | Use `.Include(o => o.LineItems)` in initial query |
| 2 | Docs | `README.md` | No mention of export feature in feature list | Low | Add to feature list |
| 3 | Security | `ExportController.cs:12` | Missing `[Authorize]` attribute | Medium | Add `[Authorize]` to controller or action |

#### Test Gaps

- [ ] No test for empty order collection in `CsvExporterTests`
- [ ] No test for Unicode characters in order descriptions
- [ ] No test for large order sets (performance boundary)

#### Security / Config Concerns

- [ ] Export endpoint missing authorisation — any authenticated user could export any customer's orders. Add ownership check.

#### Suggested PR Comment

```
Good feature, two issues to fix before merge:

1. **Bug:** `CsvExporter.cs:34` — `First()` throws on empty orders. Add empty check.
2. **Bug:** `CsvExporter.cs:47` — ASCII encoding breaks on Unicode. Switch to UTF-8.

Also worth addressing:
- N+1 query in `OrderQueryHandler` — use `.Include()` for line items.
- Missing `[Authorize]` on `ExportController`.

Tests to add: empty orders case, Unicode characters.
```
