namespace Catalog.Bll.Caching;

public interface ICacheService
{
    Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T?>> factory, CachePolicy policy,
        CancellationToken ct = default) where T : class;

    Task RemoveAsync(string key, string reason, CancellationToken ct = default);
}