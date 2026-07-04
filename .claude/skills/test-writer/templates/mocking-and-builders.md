# Mocking and Test Data Builders

Reference for the test-writer skill. Covers where to draw the mocking boundary and how to construct test data with builders. Generic examples only.

## Mocking Boundary Decision Tree

Start with one question for the code under test:

**Does this code touch an external boundary (HTTP, database, file system, queue, email/SMS)?**

```
Does the code cross an external boundary?
│
├── No  → Use the real thing. Do not mock.
│         (value objects, domain logic, pure functions,
│          in-process computation, validators, mappers)
│
└── Yes → What kind of test are you writing?
          │
          ├── Unit test
          │     → Mock the external boundary.
          │       Keep the unit isolated and fast.
          │
          └── Integration test
                → Use a real DB via Testcontainers.
                  Stub external third-party APIs you do
                  not own (HTTP, email, SMS, payment).
```

### Unit tests

Mock the boundary; use real in-process collaborators.

| Mock these | Use the real thing |
|------------|--------------------|
| `HttpClient` / typed API clients | Value objects (`Money`, `EmailAddress`) |
| `DbContext` / repositories | Domain entities and aggregates |
| File system access | Pure functions and calculations |
| Message queue / bus publishers | Validators and mappers |
| Email / SMS senders | In-memory collections and DTOs |
| Payment / external service gateways | A controllable `IClock` / `IDateTimeProvider` (a real fake, not a mock framework) |

Rule of thumb: one test exercises one real thing. Everything cheap, fast, and side-effect-free stays real. Do not mock what you can construct and use directly without consequence.

### Integration tests

The point of an integration test is to exercise infrastructure you do own against a realistic substitute, while isolating dependencies you do not own.

| Use real (via Testcontainers or in-process host) | Stub or fake |
|--------------------------------------------------|--------------|
| Database (real engine in a container) | Third-party HTTP APIs you do not own |
| EF Core mappings, migrations, queries | Email / SMS / push providers |
| The API host (`WebApplicationFactory`) | Payment gateways |
| Message broker (real container) | Anything billed per call or rate-limited |

Do not mock the database in an integration test. A mocked `DbContext` cannot catch a broken migration, a bad index, a constraint violation, or a query that fails to translate to SQL. Spin up the real engine in a container instead.

See the **integration-test-designer** skill for Testcontainers, fixture, and isolation setup.

### Anti-patterns

- Mocking a value object or pure function instead of constructing it.
- Mocking `DbContext` in a test whose purpose is to verify a query or mapping — that belongs in an integration test against a real database.
- Stubbing a real in-process collaborator and then asserting it was called — that tests the mock, not the code.
- Hitting a live third-party API from any automated test. Stub it.

## Test Data Builder Template (C#)

A builder removes irrelevant setup noise from tests. Each test states only the fields it cares about; everything else gets a safe default. This keeps tests readable and resistant to constructor changes.

```csharp
public sealed class OrderBuilder
{
    private Customer _customer = new("Generic Customer", "customer@example.com");
    private readonly List<OrderLine> _lines = new();

    public OrderBuilder WithCustomer(Customer customer)
    {
        _customer = customer;
        return this;
    }

    public OrderBuilder WithCustomer(string name, string email)
    {
        _customer = new Customer(name, email);
        return this;
    }

    public OrderBuilder WithLine(string sku, int quantity, decimal unitPrice)
    {
        _lines.Add(new OrderLine(sku, quantity, unitPrice));
        return this;
    }

    public OrderBuilder WithLines(params OrderLine[] lines)
    {
        _lines.AddRange(lines);
        return this;
    }

    public Order Build()
    {
        if (_lines.Count == 0)
        {
            _lines.Add(new OrderLine("SKU-001", 1, 9.99m));
        }

        return new Order(_customer, _lines);
    }
}
```

### Usage

```csharp
// Happy path — defaults cover everything irrelevant to this test.
var order = new OrderBuilder().Build();

// Override only what the test is about.
var multiLine = new OrderBuilder()
    .WithCustomer("Acme Co", "orders@example.com")
    .WithLine("SKU-100", 2, 19.99m)
    .WithLine("SKU-200", 1, 49.99m)
    .Build();

// Edge case — empty customer name, exercised in isolation.
var order = new OrderBuilder()
    .WithCustomer("", "customer@example.com")
    .Build();
```

### Builder guidelines

- Provide safe, valid defaults so `new OrderBuilder().Build()` always produces a usable object.
- Return `this` from every `With...` method for fluent chaining.
- Keep one builder per aggregate or DTO; do not build a graph from a single mega-builder.
- Name overrides for the field they set (`WithCustomer`, `WithLines`), not for the test scenario.
- Builders are production-quality code — apply the same standards as the code under test.
