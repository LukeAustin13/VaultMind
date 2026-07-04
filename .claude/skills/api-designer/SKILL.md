---
name: api-designer
description: Use this skill when you need to design REST/JSON API endpoints, request/response contracts, DTOs, validation rules, status codes, pagination, error responses, and versioning strategy. The api-designer produces a complete API specification that a developer can implement directly. It does not design the backend architecture (use backend-architect) or the database schema (use database-designer).
license: MIT
metadata:
  stack: agnostic
  version: 1.2
  last-reviewed: 2026-07-03
---

# API Designer

## Use When
- Designing new API endpoints for a feature.
- The user asks "what should this API look like?"
- You need to define request/response shapes, status codes, or error formats.
- An existing API needs new endpoints or contract changes.
- You need to design pagination, filtering, or sorting for a list endpoint.

## Do Not Use When
- You are designing the internal service architecture — use **backend-architect**.
- You are designing the database — use **database-designer**.
- You are implementing the API (writing controller code) — that is implementation work.
- You are reviewing an existing API for bugs — use **code-reviewer** or **bug-hunter**.

## Inputs To Look For
- Feature requirements describing what the client needs.
- Existing API conventions in the project (route patterns, naming, error format).
- Authentication model (JWT, API key, cookie).
- Client constraints (mobile app, SPA, third-party integration).
- Existing DTOs or models.

## Clarify Before Starting

If any of the following are unknown, ask before designing:

- **Who consumes this API?** (Internal service, mobile app, SPA, third-party — each has different constraints)
- **Does an existing API convention exist?** (Route style, naming, error format, versioning strategy — new endpoints must match)
- **What is the auth model?** (JWT claims, API keys, cookies — this affects every endpoint)
- **Are there breaking change constraints?** (Is this a new API, or evolving an existing one with live consumers?)

Designing without these leads to contracts that don't fit the consumer or break existing clients.

## Process
1. **Identify the resources.** What nouns does this API expose? Map them to URL paths.
2. **Define endpoints.** For each resource:
   - HTTP method and route.
   - Request body or query parameters.
   - Response body.
   - Status codes for success and failure.
3. **Design DTOs.** Define request and response shapes with field names, types, and whether they are required or optional.
4. **Define validation rules.** For each request field: required/optional, min/max length, format, allowed values.
5. **Design error responses.** Use a consistent error format (RFC 7807 ProblemDetails or the project's existing format).
6. **Handle collections.** For list endpoints: pagination strategy (offset/limit or cursor), filtering, sorting, total count.
7. **Consider versioning.** If the project has a versioning strategy, follow it. If not, recommend one only if breaking changes are likely.
8. **Check consistency.** Ensure naming, casing, and patterns match the rest of the API.
9. **Record the contested decisions.** For every decision that had a real alternative (pagination style, versioning scheme, error shape, ID format), record the choice, the reason, and the rejected option with a one-line why — the rejection list is what lets a future reader reopen the decision when constraints change instead of re-deriving it blind.

## Output Format

### API Design: [Feature Name]

**Base Path:** `/api/v1/[resource]`
**Auth:** [Required / Public / Mixed]

#### Design Decisions

| Decision | Choice | Reason | Rejected (why) |
|----------|--------|--------|----------------|
| Pagination | Cursor-based | High-churn list; offset pages drift under writes | Offset/limit (duplicate/missing rows during concurrent inserts) |
| Versioning | URL segment `/v1` | Matches this API's existing convention | Header versioning (invisible in logs, complicates cache keys) |
| Error shape | RFC 7807 ProblemDetails | Project default, one format for all consumers | Custom envelope (a second format consumers must special-case) |

#### Endpoints

##### `POST /api/v1/orders`
Create a new order.

**Request Body:**
```json
{
  "customerId": "string (required, GUID)",
  "items": [
    {
      "productId": "string (required, GUID)",
      "quantity": "integer (required, min: 1)"
    }
  ],
  "notes": "string (optional, max: 500)"
}
```

**Responses:**
| Status | Condition | Body |
|--------|-----------|------|
| 201 | Created successfully | `{ "id": "guid", "status": "pending", ... }` |
| 400 | Validation failure | ProblemDetails with field errors |
| 401 | Not authenticated | ProblemDetails |
| 409 | Duplicate order | ProblemDetails |

##### `GET /api/v1/orders`
List orders with pagination.

**Query Parameters:**
| Param | Type | Default | Description |
|-------|------|---------|-------------|
| `page` | int | 1 | Page number |
| `pageSize` | int | 20 | Items per page (max: 100) |
| `status` | string | — | Filter by status |
| `sort` | string | `createdAt:desc` | Sort field and direction |

**Response:** `200 OK`
```json
{
  "items": [...],
  "page": 1,
  "pageSize": 20,
  "totalCount": 142
}
```

[Repeat for each endpoint]

#### Error Format
```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Validation Error",
  "status": 400,
  "detail": "One or more fields failed validation.",
  "errors": {
    "customerId": ["Customer ID is required."]
  }
}
```

## Example
- [examples/api-design-example.md](examples/api-design-example.md) — orders API showing cursor-vs-offset pagination, a versioning trade-off, ProblemDetails field-level errors, and what not to expose.

## Quality Bar
- Every endpoint has defined request shape, response shape, and status codes.
- Validation rules are explicit for every request field.
- Error responses use a consistent format.
- List endpoints have pagination defined.
- Naming and casing are consistent across all endpoints.
- The design matches existing API conventions in the project.

## Failure Modes To Avoid
- Designing endpoints around database tables instead of client needs.
- Using vague status codes (200 for everything, 500 for all errors).
- Forgetting to define error responses.
- Inconsistent naming (`orderId` in one endpoint, `order_id` in another).
- Over-designing: adding versioning, HATEOAS, and hypermedia to a simple internal API.
- Exposing internal IDs or implementation details in the contract.
- Forgetting pagination on list endpoints.
