---
name: integration-test-designer
description: Use this skill when you need to design an integration testing strategy — test containers, WebApplicationFactory setup, shared fixtures, test data builders, service boundary decisions, and test isolation approaches. Focused on the integration layer that unit tests cannot cover. Tuned for .NET with xUnit and Testcontainers; principles apply broadly. It does not write unit tests (use test-writer) or design the application itself (use backend-architect).
license: MIT
metadata:
  stack: dotnet
  version: 1.3
  last-reviewed: 2026-07-03
---

# Integration Test Designer

## Use When
- The user asks "how do I integration test this?", "how do I test with a real database?", or "what should I integration test vs mock?"
- A new service is being built and the testing strategy for the infrastructure layer is needed.
- Test containers or `WebApplicationFactory` need to be set up.
- Test data management is becoming a problem (isolation, cleanup, seeding).
- The unit tests pass but real-world integration keeps breaking, suggesting the mocks are lying.
- The boundary between unit and integration tests needs to be drawn.

## Do Not Use When
- Unit tests are what is needed — use **test-writer**.
- A QA strategy including manual testing and acceptance criteria is needed — use **qa-strategist**.
- The task is a full test coverage analysis — use the **test-gap-reviewer** agent.
- The application architecture needs to be designed — use **backend-architect**.

## Inputs To Look For
- The application type (ASP.NET Core Web API, Worker Service, gRPC, frontend).
- The data stores in use (SQL Server, PostgreSQL, SQLite, Redis, blob storage).
- External service dependencies (HTTP APIs, email, queues).
- The test framework in use (xUnit, NUnit, MSTest, Jest).
- Existing test project structure, if any.
- The CI environment (Docker availability affects test container choices).

## Core Decisions

### What Belongs In Integration Tests
Integration tests verify that components work together correctly. Test at the integration layer when:
- Code interacts with a real database (EF queries, stored procedures, migrations).
- Code calls real external services (test against test doubles or sandboxes, not production).
- Code involves serialisation/deserialisation through a full HTTP pipeline.
- Business rules involve database state checks (uniqueness, FK constraints, calculated fields).
- Middleware, filters, or model binding must be exercised end-to-end.

