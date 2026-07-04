---
type: decision
status: rejected
created: 2026-05-01
updated: 2026-05-20
project: Tokenproj
tags:
  - decision
links:
  - "[[Tokenproj]]"
---

# Decision - Cache everything

Rejected: caching parsed config indefinitely breaks live reload expectations.

## Decision

Do not cache; re-validate on file change events instead.
