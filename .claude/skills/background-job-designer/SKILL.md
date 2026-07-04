---
name: background-job-designer
description: Use this skill when you need to design background processing — recurring jobs, triggered tasks, long-running workers, or in-process queues. Covers technology selection (IHostedService, BackgroundService, Hangfire, Channels), job durability, retry and failure handling, concurrency, graceful shutdown, and observability. Tuned for .NET. It does not implement the jobs (that is implementation work) or design the overall backend (use backend-architect).
license: MIT
metadata:
  stack: dotnet
  version: 1.2
  last-reviewed: 2026-06-11
---

# Background Job Designer

## Use When

- The user asks "how do I run something on a schedule?", "how do I process this in the background?", or "should I use Hangfire?"
- A feature requires work that should not block the HTTP request (email sending, report generation, data sync).
- Recurring jobs are needed (nightly cleanup, hourly sync, scheduled exports).
- A queue of work items needs to be processed asynchronously.
- Fire-and-forget tasks need retry and failure handling.

## Do Not Use When

- The task is implementing the jobs — that is implementation work.
- The task is designing the overall backend structure — use **backend-architect**.
- The task is CI/CD pipeline scheduling — use **devops-deploy**.
- The task is designing observability for jobs — use **observability-designer** (after this skill defines what to observe).

## Inputs To Look For

- What triggers the job? (HTTP request, schedule, event, queue message, user action)
- Does the job need to survive application restart? (durability requirement)
- What is the retry requirement if the job fails?
- Does the job need to run on a schedule (cron-like)?
- How many concurrent workers are needed?
- Does the job state need to be visible to users (progress, status)?

---

## Job Type Classification

Classify the job before selecting technology.

| Type | Description | Example |
|------|-------------|---------|
| **Fire-and-forget** | Triggered once, no result needed, non-critical | Send welcome email, log event, notify webhook |
| **Recurring / scheduled** | Runs on a fixed schedule regardless of triggers | Nightly cleanup, hourly data sync, daily report |
| **Triggered background task** | Started by a user action or event, runs independently | Generate PDF export, process uploaded file |
| **Long-running worker** | Continuously running, processing a stream or queue | Message consumer, outbox processor, polling worker |
| **Chained / workflow** | Multiple steps in sequence, each step may fail independently | Multi-stage import pipeline, order fulfilment workflow |

---

## Technology Selection

| Technology | Durability | Scheduling | Retry UI | Distributed | When to Use |
|-----------|-----------|-----------|----------|-------------|-------------|
| `IHostedService` | None (in-memory only) | Manual (timers) | None | No | Simple recurring tasks, startup/shutdown hooks |
| `BackgroundService` | None (in-memory only) | Manual (loops + delays) | None | No | Long-running workers, queue consumers |
| `System.Threading.Channels` | None (in-memory only) | None — producer-driven | None | No | In-process producer/consumer queue, low-latency fire-and-forget |
| **Hangfire** (open source) | Yes (SQL Server, Redis) | Cron expressions | Dashboard UI | Yes (multi-server) | Durable jobs, scheduled jobs, retries, job visibility |
| **Hangfire Pro** | Yes | Yes | Advanced dashboard | Yes | High-throughput, batches, continuations |
| **Quartz.NET** | Optional (persistent job store) | Cron expressions, calendars | None (no dashboard) | Yes (clustering) | Complex scheduling (calendars, misfire policies) when no dashboard is needed |
| **MassTransit + broker** | Yes (broker-level) | Yes (Quartz.NET integration) | Broker-level | Yes | Event-driven, message bus, saga workflows |

**Hangfire vs Quartz.NET:** default to Hangfire — the dashboard and retry visibility cover the most common operational need. Choose Quartz.NET when scheduling itself is the hard part (calendars, exclusion dates, misfire handling) and job visibility is handled elsewhere.

### Decision Tree

```
Does the job need to survive application restart?
├── No → Is it a fire-and-forget triggered from HTTP?
│   ├── Yes, low volume → Channels + BackgroundService
│   └── Yes, higher volume or retry needed → Hangfire (even in-memory)
└── Yes (durable)
    ├── Does it need a schedule (cron)?
    │   ├── Yes → Hangfire recurring jobs
    │   └── No → Hangfire fire-and-forget or background
    ├── Is it a long-running queue consumer?
    │   ├── Yes, in-process queue → Channels
    │   └── Yes, external broker → MassTransit / Azure Service Bus / RabbitMQ
    └── Does it need distributed execution (multiple app instances)?
        ├── Yes → Hangfire (with distributed lock storage)
        └── No → BackgroundService is sufficient
```

---

## Key Design Rules

**BackgroundService:** Singleton. Never inject scoped services (`DbContext`) directly — use `IServiceScopeFactory` and create a new scope per job run. Always propagate `CancellationToken`.

**Channels:** In-process only. Not durable. Bounded channels with `Wait` mode prevent unbounded queue growth.

