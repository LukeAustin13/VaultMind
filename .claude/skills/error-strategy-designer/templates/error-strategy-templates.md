# Error Strategy Templates

Starting points for the artefacts the `error-strategy-designer` process produces. Copy a block, then replace the generic names (`Widget`, `external-api`) with the real domain terms. These are templates, not finished implementation — keep only the parts the design calls for.

---

## Exception Hierarchy

A shallow base-plus-domain hierarchy. Each subclass below carries a distinct HTTP status code and handling behaviour; do not add a type that only changes the message.

```csharp
public abstract class AppException : Exception
{
    protected AppException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

// Resource requested by id does not exist. → 404
public sealed class NotFoundException : AppException
{
    public string ResourceType { get; }
    public string ResourceId { get; }

    public NotFoundException(string resourceType, string resourceId)
        : base($"{resourceType} with id '{resourceId}' was not found.")
    {
        ResourceType = resourceType;
        ResourceId = resourceId;
    }
}

// Business rule violated. → 422
public sealed class ValidationException : AppException
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("One or more validation rules failed.")
    {
        Errors = errors;
    }
}

// State conflict, e.g. duplicate or concurrent edit. → 409
public sealed class ConflictException : AppException
{
    public ConflictException(string message)
        : base(message)
    {
    }
}

// A downstream dependency failed. → 502
public sealed class ExternalServiceException : AppException
{
    public string ServiceName { get; }

    public ExternalServiceException(string serviceName, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ServiceName = serviceName;
    }
}
```

---

## ProblemDetails Response Shape

RFC 7807 body returned by the global handler. `detail` must be safe for clients — no stack traces, no internal paths. The `errors` extension appears only for `ValidationException`.

```jsonc
{
  "type": "https://errors.example.com/not-found",
  "title": "Resource not found",
  "status": 404,
  "detail": "Widget with id '00000000-0000-0000-0000-000000000000' was not found.",
  "instance": "/api/widgets/00000000-0000-0000-0000-000000000000",
  "traceId": "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01"
}
```

Validation failure adds an `errors` member:

```jsonc
{
  "type": "https://errors.example.com/validation",
  "title": "Validation failed",
  "status": 422,
  "detail": "One or more validation rules failed.",
  "instance": "/api/widgets",
  "traceId": "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01",
  "errors": {
    "name": ["Name is required."],
    "quantity": ["Quantity must be greater than zero."]
  }
}
```

Construction inside the handler:

```csharp
var problem = new ProblemDetails
{
    Type = "https://errors.example.com/not-found",
    Title = "Resource not found",
    Status = StatusCodes.Status404NotFound,
    Detail = exception.Message,
    Instance = context.Request.Path
};
problem.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;
```

---

## Polly Resilience Policies

Polly v8+ resilience pipelines, registered by name and resolved per dependency. Pick policies per failure mode — do not stack all of them onto every dependency. For a plain `HttpClient`, prefer `Microsoft.Extensions.Http.Resilience` (`AddStandardResilienceHandler()`); hand-build only when its defaults do not fit.

### Retry with backoff — transient, self-correcting failures

Use for idempotent calls hitting HTTP 429/503 or a transient timeout. Jitter is required to avoid a synchronised retry storm on recovery.

```csharp
services.AddResiliencePipeline("external-api-retry", builder =>
{
    builder.AddRetry(new RetryStrategyOptions
    {
        ShouldHandle = new PredicateBuilder()
            .Handle<HttpRequestException>()
            .Handle<TimeoutRejectedException>(),
        MaxRetryAttempts = 3,
        BackoffType = DelayBackoffType.Exponential,
        UseJitter = true,
        Delay = TimeSpan.FromMilliseconds(200)
    });
});
```

### Circuit breaker — protect a failing downstream from cascade

Use when repeated failure means the dependency is unhealthy and hammering it makes things worse. Open the circuit after a failure-ratio threshold, then probe after a break.

```csharp
services.AddResiliencePipeline("external-api-breaker", builder =>
{
    builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions
    {
        ShouldHandle = new PredicateBuilder()
            .Handle<HttpRequestException>()
            .Handle<TimeoutRejectedException>(),
        FailureRatio = 0.5,
        MinimumThroughput = 10,
        SamplingDuration = TimeSpan.FromSeconds(30),
        BreakDuration = TimeSpan.FromSeconds(15)
    });
});
```

### Timeout — bound a slow call

Use to stop a slow dependency from blocking the caller indefinitely. This is an outer, per-call timeout — not per-retry attempt.

```csharp
services.AddResiliencePipeline("external-api-timeout", builder =>
{
    builder.AddTimeout(new TimeoutStrategyOptions
    {
        Timeout = TimeSpan.FromSeconds(10)
    });
});
```

### Combined pipeline — ordered for one HTTP dependency

Order matters: timeout (outer) → retry → circuit breaker → inner timeout per attempt. The outer timeout caps the total time across retries; the inner caps each attempt.

```csharp
services.AddResiliencePipeline("external-api", builder =>
{
    builder
        .AddTimeout(TimeSpan.FromSeconds(30))
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            Delay = TimeSpan.FromMilliseconds(200)
        })
        .AddCircuitBreaker(new CircuitBreakerStrategyOptions
        {
            FailureRatio = 0.5,
            MinimumThroughput = 10,
            BreakDuration = TimeSpan.FromSeconds(15)
        })
        .AddTimeout(TimeSpan.FromSeconds(10));
});
```

Do not apply a circuit breaker to a database — connection pooling and EF Core's `EnableRetryOnFailure` handle transient database faults. Use a timeout there instead.
