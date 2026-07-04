---
type: architecture
status: active
created: 2026-05-12
updated: 2026-06-18
project: Tokenproj
tags:
  - architecture
  - config
links:
  - "[[Decision - Use flat config]]"
---

# Architecture - Config pipeline

The config pipeline loads flat files, validates them against the schema, and freezes the result before any service code runs.

## Load Path

Files are discovered by convention, parsed once, and merged in a fixed order so the effective value of every key is traceable to exactly one source line.

## Validation Stage

The validator implements [[Decision - Use flat config]]: unknown keys, type mismatches and missing required keys all fail startup with the offending key named in the error.

## Freeze And Publish

After validation the config object is frozen and published through a read-only accessor; nothing can mutate configuration at runtime.

## Appendix - Merge Semantics
Each merge layer records its source file and line so the effective value of any key can be traced during an incident without reproducing the merge in your head. Each merge layer records its source file and line so the effective value of any key can be traced during an incident without reproducing the merge in your head. Each merge layer records its source file and line so the effective value of any key can be traced during an incident without reproducing the merge in your head. Each merge layer records its source file and line so the effective value of any key can be traced during an incident without reproducing the merge in your head. Each merge layer records its source file and line so the effective value of any key can be traced during an incident without reproducing the merge in your head. Each merge layer records its source file and line so the effective value of any key can be traced during an incident without reproducing the merge in your head. Each merge layer records its source file and line so the effective value of any key can be traced during an incident without reproducing the merge in your head. Each merge layer records its source file and line so the effective value of any key can be traced during an incident without reproducing the merge in your head. Each merge layer records its source file and line so the effective value of any key can be traced during an incident without reproducing the merge in your head. Each merge layer records its source file and line so the effective value of any key can be traced during an incident without reproducing the merge in your head. Each merge layer records its source file and line so the effective value of any key can be traced during an incident without reproducing the merge in your head. Each merge layer records its source file and line so the effective value of any key can be traced during an incident without reproducing the merge in your head.
## Appendix - Failure Modes
A malformed file fails the whole load; a missing optional file logs one structured warning; an unknown key names itself in the startup error. A malformed file fails the whole load; a missing optional file logs one structured warning; an unknown key names itself in the startup error. A malformed file fails the whole load; a missing optional file logs one structured warning; an unknown key names itself in the startup error. A malformed file fails the whole load; a missing optional file logs one structured warning; an unknown key names itself in the startup error. A malformed file fails the whole load; a missing optional file logs one structured warning; an unknown key names itself in the startup error. A malformed file fails the whole load; a missing optional file logs one structured warning; an unknown key names itself in the startup error. A malformed file fails the whole load; a missing optional file logs one structured warning; an unknown key names itself in the startup error. A malformed file fails the whole load; a missing optional file logs one structured warning; an unknown key names itself in the startup error. A malformed file fails the whole load; a missing optional file logs one structured warning; an unknown key names itself in the startup error. A malformed file fails the whole load; a missing optional file logs one structured warning; an unknown key names itself in the startup error. A malformed file fails the whole load; a missing optional file logs one structured warning; an unknown key names itself in the startup error. A malformed file fails the whole load; a missing optional file logs one structured warning; an unknown key names itself in the startup error.

