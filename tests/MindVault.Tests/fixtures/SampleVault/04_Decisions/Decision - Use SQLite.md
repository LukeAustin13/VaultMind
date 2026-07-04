---
type: decision
status: accepted
created: 2026-02-01
updated: 2026-02-01
project: Alpha
tags:
  - decision
links:
  - "[[Alpha]]"
---

# Decision: Use SQLite

## Context

We need a rebuildable cache that supports full text search.

## Decision

Use SQLite with FTS5.

## Reasoning

Boring, fast, predictable, no server required.

## Consequences

Index can always be rebuilt from Markdown.

## Reversal Conditions

If FTS5 ranking proves insufficient.
