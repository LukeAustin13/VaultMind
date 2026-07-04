---
type: architecture
status: active
created: 2026-05-14
updated: 2026-06-19
project: Tokenproj
tags:
  - architecture
  - storage
links:
  - "[[Tokenproj]]"
---

# Architecture - Storage layout

<!-- mindvault-summary:start -->
summary: How config data is laid out on disk.
agentUse: How the system fits together — read before structural changes.
keyPoints:
- Directory Layout
- File Naming
- Atomicity
source: generated from headings/frontmatter/body
updated: 2026-06-19
<!-- mindvault-summary:end -->

Config data lives in a predictable directory layout so tooling never has to guess where a file belongs.

## Directory Layout

One directory per service, one flat file per environment, schema files alongside the data they constrain.

## File Naming

Names encode service and environment only; version history belongs to git, not to file names.

## Atomicity

Writers stage to a temp file in the same directory and rename into place, so readers never observe a half-written config.

## Appendix - Merge Semantics
Each merge layer records its source file and line so the effective value of any key can be traced during an incident without reproducing the merge in your head. Each merge layer records its source file and line so the effective value of any key can be traced during an incident without reproducing the merge in your head. Each merge layer records its source file and line so the effective value of any key can be traced during an incident without reproducing the merge in your head. Each merge layer records its source file and line so the effective value of any key can be traced during an incident without reproducing the merge in your head. Each merge layer records its source file and line so the effective value of any key can be traced during an incident without reproducing the merge in your head. Each merge layer records its source file and line so the effective value of any key can be traced during an incident without reproducing the merge in your head. Each merge layer records its source file and line so the effective value of any key can be traced during an incident without reproducing the merge in your head. Each merge layer records its source file and line so the effective value of any key can be traced during an incident without reproducing the merge in your head. Each merge layer records its source file and line so the effective value of any key can be traced during an incident without reproducing the merge in your head. Each merge layer records its source file and line so the effective value of any key can be traced during an incident without reproducing the merge in your head. Each merge layer records its source file and line so the effective value of any key can be traced during an incident without reproducing the merge in your head. Each merge layer records its source file and line so the effective value of any key can be traced during an incident without reproducing the merge in your head.
## Appendix - Failure Modes
A malformed file fails the whole load; a missing optional file logs one structured warning; an unknown key names itself in the startup error. A malformed file fails the whole load; a missing optional file logs one structured warning; an unknown key names itself in the startup error. A malformed file fails the whole load; a missing optional file logs one structured warning; an unknown key names itself in the startup error. A malformed file fails the whole load; a missing optional file logs one structured warning; an unknown key names itself in the startup error. A malformed file fails the whole load; a missing optional file logs one structured warning; an unknown key names itself in the startup error. A malformed file fails the whole load; a missing optional file logs one structured warning; an unknown key names itself in the startup error. A malformed file fails the whole load; a missing optional file logs one structured warning; an unknown key names itself in the startup error. A malformed file fails the whole load; a missing optional file logs one structured warning; an unknown key names itself in the startup error. A malformed file fails the whole load; a missing optional file logs one structured warning; an unknown key names itself in the startup error. A malformed file fails the whole load; a missing optional file logs one structured warning; an unknown key names itself in the startup error. A malformed file fails the whole load; a missing optional file logs one structured warning; an unknown key names itself in the startup error. A malformed file fails the whole load; a missing optional file logs one structured warning; an unknown key names itself in the startup error.

