---
name: lessons-learned
description: Use this skill when the AI agent makes a mistake, incorrect assumption, or produces wrong output in this repository. It records what went wrong, why, what was fixed, and how to avoid it next time in AGENT_LESSONS.md at the repo root. Invoke it after any significant correction or at the end of a session where notable mistakes were made.
license: MIT
metadata:
  stack: agnostic
  version: 1.0
  last-reviewed: 2026-05-18
---

# Lessons Learned

## Use When
- The user corrects a significant mistake (wrong output, broken code, incorrect assumption).
- A bug was introduced by the agent and then fixed.
- The agent misread a file, misunderstood a requirement, or produced output that had to be reverted.
- A destructive or hard-to-reverse action was taken incorrectly.
- The user says "that was wrong", "you broke X", "you misunderstood", or similar.
- At the end of a session where at least one notable mistake was made.

## Do Not Use When
- The user is simply asking for a code change — that is not a mistake to log.
- A minor nit or preference was adjusted — only log mistakes with real impact. (A recurring style preference belongs in CLAUDE.md, not here.)
- The mistake is already covered by an existing entry with the same root cause.

## Inputs To Look For
- What the agent did that was wrong (exact action: file edited, code written, assumption made).
- What the correct behaviour should have been.
- The root cause (misread instruction, assumed rather than verified, skipped a file read, etc.).
- What fix was applied.
- A prevention rule that could catch this class of mistake in future.

## Process

1. **Identify the mistake.** One sentence: what did the agent do that was wrong?
2. **Determine root cause.** Why did it happen? Common root causes:
   - Edited a file without reading it first.
   - Assumed a value instead of checking.
   - Misread an instruction or requirement.
   - Applied a pattern from one context incorrectly to another.
   - Skipped a verification step.
   - Made a destructive change without confirming.
3. **Describe the fix.** What was done to correct it?
4. **Write the prevention rule.** A single, actionable rule that applies to this class of mistake — something that can be checked against in future sessions.
5. **Check for duplicates.** Read `AGENT_LESSONS.md`. If an entry already covers the same root cause, add a note to that entry rather than creating a duplicate.
6. **Append the entry.** Add it to `AGENT_LESSONS.md` using the format below.

## Output Format

Append one entry to `AGENT_LESSONS.md` at the repo root:

```markdown
## [YYYY-MM-DD] — [Short title]

**Category:** [Bug introduced / Wrong assumption / Misread instruction / Wrong output / Destructive action / Skipped verification / Other]

**What went wrong:** [One paragraph describing the mistake — what was done and why it was wrong. Quote the exact wrong output, edit, or claim (file:line where applicable) — no reconstructions from memory]

**Root cause:** [One sentence — the underlying reason it happened]

**Fix applied:** [What was done to correct it]

**Prevention rule:** [One actionable rule for avoiding this class of mistake in future]

---
```

## Quality Bar
- Every entry has all five fields filled in — no placeholders.
- The root cause names a specific failure mode, not just "made a mistake".
- The prevention rule is actionable and general enough to apply beyond this exact instance.
- No duplicate entries for the same root cause — update the existing entry instead.
- The entry is dated accurately using today's date.

## Failure Modes To Avoid
- Logging trivial preferences as lessons — only log mistakes with real impact.
- Writing vague prevention rules ("be more careful") — rules must be actionable.
- Creating duplicate entries instead of updating existing ones.
- Skipping the duplicate check step.
- Logging user change-of-mind as an agent mistake.
