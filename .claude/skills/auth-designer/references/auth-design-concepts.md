# Auth Design Concepts Reference

Consult this file when you need decision tables, claim rules, or code patterns during `auth-designer` process steps.

Contents: Caller Type Classification · OAuth2/OIDC Flow Selection · Token Format (JWT vs Opaque, Claims Design) · Cookie vs Bearer · ASP.NET Core Identity vs Custom · RBAC vs ABAC · Refresh Tokens · Multi-Tenant Auth. If reading a partial range, this line is the full scope.

---

## Caller Type Classification

The caller type determines the auth flow. Identify it first.

| Caller | Description | Recommended Flow |
|--------|-------------|-----------------|
| **Browser (server-rendered)** | Traditional web app, Blazor Server, MVC/Razor Pages | Cookie auth with CSRF protection |
| **Browser (SPA)** | React, Angular, Blazor WASM consuming an API | Authorization Code + PKCE via BFF or direct |
| **Mobile app** | iOS, Android, MAUI | Authorization Code + PKCE |
| **API-to-API** (trusted) | Backend service calling another backend service | Client Credentials flow |
| **CLI / device** | Long-running daemon, IoT device, CLI tool | Device Authorization flow or Client Credentials |
| **Multiple callers** | API consumed by browsers, mobile, and services | Mix: OIDC for human callers, Client Credentials for services |

---

## OAuth2 / OIDC Flow Selection

| Flow | RFC | When to Use | When Not to Use |
|------|-----|-------------|----------------|
| **Authorization Code + PKCE** | RFC 7636 | Public clients (SPA, mobile) where a client secret cannot be kept safe | Server-to-server where secret is safe |
| **Authorization Code (confidential client)** | RFC 6749 | Server-rendered web apps with a secure back-channel | Public clients (no server side) |
| **Client Credentials** | RFC 6749 | Machine-to-machine: service accounts, background jobs, internal APIs | Any human-interactive flow |
| **Device Authorization** | RFC 8628 | Devices without a browser (TV, IoT, CLI) | Any context where a browser is available |
| **Implicit** | RFC 6749 | — | Deprecated. Do not use. Access token exposed in URL fragment. |
| **Resource Owner Password** | RFC 6749 | — | Deprecated except for highly trusted first-party apps with no other option. |

**BFF pattern (Backend for Frontend):** When a SPA needs auth, prefer routing auth through a server-side BFF rather than handling tokens in JavaScript. The BFF holds the client secret and exchanges tokens; the SPA uses cookies with the BFF. Avoids token exposure in browser storage.

---

## Token Format

### JWT vs Opaque Tokens

| | JWT (self-contained) | Opaque token |
|---|---|---|
| **Validation** | Verified locally with public key — no round-trip | Must call introspection endpoint |
| **Revocation** | Cannot revoke before expiry without a denylist | Revocable immediately at the issuer |
| **Contents** | Claims embedded in token | No data — reference to server-side session |
| **Best for** | Stateless APIs, microservices | When immediate revocation is required |

**Default:** JWT for stateless APIs. Opaque for scenarios where immediate revocation is a hard requirement (e.g., financial, high-security).

### JWT Claims Design

Claims to include:
- `sub` — subject (user ID, service ID). Always present.
- `iss` — issuer. Always present.
- `aud` — intended audience. Always present. Validate on the API side.
- `exp` — expiry. Always present.
- `iat` — issued at. Always present.
- `jti` — JWT ID. Include if replay prevention or revocation denylist is needed.
- Roles or permissions (for RBAC/ABAC — see below).
- `tid` — tenant ID (for multi-tenant — see below).

Claims to exclude:
- Passwords, secrets, or sensitive PII.
- Anything that changes frequently (profile photo URL, display name — these belong in a userinfo endpoint, not a token).
- Large data sets — tokens should be small. Target < 4KB.

```json
{
  "sub": "user-123",
  "iss": "https://auth.example.com",
  "aud": "api.example.com",
  "exp": 1700000000,
  "iat": 1699996400,
  "roles": ["editor", "viewer"],
  "tid": "tenant-abc"
}
```

---

## Cookie vs Bearer Token

| | Cookie Auth | Bearer Token |
|---|---|---|
| **Storage** | HttpOnly cookie — inaccessible to JS | localStorage / sessionStorage / memory |
| **CSRF** | Requires CSRF protection (`ValidateAntiForgeryToken`, `SameSite`) | No CSRF risk (not sent automatically) |
| **XSS** | HttpOnly cookie safe from XSS | Token in JS storage vulnerable to XSS |
| **Cross-origin** | Restricted by SameSite policy | Works across origins (CORS) |
| **Best for** | Server-rendered apps, Blazor Server, BFF pattern | Pure API consumption, mobile, SPA with token management |

