# CI Root Cause Report Example

### CI Triage: build-and-test.yml

**Run:** #247
**Failing Step:** `Run tests`
**Failure Category:** Code
**Root Cause Confidence:** High

#### Error

```
Failed OrderService.Tests.OrderExportTests.Should_Handle_Unicode_Characters
  System.Text.EncoderFallbackException: Unable to translate Unicode character \uD83D at index 12
  at CsvExporter.Export(IEnumerable<Order> orders) in CsvExporter.cs:line 47
```

#### Analysis

| Field | Value |
|-------|-------|
| First failing step | Run tests (step 4 of 6) |
| Error type | Test failure — unhandled exception |
| Root cause | `CsvExporter` uses ASCII encoding, fails on Unicode characters in order descriptions |
| Introduced by | Commit `a3f8c12` — added Unicode test data to test fixtures |
| Affects | Test step only. Build, format, and restore passed. Deploy step was skipped due to test failure. |

#### Minimal Fix

```csharp
// CsvExporter.cs line 45
// Before:
var writer = new StreamWriter(stream, Encoding.ASCII);

// After:
var writer = new StreamWriter(stream, Encoding.UTF8);
```

**Risk:** Low — UTF-8 is a superset of ASCII. Existing CSV consumers should handle UTF-8. Verify with downstream consumers if any explicitly expect ASCII.

#### Verification

```bash
dotnet test --filter "FullyQualifiedName~OrderExportTests" --no-build
```

#### Additional Notes

- The test data was changed to include emoji characters in `a3f8c12`. The underlying bug (ASCII encoding) existed before this commit but was never triggered by test data.
- Consider adding a test specifically for encoding edge cases.
