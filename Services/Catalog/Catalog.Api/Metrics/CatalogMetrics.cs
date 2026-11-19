using System.Diagnostics.Metrics;
using System.Diagnostics;
using Catalog.Bll.Metrics;

namespace Catalog.Api.Metrics;

public sealed class CatalogSnapshot
{
    private int _unavailable;
    public int Unavailable => Volatile.Read(ref _unavailable);
    public void Update(int value) => Volatile.Write(ref _unavailable, value);
}

public sealed class CatalogMetrics : ICatalogMetrics
{
    public const string MeterName = "Catalog.Api";

    private readonly Counter<long> _created;
    private readonly Counter<long> _updated;
    private readonly Counter<long> _deleted;
    private readonly Histogram<double> _duration;

    public CatalogMetrics(IMeterFactory meterFactory, CatalogSnapshot snapshot)
    {
        var meter = meterFactory.Create(MeterName);

        _created = meter.CreateCounter<long>("menuitems.created", unit: "{item}", description: "Created menu items");
        _updated = meter.CreateCounter<long>("menuitems.updated", unit: "{item}", description: "Updated menu items, by kind of change");
        _deleted = meter.CreateCounter<long>("menuitems.deleted", unit: "{item}", description: "Deleted menu items");
        _duration = meter.CreateHistogram<double>("catalog.operation.duration", unit: "ms",
            description: "Duration of catalog operations");

        meter.CreateObservableGauge("menuitems.unavailable", () => snapshot.Unavailable,
            unit: "{item}", description: "Menu items currently marked as unavailable");
    }

    public void MenuItemCreated() => _created.Add(1);
    public void MenuItemDeleted() => _deleted.Add(1);

    public void MenuItemUpdated(string change) =>
        _updated.Add(1, new KeyValuePair<string, object?>("change", change));

    public void RecordDuration(string operation, bool success, double milliseconds, string? cache = null)
    {
        var tags = new TagList
        {
            { "operation", operation },
            { "result", success ? "success" : "failure" }
        };
        if (cache is not null) tags.Add("cache", cache);
        _duration.Record(milliseconds, tags);
    }
}