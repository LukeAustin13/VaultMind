---
name: feature-flag-designer
description: Use this skill when you need to design a feature flag system — flag types, naming conventions, targeting rules, rollout strategy, lifecycle management, and cleanup policies. Works with Microsoft.FeatureManagement for .NET and general flag patterns for other stacks. It does not implement flags (use frontend-implementer for UI flags, engineering-guardrails for backend wiring) or design the backend architecture (use backend-architect).
license: MIT
metadata:
  stack: dotnet-primary
  version: 1.2
  last-reviewed: 2026-07-04
---

# Feature Flag Designer

## Use When
- The user says "add a feature flag", "roll this out gradually", or "hide this behind a flag".
- A feature flag system needs to be designed for a new application.
- Existing flags are growing out of control and need a lifecycle policy.
- A canary release, A/B test, or kill switch needs to be designed.
- The user asks "how do I safely roll out this feature?"

## Do Not Use When
- The task is implementing the flag in code — use **frontend-implementer** or the appropriate implementation approach.
- The task is designing the overall backend structure — use **backend-architect**.
- The task is designing a deployment pipeline — use **devops-deploy**.

## Inputs To Look For
- The type of feature being flagged (UI change, API behaviour, infrastructure switch, experiment).
- The target application stack (.NET with Microsoft.FeatureManagement, React, or other).
- The deployment environment (single-region, multi-region, edge).
- Whether targeting rules are needed (all users, specific users, percentage, role, region).
- Whether the flag needs to be changed at runtime without deployment.

## Flag Type Reference

| Type | Purpose | Typical Lifetime | Example |
|---|---|---|---|
| Release flag | Hide incomplete work in production | Hours to weeks | New checkout flow behind a flag during development |
| Ops flag | Runtime kill switch or circuit breaker | Indefinite (permanent infra) | Disable a third-party integration during an outage |
| Experiment flag | A/B test or canary rollout | Days to weeks | 10% of users see new pricing |
| Permission flag | Feature access by role or user tier | Long-lived | Beta users only, admin-only features |

Choose the type first — it determines the lifecycle, ownership, and cleanup expectations.

## Naming Conventions

A well-named flag encodes intent and ownership. Recommended pattern:

```
[area].[feature].[variant?]
```

Examples:
- `checkout.newPaymentFlow` — release flag for payment UI
- `notifications.emailEnabled` — ops kill switch
- `pricing.showAnnualDiscount.experiment` — experiment flag
- `dashboard.advancedAnalytics.betaUsers` — permission flag

Rules:
- Use camelCase or kebab-case consistently across the project.
- Never use generic names like `newFeature`, `test1`, `flag123`.
- The name should be readable without documentation.
- Include the area/domain prefix to enable grouping.

## Targeting Rules

| Rule Type | When to Use | Example |
|---|---|---|
| Environment | Enable only in dev/staging | `environment == "staging"` |
| User ID | Target specific users | `userId in ["user-1", "user-2"]` |
| Role/group | Target by role | `role == "admin"` |
| Percentage | Canary rollout | `percentage <= 10` (10% of users) |
| Region | Geographic rollout | `region == "us-east"` |
| Always on/off | Default state | No targeting — flag is on or off globally |

Combine rules with AND/OR logic cautiously — complex targeting rules are hard to reason about and debug.

## Rollout Strategy

Recommended canary sequence for a release flag:

1. **Off** — flag added to codebase, feature hidden.
2. **Internal only** — enable for the development team or specific user IDs.
3. **5–10%** — initial canary; monitor errors, latency, conversion.
4. **25%** → **50%** → **100%** — increment in stages with monitoring checkpoints.
5. **Remove flag** — once at 100% and stable, remove the flag and the dead code path.

For experiment flags:
- Define the success metric before enabling. (Conversion rate, engagement, error rate)
- Define the minimum observation period before deciding. (Often 1–2 weeks)
- Define the rollback threshold. (If error rate increases by X%, disable immediately)

## Lifecycle Policy

Every flag has a lifecycle. Without a policy, flags accumulate permanently.

