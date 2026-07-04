# Release Checklist

Run top to bottom before calling a MindVault version done. Do not tick a box you did not
run; "not run: <reason>" is the honest alternative.

## Version identity

- [ ] Bump `MindVaultVersion.Current` (`src/MindVault.Core/MindVaultVersion.cs`)
- [ ] Add a dated section to `CHANGELOG.md`
- [ ] If tables/tokenizer changed: bump `IndexDatabase.CurrentSchemaVersion` and note the
      auto-rebuild in the changelog

## Build and tests

- [ ] `dotnet restore`
- [ ] `dotnet build -c Release` — 0 warnings, 0 errors
- [ ] `dotnet test -c Release` — all green, including:
  - [ ] retrieval evals (`--filter FullyQualifiedName~RetrievalEvals`)
  - [ ] agent evals (`--filter FullyQualifiedName~AgentWorkflowEvals`)
  - [ ] mutation torture (`--filter FullyQualifiedName~MutationTorture`)

## Benchmarks

- [ ] `dotnet run -c Release --project tools/benchmarks -- --sizes 100,1000,10000`
- [ ] Update `docs/PERFORMANCE_RESULTS.md` with the new numbers (never leave stale numbers
      under a new version)

## Docker / Pi

- [ ] `docker build -t mindvault:local .` (or state that CI buildx is the only validation)
- [ ] `docker compose -f docker-compose.example.yml config` parses
- [ ] Compose still binds `127.0.0.1:7777:7777` by default (guard test also checks)
- [ ] Pi smoke (when hardware available): the checklist in
      `docs/SUPERPOWER_FINAL_AUDIT.md` §Pi validation

## Docs and skills

- [ ] README tool/command counts match reality (guard tests pin the MCP surface)
- [ ] `docs/MCP_SETUP.md` lists every tool
- [ ] Skills safety check is green (part of `dotnet test`) — 8 skills, 5 sections each,
      safe tools only

## Final honesty pass

- [ ] `docs/SUPERPOWER_FINAL_AUDIT.md` (or its successor) reflects what is actually true
      of this version — limitations included
- [ ] Everything not run is listed as not run
