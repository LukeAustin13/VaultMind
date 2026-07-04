# Caching Patterns Reference

Code examples for the patterns described in `caching-designer` SKILL.md. Consult when producing the Registration and Code Pattern sections of the output.

Contents: Cache-Aside (IMemoryCache) · HybridCache (.NET 9+) · Expiration Options · Invalidation Patterns · Stampede Protection (SemaphoreSlim) · Distributed Cache (Redis via IDistributedCache). If reading a partial range, this line is the full scope.

---

## Cache-Aside Pattern (IMemoryCache)

```csharp
public async Task<Product?> GetProductAsync(int id)
{
    var cacheKey = $"products:detail:{id}";
    if (_cache.TryGetValue(cacheKey, out Product? cached))
        return cached;

    var product = await _repository.GetByIdAsync(id);
    if (product is not null)
    {
        _cache.Set(cacheKey, product, TimeSpan.FromMinutes(10));
    }
    return product;
}
```

`GetOrCreateAsync` is simpler for most single-miss scenarios:

```csharp
var product = await _cache.GetOrCreateAsync(cacheKey, async entry =>
{
    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
    return await _repository.GetByIdAsync(id);
});
```

`GetOrCreateAsync` is not fully stampede-proof under very high concurrency. For hot keys, use `HybridCache` (below) or the SemaphoreSlim pattern.

---

## HybridCache (.NET 9+)

Two-tier cache (in-process L1, optional distributed L2) with built-in stampede protection. Prefer this over hand-rolled L1/L2 or SemaphoreSlim patterns when targeting .NET 9+.

```csharp
// Registration — works in-process only; add a distributed cache to enable L2
builder.Services.AddHybridCache();
builder.Services.AddStackExchangeRedisCache(options =>
    options.Configuration = configuration.GetConnectionString("Redis"));

// Usage — concurrent misses for the same key share one source load
var product = await _hybridCache.GetOrCreateAsync(
    $"products:detail:{id}",
    async ct => await _repository.GetByIdAsync(id, ct),
    new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(10),
        LocalCacheExpiration = TimeSpan.FromMinutes(2)
    },
    cancellationToken: cancellationToken);

// Invalidation
await _hybridCache.RemoveAsync($"products:detail:{id}");
```

---

## Expiration Options (IMemoryCache)

```csharp
// Absolute + sliding combined — never older than 1 hour, expires after 10 min if unused
var options = new MemoryCacheEntryOptions()
    .SetAbsoluteExpiration(TimeSpan.FromHours(1))
    .SetSlidingExpiration(TimeSpan.FromMinutes(10));
_cache.Set(cacheKey, value, options);

// Size limits and eviction priority
services.AddMemoryCache(options => options.SizeLimit = 1024);
options.SetSize(1)
       .SetPriority(CacheItemPriority.Normal); // Low / Normal / High / NeverRemove
```

---

## Invalidation Patterns

### Explicit removal on update

```csharp
public async Task UpdateProductAsync(Product product)
{
    await _repository.UpdateAsync(product);
    _cache.Remove($"products:detail:{product.Id}");
}
```

### Tag-based invalidation (IMemoryCache change tokens)

```csharp
// On cache set: register a cancellation token per entity ID
var cts = _productTokens.GetOrAdd(product.Id, _ => new CancellationTokenSource());
options.AddExpirationToken(new CancellationChangeToken(cts.Token));
_cache.Set(cacheKey, product, options);

// On invalidate: cancel the token — all entries tagged with this product ID expire
if (_productTokens.TryRemove(product.Id, out var cts))
    cts.Cancel();
```

---

## Stampede Protection (SemaphoreSlim)

Use when a single high-traffic key expires and many concurrent requests would hammer the database simultaneously.

```csharp
private readonly SemaphoreSlim _lock = new(1, 1);

public async Task<Product?> GetProductAsync(int id)
{
    var cacheKey = $"products:detail:{id}";
    if (_cache.TryGetValue(cacheKey, out Product? cached))
        return cached;

    await _lock.WaitAsync();
    try
    {
        // Double-check after acquiring the lock
        if (_cache.TryGetValue(cacheKey, out cached))
            return cached;

        var product = await _repository.GetByIdAsync(id);
        if (product is not null)
            _cache.Set(cacheKey, product, TimeSpan.FromMinutes(10));
        return product;
    }
    finally
    {
        _lock.Release();
    }
}
```

---

## Distributed Cache (Redis via IDistributedCache)

```csharp
// Registration
services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = configuration.GetConnectionString("Redis");
    options.InstanceName = "MyApp:";
});

// Read
var bytes = await _distributedCache.GetAsync(cacheKey, cancellationToken);
if (bytes is not null)
    return JsonSerializer.Deserialize<Product>(bytes);

// Write
var product = await _repository.GetByIdAsync(id, cancellationToken);
await _distributedCache.SetAsync(
    cacheKey,
    JsonSerializer.SerializeToUtf8Bytes(product),
    new DistributedCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
    },
    cancellationToken);
```

**Redis-specific notes:**
- Connection failure should degrade gracefully — wrap Redis calls with try/catch and fall back to the source.
- Use `System.Text.Json` with consistent options. Store bytes, not strings, for performance.
- Key expiry in Redis is global — do not rely on `IDistributedCache` abstractions for fine-grained eviction policy.
