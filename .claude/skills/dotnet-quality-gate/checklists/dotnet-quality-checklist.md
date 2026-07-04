# .NET Quality Checklist

Run these checks in order. Stop at the first failure — later steps depend on earlier ones.

## 1. Restore

- [ ] `dotnet restore` completes without errors
- [ ] No package version conflicts reported
- [ ] No deprecated package warnings (note but do not block)

## 2. Format

- [ ] `dotnet format --verify-no-changes` passes
- [ ] If `.editorconfig` exists, formatting matches its rules
- [ ] No whitespace-only changes mixed with logic changes

## 3. Build

- [ ] `dotnet build --no-restore` completes with exit code 0
- [ ] No errors (CS-prefixed diagnostics)
- [ ] Warnings reviewed — new warnings in changed files should be addressed
- [ ] No "obsolete" warnings introduced by new code

## 4. Test Selection

- [ ] Identified test projects that cover changed code
- [ ] If changes are localised, use `--filter` to target relevant tests
- [ ] If changes are broad (shared libraries, base classes), run full suite

## 5. Test Execution

- [ ] `dotnet test --no-build` passes on selected tests
- [ ] All new code paths have corresponding tests
- [ ] No tests were skipped unexpectedly
- [ ] Test output does not contain unhandled exceptions or warnings

## 6. Final Verification

- [ ] Build is green
- [ ] Tests are green
- [ ] No formatting violations
- [ ] Ready for review or PR
