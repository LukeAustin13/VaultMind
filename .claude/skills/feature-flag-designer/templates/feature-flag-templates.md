# Feature Flag Templates

Code patterns for the flag designs described in `feature-flag-designer` SKILL.md. Consult when producing the Code Location and Configuration sections of the output.

Contents: Microsoft.FeatureManagement (.NET) — appsettings configuration, service registration, usage, attribute-based gating · Frontend (React/TypeScript) · Clean pattern principles. If reading a partial range, this line is the full scope.

---

## Microsoft.FeatureManagement (.NET)

Configuration in `appsettings.json`:
```json
{
  "FeatureManagement": {
    "checkout.newPaymentFlow": false,
    "notifications.emailEnabled": {
      "EnabledFor": [
        { "Name": "Percentage", "Parameters": { "Value": 10 } }
      ]
    }
  }
}
```

Service registration:
```csharp
builder.Services.AddFeatureManagement();
```

Usage:
```csharp
// In a controller or service
if (await _featureManager.IsEnabledAsync("checkout.newPaymentFlow"))
{
    // new path
}
else
{
    // old path
}
```

Attribute-based (controllers):
```csharp
[FeatureGate("checkout.newPaymentFlow")]
public IActionResult NewCheckout() => View();
```

## Frontend (React/TypeScript)

```tsx
const { isEnabled } = useFeatureFlag('checkout.newPaymentFlow');
return isEnabled ? <NewCheckout /> : <LegacyCheckout />;
```

## Clean pattern principles

- Keep flag checks at the boundary (controller, page component), not deep in business logic.
- Avoid nested flag checks — one flag per decision point.
- Never use flag values in database queries or data models — flags are routing decisions, not data.
- Both paths (on and off) must be tested.
