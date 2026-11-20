using Microsoft.Extensions.Logging;
using Orders.Dal.Interfaces;
using Platform.EventBus.Contracts;

namespace Orders.Bll.IntegrationEvents.Handlers;

public sealed class MenuItemRenamedHandler(
    IUnitOfWork uow,
    ILogger<MenuItemRenamedHandler> logger)
{
    public async Task HandleAsync(
        MenuItemRenamedEvent e,
        CancellationToken ct = default)
    {
        await uow.BeginTransactionAsync(ct: ct);

        try
        {
            var isNew = await uow.ProcessedEvents.TryAddAsync(
                e.EventId, ct);

            if (!isNew)
            {
                logger.LogInformation(
                    "Duplicate rename event {EventId} skipped for MenuItem {MenuItemId}",
                    e.EventId,
                    e.MenuItemId);

                await uow.RollbackAsync(ct);
                return;
            }

            var updated = await uow.MenuItems.ApplyRenamedAsync(
                e.MenuItemId,
                e.NewName,
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
                "Menu item {MenuItemId} renamed from {OldName} to {NewName}. EventId: {EventId}",
                e.MenuItemId,
                e.OldName,
                e.NewName,
                e.EventId);
        }
        catch
        {
            await uow.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
