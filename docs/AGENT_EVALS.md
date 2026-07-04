# Agent Behaviour Evals

MindVault exists to make Claude Code/Fable *better behaved*, not just better informed.
`tests/MindVault.Tests/AgentWorkflowEvals/` pins the properties that produce good agent
behaviour; if any of them regress, `dotnet test` fails.

## What is enforced

### Output bounds (an agent's context is the scarce resource)

- `ContextPackStaysCompactEnoughForAgentUse` — pack JSON < 6 KB, markdown < 5 KB on the
  reference vault.
- `ProjectContextCarriesRefsNotFullNoteBodies` — a 6 KB body block planted in a decision
  note must NOT appear in project context or pack output (refs and short extracts only).
- `McpSearchOutputIsBounded` — the search tool clamps any requested limit to 100.
- `McpReadNoteTruncatesHugeBodies` — >60 K bodies come back truncated and marked
  `[truncated]`.

### No dangerous surface, ever

- `NoUnsafeToolNamesAnywhereInSkillsOrMcpSurface` — hard fail if any skill or the MCP tool
  source mentions `write_file`, `delete_file`, `run_shell`, `raw_sql`, or
  `raw_filesystem_access`.
- `HardeningGuardTests.McpToolCountMatchesTheDocumentedSurface` — the MCP tool list is
  pinned exactly (21 tools); adding or renaming one is a deliberate, test-visible act.
- `McpDiagnosticsLeakNoPathsOrSecrets` — health/diagnostics/status outputs contain no host
  paths and no token material.

### Skills that shape behaviour (not vibes)

- `EverySkillHasTheFiveRequiredSections` — all 8 skills contain `Trigger conditions`,
  `Required workflow`, `Do not`, `Efficiency rules`, `Safety rules`.
- `CreatingSkillsRequireDraftChecksBeforeCreating` — every note-creating skill teaches
  `mindvault_check_draft` *before* creating.
- `SkillsForbidVaultDumpingAndRawWritesAndShell` — every skill pins usage to `mindvault_*`
  tools and rules out shell; no skill contains executable shell fences; context skills
  carry the explicit "never the whole vault" rule.
- `HardeningGuardTests.SkillsReferenceOnlySafeMcpTools` — any tool name in any skill must
  be one of the 21 safe tools.

### Durable-record quality

- `SessionHandoffIsConciseAndStructured` — a session-end handoff block stays under 300
  characters with `Tests:` and `Follow-ups:` lines.
- `DecisionCaptureIncludesReversalConditions` — the decision template and every created
  decision carry a `## Reversal Conditions` section, and the capture skill requires it.

## Running them

```bash
dotnet test --filter "FullyQualifiedName~AgentWorkflowEvals"
```

## The design position

Agent misbehaviour is cheaper to prevent structurally than to prompt away:
bounded outputs make dumping impossible, draft checks make duplicates visible before they
exist, pinned tool surfaces make dangerous operations unrepresentable, and skills with
exact tool orders leave the model less room to improvise. These evals are the regression
net around that structure.
