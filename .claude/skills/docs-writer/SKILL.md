---
name: docs-writer
description: Use this skill when you need to create documentation such as README files, setup guides, runbooks, or handover notes. The docs-writer produces practical, accurate documentation aimed at developers who need to understand, set up, or maintain the project, with every runnable command executed or explicitly listed as unverified. It does not produce marketing copy or documentation that restates the obvious. It does not plan future work (use planner), design systems (use backend-architect), or map an existing codebase with verified diagrams (use codebase-visualiser).
license: MIT
metadata:
  stack: agnostic
  version: 1.1
  last-reviewed: 2026-07-03
---

# Docs Writer

## Use When
- The user asks for a README, setup guide, or architecture doc.
- A feature or system needs documentation for handover.
- A runbook is needed for deployment, incident response, or maintenance.
- The user asks "document this" or "write up how this works".
- Onboarding docs are needed for a new team member.

## Do Not Use When
- You are creating a plan for future work — use **planner**. (Documenting an already-approved plan for handover is in scope.)
- You are reviewing code — use **code-reviewer**.
- You are designing architecture — use **backend-architect**.
- You are explaining or diagramming an existing codebase's structure — use **codebase-visualiser**.
- The user asks for inline code comments — just add them directly.

## Inputs To Look For
- The code, system, or process to document.
- The target audience (new developer, ops team, end user, future self).
- Existing documentation in the project (to match style and avoid duplication).
- Configuration files, scripts, and setup requirements.
- Known gotchas, prerequisites, or environment requirements.

## Process
1. **Identify the audience.** Who will read this? What do they need to accomplish?
2. **Identify the scope.** What does this document cover? What does it not cover?
3. **Gather facts.** Read the code, config, and existing docs. Do not guess at setup steps — verify them.
4. **Structure the document.** Choose a structure appropriate to the document type:
   - README: What, why, setup, usage, contributing.
   - Setup guide: Prerequisites, step-by-step, verification, troubleshooting.
   - Architecture doc: Overview, components, data flow, decisions, trade-offs.
   - Runbook: When to use, steps, rollback, escalation.
   - Handover: Context, what is done, what is not done, where to find things, known issues.
5. **Write.** Be direct. Use short sentences. Use code blocks for commands and file paths. Use tables for structured data. Use headings for scannability.
6. **Verify accuracy.** Execute every setup and usage command you can in this environment and check the observed result matches what the document claims. Commands you could not run go in a mandatory "Unverified" note at the top of the document, each with the reason it could not be run. An absent Unverified note asserts that everything was executed — do not omit it by default.
7. **Cut the fluff.** Remove any sentence that does not help the reader accomplish their goal.

## Output Format

The format depends on the document type. General rules:

- Start with a one-sentence description of what the document covers.
- Use `##` headings for major sections.
- Use code blocks for all commands, paths, and config examples.
- Use tables for lists of environment variables, ports, services, etc.
- End with a "Known Issues" or "Troubleshooting" section if applicable.

### Example: README Structure

```markdown
# Project Name

One-sentence description.

## Prerequisites
- [Requirement with version]

## Setup
1. [Step with command]
2. [Step with command]

## Usage
[How to run, with examples]

## Configuration
| Variable | Purpose | Default |
|----------|---------|---------|

## Project Structure
[Brief explanation of key directories]

## Troubleshooting
[Common issues and fixes]
```

### Example: Runbook Structure

```markdown
# Runbook: [Task Name]

## When To Use
[Trigger condition]

## Prerequisites
[Access, tools, permissions needed]

## Steps
1. [Step with command]
2. [Step with verification]

## Rollback
[How to undo]

## Escalation
[Who to contact if this fails]
```

## Quality Bar
- Every command in the document works if copy-pasted — proven by running it, or listed in the Unverified note with a reason.
- Paths and configuration values are accurate.
- The document has a clear audience and purpose.
- No filler sentences ("This document aims to provide a comprehensive overview of...").
- Prerequisites are listed before steps that require them.
- The document is scannable — a reader can find what they need in 30 seconds.

## Failure Modes To Avoid
- Writing documentation that restates the code without adding understanding.
- Including setup steps you have not verified.
- Using vague instructions ("configure the database appropriately").
- Writing marketing copy instead of technical documentation.
- Producing a wall of text without headings, code blocks, or structure.
- Documenting internal implementation details that change frequently (these belong in code comments, not docs).
- Forgetting to state prerequisites.
