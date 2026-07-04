# Bug Diagnosis Examples

Two worked diagnoses showing the bug-hunter output format end to end. The first is a simple null-reference case; the second is a timezone-filter case where the symptom is far from the cause.

---

### Bug Diagnosis: Profile Page Crashes On Load

Symptom: `GET /users/42/profile` throws `NullReferenceException` when rendering the avatar, but only for some users.

Root Cause: `User.Avatar` is null for users who never uploaded an image. The view calls `user.Avatar.Url` directly with no null check, so any user without an avatar crashes the render. The error surfaces in the view, but the cause is an unguarded optional relationship loaded from the database.

#### Hypotheses

| # | Hypothesis | Likelihood | Evidence For | Evidence Against | Status |
|---|-----------|------------|-------------|-----------------|--------|
| 1 | `user.Avatar` is null for users with no uploaded image | High | Crash only affects some users; `Avatar` is a nullable navigation property; stack trace points at `Avatar.Url` | None | Confirmed |
| 2 | The whole `User` object is null (bad id / failed lookup) | Medium | A null user would also throw here | Other fields on the same `user` render fine on the preceding lines | Eliminated |
| 3 | Lazy-loading is disabled so `Avatar` is never populated | Low | Would explain a null navigation property | Users *with* avatars render correctly, so loading works | Eliminated |

#### Evidence Collected
- `ProfileView.cshtml:31` — `<img src="@user.Avatar.Url">` dereferences `Avatar` with no null guard.
- Stack trace — `NullReferenceException` originates at `ProfileView` line 31, not in the controller or data layer.
- DB query — users who crash have `AvatarId = NULL`; users who render have a populated `AvatarId`.

#### Recommended Fix
```csharp
// Before
<img src="@user.Avatar.Url">

// After
<img src="@(user.Avatar?.Url ?? "/images/default-avatar.png")">
```
**Why this fixes it:** It supplies a fallback when `Avatar` is null, so users without an uploaded image render the default image instead of dereferencing null.

#### Defense In Depth
- Entry point: keep the controller returning the user as-is; do not fabricate an `Avatar`.
- Boundary guard (view): null-coalesce at every `Avatar` dereference, not just line 31.
- Business logic: consider a non-null `AvatarUrl` computed property on the view model so views never touch the nullable navigation property at all — that removes the whole class of "forgot the null check" bug.

#### Verification
- [ ] Load `/users/42/profile` for a user with `AvatarId = NULL`; the page renders with the default image.
- [ ] Regression test `Profile_Renders_DefaultAvatar_When_User_Has_No_Avatar` added.

---

### Bug Diagnosis: Orders List Returns Empty

Symptom: GET /api/orders returns empty array even though 100 orders exist.

Root Cause: query filters order.CreatedAt < cutoff before timezone conversion; cutoff is UTC, CreatedAt stored local, so all rows look older than expected.

#### Hypotheses

| # | Hypothesis | Likelihood | Evidence For | Evidence Against | Status |
|---|-----------|------------|-------------|-----------------|--------|
| 1 | The date filter compares a UTC cutoff against local-time `CreatedAt`, so every row falls on the wrong side of the cutoff | High | 100 rows exist but 0 returned; the only filter is the date comparison; `CreatedAt` is stored in local time while `cutoff` is `DateTime.UtcNow`-derived | None — removing the filter returns all 100 rows | Confirmed |
| 2 | The query targets the wrong table or a tenant filter excludes everything | Medium | An empty result is consistent with an over-narrow filter | Removing only the date clause returns all 100 rows, so the table and tenant scope are correct | Eliminated |
| 3 | Authorisation strips the rows after the query runs | Low | Could explain an empty payload despite data existing | Logging shows the query itself returns 0 rows before any auth projection | Eliminated |

#### Evidence Collected
- `OrderRepository.cs:58` — `.Where(o => o.CreatedAt < cutoff)` where `cutoff = DateTime.UtcNow.AddDays(-30)`.
- DB inspection — `Orders.CreatedAt` values are stored in local time (UTC+10), so a UTC cutoff is ~10 hours ahead of the intended boundary, pushing every recent row out of range.
- Query log — removing the `CreatedAt` clause returns all 100 rows, isolating the filter as the cause.

#### Recommended Fix
```csharp
// Before
var cutoff = DateTime.UtcNow.AddDays(-30);
var orders = db.Orders.Where(o => o.CreatedAt < cutoff);

// After
var cutoff = DateTime.UtcNow.AddDays(-30);
var orders = db.Orders.Where(o => o.CreatedAt.ToUniversalTime() < cutoff);
```
**Why this fixes it:** It normalises `CreatedAt` to UTC before comparing, so both sides of the comparison are in the same timezone and the cutoff boundary is correct.

#### Defense In Depth
- Entry point: store and persist `CreatedAt` as UTC (`DateTimeKind.Utc`) so no row ever carries an ambiguous local time.
- Business logic: compare only UTC-to-UTC; never mix a `UtcNow`-derived value with a local timestamp in the same expression.
- Boundary guard: add an analyzer or code-review rule that flags `DateTime` comparisons where one side is UTC-derived and the other is not, so the mismatch cannot return through another query.

#### Verification
- [ ] Seed 100 orders dated within the last 30 days (local time) and confirm GET /api/orders returns all 100.
- [ ] Regression test `Orders_Returns_RecentRows_When_CreatedAt_Stored_In_Local_Time` added.
