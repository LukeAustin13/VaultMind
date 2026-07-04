---
name: backend-architect
description: Use this skill when you need to design backend application structure, service boundaries, domain models, authentication flow, background jobs, validation strategy, logging, and error handling. It is especially tuned for .NET/C# Web API projects but applies to any backend. The backend-architect produces structural decisions and diagrams, not code — implementation is a separate step. Do not use this skill for API endpoint design (use api-designer) or database schema (use database-designer).
license: MIT
metadata:
  stack: dotnet-primary
  version: 1.2
  last-reviewed: 2026-07-03
---

# Backend Architect

## Use When
- Starting a new backend project or service.
- Adding a major feature that requires new services, layers, or domain models.
- The user asks "how should I structure this?" for a backend system.
- You need to decide on patterns: CQRS, mediator, repository, unit of work, etc.
- Auth flow, middleware pipeline, or DI registration needs design.
- Background job processing, message queues, or event handling needs planning.

## Do Not Use When
- You are designing API endpoints and contracts — use **api-designer**.
- You are designing database schema — use **database-designer**.
- You are reviewing existing code — use **code-reviewer**.
- The task is small enough to implement directly without architectural decisions.

## Inputs To Look For
- Requirements or feature descriptions.
- Existing solution structure (`.sln`, `.csproj` files, folder layout).
- Current patterns in use (check `Program.cs`, `Startup.cs`, DI registration).
- Domain terminology the team uses.
- Non-functional requirements: scale, latency, auth model, multi-tenancy.

## Clarify Before Starting

If any of the following are unknown, ask before designing:

- **Greenfield or existing codebase?** If existing, what patterns are already in place and must be respected?
- **What are the scale and performance requirements?** (Expected load, latency targets, multi-tenancy needs)
- **What is already decided and cannot change?** (Auth provider, database engine, existing service contracts, deployment model)
- **What is the team's experience level with the patterns being considered?** A team unfamiliar with CQRS will struggle to maintain it.

Do not impose a design without understanding these. Architecture built on wrong assumptions is expensive to undo.

## Process
1. **Understand the domain.** Identify the core entities, their relationships, and the key operations on them.
2. **Define service boundaries.** What belongs in one service vs separate services? What is the unit of deployment?
3. **Choose architectural patterns.** Select patterns based on actual complexity, not resume-driven development:
   - Simple CRUD? Minimal layering.
   - Complex domain logic? Consider domain services or CQRS.
   - Event-driven? Define events and handlers.
   - For each significant decision, weigh at least two genuinely different shapes before committing — the first structure that comes to mind is usually the most generic one. Document the choice, the reason, and what was rejected with a one-line why: rejected alternatives are what let a reader reopen a decision when constraints change, instead of re-deriving it from scratch.
4. **Define the layer structure.** For each project/layer:
   - Responsibility (what goes here, what does not).
   - Dependencies (what it references, what references it).
5. **Design cross-cutting concerns:**
   - **Auth:** Where are tokens validated? How are claims mapped to permissions?
   - **Validation:** Where does input validation happen? Where does business rule validation happen?
   - **Error handling:** How are exceptions translated to responses? What gets logged?
   - **Logging:** What is logged at each level (info, warning, error)? What must never be logged (secrets, PII)?
   - **Background jobs:** What runs async? How is failure handled? Retry policy?
6. **Identify risks.** What could go wrong with this design? What are the scaling bottlenecks?

## Output Format

For worked examples — minimal layering for a simple CRUD service, and a complex domain that justifies CQRS with a decision table — see [examples/architecture-examples.md](examples/architecture-examples.md).

### Architecture: [System/Feature Name]

**Domain Summary:** [2-3 sentences describing the domain]

#### Project Structure
```
Solution/
  src/
    Project.Api/           -- HTTP layer, controllers, middleware
    Project.Application/   -- Use cases, commands, queries
    Project.Domain/        -- Entities, value objects, domain services
    Project.Infrastructure/ -- Data access, external services, messaging
  tests/
    Project.Tests/
```
[Adjust to match the actual technology and needs]

#### Key Entities
| Entity | Responsibility | Key Relationships |
|--------|---------------|-------------------|
| ...    | ...           | ...               |

#### Pattern Decisions
| Decision | Choice | Reason | Rejected (why) |
|----------|--------|--------|----------------|
| Data access | EF Core DbContext direct | Single consumer, no second data source | Repository layer (adds nothing over DbContext here); Dapper (no complex SQL to justify it) |
| Validation | FluentValidation in pipeline | Rules span multiple properties | DataAnnotations (too limited for cross-field rules) |
| Error handling | Middleware + ProblemDetails | One consistent shape for all consumers | Per-controller try/catch (drifts apart within a quarter) |

#### Auth Flow
[Description or sequence: request -> middleware -> claims -> authorization]

#### Cross-Cutting Concerns
| Concern | Approach | Location |
|---------|----------|----------|
| Logging | Serilog structured logging | Middleware + services |
| Validation | FluentValidation | Application layer |
| Error handling | Global exception middleware | Api layer |

#### Risks
- [Risk and mitigation]

## Quality Bar
- Every layer has a defined responsibility and clear boundary.
- Pattern choices have a stated reason, not just "best practice" — and every significant decision records at least one rejected alternative with its one-line why.
- Auth flow is explicit, not hand-waved.
- Cross-cutting concerns are addressed, not deferred.
- The design is proportional to the complexity of the problem.

## Failure Modes To Avoid
- Over-architecting a CRUD app with CQRS, event sourcing, and microservices.
- Choosing patterns because they are fashionable rather than because they solve a real problem.
- Ignoring auth and validation with "we will add that later".
- Designing layers that just pass through to the next layer without adding value.
- Creating abstractions over abstractions (wrapping EF Core in a repository that adds nothing).
- Ignoring the existing codebase patterns and imposing a completely different style.
