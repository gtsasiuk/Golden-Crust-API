using Microsoft.Extensions.Caching.Memory;

namespace Catalog.Bll.Caching;

public sealed record CachePolicy(
    TimeSpan L1Absolute,
    TimeSpan? L1Sliding,
    TimeSpan L2Absolute,
    CacheItemPriority Priority = CacheItemPriority.Normal)
{
    public static readonly CachePolicy MenuItemCard =
        new(L1Absolute: TimeSpan.FromMinutes(5), L1Sliding: TimeSpan.FromMinutes(2),
            L2Absolute: TimeSpan.FromMinutes(30));

    public static readonly CachePolicy Categories =
        new(L1Absolute: TimeSpan.FromMinutes(10), L1Sliding: null,
            L2Absolute: TimeSpan.FromHours(1), Priority: CacheItemPriority.High);
}