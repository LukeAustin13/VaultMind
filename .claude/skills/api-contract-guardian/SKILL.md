---
name: api-contract-guardian
description: Use this skill when you need to review API changes for contract drift, breaking changes, or consumer impact. It covers REST endpoints, DTOs, status codes, validation, pagination, versioning, and OpenAPI/Swagger awareness. It does not design new APIs (use api-designer) or review backend architecture (use backend-architect).
license: MIT
metadata:
  stack: agnostic
  version: 1.0
  last-reviewed: 2026-05-18
---

# API Contract Guardian

## Purpose

Prevent API contract drift by reviewing changes to endpoints, DTOs, status codes, validation rules, and response shapes. Classify changes as breaking or non-breaking, assess consumer impact, and identify required documentation and test updates.

## Use When

- API endpoint signatures, DTOs, or response shapes have changed.
- A PR modifies controllers, request/response models, or API configuration.
- The user asks "is this a breaking change?" or "will this affect clients?".
- OpenAPI/Swagger specs need updating after code changes.
- Generated API clients may be affected by a change.
- API versioning decisions need review.

## Do Not Use When

- You are designing a new API from scratch — use **api-designer**.
- You are reviewing backend architecture — use **backend-architect**.
- You are reviewing general code quality — use **code-reviewer**.
- You are checking database changes — use **ef-migration-guardian**.
- The change is to a C# public API surface consumed by other assemblies (not REST) — use the **breaking-change-detector** agent.

## Inputs To Inspect

- Controller classes and action methods.
- Request and response DTO classes.
- Validation attributes and FluentValidation rules.
- Route attributes and HTTP method bindings.
- OpenAPI/Swagger specification files (if present).
- API versioning configuration.
- Generated client code or SDK projects.
- Integration tests that call the API.

## Process

1. **Identify changed endpoints.** List every endpoint affected by the code change, including route, HTTP method, and parameters.
2. **Classify each change.** For each endpoint change, determine:
   - **Breaking:** Removed endpoint, removed field from response, changed field type, changed route, tightened validation, changed status code meaning.
   - **Non-breaking:** Added optional field to request, added field to response, added new endpoint, loosened validation.
   - **Potentially breaking:** Changed default values, added required field with default, changed error response shape.
3. **Assess consumer impact.** For breaking changes, search the repo (and any consumer repos available here) for usages of the changed field, route, or status code — cite file:line for each affected consumer. Consumers you cannot search (mobile apps, third parties, generated clients elsewhere) go in an explicit Unverified Consumers list; do not assert impact you did not trace.
4. **Check OpenAPI/Swagger.** If a spec file exists, verify it reflects the changes. If auto-generated, verify the generation is triggered.
5. **Check versioning.** If the API uses versioning, verify breaking changes go into a new version.
6. **Identify test gaps.** List integration tests that need updating or creating.
7. **Identify documentation gaps.** List API docs, changelogs, or migration guides that need updating.

## Output Format

### API Contract Review: [Endpoint/Area]

**Breaking Changes:** [Count]
**Non-Breaking Changes:** [Count]
**Consumer Impact:** None / Low / Medium / High

#### Change Summary

| # | Endpoint | Change | Classification | Consumer Impact |
|---|----------|--------|---------------|----------------|
| 1 | `GET /api/orders` | Added `shipDate` to response | Non-breaking | None |
| 2 | `POST /api/orders` | `quantity` changed from optional to required | Breaking | All consumers must send `quantity` |
| 3 | `DELETE /api/orders/{id}` | Route changed to `/api/v2/orders/{id}` | Breaking | All consumers must update URL |

#### Breaking Change Detail

For each breaking change:

**Change:** [Description]
**Before:** [Previous contract]
**After:** [New contract]
**Affected Consumers:** [List]
**Migration Path:** [How consumers should update]

#### Required Updates

- [ ] OpenAPI/Swagger spec regenerated or updated
- [ ] Generated client packages rebuilt
- [ ] Integration tests updated
- [ ] API changelog updated
- [ ] Consumer migration guide written (if breaking)

## Quality Bar

- Every changed endpoint is listed with its classification.
- Breaking changes include before/after contract details.
- Consumer impact is specific, not generic ("mobile app" not "some consumers").
- Test and documentation gaps are identified.
- Versioning is considered when breaking changes exist.

## Failure Modes To Avoid

- Classifying a breaking change as non-breaking.
- Ignoring response shape changes because "it just adds a field" (removing fields is breaking).
- Missing generated client impact.
- Forgetting that changing default values can be breaking for consumers relying on defaults.
- Reviewing only the happy path and missing error response changes.
- Ignoring pagination, sorting, or filtering contract changes.

## Related Skills And Agents

- **api-designer** — for designing new APIs.
- **code-reviewer** — for general code quality review.
- **pr-correctness-reviewer** agent — for logic correctness in the implementation.
- **test-gap-reviewer** agent — for missing test coverage.
- **docs-maintainer** agent — for documentation updates.
