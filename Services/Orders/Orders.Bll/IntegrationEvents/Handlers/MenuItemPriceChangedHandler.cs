using Microsoft.Extensions.Logging;
using Orders.Dal.Interfaces;
using Platform.EventBus.Contracts;

namespace Orders.Bll.IntegrationEvents.Handlers;

public sealed class MenuItemPriceChangedHandler(
    IUnitOfWork uow,
    ILogger<MenuItemPriceChangedHandler> logger)
{
    public async Task HandleAsync(
        MenuItemPriceChangedEvent e,
        CancellationToken ct = default)
    {
        await uow.BeginTransactionAsync(ct: ct);

        try
        {
            // Перевіряємо, чи оброблялася ця подія раніше.
            var isNew = await uow.ProcessedEvents.TryAddAsync(
                e.EventId, ct);

            if (!isNew)
            {
                logger.LogInformation(
                    "Duplicate price event {EventId} skipped for MenuItem {MenuItemId}",
                    e.EventId,
                    e.MenuItemId);

                await uow.RollbackAsync(ct);
                return;
            }

            // Оновлюємо локальну копію товару.
            var updated = await uow.MenuItems.ApplyPriceChangedAsync(
                e.MenuItemId,
                e.MenuItemName,
                e.NewPrice,
                e.OccurredAt.ToUniversalTime(),
                ct);

            if (!updated &&
                await uow.MenuItems.GetByIdAsync(e.MenuItemId, ct) is null)
            {
                throw new InvalidOperationException(
                    $"MenuItem {e.MenuItemId} does not exist in Orders database.");
            }

            await uow.CommitAsync(ct);

            logger.LogInformation(
                "Menu item {MenuItemId} price synchronized: {OldPrice} -> {NewPrice}. EventId: {EventId}",
                e.MenuItemId,
                e.OldPrice,
                e.NewPrice,
                e.EventId);
        }
        catch
        {
            await uow.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
