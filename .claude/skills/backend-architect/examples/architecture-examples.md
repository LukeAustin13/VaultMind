# Architecture Examples

Two worked examples showing how to size the design to the problem. The first shows minimal layering for a simple service — and where added structure would be waste. The second shows a domain where CQRS earns its cost, with a decision table for when to reach for it and when not to.

---

## Example 1: Simple CRUD Service — Minimal Layering

A team needs an internal service to manage a list of office locations: name, address, capacity, and an active flag. Editors create and update records through a small admin UI. There is no workflow, no derived state, and no complex validation beyond required fields and a positive capacity.

### Don't over-engineer this

The instinct to reach for separate Application, Domain, and Infrastructure projects with repositories and a mediator adds layers that pass data straight through without adding value. For a CRUD service this small, that structure costs more to read and maintain than it saves. Map the request to the database and stop.

### Project Structure

```
LocationService/
  src/
    LocationService.Api/        -- Controllers, DTOs, EF Core DbContext, validation
  tests/
    LocationService.Tests/      -- Endpoint and validation tests
```

One project. EF Core is used directly from the endpoint handlers. No repository wraps the `DbContext` because `DbContext` is already a unit of work and a repository.

### Layer Responsibilities

| Concern | Where it lives | Note |
|---------|---------------|------|
| HTTP and routing | Minimal API endpoints in `Api` | No controller base classes needed |
| Validation | `DataAnnotations` or a small `FluentValidation` validator | Required fields, capacity > 0 |
| Data access | EF Core `DbContext` directly | No repository abstraction |
| Mapping | Inline in the endpoint, or a small static mapper | No AutoMapper for four fields |
| Error handling | Built-in `ProblemDetails` + a 404 helper | No custom exception hierarchy |

### What would be over-engineering here

| Pattern | Why it is wrong for this service |
|---------|----------------------------------|
| CQRS | No read/write asymmetry; the same model serves both |
| Repository over EF Core | Adds an interface that only forwards calls to `DbContext` |
| Mediator (MediatR) | No cross-cutting pipeline behaviour needed for four endpoints |
| Separate Domain project | The entity has no behaviour beyond data; nothing to protect |
| Event publishing | Nothing reacts to a location changing |

Apply layering when a layer earns its place by holding real logic. Here it would not.

---

## Example 2: Complex Domain — CQRS Justified

A claims-processing system handles insurance claims through a multi-step lifecycle: submitted, under review, evidence requested, approved or denied, paid. Writes enforce a state machine, run eligibility rules, and emit events. Reads are very different: dashboards aggregate claims by status and adjuster, search spans free text and dates, and reporting joins across claims, payments, and customers. Read traffic is roughly ten times write traffic, and the read shapes do not match the write model.

This is the asymmetry CQRS exists for. The write side needs a rich domain model that protects invariants. The read side needs denormalised, query-shaped projections that the write model would make awkward and slow.

### Project Structure

```
Claims/
  src/
    Claims.Api/                 -- Controllers, request/response DTOs, middleware
    Claims.Application/         -- Commands, queries, handlers, validators
      Commands/                 -- SubmitClaim, RequestEvidence, ApproveClaim, ...
      Queries/                  -- GetClaimDashboard, SearchClaims, GetClaimDetail
    Claims.Domain/              -- Claim aggregate, value objects, domain events, rules
    Claims.Infrastructure/      -- Write DbContext, read models, projections, messaging
  tests/
    Claims.Domain.Tests/        -- State machine and rule tests
    Claims.Application.Tests/   -- Handler tests
    Claims.IntegrationTests/    -- End-to-end across write and read paths
```

### Layer Responsibilities

| Layer | Responsibility | Must not |
|-------|---------------|----------|
| `Api` | Accept requests, map to commands/queries, return responses | Contain business rules |
| `Application` | Orchestrate use cases; one handler per command/query | Hold domain invariants or talk HTTP |
| `Domain` | `Claim` aggregate, state transitions, eligibility rules, events | Reference EF Core or `Api` |
| `Infrastructure` | Write model persistence, read projections, event dispatch | Contain business decisions |

The write path loads the `Claim` aggregate, calls a method that enforces the transition (for example `claim.Approve(adjuster)`), and persists it. Domain events (`ClaimApproved`) update read projections, so dashboard and search queries never touch the write model.

### Why CQRS / Why Not — Decision Table

| Signal | Points toward CQRS | Points away from CQRS |
|--------|-------------------|----------------------|
| Read vs write shape | Read shapes differ sharply from the write model | Reads and writes use the same shape |
| Read/write ratio | Reads vastly outnumber writes; scaling them separately matters | Balanced, low-volume traffic |
| Domain logic | Rich invariants and a state machine on the write side | Simple field validation only |
| Query complexity | Aggregations, search, cross-entity reporting | Single-row lookups by id |
| Team experience | Team understands the pattern and its cost | Team is new to it; maintenance risk is high |
| Consistency needs | Eventual consistency on reads is acceptable | Reads must reflect writes instantly with no projection lag |

CQRS is justified here because the read and write models genuinely diverge, the read load dominates, and the write side holds real invariants worth isolating. Drop the pattern the moment those signals weaken — a single shared model is cheaper whenever reads and writes look the same.

### Lighter alternatives before full CQRS

If only some of the signals are present, reach for the smallest step that solves the problem:

| Situation | Use instead of full CQRS |
|-----------|--------------------------|
| Reads differ but writes are simple | Same model for writes, separate read-only query objects or views |
| One slow report | A dedicated read query or database view, not a separate read store |
| Want handler structure without read/write split | Mediator with command/query handlers over one model |
| Need separate scaling, same model | Read replica at the database, not a projected read model |

Full CQRS with separate read projections is the heaviest option. Earn your way up to it.
