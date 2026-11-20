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
        Validate(e);

        var occurredAt = NormalizeUtc(e.OccurredAt);

        await uow.BeginTransactionAsync(ct: ct);

        try
        {
            // Реєстрація EventId та оновлення локальної копії
            // виконуються в одній SQL-транзакції.
            var isNew = await uow.ProcessedEvents.TryAddAsync(
                e.EventId, ct);

            if (!isNew)
            {
                await uow.RollbackAsync(ct);

                logger.LogInformation(
                    "Duplicate price event skipped. " +
                    "EventId: {EventId}, MenuItemId: {MenuItemId}",
                    e.EventId,
                    e.MenuItemId);

                return;
            }

            // Оновлюємо тільки локальний довідник MenuItems.
            // OrderItems не змінюємо, оскільки там зберігається
            // ціна товару на момент оформлення замовлення.
            var updated = await uow.MenuItems.ApplyPriceChangedAsync(
                e.MenuItemId,
                e.MenuItemName.Trim(),
                e.NewPrice,
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

                // Подія застаріла: локальний довідник уже має
                // новіший стан. Реєструємо її як оброблену,
                // щоб не повторювати її нескінченно.
                await uow.CommitAsync(ct);

                logger.LogInformation(
                    "Stale price event ignored. EventId: {EventId}, " +
                    "MenuItemId: {MenuItemId}, OccurredAt: {OccurredAt}",
                    e.EventId,
                    e.MenuItemId,
                    occurredAt);

                return;
            }

            await uow.CommitAsync(ct);

            logger.LogInformation(
                "Menu item price synchronized in Orders. " +
                "MenuItemId: {MenuItemId}, OldPrice: {OldPrice}, " +
                "NewPrice: {NewPrice}, EventId: {EventId}, " +
                "CorrelationId: {CorrelationId}",
                e.MenuItemId,
                e.OldPrice,
                e.NewPrice,
                e.EventId,
                e.CorrelationId);
        }
        catch
        {
            await uow.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static void Validate(MenuItemPriceChangedEvent e)
    {
        if (e.EventId == Guid.Empty)
            throw new ArgumentException("EventId cannot be empty.");

        if (e.MenuItemId <= 0)
            throw new ArgumentException("Invalid MenuItemId.");

        if (string.IsNullOrWhiteSpace(e.MenuItemName))
            throw new ArgumentException("MenuItemName is required.");

        if (e.OldPrice <= 0 || e.NewPrice <= 0)
            throw new ArgumentException("Prices must be positive.");

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
