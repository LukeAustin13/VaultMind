# .NET Background Job Patterns Reference

Code patterns for the technologies described in `background-job-designer` SKILL.md. Consult when producing the Registration and Implementation Pattern sections of the output.

Contents: BackgroundService (recurring task) · Channels (in-process queue) · Hangfire · Failure Handling · Concurrency · Graceful Shutdown · Idempotency · Observability. If reading a partial range, this line is the full scope.

---

## BackgroundService (recurring task)

```csharp
public class CleanupService : BackgroundService
{
    private readonly ILogger<CleanupService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public CleanupService(ILogger<CleanupService> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // PeriodicTimer (.NET 6+) — preferred over a Task.Delay loop: no drift
        // from job duration, and cancellation surfaces cleanly on WaitForNextTickAsync
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            await DoCleanupAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task DoCleanupAsync(CancellationToken cancellationToken)
    {
        // Use a new scope per job execution — never inject scoped services into a singleton
        await using var scope = _scopeFactory.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<ICleanupRepository>();
        await repo.DeleteExpiredAsync(cancellationToken);
    }
}
```

**Critical rule:** `BackgroundService` is a singleton. Never inject scoped services (like `DbContext`) directly. Always use `IServiceScopeFactory` and create a new scope per job run.

---

## Channels (In-Process Queue)

```csharp
// Registration
builder.Services.AddSingleton(Channel.CreateBounded<EmailMessage>(
    new BoundedChannelOptions(capacity: 500)
    {
        FullMode = BoundedChannelFullMode.Wait
    }));
builder.Services.AddHostedService<EmailDispatchWorker>();

// Producer (called from API controller or service)
await _channel.Writer.WriteAsync(new EmailMessage(to, subject, body), cancellationToken);

// Consumer (BackgroundService)
public class EmailDispatchWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            await _emailSender.SendAsync(message, stoppingToken);
        }
    }
}
```

---

## Hangfire

```csharp
// Registration (SQL Server storage)
builder.Services.AddHangfire(config =>
    config.UseSqlServerStorage(connectionString));
builder.Services.AddHangfireServer();

// Fire-and-forget
BackgroundJob.Enqueue<IEmailService>(x => x.SendWelcomeEmailAsync(userId));

// Scheduled (delayed)
BackgroundJob.Schedule<IReportService>(
    x => x.GenerateMonthlyReportAsync(month),
    TimeSpan.FromMinutes(5));

// Recurring
RecurringJob.AddOrUpdate<ICleanupService>(
    "nightly-cleanup",
    x => x.RunAsync(),
    Cron.Daily(hour: 2)); // 02:00 every day

// Dashboard (read-only in production)
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new HangfireAuthFilter()]
});
```

**Hangfire method constraints:**
- Job methods must be public.
- Parameters must be serializable (JSON). Avoid passing large objects — pass IDs and load in the job.
- `CancellationToken` parameters are supported.

---

## Failure Handling

### Hangfire automatic retry

```csharp
// Global retry policy: 3 attempts, exponential backoff
GlobalJobFilters.Filters.Add(new AutomaticRetryAttribute { Attempts = 3 });

// Per-job override
[AutomaticRetry(Attempts = 5, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
public async Task ProcessImportAsync(int importId) { }
```

### BackgroundService exception handling

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        try
        {
            await DoWorkAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            break; // Graceful shutdown, not an error
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Background job failed. Retrying after delay.");
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
```

---

## Concurrency

```csharp
// Hangfire server with concurrency limit
builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = 5; // Max concurrent jobs
});

// Hangfire distributed singleton job (prevents concurrent execution across instances)
[DisableConcurrentExecution(timeoutInSeconds: 30)]
public async Task RunSingletonJobAsync() { }
```

---

## Graceful Shutdown

```csharp
// Always propagate the cancellation token
await _repository.ProcessBatchAsync(batch, stoppingToken);

// Configure shutdown timeout (default is 30s)
builder.Services.Configure<HostOptions>(options =>
{
    options.ShutdownTimeout = TimeSpan.FromSeconds(60);
});
```

Do not catch `OperationCanceledException` and continue when `stoppingToken.IsCancellationRequested` is true — this blocks graceful shutdown.

---

## Idempotency

```csharp
public async Task SendOrderConfirmationAsync(int orderId)
{
    var order = await _repository.GetByIdAsync(orderId);
    if (order.ConfirmationEmailSentAt is not null)
        return; // Already done, idempotent exit

    await _emailSender.SendAsync(order.CustomerEmail, ...);
    await _repository.MarkConfirmationSentAsync(orderId);
}
```

---

## Observability

```csharp
// Structured logging for job lifecycle
_logger.LogInformation("Job {JobName} started. JobId={JobId}", nameof(CleanupJob), jobId);
// ... job work ...
_logger.LogInformation("Job {JobName} completed in {DurationMs}ms. JobId={JobId}",
    nameof(CleanupJob), sw.ElapsedMilliseconds, jobId);
```
