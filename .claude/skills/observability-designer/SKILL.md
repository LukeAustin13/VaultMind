---
name: observability-designer
description: Use this skill when you need to design a logging, metrics, health check, and tracing strategy for a .NET application. The observability-designer covers structured logging, what to log at each level, correlation IDs, health endpoints, metrics choices, and what must never be logged. It produces a design specification, not implementation code. It decides WHAT to log, trace, and check — wiring health checks or log shipping into Docker/CI/proxy config is devops-deploy. It is especially tuned for .NET/C# but the principles apply broadly.
license: MIT
metadata:
  stack: dotnet
  version: 1.2
  last-reviewed: 2026-07-03
---

# Observability Designer

## Use When

- Starting a new service and designing its logging and monitoring approach before writing code.
- An existing service has poor logging and needs a consistent strategy.
- The user asks "what should we log?", "how do we add tracing?", or "what health checks do we need?".
- You need to design health check endpoints for a deployment or load balancer.
- A production incident revealed a logging gap that needs addressing systematically.
- You need to define what must never be logged (secrets, PII, sensitive data).

## Do Not Use When

- You are implementing logging code — that is implementation work.
- You are debugging a specific error — use **bug-hunter**.
- You are designing the overall backend architecture — use **backend-architect** (reference this skill for observability concerns).
- You are reviewing existing logging code — use **code-reviewer**.

## Inputs To Look For

- The application type (API, background service, worker, console app).
- Existing logging configuration (`appsettings.json`, `Program.cs`, Serilog/NLog setup).
- Deployment target (Docker, Azure, AWS, on-premises) — this affects sink and format choices.
- Whether distributed tracing is needed (multiple services vs single service).
- Regulatory constraints (GDPR, HIPAA, PCI-DSS) that affect what can be logged.
- Existing monitoring infrastructure (Application Insights, Datadog, Seq, ELK, Grafana).

## Clarify Before Starting

If any of the following are unknown, ask before designing:

- **What is the deployment target and monitoring infrastructure?** The sink (Application Insights, Seq, stdout JSON) changes the design significantly.
- **Is this a single service or part of a distributed system?** Distributed tracing with correlation IDs across services is a different design problem than single-service logging.
- **Are there regulatory constraints on what can be logged?** (GDPR, HIPAA, PCI-DSS — these restrict logging of personal data, health data, card data)
- **What is the alerting model?** Knowing which log levels will trigger alerts changes what should be logged at Warning vs Error vs Critical.

## Process

1. **Read the existing telemetry setup.** Read the `appsettings.json` logging sections, the logger/OpenTelemetry registration in `Program.cs`, and any Serilog/NLog config. State what exists in one line at the top of the output — the design extends it or explicitly justifies replacing it.
2. **Identify observability goals.** What questions must operators be able to answer from the telemetry? (Is the service healthy? What went wrong? How long did this take? Who did this?)
3. **Design the log level strategy.** Define what belongs at each level: Trace, Debug, Information, Warning, Error, Critical. The common failure is over-logging at Information and never using Warning — assign each level a one-line admission rule.
4. **Define structured logging fields.** What fields appear on every log entry (correlation ID, user ID, tenant ID, operation name)? What fields are event-specific?
5. **Design correlation and tracing.** Default to OpenTelemetry with W3C trace context (`traceparent`) — ASP.NET Core and `HttpClient` propagate it automatically, and `Activity.TraceId` gives every log entry a trace link with no custom middleware. Design a hand-rolled `X-Correlation-ID` only when an external system requires a specific header. For each non-HTTP entry point (queue message, scheduled job), define where the trace context is created or restored.
6. **Define health checks.** What liveness and readiness checks are needed? What dependencies (database, external APIs, message queues) must be checked? What is the expected response shape?
7. **Define metrics.** What counters, gauges, and histograms matter? (Request count, error rate, queue depth, processing duration.) Default mechanism: `System.Diagnostics.Metrics.Meter` instruments exported via OpenTelemetry; use a platform SDK directly only when the platform requires it. Use the existing infrastructure's naming conventions.
8. **Define the PII and secrets exclusion list.** Explicit list of data types that must never appear in logs, regardless of log level.
9. **Choose logging sinks and format.** Structured JSON to stdout (for containerised deployments), direct sink to monitoring platform, or both. Define the format that the monitoring platform expects.

## Output Format

### Observability Design: [Service Name]

**Application Type:** [API / Background Service / Worker / Mixed]
**Deployment Target:** [Docker/Kubernetes / Azure App Service / On-prem / etc.]
**Monitoring Platform:** [Application Insights / Seq / ELK / Datadog / stdout only]

#### Log Level Strategy

