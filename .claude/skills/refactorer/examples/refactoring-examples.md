# Refactoring Examples

Two small, behaviour-preserving refactorings. Each runs the existing tests before and after to prove behaviour is unchanged.

## 1. Extract Method + Rename For Clarity

**Goal:** pull a tangled inline block into a named method and rename a cryptic local.

Before:

```csharp
public decimal Total(IEnumerable<Item> items)
{
    decimal t = 0;
    foreach (var i in items)
    {
        var d = i.Qty > 10 ? 0.1m : 0m;
        t += i.Price * i.Qty * (1 - d);
    }
    return t;
}
```

After:

```csharp
public decimal Total(IEnumerable<Item> items)
{
    decimal runningTotal = 0;
    foreach (var item in items)
    {
        runningTotal += LineTotal(item);
    }
    return runningTotal;
}

private static decimal LineTotal(Item item)
{
    var discount = item.Qty > 10 ? 0.1m : 0m;
    return item.Price * item.Qty * (1 - discount);
}
```

**Prove behaviour unchanged** — run the existing tests:

```
$ dotnet test
Passed!  - Failed: 0, Passed: 14, Skipped: 0, Total: 14
```

Green before the change and green after, with no test edits, confirms the extraction and rename preserved behaviour.

## 2. Remove Duplication Via A Shared Guard Clause

**Goal:** two methods repeat the same null-and-empty check. Collapse it into one guard.

Before:

```csharp
public void Register(string email)
{
    if (string.IsNullOrWhiteSpace(email))
        throw new ArgumentException("Email required", nameof(email));
    _store.Add(email);
}

public void Unsubscribe(string email)
{
    if (string.IsNullOrWhiteSpace(email))
        throw new ArgumentException("Email required", nameof(email));
    _store.Remove(email);
}
```

After:

```csharp
public void Register(string email)
{
    RequireEmail(email);
    _store.Add(email);
}

public void Unsubscribe(string email)
{
    RequireEmail(email);
    _store.Remove(email);
}

private static void RequireEmail(string email)
{
    if (string.IsNullOrWhiteSpace(email))
        throw new ArgumentException("Email required", nameof(email));
}
```

**Prove behaviour unchanged** — run the existing tests:

```
$ dotnet test
Passed!  - Failed: 0, Passed: 9, Skipped: 0, Total: 9
```

The same exception type, message, and parameter name are thrown from one place now. A green run confirms both call sites still behave as before.
