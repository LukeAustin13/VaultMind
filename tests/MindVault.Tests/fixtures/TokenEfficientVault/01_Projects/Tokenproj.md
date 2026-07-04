---
type: project
status: active
created: 2026-05-10
updated: 2026-06-25
project: Tokenproj
tags:
  - project
aliases:
  - tp
repoNames:
  - token-proj
links:
  - "[[Decision - Use flat config]]"
---

# Tokenproj

The configuration pipeline rewrite: flat config files, validated at load, no runtime surprises.

## Goal

Ship the config pipeline v2 with validation at load time and zero silent fallbacks.

## Non-Negotiables

- Config errors fail fast at startup, never at request time
- Every config key is documented in the schema note
- No environment-variable overrides outside the allowlist

## Open Questions

- Do we need per-environment config layering at all?

## Architecture

See [[Architecture - Config pipeline]] for the load path.
