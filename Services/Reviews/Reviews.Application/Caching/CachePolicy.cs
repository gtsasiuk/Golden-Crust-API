using Microsoft.Extensions.Caching.Memory;

namespace Reviews.Application.Caching;

public sealed record CachePolicy(
    TimeSpan L1Absolute,
    TimeSpan? L1Sliding,
    TimeSpan L2Absolute,
    CacheItemPriority Priority = CacheItemPriority.Normal,
    bool RecordMetrics = true)
{
    public static readonly CachePolicy ReviewsList =
        new(L1Absolute: TimeSpan.FromMinutes(1), L1Sliding: null, L2Absolute: TimeSpan.FromMinutes(5));

    public static readonly CachePolicy ListVersion =
        new(L1Absolute: TimeSpan.FromSeconds(30), L1Sliding: null, L2Absolute: TimeSpan.FromHours(1),
            Priority: CacheItemPriority.High, RecordMetrics: false);
}