# AI-Generated Code Failure Modes

Load this when reviewing code written by an AI (including code you wrote yourself earlier in the session). AI code fails differently from human code: it is syntactically clean, well-commented, confidently named — and wrong in ways that skim-reading never catches. Every check below is a lookup or comparison you can actually perform, not a vibe.

## The core property

AI code optimises for looking correct. Human bugs cluster around carelessness (typos, missed cases); AI bugs cluster around plausibility (an API that should exist but doesn't, logic that matches the common pattern but not this problem). Review accordingly: verify, don't assess fluency.

## Failure modes and how to catch each

### 1. Hallucinated or wrong-version APIs
Methods, parameters, or config keys that don't exist, or exist in a different version of the library than the project uses.
**Catch:** For any API call you don't recognise, check the project's actual dependency version (`.csproj`, `package.json`, lock file) and confirm the member exists — search the codebase for other usages, or check the official docs for that version. A method nobody else in the repo calls is a candidate hallucination.

### 2. Plausible-but-wrong logic
The code implements the *usual* version of the problem, not the *actual* one — e.g. pagination that assumes 0-based pages when the API is 1-based, a date range check that should be inclusive but follows the common exclusive pattern.
**Catch:** Trace one concrete input through the code by hand, chosen from the requirement (not from the code). If the requirement says "orders placed on the last day of the month are included", pick that exact value and walk it through.

### 3. Invented helpers duplicating existing utilities
A fresh `Retry()`, `Slugify()`, or `ToSafeFileName()` when the repo already has one.
**Catch:** For every new private helper or utility, grep the repo for an existing function with a similar name or purpose before accepting it. Duplicates diverge and rot.

### 4. Over-scaffolded abstraction
Interfaces with one implementation, factories creating one type, config options nobody set, generic parameters used at one type — speculative structure the task never asked for.
**Catch:** For each new abstraction, ask what second use exists *today*. If the answer is "might be useful later", flag it: YAGNI violation, route to **principles-reviewer** if there is a pattern of it.

### 5. Silently swallowed errors
`catch (Exception) { }`, `catch { return null; }`, `.catch(() => {})` — added to make the happy path demo work.
**Catch:** Grep the diff for `catch`, `except`, `.catch`, `rescue`. Every handler must either handle meaningfully, log with context, or rethrow. Empty and return-default handlers are blockers unless the swallow is explicitly justified in code.

### 6. Tests that assert nothing
Tests that exercise code but can't fail: asserting the mock returned what the mock was told to return, `Assert.NotNull` on an object that can't be null, snapshot tests that snapshot the bug.
**Catch:** For each new test, ask "what change to the production code would make this fail?" If you can't name one, the test is decoration. Mentally (or actually) invert the key production condition and check the test would go red.

### 7. Stale-pattern security
Password hashing with MD5/SHA1, string-concatenated SQL, disabled certificate validation, JWT `none` acceptance — patterns common in old training data.
**Catch:** Any crypto, SQL construction, TLS setting, or auth decision in AI code gets checked against current guidance, not accepted because it compiles. Route anything non-trivial to **security-reviewer**.

### 8. Comment/behaviour drift
Comments and doc-comments that describe what the code *should* do, copied from the prompt — while the code does something subtly different.
**Catch:** Treat every comment in AI code as a claim to verify, not documentation. Where comment and code disagree, the code is what ships; the disagreement is a finding.

### 9. Convention mismatch
The code is fine in isolation but ignores the repo's established patterns — different error-response shape, a second DI style, raw SQL where the repo uses an ORM.
**Catch:** Open one existing file that does the same kind of work and diff the approaches. New patterns need justification; absent one, conformance is the fix.

### 10. Phantom completeness
The change claims to handle a case it doesn't: a `TODO` where the logic should be, a branch that logs "not implemented" and continues, a switch missing the enum member added in the same PR.
**Catch:** Grep the diff for `TODO`, `FIXME`, `NotImplemented`, `throw new NotSupportedException`. Cross-check any enum/union extended in the diff against every switch/match over it.

## Verdict pressure

When three or more of these fire in one change, the correct verdict is usually RED, not a long AMBER list — the change was generated without understanding the codebase, and patching findings one by one costs more than regenerating with better context.
