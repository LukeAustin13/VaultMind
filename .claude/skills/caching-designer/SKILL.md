---
name: caching-designer
description: Use this skill when you need to design a caching strategy — choosing between in-process and distributed cache, selecting cache patterns (cache-aside, write-through), designing key conventions, TTL and eviction policies, invalidation strategies, and stampede protection. Tuned for .NET with IMemoryCache, IDistributedCache, HybridCache, and Redis via StackExchange.Redis. It does not implement the cache (that is implementation work) or review existing cache code (use code-reviewer).
license: MIT
metadata:
  stack: dotnet
  version: 1.2
  last-reviewed: 2026-06-11
---

# Caching Designer

## Use When

- The user asks "should I cache this?", "how do I cache X?", or "what caching strategy should I use?"
- A new feature introduces data that is expensive to compute or retrieve and read frequently.
- Existing queries or API calls are slow and caching is a candidate solution.
- A caching layer needs to be added to an existing service.
- Cache invalidation is causing stale data problems and a strategy is needed.

## Do Not Use When

- The task is implementing the cache — that is implementation work.
- The concern is query performance without caching — use **performance-profiler** or the **data-access-reviewer** agent.
- The task is designing the overall backend — use **backend-architect**.

## Inputs To Look For

- What data is being cached (entity type, query result, computed value, external API response).
- Read/write ratio for the data (read-heavy data benefits most from caching).
- Staleness tolerance: how out-of-date can the cached data be before it causes problems?
- Deployment environment: single instance or multiple instances (single-node vs distributed).
- Whether Redis or another distributed cache is available.
- Current performance problem or expected load.

---

## Cache Type Decision

Choose the cache type first. Everything else follows from this.

| Type | Technology | When to Use |
|------|-----------|-------------|
| **In-process** | `IMemoryCache` | Single-instance deployment. Low latency requirement. Data fits in app memory. Cache loss on restart is acceptable. |
| **Distributed** | `IDistributedCache` / Redis | Multiple app instances (load balanced). Cache must survive app restarts. Shared session state. Large cache datasets. |
| **Hybrid (L1/L2)** | `HybridCache` (.NET 9+, `Microsoft.Extensions.Caching.Hybrid`) | Latency-sensitive hot data with distributed coherence. In-process L1 backed by Redis L2, with built-in stampede protection. |

**Decision rule:** Use `IMemoryCache` unless you have multiple instances or need the cache to survive restarts. Redis adds operational complexity — only introduce it when `IMemoryCache` genuinely cannot serve the need. On .NET 9+, prefer `HybridCache` over hand-rolling an L1/L2 layer — it provides two-tier reads and stampede protection out of the box, and works as in-process-only when no distributed cache is registered.

---

## Cache Pattern Selection

| Pattern | How It Works | When to Use |
|---------|-------------|-------------|
| **Cache-aside** (lazy load) | Application checks cache first; on miss, loads from source and writes to cache | Read-heavy data, tolerable first-miss latency, most common default |
| **Write-through** | Write to cache and source simultaneously on every update | Data must never be stale immediately after a write; write overhead acceptable |
| **Write-behind** (write-back) | Write to cache immediately, write to source asynchronously | High write throughput required; eventual consistency acceptable; complex to implement correctly |
| **Read-through** | Cache handles loading from source on miss (cache library does the work) | Only when the cache library or abstraction layer supports it natively |

**Default:** Cache-aside. It is the simplest, most explicit pattern and handles most cases.

See `references/caching-patterns.md` for a cache-aside code example with `IMemoryCache` and `GetOrCreateAsync`.

---

## Key Design

Cache keys must be unique, predictable, and version-safe.

**Pattern:** `[namespace]:[entity]:[identifier]:[version?]`

Examples:
- `products:detail:42` — product detail by ID
- `users:profile:user-123` — user profile
- `orders:list:customer-456:page-1` — paginated order list
- `v2:products:detail:42` — versioned key (use when cache schema changes)

**Rules:**
- Use a consistent separator (`:` is conventional for Redis, `-` for flat stores).
- Include all parameters that affect the result — if `GetOrders(customerId, status)` has two params, both must be in the key.
- Never use user-supplied input directly in cache keys without sanitisation.
- Add a version prefix when the cached data structure changes and you cannot clear the entire cache.

---

## TTL and Eviction Policy

| Data Type | Suggested TTL | Reasoning |
|-----------|--------------|-----------|
| Reference data (countries, categories) | 1–24 hours | Changes rarely |
| User profile / settings | 5–15 minutes | Changes occasionally; stale briefly is acceptable |
| Product catalogue | 5–30 minutes | Changes occasionally |
| Session-related data | Match session lifetime | Must expire with the session |
| External API responses | 1–5 minutes | Depends on the API's own refresh rate |
| Computed aggregates (reports, dashboards) | 1–60 minutes | Expensive to recompute; staleness is acceptable |

