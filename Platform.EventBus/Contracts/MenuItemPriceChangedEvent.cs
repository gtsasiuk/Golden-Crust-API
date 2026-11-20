namespace Platform.EventBus.Contracts;

public sealed record MenuItemPriceChangedEvent(
    long MenuItemId,
    string MenuItemName,
    decimal OldPrice,
    decimal NewPrice
) : IntegrationEvent;
