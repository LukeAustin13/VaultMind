---
name: refactorer
description: Use this skill when you need to improve existing code without changing its external behaviour. The refactorer reduces duplication, improves naming, simplifies control flow, extracts or inlines methods, and cleans up structure — all while preserving existing functionality. It does not add features, fix bugs, or redesign architecture. If behaviour needs to change, that is a different skill's job.
license: MIT
metadata:
  stack: agnostic
  version: 2.0
  last-reviewed: 2026-07-03
---

# Refactorer

## Use When
- Code works but is hard to read, maintain, or extend.
- The user asks to "clean this up" or "simplify this".
- You spot duplication, overly complex control flow, or poor naming during another task.
- A file has grown too large and needs decomposition.
- Preparatory refactoring is needed before a feature can be added cleanly.

## Do Not Use When
- The code has a bug — fix the bug first with **bug-hunter**, then refactor.
- The user wants new functionality — that is implementation work.
- The code needs a full architectural redesign — use **backend-architect** or **planner**.
- You are reviewing code for someone else — use **code-reviewer**.

## Inputs To Look For
- The file(s) to refactor.
- Tests that cover the code (critical — refactoring without tests is risky).
- The reason for refactoring (readability, preparation for a feature, reducing duplication).
- Project conventions for naming, structure, and patterns.

## Tool-Use Rules

These are the mechanical disciplines that make refactoring safe for an agent. Each is checkable.

- **Baseline green first.** Run the covering tests BEFORE the first change and record the result. Without a recorded green baseline, a later red cannot be attributed to your change — you might be debugging a pre-existing failure.
- **Grep before rename.** Before renaming anything, search for every usage of the identifier — including string literals, config files, reflection, and serialized names, not just compiler-visible references. Never rename from memory of where it is used.
- **One move, one verify.** After each refactoring move, re-run the affected test file (fast loop). Full suite once at the end. Do not batch five moves and hope.
- **Revert the increment, not the session.** If tests break after a move, revert that single move and try a different approach — never a repo-wide reset to recover from one bad edit.
- **Diff check before handoff.** Read the final `git diff` and confirm every hunk maps to a move in your change list. A hunk with no corresponding move is scope creep or an accident — remove it.

## Process
1. **Read the code thoroughly.** Understand what it does before changing anything.
2. **Identify existing test coverage.** If there are no tests for this code, flag this risk to the user before proceeding. Suggest writing tests first.
3. **List what you will change.** Be explicit about each refactoring move:
   - Rename (what, from, to)
   - Extract method/class (what, where)
   - Inline (what)
   - Simplify conditional (where)
   - Remove duplication (where, how)
   - Move (what, from, to)
4. **Make changes incrementally.** Each change should be small enough to verify independently. Do not combine multiple refactoring moves into one large edit.
5. **Preserve external behaviour.** After each change, the code must do exactly what it did before. No new features, no changed APIs, no altered return values.
6. **Verify.** Run the affected tests after each move (per the tool-use rules) and the full covering suite at the end, comparing against the recorded baseline. If tests break, the refactoring introduced a behaviour change — revert that move and try a different approach.

See [examples/refactoring-examples.md](examples/refactoring-examples.md) for two worked before/after refactorings, each with the green test step that proves behaviour is unchanged.

## Output Format

### Refactoring: [File or area]

**Reason:** [Why this refactoring is being done]

**Test Coverage:** [Exists / Partial / None — risk level]

#### Changes

| # | Type | Location | Before | After | Reason |
|---|------|----------|--------|-------|--------|
| 1 | Rename | `File.cs:15` | `proc` | `ProcessOrder` | Clarity |
| 2 | Extract method | `File.cs:40-65` | inline block | `ValidateInput()` | Single responsibility |
| 3 | Remove duplication | `File.cs:80,120` | repeated null check | shared guard clause | DRY |

#### Behaviour Preserved
- [ ] Baseline before changes: [command + result, e.g. `dotnet test --filter OrderTests` — 34 passed]
- [ ] Same suite after changes: [command + result — same counts]
- [ ] No public API signatures changed
- [ ] No return values altered
- [ ] No side effects added or removed
- [ ] Final diff maps 1:1 to the change list — no stray hunks

## Quality Bar
- Every change is listed with a clear reason.
- External behaviour is unchanged — tests still pass.
- Names are improved, not just different.
- Complexity is reduced, not moved.
- No unnecessary abstractions introduced.
- Changes are small enough to review individually.

## Failure Modes To Avoid
- Refactoring without tests and hoping for the best.
- Skipping the green baseline, then wasting the session debugging a failure that predates your changes.
- Renaming from memory and missing the usage in a config string or reflection call.
- Renaming things to subjectively "better" names that are not actually clearer.
- Extracting tiny methods that make the code harder to follow.
- Introducing abstractions for a single use case.
- Changing behaviour while claiming to refactor.
- Refactoring code that is not part of the current task.
- Making the code "more elegant" at the cost of readability.

## Related Skills

- **principles-reviewer** — to identify what is worth refactoring before starting; its findings are this skill's natural input.
- **test-writer** — when step 2 finds no coverage; write the safety net before restructuring.
- **dotnet-quality-gate** — to verify behaviour is preserved after each increment (.NET projects).

## Coding Standards Reference

For C# projects, apply the conventions in [docs/csharp-coding-standards.md](../../../docs/csharp-coding-standards.md) when renaming identifiers, restructuring declarations, or applying modern language features.
