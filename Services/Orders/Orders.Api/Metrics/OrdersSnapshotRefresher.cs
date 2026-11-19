using Orders.Dal.Interfaces;
using Platform.ServiceDefaults;

namespace Orders.Api.Metrics;

public sealed class OrdersSnapshotRefresher(
    IServiceScopeFactory scopes, OrdersSnapshot snapshot, ILogger<OrdersSnapshotRefresher> logger)
    : PeriodicRefreshService(logger, TimeSpan.FromSeconds(15))
{
    protected override async Task RefreshAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        snapshot.Update(await uow.Orders.CountActiveByStatusAsync(ct));
    }
}