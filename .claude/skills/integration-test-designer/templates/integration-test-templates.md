# Integration Test Templates

Reusable C#/xUnit scaffolding for integration tests. Copy a template, rename the
generic types, and adapt the replacements to your project. All data here is
deliberately generic.

---

## 1. WebApplicationFactory Test Host

A single test host that boots the real application pipeline and swaps
infrastructure for test-controlled implementations. Point the connection string
at a fixture-owned container so the host and the data isolation share one source
of truth.

```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

public sealed class TestAppFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public TestAppFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Replace the production DbContext registration with the test connection string.
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(_connectionString));

            // Replace outbound HTTP clients with deterministic test doubles.
            services.RemoveAll<IPaymentClient>();
            services.AddSingleton<IPaymentClient, FakePaymentClient>();

            // Replace side-effecting services with no-ops.
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender, NoOpEmailSender>();
        });
    }
}
```

`RemoveAll<T>` lives in `Microsoft.Extensions.DependencyInjection.Extensions`.
Register the replacement only after removing the original, or both descriptors
remain and resolution becomes order-dependent.

Wire a typed `HttpClient` from the factory in each test:

```csharp
public class CreateOrderTests : IClassFixture<DatabaseFixture>
{
    private readonly HttpClient _client;

    public CreateOrderTests(DatabaseFixture fixture)
    {
        var factory = new TestAppFactory(fixture.ConnectionString);
        _client = factory.CreateClient();
    }
}
```

---

## 2. DatabaseFixture

### 2a. With Testcontainers

Starts one real database engine for the whole collection, applies migrations
once, and exposes the connection string. Preferred when CI has Docker.

```csharp
using Testcontainers.PostgreSql;
using Xunit;

public sealed class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("app_test")
        .WithUsername("test_user")
        .WithPassword("test_password")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new AppDbContext(options);
    }
}
```

Share it across a collection so the container starts once:

```csharp
[CollectionDefinition("Database")]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture> { }
```

### 2b. Without Testcontainers (external test database)

For environments where Docker is unavailable, point at a pre-provisioned test
database read from configuration. The fixture surface stays identical so tests
do not change.

```csharp
using Microsoft.Extensions.Configuration;
using Xunit;

public sealed class DatabaseFixture : IAsyncLifetime
{
    public string ConnectionString { get; }

    public DatabaseFixture()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.Testing.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        ConnectionString = config.GetConnectionString("TestDatabase")
            ?? throw new InvalidOperationException(
                "Set ConnectionStrings__TestDatabase for the test database.");
    }

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
```

The external database is shared infrastructure, so isolation between tests
becomes mandatory — see the decision tree below.

---

## 3. Test Data Builder

A fluent builder keeps fixture data out of every test and makes the relevant
field of each test obvious. Default everything to a valid baseline; let each
test override only what it asserts on.

```csharp
public sealed class OrderBuilder
{
    private string _customerId = "customer-001";
    private OrderStatus _status = OrderStatus.Pending;
    private readonly List<OrderLine> _lines = new();

    public OrderBuilder ForCustomer(string customerId)
    {
        _customerId = customerId;
        return this;
    }

    public OrderBuilder WithStatus(OrderStatus status)
    {
        _status = status;
        return this;
    }

    public OrderBuilder WithLine(string sku, int quantity)
    {
        _lines.Add(new OrderLine(sku, quantity));
        return this;
    }

    public Order Build()
    {
        if (_lines.Count == 0)
        {
            _lines.Add(new OrderLine("sku-default", 1));
        }
        return new Order(_customerId, _status, _lines);
    }

    public async Task<Order> PersistAsync(AppDbContext db)
    {
        var order = Build();
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order;
    }
}
```

Usage reads as the test's intent:

```csharp
var order = await new OrderBuilder()
    .ForCustomer("customer-042")
    .WithStatus(OrderStatus.Paid)
    .WithLine("sku-101", 2)
    .PersistAsync(db);
```

---

## 4. Isolation Strategy Decision Tree

Pick one isolation strategy per suite and apply it consistently. The choice
depends on whether tests commit, which engine is in use, and whether the
database is shared infrastructure.

```
Start: how is the database provisioned?
│
├─ Fresh container per test class is affordable (small suite, fast image)?
│     └─ Yes → FRESH CONTAINER per class.
│              Maximum isolation, no reset logic, slowest. Use sparingly.
│
└─ Sharing one database across tests (the common case):
      │
      ├─ Do any tests commit (background jobs, nested scopes, raw SQL COMMIT,
      │  code that opens its own connection)?
      │     │
      │     ├─ No  → TRANSACTION ROLLBACK.
      │     │        Open a transaction in the fixture, run the test, roll back.
      │     │        Fastest; nothing reaches other tests. Breaks the moment a
      │     │        test commits on its own connection.
      │     │
      │     └─ Yes → RESPAWN (table reset).
      │              Reset committed state between tests, respecting FK order.
      │              Works regardless of who commits. Slower than rollback.
      │
      └─ Engine without reliable Respawn support or heavy schema churn?
            └─ Recreate schema (migrate down/up or drop-create) between classes.
               Reliable, slow; reserve for when Respawn cannot be used.
```

### Transaction rollback (no test commits)

```csharp
public abstract class TransactionalTest : IAsyncLifetime
{
    protected AppDbContext Db = null!;
    private IDbContextTransaction _transaction = null!;
    private readonly DatabaseFixture _fixture;

    protected TransactionalTest(DatabaseFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;
        Db = new AppDbContext(options);
        _transaction = await Db.Database.BeginTransactionAsync();
    }

    public async Task DisposeAsync()
    {
        await _transaction.RollbackAsync();
        await Db.DisposeAsync();
    }
}
```

### Respawn (tests commit)

```csharp
using Respawn;

public sealed class DatabaseFixture : IAsyncLifetime
{
    private Respawner _respawner = null!;

    // ... container/connection setup as above ...

    public async Task ResetAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await _respawner.ResetAsync(connection);
    }

    private async Task InitRespawnerAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            TablesToIgnore = new Respawn.Graph.Table[] { "__EFMigrationsHistory" }
        });
    }
}
```

Call `ResetAsync` before or after each test (a base class or xUnit fixture
hook), so committed rows never leak between tests.
