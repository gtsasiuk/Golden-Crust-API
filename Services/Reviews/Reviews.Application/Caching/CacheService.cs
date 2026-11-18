using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Reviews.Application.Caching;

/// Дворівневий кеш: L1 (IMemoryCache) -> L2 (Redis) -> джерело (MongoDB).
/// Redis недоступний -> працюємо з L1 + MongoDB.
public sealed class CacheService(
    IMemoryCache memory, IDistributedCache redis, ILogger<CacheService> logger) : ICacheService
{
    private static readonly Meter Meter = new("GoldenCrust.Cache");   // додано в ServiceDefaults через AddMeter
    private static readonly Counter<long> Hits = Meter.CreateCounter<long>("cache.hits", description: "Cache hits by level");
    private static readonly Counter<long> Misses = Meter.CreateCounter<long>("cache.misses", description: "Cache misses by level");
    private static readonly Counter<long> Invalidations = Meter.CreateCounter<long>("cache.invalidations");
    private static readonly Counter<long> Evictions = Meter.CreateCounter<long>("cache.evictions", description: "L1 evictions caused by SizeLimit");
    private static readonly Counter<long> L2Errors = Meter.CreateCounter<long>("cache.l2.errors", description: "Failed Redis operations");
    private static readonly Histogram<double> L2Duration = Meter.CreateHistogram<double>("cache.l2.duration", unit: "ms");

    private static readonly TimeSpan L2Timeout = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan L2PauseAfterFailure = TimeSpan.FromSeconds(30);
    private long _l2RetryAfter;
    private bool L2Available => Environment.TickCount64 >= Volatile.Read(ref _l2RetryAfter);

    public async Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T?>> factory, CachePolicy policy,
        CancellationToken ct = default) where T : class
    {
        if (memory.TryGetValue(key, out T? cached) && cached is not null)
        {
            if (policy.RecordMetrics)
            {
                Hits.Add(1, Level("L1"));
                logger.LogInformation("Cache hit {Key}, level {Level}", key, "L1");
            }
            return cached;
        }
        if (policy.RecordMetrics) Misses.Add(1, Level("L1"));

        var fromRedis = await TryReadL2Async<T>(key, policy, ct);
        if (fromRedis is not null)
        {
            memory.Set(key, fromRedis, BuildL1Options(policy));
            return fromRedis;
        }

        if (policy.RecordMetrics)
            logger.LogInformation("Cache miss {Key}, loading from source", key);
        var value = await factory();
        if (value is null) return null;

        await TryWriteL2Async(key, value, policy, ct);
        memory.Set(key, value, BuildL1Options(policy));
        return value;
    }

    public async Task RemoveAsync(string key, string reason, CancellationToken ct = default)
    {
        memory.Remove(key);
        Invalidations.Add(1);

        try
        {
            await redis.RemoveAsync(key, ct).WaitAsync(L2Timeout, ct);
            logger.LogInformation("Cache invalidated {Key}, reason {Reason}, levels {Levels}", key, reason, "L1+L2");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            MarkL2Failed("remove", key, ex);
            logger.LogWarning("Cache invalidated {Key}, reason {Reason}, levels {Levels}: L2 entry may stay stale until its TTL",
                key, reason, "L1 only");
        }
    }

    private async Task<T?> TryReadL2Async<T>(string key, CachePolicy policy, CancellationToken ct) where T : class
    {
        if (!L2Available) return null;

        var sw = Stopwatch.StartNew();
        try
        {
            var bytes = await redis.GetAsync(key, ct).WaitAsync(L2Timeout, ct);
            L2Duration.Record(sw.Elapsed.TotalMilliseconds, Op("get"));

            var value = bytes is null ? null : JsonSerializer.Deserialize<T>(bytes);
            if (value is null)
            {
                if (policy.RecordMetrics) Misses.Add(1, Level("L2"));
                return null;
            }

            if (policy.RecordMetrics)
            {
                Hits.Add(1, Level("L2"));
                logger.LogInformation("Cache hit {Key}, level {Level}, size {Bytes} bytes", key, "L2", bytes!.Length);
            }
            return value;
        }
        catch (JsonException ex)
        {
            if (policy.RecordMetrics) Misses.Add(1, Level("L2"));
            logger.LogWarning(ex, "Cache entry {Key} in L2 cannot be deserialized, treated as miss", key);
            return null;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            MarkL2Failed("get", key, ex);
            return null;
        }
    }

    private async Task TryWriteL2Async<T>(string key, T value, CachePolicy policy, CancellationToken ct)
    {
        if (!L2Available) return;

        var sw = Stopwatch.StartNew();
        try
        {
            await redis.SetAsync(key, JsonSerializer.SerializeToUtf8Bytes(value),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = policy.L2Absolute }, ct)
                .WaitAsync(L2Timeout, ct);
            L2Duration.Record(sw.Elapsed.TotalMilliseconds, Op("set"));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            MarkL2Failed("set", key, ex);
        }
    }

    private void MarkL2Failed(string operation, string key, Exception ex)
    {
        Volatile.Write(ref _l2RetryAfter, Environment.TickCount64 + (long)L2PauseAfterFailure.TotalMilliseconds);
        L2Errors.Add(1, Op(operation));
        logger.LogWarning(ex, "Redis {Operation} failed for {Key}; L2 is paused for {PauseSeconds} s, falling back to L1 + MongoDB",
            operation, key, (int)L2PauseAfterFailure.TotalSeconds);
    }

    private MemoryCacheEntryOptions BuildL1Options(CachePolicy policy)
    {
        var options = new MemoryCacheEntryOptions
        {
            Size = 1,
            Priority = policy.Priority,
            AbsoluteExpirationRelativeToNow = policy.L1Absolute,
            SlidingExpiration = policy.L1Sliding
        };
        options.RegisterPostEvictionCallback(OnEvicted);
        return options;
    }

    private void OnEvicted(object key, object? value, EvictionReason reason, object? state)
    {
        if (reason != EvictionReason.Capacity) return;
        Evictions.Add(1);
        logger.LogWarning("Cache evicted {Key}, reason {Reason}", key, reason);
    }

    private static KeyValuePair<string, object?> Level(string level) => new("cache.level", level);
    private static KeyValuePair<string, object?> Op(string operation) => new("cache.operation", operation);
}