| Phase | Description | Owner Action |
|---|---|---|
| Created | Flag added to configuration and code | Assign an owner and a planned removal date |
| Active (rollout) | Feature being rolled out incrementally | Monitor, increment percentage |
| Active (stable) | Feature at 100% or permanently on | Plan removal (release flags only) |
| Cleanup | Flag removed from config and code | Delete config entry, delete flag branches |

**Maximum flag age for release flags:** 30–90 days (set per team). Flags older than the maximum should be reviewed and either cleaned up or reclassified as ops/permission flags.

**Cleanup checklist:**
- [ ] Remove flag check from code.
- [ ] Remove the inactive code path (the old branch).
- [ ] Remove flag from configuration / feature management service.
- [ ] Remove tests that specifically test the disabled state.
- [ ] Update documentation if the feature was described as beta.

## Code Patterns

See `templates/feature-flag-templates.md` for Microsoft.FeatureManagement (.NET) configuration, registration, and usage patterns, the React/TypeScript pattern, and clean pattern principles.

## Process

1. **Inventory existing flags.** Grep the repo for the feature-management config section and flag-check call sites (`IsEnabledAsync`, `FeatureGate`, feature config keys); list current flags and their naming style. New flags must match the existing convention, and flags past their removal date get flagged in the output.
2. **Identify the flag type.** Release, ops, experiment, or permission? This determines lifecycle and tooling.
3. **Name the flag.** Apply naming conventions. The name must be self-explanatory.
4. **Define targeting rules.** Who sees this? All users, percentage, specific users/roles?
5. **Define the rollout sequence.** What are the stages? What are the monitoring checkpoints?
6. **Define the success/abort criteria.** What metrics indicate the rollout should proceed? What triggers a rollback?
7. **Set a cleanup deadline.** Assign a removal date for release flags. Assign ownership.
8. **Design the code pattern.** Where does the flag check live? Which library/approach?

## Output Format

### Feature Flag Design: [Feature Name]

**Flag Name:** `[area.feature]`
**Flag Type:** [Release / Ops / Experiment / Permission]
**Owner:** [Team or individual]
**Planned Removal:** [Date or "Indefinite — ops flag"]

---

#### Targeting Rules

| Rule | Value | Notes |
|------|-------|-------|
| Environment | staging, production | |
| Percentage | Starting at 5% | Increment weekly |

#### Rollout Sequence

| Stage | Audience | Duration | Gate Condition |
|-------|----------|----------|----------------|
| 1. Internal | Dev team user IDs | 1 day | No errors |
| 2. Canary | 5% | 1 week | Error rate stable |
| 3. Broad | 50% | 1 week | Conversion metric meets target |
| 4. Full | 100% | — | Stable |
| 5. Cleanup | — | — | Remove flag and dead code |

#### Success / Abort Criteria

- **Proceed:** [Metric and threshold]
- **Rollback:** [Metric and threshold that triggers disabling the flag]

#### Code Location

**Flag check lives at:** [Controller / Page component / Middleware]
**Rationale:** [One sentence]

#### Configuration

```json
// appsettings snippet or equivalent
```

#### Cleanup Checklist
- [ ] Code flag check removed
- [ ] Inactive code path removed
- [ ] Configuration entry deleted
- [ ] Dead-state tests removed

## Quality Bar
- Every release flag has an assigned removal date.
- The rollout sequence includes explicit monitoring checkpoints, not just "go to 100%".
- Abort criteria are defined before rollout begins, not after a problem appears.
- Flag checks sit at the boundary layer, not inside domain logic.
- Both the enabled and disabled code paths are testable.

## Failure Modes To Avoid
- Creating flags without a removal plan — they become permanent tech debt.
- Nesting multiple flag checks in the same code path — impossible to reason about combinations.
- Using flag values in database columns or query filters — flags are routing decisions, not persistent state.
- Skipping the disabled-state test — the old code path will be removed without ever being tested in CI.
- Giving a flag a generic name (`newFeature`, `test2`) that requires a comment to understand.
- Treating an experiment flag as a release flag — experiments have a decision deadline; release flags have a rollout deadline.
