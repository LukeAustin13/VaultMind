# Code Reviewer — Worked Examples

These examples show the Output Format from `SKILL.md` applied to a real review. Data is generic.

## Code Review: OrderService.cs [EXAMPLE]

**Overall Assessment:** Request changes — one Critical null-deref must be fixed before merge.

#### Issues

| # | Severity | Location | Evidence | Issue | Suggested Fix |
|---|----------|----------|----------|-------|---------------|
| 1 | Critical | `OrderService.cs:42` | `orders` passed unfiltered from the caller at line 18; no guard before `.First()` | Null reference when the collection is empty | Add a null/empty check, or use `FirstOrDefault()` and handle the null case |
| 2 | Major    | `OrderService.cs:88` | `GetTotalAsync(id).Result` blocks the calling thread on an async call | Sync-over-async risks thread-pool starvation and deadlock under load | Make the method async and `await GetTotalAsync(id)` |
| 3 | Minor    | `OrderService.cs:60` | Literal `"PENDING"` appears at lines 12, 34, and 60 | Magic string duplicated across the file | Extract a `const string PendingStatus` or an `OrderStatus` enum value |
| 4 | Nit      | `OrderService.cs:5`  | Field declared as `private List<Order> o;` | Single-letter field name obscures intent | Rename `o` to `orders` |

The Evidence column states the concrete code fact that triggered the finding — verifiable, not asserted.

**Severity definitions:**
- **Critical:** Bug, security flaw, or data loss risk. Must fix.
- **Major:** Likely to cause problems. Should fix before merge.
- **Minor:** Improvement to readability, maintainability, or convention adherence.
- **Nit:** Style preference. Optional.

#### Positive Notes
- Method names (`PlaceOrder`, `CancelOrder`) clearly describe intent.
- Input validation on `customerId` is present at the top of each public method.
- The new code follows the existing constructor-injection pattern used elsewhere in the service layer.
