---
name: auth-designer
description: Use this skill when you need to design an authentication and authorisation strategy — OAuth2/OIDC flow selection, JWT claims design, cookie vs bearer token, ASP.NET Core Identity vs custom auth, RBAC vs ABAC, refresh token patterns, and multi-tenant auth. Tuned for ASP.NET Core but principles apply broadly. It does not implement auth (that is implementation work) or review auth for security vulnerabilities (use security-reviewer for that).
license: MIT
metadata:
  stack: dotnet-primary
  version: 1.1
  last-reviewed: 2026-07-03
---

# Auth Designer

## Use When

- The user asks "how should I handle auth?", "what auth flow should I use?", or "how do I add login?"
- A new application or service needs authentication designed from scratch.
- An existing auth setup needs to be redesigned (migrating from cookies to tokens, adding OAuth2, adding multi-tenancy).
- The user needs to decide between ASP.NET Core Identity, a custom auth solution, or an external identity provider.
- Authorisation rules are growing complex and a structured RBAC or ABAC model is needed.

## Do Not Use When

- The task is implementing auth — that is implementation work.
- The task is reviewing existing auth code for security vulnerabilities — use **security-reviewer** or **security-config-reviewer** agent.
- The task is designing the overall backend structure — use **backend-architect** (which references this skill for the auth cross-cutting concern).

## Inputs To Look For

- Who are the callers? (browser users, mobile apps, API-to-API, CLI tools, IoT devices)
- Is there an existing identity provider (Azure AD, Auth0, Keycloak, Google)?
- Is multi-tenancy required?
- What are the session and expiry requirements?
- Is RBAC (role-based) or ABAC (attribute/claim-based) authorisation needed?
- .NET version and any existing auth infrastructure already in place.

## Concepts Reference

See `references/auth-design-concepts.md` for: caller type classification, OAuth2/OIDC flow selection, token format (JWT vs opaque, claims design), cookie vs bearer trade-offs, ASP.NET Core Identity vs custom auth, RBAC vs ABAC with code patterns, refresh token design, and multi-tenant auth approaches.

## Process

1. **Read the current state first.** Read Program.cs/Startup, auth-related packages in the csproj, and any appsettings auth sections. Record what exists before proposing anything — a design that ignores installed auth middleware is wrong by default.
2. **Classify the callers.** Who calls this system? Browser, mobile, service, device?
3. **Select the OAuth2 flow.** Use the flow selection table based on caller type.
4. **Decide on the identity system.** External IdP, ASP.NET Core Identity, or token validation only?
5. **Choose token format.** JWT vs opaque. Define which claims go in the token.
6. **Choose cookie vs bearer.** Based on caller type and XSS/CSRF trade-offs.
7. **Design authorisation model.** RBAC, ABAC, or both. Define roles/policies.
8. **Design refresh token strategy.** Rotation policy, lifetime, revocation approach.
9. **Handle multi-tenancy.** If applicable: tenant claim, separate issuers, or both.
10. **Define logout behaviour.** Clear cookies, revoke tokens, and invalidate sessions.

## Output Format

### Auth Design: [Application or Service Name]

**Caller Types:** [Browser SPA / Mobile / API-to-API / etc.]
**Auth Flow:** [Authorization Code + PKCE / Client Credentials / etc.]
**Identity System:** [ASP.NET Core Identity / External IdP (provider name) / Token validation only]
**Token Format:** JWT / Opaque
**Session Storage:** Cookie (HttpOnly) / Bearer (in-memory) / BFF pattern

---

#### Token Claims

| Claim | Value | Purpose |
|-------|-------|---------|
| `sub` | User ID | Subject identifier |
| `roles` | `["editor"]` | RBAC role assignment |
| `tid` | Tenant ID | Multi-tenant isolation |

**Access token lifetime:** [e.g., 15 minutes]
**Refresh token lifetime:** [e.g., 30 days, rotating]

#### Authorisation Model

**Type:** RBAC / ABAC / Mixed

| Role / Policy | Access Rule |
|---------------|-------------|
| `admin` | Full access to all resources |
| `editor` | Create and update own resources |
| `CanPublish` policy | Requires `editor` or `admin` role |

#### Multi-Tenancy

[How tenant is identified, propagated, and enforced — or "Not applicable"]

#### Registration (Program.cs)

```csharp
// Auth registration snippet
```

#### Security Checklist

- [ ] `ClockSkew` set to a short value (30 seconds or less)
- [ ] `aud` claim validated on every API
- [ ] Refresh tokens use rotation
- [ ] Refresh tokens not stored in localStorage
- [ ] Logout revokes refresh tokens server-side
- [ ] Tenant ID validated against resource on every data access (if multi-tenant)
- [ ] CSRF protection enabled for cookie auth (`SameSite`, anti-forgery tokens)

## Quality Bar

- The design accounts for auth infrastructure already installed — verified by reading Program.cs and packages, not assumed.
- The OAuth2 flow is matched to the actual caller type — not chosen by familiarity.
- JWT claims contain only what is needed; no PII, no large payloads.
- Refresh token rotation and revocation strategy is defined.
- RBAC vs ABAC decision is justified.
- Multi-tenancy enforcement is defined if applicable.
- The security checklist is completed, not skipped.

## Failure Modes To Avoid

- Using the Implicit flow or Resource Owner Password flow — both deprecated.
- Storing tokens in `localStorage` without acknowledging XSS risk.
- Putting sensitive user data in JWT claims — tokens are base64-encoded, not encrypted.
- Omitting `aud` validation — a token issued for one API is valid on another if audience is not checked.
- Issuing long-lived access tokens instead of using refresh tokens — a compromised access token is valid until it expires.
- Not defining a logout flow — tokens with no revocation and no logout leave sessions open indefinitely.
- Skipping tenant ID validation on data queries in a multi-tenant system — tenant A can read tenant B's data.
- Implementing a custom JWT signing or encryption scheme — use established libraries (`Microsoft.IdentityModel.Tokens`).
