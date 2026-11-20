namespace Platform.EventBus.Contracts;

public sealed record MenuItemRenamedEvent(
    long MenuItemId,
    string OldName,
    string NewName
) : IntegrationEvent;