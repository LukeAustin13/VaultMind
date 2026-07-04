# Release Checklist

Work through this checklist during Step 4 of the `release-manager` process. Flag any items that are not complete.

Contents: Code Readiness · Version and Documentation · Pre-Release Validation · Release Mechanics · Post-Release. If reading a partial range, this line is the full scope.

---

## Code Readiness

- [ ] All planned work for this release is merged.
- [ ] No known critical bugs are outstanding.
- [ ] The build passes on the main/release branch.
- [ ] All tests pass (unit, integration).

## Version and Documentation

- [ ] Version number updated in project files (`.csproj`, `package.json`, `AssemblyInfo`, etc.).
- [ ] CHANGELOG.md updated (if used).
- [ ] README updated if public-facing behaviour has changed.
- [ ] API documentation updated if endpoints or contracts have changed.

## Pre-Release Validation

- [ ] Smoke tested against staging or a local environment.
- [ ] Migration scripts (if any) have been reviewed and tested.
- [ ] Breaking changes have migration notes written.
- [ ] Dependencies are up to date and no known vulnerabilities are outstanding.

## Release Mechanics

- [ ] Release branch or tag created from the correct base commit.
- [ ] Tag follows naming convention (`v1.2.3` not `1.2.3` or `release-1.2.3`).
- [ ] Release notes drafted and reviewed.
- [ ] Deployment pipeline is ready (if automated).

## Post-Release

- [ ] Release published (GitHub Releases, NuGet, npm, or internal registry).
- [ ] Deployment initiated or scheduled.
- [ ] Any deprecated features noted in communications if applicable.
