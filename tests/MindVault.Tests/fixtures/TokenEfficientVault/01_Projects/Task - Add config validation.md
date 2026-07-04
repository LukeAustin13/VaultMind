---
type: task
status: open
created: 2026-05-20
updated: 2026-06-24
project: Tokenproj
tags:
  - task
  - config
links:
  - "[[Tokenproj]]"
  - "[[Decision - Use flat config]]"
---

# Task - Add config validation

Implement the ConfigValidator that checks every flat config key against the schema at load time.

## Scope

- Schema check in ConfigValidator before the service starts
- Fail-fast validation errors with the offending key name
- Wire into the config pipeline load path

## Acceptance

- Startup fails with a clear message on any unknown or mistyped key
