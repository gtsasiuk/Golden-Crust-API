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
        Validate(e);

        var occurredAt = NormalizeUtc(e.OccurredAt);

        await uow.BeginTransactionAsync(ct: ct);

        try
        {
            var isNew = await uow.ProcessedEvents.TryAddAsync(
                e.EventId, ct);

            if (!isNew)
            {
                await uow.RollbackAsync(ct);

                logger.LogInformation(
                    "Duplicate rename event skipped. " +
                    "EventId: {EventId}, MenuItemId: {MenuItemId}",
                    e.EventId,
                    e.MenuItemId);

                return;
            }

            var updated = await uow.MenuItems.ApplyRenamedAsync(
                e.MenuItemId,
                e.NewName.Trim(),
                occurredAt,
                ct);

            if (!updated)
            {
                var existing = await uow.MenuItems.GetByIdAsync(
                    e.MenuItemId, ct);

                if (existing is null)
                {
                    throw new InvalidOperationException(
                        $"MenuItem {e.MenuItemId} does not exist " +
                        "in Orders local catalog.");
                }

                // Подія старіша за вже застосовану.
                // Фіксуємо її EventId, але не змінюємо товар.
                await uow.CommitAsync(ct);

                logger.LogInformation(
                    "Stale rename event ignored. EventId: {EventId}, " +
                    "MenuItemId: {MenuItemId}, OccurredAt: {OccurredAt}",
                    e.EventId,
                    e.MenuItemId,
                    occurredAt);

                return;
            }

            await uow.CommitAsync(ct);

            logger.LogInformation(
                "Menu item renamed in Orders. " +
                "MenuItemId: {MenuItemId}, OldName: {OldName}, " +
                "NewName: {NewName}, EventId: {EventId}, " +
                "CorrelationId: {CorrelationId}",
                e.MenuItemId,
                e.OldName,
                e.NewName,
                e.EventId,
                e.CorrelationId);
        }
        catch
        {
            await uow.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static void Validate(MenuItemRenamedEvent e)
    {
        if (e.EventId == Guid.Empty)
            throw new ArgumentException("EventId cannot be empty.");

        if (e.MenuItemId <= 0)
            throw new ArgumentException("Invalid MenuItemId.");

        if (string.IsNullOrWhiteSpace(e.NewName))
            throw new ArgumentException("NewName is required.");

        if (e.SchemaVersion <= 0)
            throw new ArgumentException("Invalid SchemaVersion.");

        if (e.OccurredAt == default)
            throw new ArgumentException("OccurredAt is required.");
    }

    private static DateTime NormalizeUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            DateTimeKind.Unspecified =>
                DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => value
        };
    }
}