**Guidance:**
- For server-rendered apps: use cookies with `SameSite=Strict` or `Lax` and anti-forgery tokens.
- For SPAs: prefer the BFF pattern (cookie to BFF, BFF holds the bearer token).
- Never store bearer tokens in `localStorage` — it is XSS-accessible.
- If you must store tokens in a SPA without a BFF: use in-memory storage and refresh on page load.

---

## ASP.NET Core Identity vs Custom Auth

| Approach | When to Use | When Not to Use |
|----------|-------------|----------------|
| **ASP.NET Core Identity** | You need user management (registration, password reset, email confirmation, lockout, 2FA) and do not have an external IdP | You already have an external identity provider |
| **External IdP (Azure AD, Auth0, Keycloak)** | Enterprise SSO, existing tenant directory, or you want to offload user management entirely | Small apps where the IdP adds operational overhead |
| **Custom JWT validation** | You receive tokens from an existing issuer and only need to validate and extract claims | You need user management features — do not build your own |
| **API Key** | Simple machine-to-machine with a small, known set of clients; not human auth | Human auth, or when key rotation and revocation need to be robust |

**Rule:** Do not build your own identity system. If users need registration and password management, use ASP.NET Core Identity. If they authenticate via a corporate directory or social login, use an external IdP. Custom auth means validating tokens someone else issued.

```csharp
// JWT Bearer validation
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "https://auth.example.com";
        options.Audience = "api.example.com";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
```

---

## RBAC vs ABAC

### RBAC — Role-Based Access Control

Users are assigned roles. Permissions are defined per role.

- Simple to implement and reason about.
- Roles are coarse-grained. Use when: a small, stable set of roles covers all access scenarios.
- In ASP.NET Core: `[Authorize(Roles = "admin,editor")]` or `policy.RequireRole("admin")`.

```csharp
// Policy-based RBAC
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CanPublish", policy =>
        policy.RequireRole("editor", "admin"));
});
```

### ABAC — Attribute/Claim-Based Access Control

Access decisions based on claims on the user, resource, or environment.

- Fine-grained. Use when: access depends on attributes (e.g., "owner of this resource", "user in same tenant", "subscription tier includes this feature").
- In ASP.NET Core: custom `IAuthorizationRequirement` + `AuthorizationHandler<T>`.

```csharp
// Custom requirement + handler
public class ResourceOwnerRequirement : IAuthorizationRequirement { }

public class ResourceOwnerHandler : AuthorizationHandler<ResourceOwnerRequirement, Order>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ResourceOwnerRequirement requirement,
        Order resource)
    {
        if (context.User.FindFirst("sub")?.Value == resource.OwnerId)
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
```

**Decision rule:** Start with RBAC. Add ABAC when role-based rules cannot express the required access logic (ownership, tenancy, feature entitlements).

---

## Refresh Tokens

Refresh tokens allow access tokens to be renewed without re-authentication.

**Design decisions:**
- **Rotation:** Issue a new refresh token on every use; invalidate the old one. This is the current best practice. An old refresh token being used signals possible token theft.
- **Absolute lifetime:** Refresh tokens must have a maximum lifetime. After that, the user must re-authenticate.
- **Storage:** Never store refresh tokens in `localStorage`. Use HttpOnly cookies (for web) or secure storage (for mobile).
- **Revocation:** Store a reference to valid refresh tokens (a denylist or allowlist). On logout, invalidate the token server-side.

```
Access token:  short-lived (5–15 minutes)
Refresh token: medium-lived (1–30 days, with rotation)
Max session:   absolute limit (e.g., 90 days — requires re-login)
```

---

## Multi-Tenant Auth

| Approach | How It Works | When to Use |
|----------|-------------|-------------|
| **Tenant claim in token** | `tid` claim identifies the tenant. All users in one identity pool. | Simpler ops. Tenant is a logical concept only. |
| **Separate issuers per tenant** | Each tenant has its own identity provider (Azure AD tenant, separate Auth0 tenant). | Enterprise SSO where tenants manage their own identity. |
| **Subdomain routing** | Tenant identified by subdomain (`tenant.app.com`). Combined with any of the above. | When tenant isolation is needed at the routing layer. |

**Tenant ID propagation:**
- Include `tid` (tenant ID) in every JWT.
- All data queries must filter by tenant ID — this is the primary multi-tenancy control.
- Validate that the authenticated user's `tid` matches the resource's tenant before any data access.

```csharp
// Extract tenant ID in middleware or base controller
var tenantId = User.FindFirst("tid")?.Value
    ?? throw new UnauthorizedAccessException("No tenant claim.");
```
