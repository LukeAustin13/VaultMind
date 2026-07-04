---
type: decision
status: accepted
created: 2026-05-15
updated: 2026-06-20
project: Tokenproj
tags:
  - decision
  - config
links:
  - "[[Tokenproj]]"
  - "[[Task - Add config validation]]"
  - "[[Decision - Use nested config]]"
---

# Decision - Use flat config

We use a single flat config file per service, validated against a schema at load time.

## Context

Nested config trees hid defaults and made overrides untraceable.

## Decision

Flat keys, explicit schema, fail-fast validation. Supersedes [[Decision - Use nested config]].

## Consequences

- Tracked by [[Task - Add config validation]]
- Migration needed for old nested files
