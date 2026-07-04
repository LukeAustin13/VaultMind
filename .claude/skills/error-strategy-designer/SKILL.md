---
name: error-strategy-designer
description: Use this skill when you need to design an exception and error handling strategy — exception hierarchies, global error middleware, ProblemDetails response formatting, retry policies, circuit breakers, and structured error logging. Especially tuned for ASP.NET Core and Polly, but principles apply broadly. It produces a design specification, not implementation code. It does not hunt runtime bugs (use bug-hunter) or review existing error handling code quality (use code-reviewer).
license: MIT
metadata:
  stack: dotnet
  version: 1.3
  last-reviewed: 2026-06-29
---

# Error Strategy Designer

## Use When
- Starting a new service and the error handling model needs to be designed.
- The user asks "how should we handle errors?", "what exception types should we create?", or "how do I set up global error handling?"
- Polly retry, circuit breaker, or timeout policies need to be designed.
- The error responses from an API are inconsistent and need standardising.
- Error logging is unclear, missing correlation, or too noisy.
- A Result pattern vs exceptions decision needs to be made.

## Do Not Use When
- A specific runtime error needs to be diagnosed — use **bug-hunter**.
- Existing error handling code needs to be reviewed for quality issues — use **code-reviewer**.
- The task is designing the overall backend structure — use **backend-architect** (this skill handles error handling specifically).
- The task is designing API response contracts — use **api-designer** (though error response format can be coordinated between the two).

## Inputs To Look For
- The type of application (ASP.NET Core Web API, Worker Service, console, frontend).
- Whether external service calls exist (HTTP clients, databases, message brokers).
- Any existing exception classes or middleware already in place.
- Preferred error response format (ProblemDetails, custom envelope, or no preference).
- Whether a Result pattern is already in use or being considered.

## Concepts Reference

See `references/error-strategy-concepts.md` for: exception hierarchy design, ASP.NET Core global error handling options, ProblemDetails shape and key decisions, Polly v8+ resilience pipeline settings, Result vs exceptions guidance, and structured error logging rules including what never to log.

For copy-ready starting points (exception hierarchy, ProblemDetails body, and Polly retry/circuit-breaker/timeout pipelines), see [templates/error-strategy-templates.md](templates/error-strategy-templates.md).

## Process

1. **Understand the application type.** Web API, background worker, and frontend have different error handling needs.
2. **Map exception categories.** First grep for existing `Exception` subclasses, `IExceptionHandler`/`UseExceptionHandler` registrations, and Polly usage — the design must extend what exists or explicitly state the replacement decision. Then identify domain, validation, infrastructure, and unexpected errors.
3. **Design the exception hierarchy.** Keep it shallow (max 2–3 levels). Add types only when they carry distinct HTTP status codes or handling behaviour.
4. **Design global middleware.** Choose between `IExceptionHandler`, `UseExceptionHandler`, or middleware. One handler catches all.
5. **Define the error response format.** ProblemDetails for HTTP APIs. Decide on extensions.
6. **Design resilience policies.** For each external dependency (HTTP, database), specify retry, timeout, and circuit breaker parameters.
7. **Define Result usage.** Decide where Results replace exceptions and where exceptions remain.
8. **Define logging rules.** Map exception types to log levels and specify required context fields.

## Output Format

### Error Strategy: [Application Name]

#### Exception Hierarchy

```
[Hierarchy diagram]
```

| Exception Type | HTTP Status | When to Use |
|---|---|---|
| `NotFoundException` | 404 | Resource not found by ID |
| `ValidationException` | 422 | Business rule validation failure |

#### Global Error Middleware

**Recommended approach:** [IExceptionHandler / UseExceptionHandler / other]
**Rationale:** [One sentence]

**Mapping table:**

| Exception | Status Code | Log Level | Include Detail in Response |
|---|---|---|---|
| `NotFoundException` | 404 | Warning | Yes |
| `ValidationException` | 422 | Warning | Yes |
| `ExternalServiceException` | 502 | Error | No (log internally) |
| Unhandled `Exception` | 500 | Error | No |

#### Error Response Shape

```json
{
  "type": "...",
  "title": "...",
  "status": 0,
  "detail": "...",
  "traceId": "..."
}
```

[Note any extensions to the standard shape]

#### Resilience Policies

| Dependency | Policy | Configuration |
|---|---|---|
| External HTTP API | Retry + Timeout | 3 retries, exponential backoff, 10s timeout |
| Database | Timeout only | 30s — let EF retry handle transient errors |

#### Result Pattern Usage

- Use Result: [list of layers or operations]
- Use exceptions: [list of layers or operations]

#### Logging Rules

| Exception / Event | Level | Required Fields |
|---|---|---|
| `NotFoundException` | Warning | `traceId`, `resourceType`, `resourceId` |
| Unhandled exception | Error | `traceId`, `endpoint`, `exceptionType` |

#### Follow-up Decisions
- [Any decisions that require implementation context not yet available]

## Quality Bar
- Every exception type maps to a specific HTTP status code and a log level.
- The error response never exposes stack traces, internal paths, or credentials.
- Resilience policies specify actual numbers, not just "add retry".
- The Result vs exceptions decision is clear and scoped, not left ambiguous.
- Logging rules specify what must never be logged alongside what should be.

## Failure Modes To Avoid
- Designing a deep exception hierarchy (more than 3 levels) — complexity outweighs value.
- Logging at Error level for expected domain failures (404, validation) — this pollutes alerting.
- Returning stack traces or internal exception messages in API error responses.
- Specifying retry policies without jitter — causes thundering herd on recovery.
- Applying circuit breakers to databases — they have their own connection resilience; use timeouts instead.
- Recommending Result everywhere without considering the noise cost at the calling layer.
