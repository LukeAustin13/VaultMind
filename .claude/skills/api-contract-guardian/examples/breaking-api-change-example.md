# Breaking API Change Example

### API Contract Review: Order Endpoints

**Breaking Changes:** 2
**Non-Breaking Changes:** 1
**Consumer Impact:** High

#### Change Summary

| # | Endpoint | Change | Classification | Consumer Impact |
|---|----------|--------|---------------|----------------|
| 1 | `POST /api/orders` | `quantity` changed from optional (default: 1) to required | Breaking | All consumers must now send `quantity` |
| 2 | `GET /api/orders/{id}` | `customerName` removed from response | Breaking | Frontend order detail page displays customer name from this field |
| 3 | `GET /api/orders` | Added `shipDate` to response | Non-breaking | No consumer impact |

#### Breaking Change Detail

**Change 1: `quantity` now required on POST /api/orders**

**Before:**
```json
{ "productId": "abc-123" }
// quantity defaults to 1
```

**After:**
```json
{ "productId": "abc-123", "quantity": 1 }
// quantity is required, no default
```

**Affected Consumers:** Frontend order form, mobile app quick-order, third-party integration (Acme Corp)
**Migration Path:** All consumers must include `quantity` in the request body. Frontend and mobile already send it. Third-party integration needs notification.

---

**Change 2: `customerName` removed from GET /api/orders/{id}**

**Before:**
```json
{ "id": 1, "productId": "abc-123", "quantity": 1, "customerName": "Jane Smith" }
```

**After:**
```json
{ "id": 1, "productId": "abc-123", "quantity": 1 }
```

**Affected Consumers:** Frontend order detail page renders `customerName` directly from this response.
**Migration Path:** Frontend must fetch customer name from `GET /api/customers/{customerId}` instead, or add `customerId` to the order response so frontend can resolve it.

#### Required Updates

- [ ] OpenAPI spec regenerated
- [ ] Frontend order detail page updated to fetch customer separately
- [ ] Third-party integration notified about `quantity` requirement
- [ ] Integration tests updated for both breaking changes
- [ ] API changelog updated
