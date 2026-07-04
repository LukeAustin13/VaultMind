# Principles Review: Worked Examples

These examples show how findings are written: the code, the violation, the fix, and why the severity was assigned. The last example shows an apparent violation that is left unflagged because it is justified pragmatism.

## Example 1 — SRP (Major)

### Code

```csharp
public class OrderService
{
    public void PlaceOrder(Order order)
    {
        if (order.Lines.Count == 0)
            throw new InvalidOperationException("Order has no lines.");
        if (order.Total <= 0)
            throw new InvalidOperationException("Order total must be positive.");

        _repository.Save(order);

        var body = $"Thanks for your order #{order.Id}. Total: {order.Total:C}.";
        var message = new MailMessage("orders@example.com", order.CustomerEmail)
        {
            Subject = $"Order {order.Id} confirmed",
            Body = body
        };
        using var client = new SmtpClient("smtp.example.com");
        client.Send(message);
    }
}
```

### Violation
`OrderService` has two reasons to change: order validation and persistence rules, and the email confirmation format and transport. A change to the SMTP host or the email body forces an edit to the same class that owns order logic, and the email path cannot be tested without a live SMTP server.

### Fix
Extract the notification concern behind an abstraction the service depends on:

```csharp
public class OrderService
{
    private readonly IOrderRepository _repository;
    private readonly IOrderNotifier _notifier;

    public void PlaceOrder(Order order)
    {
        if (order.Lines.Count == 0)
            throw new InvalidOperationException("Order has no lines.");
        if (order.Total <= 0)
            throw new InvalidOperationException("Order total must be positive.");

        _repository.Save(order);
        _notifier.SendConfirmation(order);
    }
}
```

`OrderConfirmationNotifier : IOrderNotifier` owns the email body and SMTP transport.

### Severity justification
**Major.** This is not theoretical — it blocks unit testing of `PlaceOrder` (no SMTP in tests) and couples order rules to mail infrastructure. Two genuine reasons to change live in one class today.

## Example 2 — DRY (Minor)

### Code

```csharp
public class CustomerValidator
{
    public bool IsRegistrationValid(Registration r)
    {
        if (!r.Email.Contains("@") || !r.Email.Contains("."))
            return false;
        return r.Age >= 18;
    }

    public bool IsContactValid(Contact c)
    {
        if (!c.Email.Contains("@") || !c.Email.Contains("."))
            return false;
        return c.Phone.Length >= 7;
    }
}
```

### Violation
The email format check `!Email.Contains("@") || !Email.Contains(".")` is duplicated across both methods. If the rule changes (for example, to a proper regex), both copies must be found and updated in step, and one is easy to miss.

### Fix
Extract a single method:

```csharp
private static bool IsValidEmail(string email) =>
    email.Contains("@") && email.Contains(".");
```

Both call sites then read `if (!IsValidEmail(r.Email)) return false;`.

### Severity justification
**Minor.** It is a real duplication with a real maintenance cost, but the scope is two methods in one class and the rule is currently trivial. It harms maintainability without causing a defect today, so it sits below Major.

## Example 3 — YAGNI (Nit)

### Code

```csharp
public interface IReportExporter
{
    byte[] ExportPdf(Report report);
    byte[] ExportCsv(Report report);
    byte[] ExportXml(Report report);
}
```

The product exports PDF and CSV. `ExportXml` has no callers anywhere in the solution, and there is no XML export feature on the roadmap.

### Violation
`ExportXml` is speculative surface area. Every implementer of `IReportExporter` must provide it, and readers assume XML export is a supported feature when it is not.

### Fix
Remove `ExportXml` from the interface and any stub implementations. Add it back when an XML export requirement actually exists.

### Severity justification
**Nit.** It is genuinely unused, which clears the bar for raising YAGNI at all, but the practical cost is small — one extra method on a narrow interface. It is worth removing, not worth blocking on.

## Example 4 — Apparent violation, not flagged (justified pragmatism)

### Code

```csharp
public decimal CalculateShipping(Order order)
{
    if (order.Total >= 50m)
        return 0m;
    if (order.IsExpress)
        return 12.50m;
    return 4.99m;
}
```

### Why it looks like a violation
A purist might flag the literals `50m`, `12.50m`, and `4.99m` as magic numbers (a DRY / Naming concern) and propose extracting a `ShippingRules` configuration object, or flag the `if` chain as an OCP issue and propose a strategy pattern.

### Why it is not flagged
Each number appears exactly once, so there is no duplication to remove — extracting constants here adds indirection without removing any. The method is short, the branches read clearly as the business rule itself, and there is no current requirement for configurable or pluggable shipping rules. Introducing a configuration object or strategy pattern would be premature generalisation — a KISS and YAGNI cost paid to satisfy a rule that is not actually being broken. This code is left as-is, and the review notes it as a deliberate non-finding so the reader knows it was considered.
