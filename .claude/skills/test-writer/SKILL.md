---
name: test-writer
description: Use this skill when you need to create tests for existing or planned code. The test-writer covers happy paths, edge cases, failure scenarios, and regressions. It produces well-named, well-structured tests that serve as documentation and safety nets. It does not fix bugs (use bug-hunter) or review test quality in isolation (use code-reviewer for that).
license: MIT
metadata:
  stack: agnostic
  version: 2.1
  last-reviewed: 2026-07-03
---

# Test Writer

## Use When
- The user asks for tests for existing code.
- You are about to refactor and need test coverage first.
- A bug was fixed and a regression test is needed.
- New code is being written and tests should accompany it.
- The user asks "how should I test this?"

## Do Not Use When
- You are debugging a failing test — use **bug-hunter**.
- You are reviewing existing test quality — use **code-reviewer**.
- You need to understand what the code does before writing tests — read the code first, then use this skill.

## Inputs To Look For
- The code to test (file paths, classes, methods).
- The testing framework in use (xUnit, NUnit, MSTest, Jest, pytest, etc.).
- Existing test files and conventions in the project.
- Requirements or specifications describing expected behaviour.
- Known edge cases or past bugs.

## Tool-Use Rules

- **Read a sibling test first.** Before writing anything, find the nearest existing test for the same layer (Glob the test tree, Read one file) and copy its conventions — fixtures, builders, naming, assertion style. Do not import your own style into someone else's suite.
- **Discover the real runner.** Find how this project actually executes tests (test project config, `package.json` script, CI workflow) and use that command — do not guess it.
- **Run what you write.** Execute the new tests before delivering and include the runner output. A test that has never been run is not a deliverable — it is a hypothesis with syntax highlighting.
- **Prove each test can fail.** For every test, name the specific production change that would make it go red. If you cannot name one, the test asserts nothing — fix the assertion. Never compute the expected value by calling the code under test.

## Process
1. **Identify what to test.** List the public methods, endpoints, or behaviours under test.
2. **Determine the testing framework.** Check the project for existing test projects, dependencies, and conventions.
3. **Decide the test type.**
   - **Unit test:** The code has no external dependencies, or those dependencies can be substituted with fast in-memory fakes. Use for: isolated logic, calculations, validation, transformations, domain rules.
   - **Integration test:** Correctness depends on real infrastructure — database, HTTP client, file system, message bus. Use `integration-test-designer` for fixture and container setup. Use for: repository methods, EF queries, API endpoints, external service adapters.
   - **Contract test:** You are testing a published interface consumed by another system. Use for: REST APIs or message schemas where consumer expectations must be verified independently of implementation.
   - Default to unit tests. Choose integration tests when the infrastructure behaviour itself is what needs to be verified, not just an obstacle to testing the logic.
4. **Apply the mocking boundary rule.**
   - **Mock:** External system boundaries — `HttpClient`, `DbContext` (in unit tests only), file system, email/SMS senders, external APIs, `IDateTimeProvider` for time-dependent logic.
   - **Do not mock:** Value objects, domain logic, pure functions, in-process computations, or anything that is cheap, fast, and side-effect-free. Do not mock what you can use directly without consequence.
   - One test = one real thing. Everything else is either a real implementation or a minimal, purposeful test double.
   - For the full mocking boundary decision tree and a C# test data builder template, see [templates/mocking-and-builders.md](templates/mocking-and-builders.md).
5. **Categorise test cases:**
   - **Happy path:** Normal inputs, expected outputs.
   - **Edge cases:** Boundary values, empty inputs, nulls, maximum values.
   - **Failure cases:** Invalid inputs, missing dependencies, timeout scenarios.
   - **Regression:** Specific bugs that were fixed and must not recur.
6. **Name tests clearly.** Use the pattern: `MethodName_Scenario_ExpectedResult` or the project's existing convention.
7. **Structure each test.** Follow Arrange-Act-Assert (AAA):
   - **Arrange:** Set up inputs and dependencies.
   - **Act:** Call the method under test.
   - **Assert:** Verify the result.
8. **Keep tests independent.** No test should depend on another test's state or execution order.
9. **Avoid testing implementation details.** Test behaviour and outputs, not internal method calls or private state.
10. **Write the tests.** Produce complete, runnable test code.
11. **Run them.** Execute the new tests with the project's real runner and record the result. If any fail against correct production code, fix the test, not the code.

## Output Format

### Tests For: [Class/Method/Feature]

**Framework:** [xUnit / NUnit / Jest / etc.]
**Test File:** [Path where tests should be placed]
**Run evidence:** [Command + result, e.g. `dotnet test --filter OrderServiceTests` — 6 passed, 0 failed. Or: "not run — no runner available in this environment" stated plainly.]

#### Test Cases

| # | Test Name | Category | Description |
|---|-----------|----------|-------------|
| 1 | `CreateOrder_ValidInput_ReturnsOrder` | Happy path | Verifies order creation with valid data |
| 2 | `CreateOrder_NullCustomer_ThrowsArgumentException` | Failure | Verifies null input is rejected |
| 3 | `CreateOrder_EmptyItems_ThrowsValidationException` | Edge case | Verifies empty order is rejected |

```csharp
// [Complete test code here]
```

#### Not Covered
- [Scenarios intentionally skipped and why]

## Quality Bar
- The new tests were executed and the run output is included — or the inability to run is stated plainly, never implied.
- Every test has a nameable production change that would make it fail; expected values are independent of the code under test.
- Happy path, at least one edge case, and at least one failure case are covered.
- Test names describe the scenario and expected result.
- Tests are independent and can run in any order.
- Arrange-Act-Assert structure is followed.
- No logic in tests (no if/else, no loops, no try/catch in test methods).
- Tests use the project's existing framework and conventions.
- Any intentionally skipped scenarios are documented.

## Failure Modes To Avoid
- Writing tests that test the mocking framework instead of the real code.
- Writing tests so tightly coupled to implementation that any refactor breaks them.
- Only testing happy paths.
- Using vague test names (`Test1`, `TestMethod`, `ItWorks`).
- Writing tests that pass when the code is broken (assert nothing meaningful).
- Coverage theatre — accepting "just get the number to N%" as the goal and generating assertion-free tests to hit it. Coverage is a by-product of testing behaviour; if the requester wants a number without assertions, say what that number will not protect and write the real tests for the riskiest paths first.
- Duplicating setup code across every test instead of using shared fixtures.
- Over-mocking: mocking things that are cheap and safe to use directly.
- Defaulting to unit tests when the thing being tested is infrastructure behaviour — use integration tests.
- Mocking internal domain collaborators when the test should verify their interaction.

## Related Skills

- **integration-test-designer** — Use when step 3 resolves to integration tests. Handles Testcontainers, WebApplicationFactory, fixtures, and isolation strategy.
- **test-gap-reviewer** agent — Use to identify gaps in existing test coverage rather than writing new tests from scratch.
- **qa-strategist** — Use when a full test strategy (acceptance criteria, manual test cases, regression plan) is needed rather than test code.

## Coding Standards Reference

For C# test code, apply the conventions in [docs/csharp-coding-standards.md](../../../docs/csharp-coding-standards.md). Test code is production code — the same naming, layout, and language-feature rules apply.
