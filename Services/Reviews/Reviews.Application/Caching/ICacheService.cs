namespace Reviews.Application.Caching;

public interface ICacheService
{
    Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T?>> factory, CachePolicy policy,
        CancellationToken ct = default) where T : class;

    Task RemoveAsync(string key, string reason, CancellationToken ct = default);
}

public static class CacheServiceExtensions
{
    public static async Task<string> GetListVersionAsync(this ICacheService cache, CancellationToken ct = default)
        => (await cache.GetOrCreateAsync(CacheKeys.ListVersion,
                () => Task.FromResult<string?>(Guid.NewGuid().ToString("N")),
                CachePolicy.ListVersion, ct))!;
}