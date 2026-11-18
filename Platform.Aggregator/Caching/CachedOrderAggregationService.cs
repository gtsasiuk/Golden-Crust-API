using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Platform.Aggregator.Models;
using Platform.Aggregator.Services;

namespace Platform.Aggregator.Caching;

public class CachedOrderAggregationService(
    OrderAggregationService inner,
    IDistributedCache cache,
    ILogger<CachedOrderAggregationService> logger)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);

    private static readonly Meter Meter = new("GoldenCrust.Cache");
    private static readonly Counter<long> Hits = Meter.CreateCounter<long>("cache.hits", description: "Cache hits by level");
    private static readonly Counter<long> Misses = Meter.CreateCounter<long>("cache.misses", description: "Cache misses by level");
    private static readonly Counter<long> Skipped = Meter.CreateCounter<long>("cache.skipped", description: "Responses deliberately not cached");
    private static readonly Counter<long> Errors = Meter.CreateCounter<long>("cache.errors", description: "Failed Redis operations");
    private static readonly KeyValuePair<string, object?> Level = new("cache.level", "redis");

    private static readonly TimeSpan RedisTimeout = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan PauseAfterFailure = TimeSpan.FromSeconds(30);
    private static long _retryAfter;
    private static bool RedisAvailable => Environment.TickCount64 >= Volatile.Read(ref _retryAfter);

    public static string Key(long orderId) => $"aggregator:order:{orderId}:full";

    public async Task<FullOrderDto?> GetFullOrderAsync(long orderId, CancellationToken ct)
    {
        var key = Key(orderId);

        var cached = await TryReadAsync(key, ct);
        if (cached is not null)
            return cached;

        var result = await inner.GetFullOrderAsync(orderId, ct);

        if (result is null)
        {
            Skipped.Add(1, new KeyValuePair<string, object?>("cache.reason", "not_found"));
            return null;
        }

        if (result.Warnings.Count > 0)
        {
            Skipped.Add(1, new KeyValuePair<string, object?>("cache.reason", "partial"));
            logger.LogInformation("Aggregated response {Key} is partial ({WarningCount} warnings), not cached",
                key, result.Warnings.Count);
            return result;
        }

        await TryWriteAsync(key, result, ct);
        return result;
    }

    private async Task<FullOrderDto?> TryReadAsync(string key, CancellationToken ct)
    {
        if (!RedisAvailable) return null;

        try
        {
            var bytes = await cache.GetAsync(key, ct).WaitAsync(RedisTimeout, ct);
            if (bytes is null)
            {
                Misses.Add(1, Level);
                logger.LogInformation("Cache miss {Key}, level {Level}", key, "redis");
                return null;
            }

            var dto = JsonSerializer.Deserialize<FullOrderDto>(bytes);
            if (dto is null)
            {
                Misses.Add(1, Level);
                return null;
            }

            Hits.Add(1, Level);
            logger.LogInformation("Cache hit {Key}, level {Level}, size {Bytes} bytes, ttl {TtlSeconds} s",
                key, "redis", bytes.Length, (int)Ttl.TotalSeconds);
            return dto;
        }
        catch (JsonException ex)
        {
            Misses.Add(1, Level);
            logger.LogWarning(ex, "Cache entry {Key} cannot be deserialized, treated as miss", key);
            return null;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            MarkRedisFailed("get", key, ex);
            return null;
        }
    }

    private async Task TryWriteAsync(string key, FullOrderDto dto, CancellationToken ct)
    {
        if (!RedisAvailable) return;

        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(dto);
            await cache.SetAsync(key, bytes,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl }, ct)
                .WaitAsync(RedisTimeout, ct);
            logger.LogInformation("Cache set {Key}, level {Level}, size {Bytes} bytes, ttl {TtlSeconds} s",
                key, "redis", bytes.Length, (int)Ttl.TotalSeconds);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            MarkRedisFailed("set", key, ex);
        }
    }

    private void MarkRedisFailed(string operation, string key, Exception ex)
    {
        Volatile.Write(ref _retryAfter, Environment.TickCount64 + (long)PauseAfterFailure.TotalMilliseconds);
        Errors.Add(1, new KeyValuePair<string, object?>("cache.operation", operation));
        logger.LogWarning(ex, "Redis {Operation} failed for {Key}; aggregator cache is paused for {PauseSeconds} s, serving without cache",
            operation, key, (int)PauseAfterFailure.TotalSeconds);
    }
}