**Hangfire:** Job methods must be public and parameters must be JSON-serialisable. Pass IDs, not large objects. Expose the dashboard with auth in production.

**Idempotency:** Any retryable job must be idempotent — check if the work is already done before executing. Use database upsert or a completion flag within the same transaction as the job's side effects.

**Graceful shutdown:** All workers must respect the `CancellationToken`. Do not catch `OperationCanceledException` and continue when `stoppingToken.IsCancellationRequested` is true.

**Dead letter policy:** Define what happens to jobs that exhaust all retries — log and alert, move to dead letter table, or send to monitoring.

See `references/dotnet-job-patterns.md` for code patterns for each technology.

---

## Concurrency

| Scenario | Approach |
|----------|----------|
| Single concurrent execution | `BackgroundService` default (one loop) |
| Limited concurrency | `SemaphoreSlim` inside the worker or Hangfire worker count |
| N parallel workers | `Parallel.ForEachAsync` or multiple `BackgroundService` registrations |
| Distributed single execution | Hangfire `[DisableConcurrentExecution]` attribute |

---

## Observability

Define what to measure for each job type.

| Metric | How | Alert Threshold |
|--------|-----|----------------|
| Job duration | Stopwatch + structured log | > P99 baseline |
| Job failure rate | Hangfire failed queue count / log ERROR count | > N failures in window |
| Queue depth | Channel.Reader.Count or Hangfire enqueued count | > capacity threshold |
| Last successful run | Log timestamp of last success | No success in > 2× expected interval |

See `references/dotnet-job-patterns.md` for structured logging patterns.

---

## Process

1. **Classify the job type.** Fire-and-forget, recurring, triggered, long-running worker, or chained workflow.
2. **Assess durability requirement.** Does the job need to survive app restart? If yes, Hangfire is the minimum choice.
3. **Select technology.** Apply the decision tree.
4. **Define concurrency.** Single execution, limited parallel, or distributed singleton?
5. **Define the retry and failure policy.** How many retries? What is the backoff? What happens at max retries?
6. **Define idempotency.** How does the job detect it has already run? How does it avoid double-execution?
7. **Define graceful shutdown behaviour.** What happens to in-progress jobs on shutdown?
8. **Define observability.** What metrics and logs identify a healthy job from a failing one?

---

## Output Format

### Background Job Design: [Feature or Job Name]

**Job Type:** Fire-and-forget / Recurring / Triggered / Long-running worker
**Trigger:** HTTP request / Schedule (`0 2 * * *`) / Event / Queue message
**Technology:** IHostedService / BackgroundService / Channels / Hangfire

---

#### Job Specification

| Property | Value |
|----------|-------|
| Durability | Durable (survives restart) / In-memory only |
| Concurrency | Single / Max N workers / Distributed singleton |
| Retry | N attempts, exponential backoff / No retry |
| On failure (max retries) | Log + alert / Dead letter / Requeue manually |
| Idempotent | Yes — [how] / No — [why safe] |
| Graceful shutdown | Completes current item then stops / Checkpoints every N items |

#### Schedule (if recurring)

`[Cron expression]` — [Human-readable description, e.g., "02:00 every day"]

#### Observability

| Signal | Log / Metric | Alert |
|--------|-------------|-------|
| Job started | `LogInformation` with job ID | — |
| Job completed | `LogInformation` with duration | Duration > threshold |
| Job failed | `LogError` with exception | Any failure |
| Queue depth | `Channel.Reader.Count` log | > N |

#### Registration

```csharp
// Program.cs snippet
```

#### Implementation Pattern

```csharp
// Skeleton for the job class
```

---

## Quality Bar

- Technology is chosen based on durability and scheduling requirements, not familiarity.
- Every job that can be retried is idempotent.
- `CancellationToken` is propagated through all async calls in the job.
- Scoped services (`DbContext`) are never injected into singleton hosted services — `IServiceScopeFactory` is used.
- Retry and failure policies are defined — not left to defaults.
- Observability signals are defined per job.

## Failure Modes To Avoid

- Using `BackgroundService` for a job that must survive app restart — it is in-memory only.
- Injecting `DbContext` directly into a `BackgroundService` — it is a scoped service in a singleton context.
- No idempotency on a retryable job — duplicate execution causes data corruption or double-sending.
- Catching `OperationCanceledException` and ignoring `stoppingToken.IsCancellationRequested` — blocks graceful shutdown.
- Passing large objects as Hangfire job arguments — they are serialised to JSON and stored in the database.
- Running Hangfire jobs without a dashboard or alerting on the failed queue — failures become invisible.
- Using Channels for a job that must survive restart — messages are lost on shutdown.
- No concurrency limit on distributed jobs — multiple app instances can execute the same job simultaneously.

## References

- `references/dotnet-job-patterns.md` — BackgroundService, Channels, Hangfire, failure handling, concurrency, graceful shutdown, idempotency, and observability code patterns.
