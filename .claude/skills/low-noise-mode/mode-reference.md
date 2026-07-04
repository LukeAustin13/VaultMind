# Low Noise Mode — Quick Reference

## On / Off

| Phrase | Effect |
|--------|--------|
| "use low-noise mode" | Activate |
| "low-noise mode" | Activate |
| "be concise" / "cut the filler" | Activate |
| "just tell me the fix" | Activate |
| "normal mode" / "stop low-noise" | Deactivate |
| "verbose" / "full explanation" | Deactivate |

## Output Patterns (context-driven)

Claude picks the pattern automatically based on what is being asked. The four patterns (Debugging, Planning, Code Review, Default) are defined once in [SKILL.md](SKILL.md) § Output Patterns — that copy is canonical.

## Safety Override

Fires automatically — regardless of whether low-noise mode is active — for:

- Destructive operations (file deletion, DROP TABLE, data overwrite)
- Infrastructure, secrets, certificates, deployments
- Security vulnerabilities
- Database migrations, SQL DDL/DML
- Legal, financial, medical, or safety-critical content
- User confusion or corrective follow-ups

Response expands to full clarity. Optionally prefixed: `[Full clarity: reason]`. Returns to low-noise mode after.

**Safety override beats concision.**

## Good vs Bad Compression

**Good** — complete, actionable:
```
Symptom: 401 on POST /api/orders after deploy
Cause: JWT audience mismatch — prod config has wrong Audience value
Fix: Set Audience: "https://api.example.com" in appsettings.Production.json
Verify: POST /api/orders with valid token — expect 200
```

**Bad** — too short, ambiguous:
```
Fix: Add the middleware.
```
No context, no location, not actionable.

**Bad** — skips uncertainty:
```
Cause: Race condition.
Fix: Add lock().
```
If the cause is not confirmed, say so: `Cause: likely race condition (unconfirmed — reproduce under load first)`.

## Examples

Full before/after examples by context:

- [`examples/low-noise-mode/before-after.md`](../../../examples/low-noise-mode/before-after.md) — general before/after pairs
- [`examples/low-noise-mode/code-review-output.md`](../../../examples/low-noise-mode/code-review-output.md) — blocking bugs, maintainability, test gaps, security findings
- [`examples/low-noise-mode/planning-output.md`](../../../examples/low-noise-mode/planning-output.md) — feature plan, bug fix plan, docs update plan
- [`examples/low-noise-mode/bug-triage-output.md`](../../../examples/low-noise-mode/bug-triage-output.md) — build error, failing test, runtime exception, config issue