Do NOT move these to integration tests:
- Pure business logic with no infrastructure dependency — that is a unit test.
- Every permutation of validation rules — unit test the validator, integration test one realistic path.
- Error cases already proven by the infrastructure library (EF throwing on constraint violations is the library's job to test).

### Test Container vs SQLite vs Real Server

| Approach | Pros | Cons | When to Use |
|---|---|---|---|
| Testcontainers (Docker) | Real database engine, full feature parity | Requires Docker in CI | Preferred for production databases |
| SQLite in-memory | Fast, no Docker needed | Different engine behaviours, some EF features unavailable | Acceptable for simple projects, not for complex queries |
| LocalDB / SQL Express | Windows only, no Docker | CI friction, platform-specific | Legacy .NET projects |
| Hosted test database | No Docker friction | Shared state, isolation problems | Avoid for new projects |

### Isolation Strategy

| Strategy | How It Works | Best For |
|---|---|---|
| Transaction rollback | Wrap each test in a transaction, roll back after | Fast; works well when no tests commit explicitly |
| Database reset | Drop and recreate tables or use `TRUNCATE` between tests | Reliable; needed when tests commit |
| Respawn (library) | Smart table reset that respects FK order | Recommended for SQL Server/PostgreSQL |
| Separate database per test run | Each CI run gets a fresh database | Full isolation; expensive |

## Process

### Step 1 — Map the integration surface
1. Read the existing test projects and CI workflow first (test csproj files, any existing fixtures, whether the CI runner has Docker). Then list all infrastructure dependencies — from the actual DI registrations in Program.cs, not from memory.
2. For each dependency: is a real implementation available in tests? (Docker, test API sandbox, in-memory replacement)
3. Identify which application layers touch infrastructure directly.

### Step 2 — Define the test boundary
1. Decide which tests drive through the full HTTP stack (controller → service → repository → database).
2. Decide which tests start at the service or repository layer (bypassing HTTP).
3. Document the rationale — driving through HTTP catches middleware bugs; starting at the service layer is faster.

### Step 3 — Design the test host
For ASP.NET Core, see [templates/integration-test-templates.md](templates/integration-test-templates.md) § WebApplicationFactory Test Host for the skeleton.

Key decisions:
- Replace the real connection string with the test container connection string.
- Replace external HTTP clients with `WireMock` or `HttpMessageHandler` fakes.
- Replace email senders, blob storage, queues with in-memory or no-op implementations.
- Set `ASPNETCORE_ENVIRONMENT` to `Testing` to suppress production-only middleware.

### Step 4 — Design shared fixtures
Shared fixtures prevent test container restart overhead. See [templates/integration-test-templates.md](templates/integration-test-templates.md) § DatabaseFixture for collection-fixture skeletons with and without Testcontainers.

Rules for shared fixtures:
- The fixture starts the container once and shares it across tests.
- Each test is responsible for its own data isolation (transaction rollback or table reset).
- Tests must not depend on the order they run.
- Tests must not assume the database is empty at the start.

### Step 5 — Design test data builders
Avoid hard-coded fixture data in every test. Use a fluent builder (see [templates/integration-test-templates.md](templates/integration-test-templates.md) § Test Data Builder), or `Bogus` for realistic fake data.

### Step 6 — Design the test structure

Recommended folder layout:
```
tests/
  ProjectName.IntegrationTests/
    Fixtures/
      DatabaseFixture.cs
      TestWebApp.cs
    Builders/
      OrderBuilder.cs
      CustomerBuilder.cs
    Features/
      Orders/
        CreateOrderTests.cs
        GetOrderTests.cs
    Helpers/
      HttpClientExtensions.cs
      DatabaseExtensions.cs
```

For copy-ready scaffolding (test host, `DatabaseFixture` with and without Testcontainers, a test-data builder, and an isolation-strategy decision tree), see [templates/integration-test-templates.md](templates/integration-test-templates.md).

## Output Format

### Integration Test Design: [Application Name]

#### Integration Surface

| Dependency | Test Approach | Rationale |
|---|---|---|
| PostgreSQL | Testcontainers | Real engine, CI has Docker |
| External payment API | WireMock | Deterministic responses, no sandbox available |
| Email sender | In-memory no-op | Not testing email content here |

#### Test Boundary Decision

- **Full HTTP stack tests:** [List of features tested end-to-end]
- **Service/repository layer tests:** [List of features tested below HTTP]
- **Rationale:** [One sentence]

#### Test Host Setup

```csharp
// Key replacements in TestWebApp
```

#### Isolation Strategy

**Chosen approach:** [Transaction rollback / Respawn / Database reset]
**Rationale:** [One sentence]

#### Fixture Design

```csharp
// Fixture skeleton
```

#### Test Data Approach

**Chosen approach:** [Builder pattern / Bogus / AutoFixture / Other]
**Key builders needed:** [List]

#### Folder Structure

```
[Directory tree]
```

#### CI Considerations
- [Docker availability requirements]
- [Test run time estimate]
- [Parallelisation constraints]

## Quality Bar
- The integration surface is fully mapped before recommending tools.
- The isolation strategy is chosen based on the actual test commit behaviour, not default.
- Shared fixtures are used for expensive resources (containers); cheap resources can be per-test.
- Test data builders are designed for the domain, not generic.
- The design accounts for CI constraints (Docker, parallelism, run time).

## Failure Modes To Avoid
- Recommending SQLite as equivalent to production SQL Server — engine differences cause false positives.
- Using a single shared database state across tests without isolation — tests break each other.
- Starting a new container per test — prohibitively slow.
- Writing integration tests that duplicate every unit test — test what unit tests cannot.
- Seeding data in `InitializeAsync` and relying on that data in specific tests — order dependency is fragile.
- Forgetting to stub external HTTP calls — integration tests should not call production services.

## Related Skills
- **flaky-test-stabiliser** — when an existing integration test is intermittently failing; this skill designs the fixtures and isolation that prevent flakiness.
