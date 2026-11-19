using Catalog.Dal;
using Microsoft.EntityFrameworkCore;
using Platform.ServiceDefaults;

namespace Catalog.Api.Metrics;

public sealed class CatalogSnapshotRefresher(
    IServiceScopeFactory scopes, CatalogSnapshot snapshot, ILogger<CatalogSnapshotRefresher> logger)
    : PeriodicRefreshService(logger, TimeSpan.FromSeconds(15))
{
    protected override async Task RefreshAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        snapshot.Update(await db.MenuItems.CountAsync(m => !m.IsAvailable, ct));
    }
}