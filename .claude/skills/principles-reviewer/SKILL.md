---
name: principles-reviewer
description: Use this skill when you need to review code specifically against software engineering principles — SOLID, DRY, KISS, YAGNI, Law of Demeter, and related fundamentals. The principles-reviewer focuses exclusively on structural and philosophical code quality. It does not check correctness, security, or performance — use code-reviewer, security-reviewer, or performance-profiler for those.
license: MIT
metadata:
  stack: agnostic
  version: 1.1
  last-reviewed: 2026-06-29
---

# Principles Reviewer

## Use When
- The user asks "is this over-engineered?", "does this follow SOLID?", "is this too complex?"
- A completed code-reviewer pass flagged structural concerns for follow-up (its step 13 routes here), or the user names a principle explicitly.
- A code review surfaces concerns about design quality but not correctness or bugs.
- Refactoring is planned and you need to identify what principles are being violated first.
- The user asks about DRY, KISS, YAGNI, SOLID, or similar principles by name.

## Do Not Use When
- The code has bugs or incorrect logic — use **bug-hunter** or **pr-correctness-reviewer**.
- The concern is security or injection risk — use **security-reviewer**.
- The concern is performance or query efficiency — use **performance-profiler**.
- The code needs to be restructured, not just analysed — use **refactorer** after this review.

## Inputs To Look For
- The code to review (file paths, snippets, or diffs).
- The purpose of the code — what problem it solves.
- Project context — what patterns are already established.
- Any specific principles the user wants to check against.

## Principles Reference

### SOLID
- **SRP — Single Responsibility Principle:** A class or method should have one reason to change. Look for classes that do too many things, methods that mix concerns, or files that grow without bound.
- **OCP — Open/Closed Principle:** Code should be open for extension, closed for modification. Look for switch/if chains that must be edited to add new cases instead of extending through abstraction.
- **LSP — Liskov Substitution Principle:** Subtypes must be substitutable for their base types. Look for overrides that throw `NotImplementedException`, preconditions strengthened in subclasses, or subclasses that break expected behaviour.
- **ISP — Interface Segregation Principle:** Clients should not depend on methods they do not use. Look for fat interfaces where implementers leave methods empty or throw.
- **DIP — Dependency Inversion Principle:** Depend on abstractions, not concretions. Look for `new` inside constructors or methods where a dependency should be injected, or hardcoded service resolution.

### DRY — Don't Repeat Yourself
- Duplicated logic across methods, classes, or files.
- Copy-paste code with minor variations that could be parameterised.
- Magic numbers or strings repeated across multiple locations.
- Parallel class hierarchies that mirror each other without abstraction.

### KISS — Keep It Simple, Stupid
- Unnecessary abstraction layers that add indirection without value.
- Generic solutions to specific problems (premature generalisation).
- Complex control flow where a simpler alternative exists.
- Patterns applied for their own sake rather than because the problem demands them.

### YAGNI — You Aren't Gonna Need It
- Methods, parameters, or overloads that are never called.
- Configuration for scenarios that do not exist yet.
- Interfaces with a single implementation and no planned extension.
- Dead code paths or unreachable branches.

### Law of Demeter — Principle of Least Knowledge
- Chained calls: `order.Customer.Address.City` instead of asking the order directly.
- Tell-don't-ask violations: retrieving state to make a decision outside the object that owns it.
- Methods that know too much about collaborators' internals.

### Composition Over Inheritance
- Deep inheritance chains where composition would be simpler and more flexible.
- Base classes used primarily for code sharing rather than expressing a true "is-a" relationship.
- Subclasses that override many methods to change behaviour rather than plugging in behaviour.

### Single Level of Abstraction
- Methods that mix high-level orchestration with low-level implementation detail.
- Code that alternates between business logic and infrastructure concerns in the same block.

### Naming and Intent
- Names that describe implementation rather than intent (`data`, `temp`, `helper`, `manager`).
- Method names that do not match what the method actually does.
- Boolean parameters that require the caller to know what `true` and `false` mean.

## Process
1. **Read the full file or diff.** Understand what the code is doing before evaluating how it does it.
2. **Identify the purpose.** What problem is this code solving? This determines what complexity is justified.
3. **Scope the relevant principle families first.** Determine which apply to this code's size and shape — SOLID (multiple classes/responsibilities), DRY (genuinely repeated code), KISS (deep control flow or abstraction), YAGNI (unused or speculative code), Law of Demeter (chained calls). Skip families that do not apply and say so, then apply each relevant principle, focusing on violations that matter for this code.
4. **Assess severity.** Is the violation theoretical or does it cause real problems today?
5. **Identify the root pattern.** Group related violations — often one decision causes multiple symptoms.
6. **Suggest concrete improvements.** Every finding must have a specific, actionable suggestion, not just a label.
7. **Note what is done well.** If the code applies principles correctly in non-obvious ways, say so.

For worked findings — an SRP Major, a DRY Minor, a YAGNI Nit, and a case where an apparent violation is justified pragmatism and is not flagged — see [examples/principles-review-examples.md](examples/principles-review-examples.md).

## Output Format

### Principles Review: [File or component name]

**Overall Assessment:** [One sentence: well-structured, minor violations, significant violations, or needs redesign]

#### Findings

| # | Principle | Severity | Location | Violation | Suggested Fix |
|---|-----------|----------|----------|-----------|---------------|
| 1 | SRP | Major | `OrderService.cs:42` | Class handles both order validation and email sending | Extract `OrderNotificationService` |
| 2 | DRY | Minor | `CustomerValidator.cs:18,47` | Same email format check duplicated | Extract `IsValidEmail()` method |
| 3 | YAGNI | Nit | `IRepository.cs:12` | `BulkDelete` method has no callers | Remove until needed |

**Severity definitions:**
- **Major:** The violation causes real design problems today — harder to test, extend, or understand.
- **Minor:** The violation is a real problem but low-impact in the current scope.
- **Nit:** A theoretical violation with minimal practical impact.

#### Positive Notes
- [Principles applied well, design decisions worth keeping]

#### Root Patterns
- [If multiple findings share a common root cause, call it out here]

## Quality Bar
- Every finding cites a specific location and a concrete fix.
- Every Major finding survived an attempt to justify the design as pragmatic before being reported (see the unflagged example in examples/ for what justified pragmatism looks like); findings that survive are labelled CONFIRMED.
- Findings are focused on real problems, not theoretical purism.
- The review distinguishes between "violates a principle" and "causes a real problem".
- YAGNI findings are only raised when the code is genuinely unused or hypothetical.
- For files under ~100 lines or single-method changes, YAGNI and Law-of-Demeter findings are raised only when egregious.
- The review acknowledges good decisions alongside problems.

## Failure Modes To Avoid
- Demanding every class implement a formal interface regardless of context.
- Flagging every use of `new` as a DIP violation without checking if injection is warranted.
- Labelling pragmatic simplicity as a KISS violation.
- Raising YAGNI on code that clearly serves a present need.
- Penalising the use of inheritance in truly "is-a" relationships.
- Generating a list of principle names without explaining what the violation actually is in context.
- Reviewing principles in isolation — consider how a fix to one might affect others.