**Absolute vs sliding expiration:**
- **Absolute expiration:** Entry expires at a fixed time after creation regardless of access. Use for time-sensitive data that must refresh periodically.
- **Sliding expiration:** Entry's TTL resets on each access. Use for session data or frequently accessed hot data. Combine with a maximum absolute expiration to prevent an item never expiring.

See `references/caching-patterns.md` for `MemoryCacheEntryOptions` code with combined absolute + sliding expiration.

---

## Invalidation Strategy

Stale cache entries are the primary source of cache-related bugs. Define an invalidation strategy for each cached entity.

| Strategy | How It Works | When to Use |
|----------|-------------|-------------|
| **TTL-only** | Entry expires automatically | Staleness is acceptable for the TTL duration |
| **Explicit removal** | Application removes entry on write/delete | Low write frequency; writes are known at a specific point |
| **Event-driven** | A domain event triggers cache removal | Multiple writers or multiple services can invalidate; requires event infrastructure |
| **Tag-based** | Entries tagged by entity; invalidate all entries with a tag | One entity change invalidates multiple related cache entries |

See `references/caching-patterns.md` for explicit removal and tag-based invalidation code examples.

---

## Stampede Protection

A cache stampede occurs when a popular cache entry expires and many concurrent requests simultaneously attempt to load from the source.

On .NET 9+, `HybridCache.GetOrCreateAsync` provides built-in stampede protection — prefer it over manual patterns. On older targets, `IMemoryCache.GetOrCreateAsync` is sufficient for most cases; for high-traffic hot keys, use a `SemaphoreSlim` double-check pattern.

See `references/caching-patterns.md` for both approaches.

---

## Distributed Cache (Redis)

Use `IDistributedCache` backed by StackExchange.Redis for multi-instance or restart-resilient caching. Wrap Redis calls with try/catch — cache unavailability must never crash the application.

See `references/caching-patterns.md` for registration and read/write code patterns.

---

## Process

1. **Identify what to cache and why.** What data? How frequently is it read? How expensive to produce? What is the acceptable staleness?
2. **Choose the cache type.** In-process, distributed, or hybrid. Apply the decision rule.
3. **Choose the cache pattern.** Cache-aside is the default unless there is a specific reason for another.
4. **Design the key convention.** Document the key pattern for each cached entity.
5. **Define TTL and expiration type.** Absolute, sliding, or combined. Assign TTL per data type.
6. **Define the invalidation strategy.** TTL-only, explicit removal, event-driven, or tag-based. One strategy per entity.
7. **Assess stampede risk.** For high-traffic, single-key hot data, recommend the SemaphoreSlim pattern.
8. **Define failure behaviour.** What happens if the cache is unavailable? Fall back to source. Never let cache failure block requests.

---

## Output Format

### Caching Design: [Feature or Entity Name]

**Cache Type:** In-process / Distributed / Hybrid
**Pattern:** Cache-aside / Write-through / Write-behind
**Technology:** IMemoryCache / IDistributedCache + Redis

---

#### Cache Entries

| Entry | Key Pattern | TTL | Expiry Type | Invalidation | Stampede Risk |
|-------|------------|-----|-------------|--------------|---------------|
| Product detail | `products:detail:{id}` | 10 min | Absolute | Explicit on update/delete | Medium — use GetOrCreateAsync |
| Product list | `products:list:{page}` | 5 min | Absolute | TTL-only | Low |
| User profile | `users:profile:{userId}` | 15 min | Sliding + 1h max | Explicit on profile update | Low |

#### Key Convention

`[namespace]:[entity]:[identifier]` — e.g., `products:detail:42`

#### Failure Behaviour

[How the application behaves when cache is unavailable — fall through to source, log warning, no exception to caller]

#### Registration

```csharp
// Program.cs snippet
```

#### Code Pattern

```csharp
// Cache-aside implementation for the primary entry
```

---

## Quality Bar

- Cache type choice is justified by deployment topology, not preference.
- Every cached entry has a defined TTL and invalidation strategy.
- Stampede risk is assessed — not assumed to be absent.
- Failure behaviour is defined — cache unavailability must not crash the application.
- Key patterns include all parameters that affect the result.

## Failure Modes To Avoid

- Using `IMemoryCache` in a multi-instance deployment without acknowledging that each instance has its own cache (stale data divergence).
- No invalidation strategy beyond TTL for data that is updated by the application.
- Caching mutable user-specific data without scoping the key to the user — cache poisoning risk.
- Setting TTL so long that a product update takes hours to appear — TTL must match staleness tolerance.
- Ignoring cache failure — if Redis goes down and the application throws, the cache is a single point of failure.
- Caching exceptions or null results without explicit intent — a null product should not be cached unless you deliberately want to cache "not found".
- Using `NeverRemove` priority for large objects — can cause memory pressure and OOM in the host process.

## References

- `references/caching-patterns.md` — cache-aside code, expiration options, invalidation patterns, stampede protection, and Redis usage.