| Level | Use For | Example |
|-------|---------|---------|
| Trace | Internal flow details, loop iterations | "Processing item 3 of 12" |
| Debug | Diagnostic detail needed during development | "Cache miss for key {Key}" |
| Information | Significant application events | "Order {OrderId} created" |
| Warning | Unexpected but recoverable situations | "Retry attempt {Attempt} for {Operation}" |
| Error | Failures that affect a specific operation | "Payment failed for order {OrderId}: {Reason}" |
| Critical | Service-level failures requiring immediate attention | "Database connection pool exhausted" |

#### Structured Log Fields

**Always present (every log entry):**

| Field | Type | Source | Notes |
|-------|------|--------|-------|
| `CorrelationId` | string | HTTP header / generated | Propagated through all calls |
| `UserId` | string | Claims principal | Omit for unauthenticated requests |
| `TenantId` | string | Claims principal | Only if multi-tenant |
| `Application` | string | Config / env var | Service name |
| `Environment` | string | Config / env var | dev / staging / prod |

**Event-specific fields:** Defined per-operation. Use consistent names across similar events (`OrderId`, not `orderId` in one place and `order_id` in another).

#### Correlation ID Design

| Entry Point | ID Source | Propagation | Outbound Header |
|-------------|-----------|-------------|-----------------|
| HTTP request | `X-Correlation-ID` header, generate if absent | `IHttpContextAccessor` | `X-Correlation-ID` response header |
| Queue message | Message property, generate if absent | Passed through handler context | Added to any outbound messages |
| Scheduled job | Generated at job start | Scoped to job execution | N/A |

**Default:** with OpenTelemetry, the HTTP rows are handled by automatic W3C `traceparent` propagation — design custom correlation headers only for systems that cannot accept trace context. Non-HTTP entry points still need explicit design: start a new `Activity` (restoring the parent context from the message where available) so queue and job telemetry joins the same trace.

#### Health Checks

**Liveness check** (`/health/live`): Confirms the process is running. No dependency checks. Returns 200 if alive.

**Readiness check** (`/health/ready`): Confirms dependencies are available. Returns 200 only when all pass.

| Check | Dependency | Failure Impact | Timeout |
|-------|------------|---------------|---------|
| Database | SQL Server / PostgreSQL | Readiness fails | 5s |
| Message broker | RabbitMQ / Azure Service Bus | Readiness fails | 3s |
| External API | [Name] | Degraded (not fail) | 2s |

#### Metrics

| Metric | Type | Labels | Purpose |
|--------|------|--------|---------|
| `http_requests_total` | Counter | `method`, `route`, `status` | Request volume and error rate |
| `http_request_duration_seconds` | Histogram | `method`, `route` | Latency percentiles |
| `queue_messages_processed_total` | Counter | `queue`, `result` | Processing throughput |
| `active_database_connections` | Gauge | — | Connection pool health |

#### PII and Secrets Exclusion List

The following must **never** appear in log output at any level:

- Passwords, API keys, tokens, connection strings
- Full credit card numbers, CVV values
- National identity numbers, tax IDs
- Full email addresses (log first character + domain only if needed for debugging)
- Full names combined with other identifiers
- Request/response bodies unless explicitly reviewed for sensitivity

**Implementation note:** Use destructured logging with allowlists for complex objects, not `{@object}` on untrusted data.

#### Logging Sink and Format

- **Format:** Structured JSON
- **Sink:** [stdout for container platform / Application Insights SDK / Seq HTTP sink]
- **Minimum level in production:** Information (Debug and Trace off)
- **Minimum level in development:** Debug

## Quality Bar

- Log levels have clear definitions — Warning is not a weaker Error.
- Correlation/trace context is designed for every entry point, not just HTTP.
- Tracing and metrics use OpenTelemetry and W3C trace context unless a stated constraint forbids it.
- Health checks distinguish liveness from readiness.
- The PII exclusion list is explicit and reviewed for the application's data model.
- Metrics cover the four signals relevant to this service type (request rate, error rate, latency, saturation).
- The design fits the existing monitoring infrastructure — not a greenfield design dropped on an existing platform.

## Failure Modes To Avoid

- Logging everything at Information level (produces noise that hides real signals).
- Logging PII or secrets at Debug/Trace "because it's not production" — it will reach production eventually.
- Designing correlation IDs for HTTP only, leaving queue and scheduled jobs with no traceability.
- Health checks that always return 200 (liveness-only with no readiness check).
- Specifying metrics with no labels — unlabelled counters cannot be sliced for diagnosis.
- Recommending a monitoring platform not already in use without justifying the change.
- Treating observability as an afterthought — design it before writing the first handler.
