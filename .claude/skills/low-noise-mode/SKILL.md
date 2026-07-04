---
name: low-noise-mode
description: >
  Use this skill when the user says "be concise", "cut the filler", "just tell me the fix",
  "low-noise mode", or wants compact review, debugging, or planning output. Persistent
  compressed-output mode with an automatic safety override that expands for destructive
  actions and security-critical content. It does not reduce token or context spend from
  reading files or delegating work — use token-economy for that.
license: MIT
metadata:
  stack: agnostic
  version: 1.2
  last-reviewed: 2026-07-03
---

# Low Noise Mode

## Purpose

This skill reduces conversational noise in Claude's responses while preserving technical accuracy, completeness, and safety. It removes filler phrases, unnecessary reassurance, verbose preambles, and over-explained reasoning. It does not remove substance.

The goal is faster, denser communication for experienced developers who want direct answers and minimal overhead.

## Use When

- User asks for shorter or more direct responses.
- User says "use low-noise mode", "low-noise mode", or equivalent.
- User says "be concise", "cut the filler", "just tell me the fix", or equivalent.
- User wants compact code review output with findings, not narrative.
- User wants less explanation and more action — e.g. "just fix it", "show me the command".
- User wants a debugging answer focused on locating and fixing the problem.
- User wants planning output without preamble or summary paragraphs.
- User is experienced in the domain and does not need concepts explained from scratch.

## Do Not Use When

- The task requires teaching-style explanation for a learner.
- User has explicitly asked for a full report or full explanation.
- Ambiguity in the answer could lead to damage — data loss, broken deployments, security gaps.
- The topic is security-critical, legal, financial, medical, or safety-related and needs full context.
- A destructive action is involved — file deletion, database mutation, infrastructure change.
- Exact wording matters — migration scripts, legal copy, configuration values.
- User appears confused, has asked a follow-up correction question, or has contradicted themselves.

## Persistence

- Once activated, stay in low-noise mode for the remainder of the conversation.
- Do not silently drift back into verbose style after a few responses.
- Return to normal mode when the user says "normal mode", "stop low-noise", "verbose", "full explanation", or equivalent.
- Safety override beats persistence. See **Safety Override** below.

## Core Rules

- Remove filler: "Great question!", "Certainly!", "Happy to help!", "Of course!", "Sure thing!".
- Remove unnecessary reassurance: "Don't worry, this is easy", "This should work fine".
- Remove padded preambles: "In this response I will explain...", "To answer your question...".
- Remove trailing summaries that restate what was just written.
- Prefer direct technical language over hedged diplomatic phrasing.
- Preserve all technical substance — the answer must still be correct and complete.
- Keep code blocks, commands, logs, error messages, file paths, API names, class names, method names, config keys, package names, and version numbers exactly as they would appear in normal mode. Never shorten or paraphrase these.
- Do not compress so hard that the answer becomes ambiguous or requires a follow-up to act on.
- Prefer structured output (list, table, labelled block) over vague short prose.

## Output Patterns

Choose the pattern that fits the context. These are not modes the user selects — pick the right one automatically.

### Debugging

```
Symptom: [what is failing]
Cause: [most likely root cause]
Fix: [exact change or command]
Verify: [how to confirm it is fixed]
```

### Planning

```
Goal: [what this achieves]
Steps:
  1. [step]
  2. [step]
Risks: [anything that could go wrong]
First action: [the specific thing to do now]
```

### Code Review

```
Finding: [what the problem is]
Severity: [blocking | non-blocking | suggestion]
Evidence: [file:line or code snippet]
Fix: [exact correction]
```

### Default

For answers that do not fit the above:

```
Answer: [direct answer]
Reason: [one line if not obvious]
Next: [concrete next step]
```

## Safety Override

Low-noise mode must automatically expand to full clarity when any of the following are true:

- The action is destructive: deletes files, drops tables, removes records, overwrites data.
- The command modifies infrastructure, secrets, certificates, or deployments.
- A security warning or vulnerability is involved.
- The response includes a database migration, SQL DDL/DML, or infrastructure-as-code change.
- Exact wording matters and compression could introduce ambiguity.
- The user appears confused or is asking a corrective follow-up.
- Multi-step ordering could be misread if compressed.
- The topic involves legal, financial, medical, or safety-critical considerations.

**Safety override beats concision.**

When safety override activates, give the full clear answer. Optionally prefix with `[Full clarity: reason]` so the user knows why the response expanded. Return to low-noise mode after.

## Never Compress These

Code blocks, terminal commands, error messages, stack traces, file paths, API names, class names, method names, configuration keys, package names, version numbers, security warnings, migration commands, SQL statements, Git commands, deployment commands.

## Examples

### C# Exception Debugging

**Verbose:**
> It looks like the issue might be related to a null reference somewhere in your service layer. You should check whether the `UserRepository` is being properly injected. Let me walk you through what might be happening...

**Low-noise:**
```
Symptom: NullReferenceException at UserService.cs:42
Cause: _repository is null — constructor injection not registered
Fix: services.AddScoped<IUserRepository, UserRepository>(); in Program.cs
Verify: dotnet run, reproduce the call, exception should not recur
```

### PR Review Finding

**Verbose:**
> Looking at this method, I noticed that the exception handling here might be a bit risky. The catch block is swallowing the exception without logging it, which could make debugging very difficult in production...

**Low-noise:**
```
Finding: Exception swallowed silently in PaymentService.ProcessAsync
Severity: blocking
Evidence: PaymentService.cs:87 — catch block with no log or rethrow
Fix: Add _logger.LogError(ex, "..."); before return or rethrow
```

### Destructive Command (safety override)

Even in low-noise mode, a destructive command expands to full clarity:

```
[Full clarity: destructive action]

The following command will permanently delete the Orders table and all its data.
There is no automatic rollback. Confirm a recent backup exists before running.

  DROP TABLE Orders;
```

### Over-Compression (anti-example)

Too terse:
```
Use AsNoTracking.
```

Terse but complete:
```
Finding: Tracked query loaded for read-only list — extra change-tracking overhead
Evidence: OrderService.cs:58 — _db.Orders.Where(o => o.Active).ToList()
Fix: _db.Orders.AsNoTracking().Where(o => o.Active).ToList()
```

The first omits where, why, and what to change, so the reader must still ask — compression that fails its own goal.

## Boundaries

- This skill changes communication style, not technical standards.
- It does not replace code review, testing, security review, or planning skills.
- It does not hide uncertainty. If the answer is uncertain, say so — briefly.
- It does not skip validation. A compressed fix still needs to be tested.

## Related Skills And Agents

- **planner** — pairs well; produces compact planning output.
- **token-economy** — distinct concern: low-noise-mode compresses what Claude *says*; token-economy reduces what Claude *reads* (context spend). Use both together on a large task.
- **code-reviewer** — pairs well; produces compact finding output.
- **bug-hunter** — pairs well; produces focused root-cause output.
- **security-reviewer** — safety override applies; security findings always use full clarity.
- **ci-triage** — pairs well; produces focused failure diagnosis.
- **pr-correctness-reviewer** (agent) — pairs well; formats findings compactly.
- **security-config-reviewer** (agent) — safety override applies; always full clarity.
