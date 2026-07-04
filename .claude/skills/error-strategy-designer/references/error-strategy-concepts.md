# Error Strategy Concepts Reference

Consult this file when you need implementation detail during `error-strategy-designer` process steps.

---

## Exception Hierarchy Design

Well-designed exception hierarchies have:
- A single `AppException` (or domain-specific equivalent) as the base.
- Subclasses that add semantic meaning, not just a different message.
- Exception types for domain violations (`NotFoundException`, `ValidationException`, `ConflictException`) separate from infrastructure failures (`ExternalServiceException`, `DatabaseException`).
- No exceptions used for normal control flow.

```
AppException (base)
├── DomainException
│   ├── NotFoundException        → 404
│   ├── ValidationException      → 422
│   ├── ConflictException        → 409
│   └── ForbiddenException       → 403
└── InfrastructureException
    ├── ExternalServiceException → 502
    └── DatabaseException        → 500
```

---

## Global Error Handling in ASP.NET Core

Options in order of preference for new projects:

1. **`IExceptionHandler` (ASP.NET Core 8+)** — strongly typed, composable, testable:
   ```csharp
   public class AppExceptionHandler : IExceptionHandler
   {
       public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
   }
   ```

2. **`UseExceptionHandler` with a handler path** — simple and supported across all versions.

3. **Exception filter (`IExceptionFilter`)** — MVC-specific, less suitable for minimal APIs.

Avoid: catching all exceptions in individual controllers. That belongs in one place.

---

## ProblemDetails (RFC 7807)

Standard error response shape for HTTP APIs:
```json
{
  "type": "https://example.com/errors/not-found",
  "title": "Resource not found",
  "status": 404,
  "detail": "Order with ID 123 was not found.",
  "instance": "/api/orders/123",
  "traceId": "00-abc123-def456-01"
}
```

Key decisions:
- `type` should be a stable URI, not a URL that changes.
- `detail` should be safe to return to clients (no stack traces, no internal paths).
- Extend with custom properties (`errors` array for validation) only if needed.
- `traceId` links to distributed trace for internal correlation.

---

## Polly Resilience Policies

| Policy | Use When | Key Settings |
|---|---|---|
| Retry | Transient failures (HTTP 429, 503, timeout) | Retry count (3–5), exponential backoff with jitter |
| Circuit Breaker | Protect downstream from cascade failure | Break after N failures, open for X seconds |
| Timeout | Prevent slow callers from blocking indefinitely | Per-call timeout (not per-retry) |
| Fallback | Provide degraded response when all else fails | Return cached data or empty response |
| Bulkhead | Limit concurrent calls to a service | Max parallelism, queue size |

Polly v8+ (resilience pipelines):
```csharp
services.AddResiliencePipeline("external-api", builder =>
{
    builder
        .AddRetry(new RetryStrategyOptions { MaxRetryAttempts = 3 })
        .AddCircuitBreaker(new CircuitBreakerStrategyOptions())
        .AddTimeout(TimeSpan.FromSeconds(10));
});
```

For `HttpClient` specifically, default to `Microsoft.Extensions.Http.Resilience` — `AddStandardResilienceHandler()` applies a pre-tuned rate limiter + retry + circuit breaker + timeout pipeline; hand-build a custom pipeline only when its defaults do not fit:
```csharp
services.AddHttpClient("payments", c => c.BaseAddress = new Uri("https://api.example.com"))
    .AddStandardResilienceHandler();
```

---

## Result Pattern vs Exceptions

Use a Result pattern (`Result<T, TError>`) when:
- The error is an expected outcome of a business operation, not an exceptional condition.
- The caller must always handle both success and failure paths.
- The code is a library or domain layer without an HTTP context.

Use exceptions when:
- The error is truly exceptional (unexpected, programmer error, infrastructure failure).
- The code is in an application layer with global exception handling in place.
- Propagating Result types through many layers adds more noise than it removes.

Mixing both is valid: domain/application layer returns Results, infrastructure throws exceptions caught by global middleware.

---

## Structured Error Logging

Log at the right level:
- **Warning:** Expected domain errors (`NotFoundException`, `ValidationException`) — not a system problem.
- **Error:** Unexpected exceptions, external service failures — needs investigation.
- **Critical:** Startup failure, database unavailable, security breach.

Always include:
- Correlation / trace ID (link to the request or distributed trace).
- The exception type and message (never the stack trace at Warning level).
- Relevant request context (endpoint, user ID if safe, request ID).

Never log:
- Passwords, tokens, or credentials even in error context.
- Full request bodies that may contain sensitive data.
- Stack traces at Warning level — reserve for Error/Critical.
