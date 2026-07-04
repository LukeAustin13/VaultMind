# Retrieval Evals

`tests/MindVault.Tests/RetrievalEvals/` proves search and context find the RIGHT notes in
the RIGHT order — not merely "some results exist". Every case asserts ranking position or
exact membership, so a ranking regression fails `dotnet test`.

## Why evals instead of unit tests

Ranking is policy, not plumbing. Unit tests pin the plumbing (bm25 weights get applied);
evals pin the *behaviour an agent depends on*: "when I ask for the decision, the decision
comes first." When you tune weights (via `search --explain` evidence), these evals are the
guardrail.

## The cases

| # | Eval | Asserts |
| --- | --- | --- |
| 1 | `ExactDecisionTitleRanksFirst` | The exact-title decision beats a body-spam competitor for rank 1 |
| 2 | `ProjectScopedSearchPrefersProjectNotes` | Project filter returns the project's task first and excludes unrelated global notes; `scope` is null (no fallback) |
| 2b | `EmptyProjectScopeFallsBackVisibly` | No project hits → vault-wide results marked `scope: "global-fallback"` |
| 3 | `ArchivedNotesExcludedUnlessRequested` | Archived note absent by default, present with `includeArchived` |
| 4 | `SupersededDecisionRanksBelowItsReplacement` | Accepted replacement outranks its superseded predecessor on the shared topic |
| 5 | `ArchitectureQuerySurfacesArchitectureNotes` | An architecture query puts the architecture note at rank 1 |
| 6 | `VagueQueryReturnsBoundedUsefulCandidates` | Vague OR-query returns 1..limit results — never a dump, never nothing |
| 7 | `ContextPackSurfacesTaskRelevantDecisions` | Task-aware pack lists the relevant decision in `taskRelevantNotes` AND keeps it in decisions-in-force |
| 8 | `BrokenLinksAppearInValidationWarnings` | Broken wiki link → `broken-link` warning with the source path |
| 9 | `DuplicateTitlesProduceAmbiguityProtection` | Duplicate titles → critical issue + resolver refuses to guess |
| 10 | `StaleTasksSurfaceInValidationAndContextWarnings` | 120-day-old open task appears in `stale-task` info AND project-context warnings |

## Running them

```bash
dotnet test --filter "FullyQualifiedName~RetrievalEvals"
```

## Extending them

When real-vault usage surfaces a retrieval miss:

1. Reproduce it as a new eval case (fixture notes + expected ranking).
2. Watch it fail.
3. Tune the ranking (weights live in `SearchService.Score`; use `--explain` output as
   evidence).
4. All evals green = the fix didn't silently break another behaviour.

That loop — not adding embeddings — is the intended first response to any retrieval
weakness.
