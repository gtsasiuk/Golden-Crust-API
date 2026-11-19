namespace Catalog.Bll.Metrics;

public interface ICatalogMetrics
{
    void MenuItemCreated();
    void MenuItemUpdated(string change);
    void MenuItemDeleted();
    void RecordDuration(string operation, bool success, double milliseconds, string? cache = null);
}