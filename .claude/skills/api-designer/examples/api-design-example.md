# Example: Orders API Design

A worked example showing the decisions the api-designer should make explicit: pagination strategy, versioning, field-level validation errors, and contract hygiene. Domain is a generic order-management service. All data is fake.

## API Design: Order Listing And Retrieval

**Base Path:** `/api/v1/orders`
**Auth:** Required (bearer token)

### `GET /api/v1/orders`

List the authenticated customer's orders, newest first.

**Query Parameters:**
| Param | Type | Default | Description |
|-------|------|---------|-------------|
| `cursor` | string | — | Opaque token from the previous page's `nextCursor`. Omit for the first page. |
| `limit` | int | 20 | Items per page (max: 100). |
| `status` | string | — | Filter by status: `pending`, `shipped`, `delivered`, `cancelled`. |

**Response:** `200 OK`
```json
{
  "items": [
    {
      "orderNumber": "ORD-2026-0001",
      "status": "shipped",
      "total": { "amount": 4250, "currency": "USD" },
      "placedAt": "2026-06-20T14:03:11Z"
    }
  ],
  "nextCursor": "eyJwbGFjZWRBdCI6IjIwMjYtMDYtMjAifQ",
  "hasMore": true
}
```

## Pagination: Cursor vs Offset

**Decision: cursor-based.**

| Concern | Offset (`page`/`pageSize`) | Cursor (chosen) |
|---------|----------------------------|-----------------|
| New rows during paging | Items shift; rows skipped or duplicated across pages | Stable; cursor anchors to a sort position |
| Deep pages | `OFFSET 10000` scans and discards rows; slow | Seeks directly via indexed key; constant cost |
| Jump to arbitrary page | Supported | Not supported (next/prev only) |

Orders are written continuously and the common access pattern is "scroll my recent orders," not "jump to page 47." Offset paging would let a new order push every item down a slot between requests, causing the client to see a duplicate or miss one. The cursor encodes the sort key (`placedAt` plus `orderNumber` as a tiebreaker) so paging stays stable under writes and deep pages stay cheap. We accept losing arbitrary page-jumping because no consumer needs it. If a future admin console needs total counts and page numbers, that is a separate offset-based endpoint, not a change to this one.

## Versioning

**Decision: URI path versioning (`/api/v1/...`).**

Trade-off: path versioning is the most visible and the easiest to route, cache, and document, but it leaks the version into every URL and a `v2` means consumers rewrite paths rather than flipping a header. The alternative, a header like `Accept: application/vnd.orders.v2+json`, keeps URLs clean but is invisible in logs and browser testing and is routinely dropped by intermediaries. For an API with external consumers who pin to a version and upgrade deliberately, the visibility of path versioning is worth the URL noise. Additive, non-breaking changes (new optional fields, new endpoints) ship within `v1`; only a breaking change cuts a `v2`.

## Error Response: Field-Level Validation

`POST /api/v1/orders` with invalid input returns `400` using ProblemDetails (RFC 7807) with an `errors` map keyed by field path. Each field maps to an array so multiple failures on one field surface together.

```json
{
  "type": "https://example.com/problems/validation-error",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "detail": "The request did not pass validation.",
  "instance": "/api/v1/orders",
  "errors": {
    "items": ["At least one item is required."],
    "items[0].quantity": ["Quantity must be at least 1."],
    "shippingAddress.postalCode": [
      "Postal code is required.",
      "Postal code format is invalid for the selected country."
    ]
  }
}
```

Notes:
- Field keys use the same casing and nesting as the request body so the client can bind errors back to form fields.
- The top-level `title`/`detail` stay generic; specifics live in `errors`. Do not put a raw exception message in `detail`.

## What NOT To Expose

The contract above deliberately omits internal detail:

- **No database primary keys.** The client sees `orderNumber` (`ORD-2026-0001`), a stable public identifier, never the internal auto-increment `id` or row GUID. Exposing sequential internal IDs leaks order volume and invites enumeration of other customers' records.
- **No storage or implementation hints.** No table names, no `is_deleted` flags, no internal status codes like `status: 3`. Status is a named string from a documented set.
- **No internal foreign keys.** `customerId` as an internal reference is implied by the auth token, not echoed back as a raw key the client could probe.
- **No upstream error text.** Validation responses describe the field problem, not the stack trace, ORM error, or downstream service name.

Rule of thumb: the contract describes what the consumer needs to act, in the consumer's vocabulary. Anything that exists only because of how the service is built internally stays out of the response.
