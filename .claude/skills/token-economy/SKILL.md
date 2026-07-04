---
name: token-economy
description: Use this skill when token or context spend needs to be minimised during a task — "save tokens", "be efficient with context", "this is a huge codebase", "don't blow the context window", "keep it cheap", or any long task at risk of filling context. It applies concrete context-economy tactics — search before read, delegate read-heavy work to sub-agents, progressive disclosure, ranged reads, batching, summarise-don't-dump — and can state a context budget plan. It does NOT compress conversational output style (use **low-noise-mode**) or persist task state across sessions (use **task-continuity**).
license: MIT
metadata:
  stack: agnostic
  version: 1.1
  last-reviewed: 2026-07-03
---

# Token Economy

This skill governs how the agent spends *context*, not how it *talks*. It is a working discipline plus an optional up-front budget plan for large tasks.

## Use When

- The task touches a large codebase, many files, or long documents.
- The user explicitly asks to save tokens, be efficient, or protect the context window.
- A task is long-running and context is filling up.
- You are about to read many files to answer a question that needs only a conclusion.

## Do Not Use When

- The goal is shorter conversational replies / less filler — use **low-noise-mode**.
- The goal is to save and resume state across sessions — use **task-continuity**.
- The task is small and already cheap — applying ceremony here wastes more than it saves.

## Core Tactics

Search-before-read, ranged reads, batched independent calls, and no confirmation re-reads are default harness behaviour — this skill does not restate them. It adds the disciplines the harness does not apply for you:

1. **Delegate read-heavy exploration to sub-agents.** When answering means sweeping many files, dispatch a sub-agent that returns the *conclusion*, not the file dumps. Each sub-agent runs in its own isolated context, so the raw reading never enters the main window. This is the single highest-leverage tactic in this skill.
2. **One task per sub-agent (isolation).** Give each delegated unit a fresh, narrowly-scoped agent rather than piling everything into one long-lived context. Delegated tasks are independent and individually summarised.
3. **Progressive disclosure.** Load reference material only when the step needs it; do not pre-read everything "just in case."
4. **Summarise, don't dump.** Capture the conclusion and the few lines that matter; drop the rest. No large pasted blobs that are never referenced again.
5. **Prune by externalising.** When context grows, write durable findings to a scratch note or memory and continue from the summary. Long intermediate output lives in a file, not the window.

## Delegate Or Inline? — Decision Guide

| Situation | Do |
|---|---|
| Need one known fact from one known file | **Inline** — single ranged read |
| Need to find where/whether something exists across the repo | **Inline Grep/Glob** first |
| Need to read and synthesise across many files | **Delegate** to a sub-agent returning conclusions |
| Need several independent investigations | **Delegate in parallel**, one agent each |
| Output will be large and intermediate | **Externalise** to a file, keep a summary |

## Process

1. **Size the task.** Estimate how much reading it implies. If small, skip the ceremony and just work efficiently.
2. **State a context budget plan** (for large tasks): what to search vs read fully, what to delegate, what to externalise. One short paragraph.
3. **Execute** using the tactics above.
4. **Close with the delegation record** (large tasks only) — name the sub-agent tasks actually dispatched and the files deliberately not read in full. Every named dispatch must correspond to a real Agent tool call in this session; a savings claim with no matching dispatch is fabrication.

## Output Format

For a large task, a short plan before diving in:

> **Context plan:** Locate the auth flow with Grep (not a full read of `Auth/`). Delegate the "how do other services call this" sweep to one sub-agent returning a call-site list. Keep only the conclusion in context. Externalise the migration inventory to a scratch file.

Otherwise the deliverable is the efficient execution itself, optionally closing with one line naming what was delegated: *"Delegated the cross-module sweep to one sub-agent; read only `AuthService.cs` in full."*

## Quality Bar

- Read-heavy, breadth-first work is delegated; the main context holds conclusions, not dumps.
- Reference material is loaded when a step needs it, not pre-read.
- The delegation record names real Agent dispatches — never a prose claim of efficiency with no matching tool call.

## Failure Modes To Avoid

- Reading whole files when a Grep + ranged read would do.
- Pulling large file contents into the main context that a sub-agent could have summarised.
- Re-reading a file just edited or already read to "double check".
- Over-applying the discipline to a tiny task, spending more on planning than the work.
- Confusing this with **low-noise-mode** — terse output does not reduce the context cost of reading; this does.

## Related Skills

- **low-noise-mode** — use instead when the aim is shorter, lower-filler replies rather than lower context spend.
- **task-continuity** — use instead when state must survive across sessions, not be minimised within one.
- **codebase-visualiser** — its Internal Orientation Mode is the efficient way to get the lay of unfamiliar code before editing.
