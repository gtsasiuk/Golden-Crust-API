using System.Diagnostics.Metrics;
using Orders.Bll.Metrics;
using Orders.Domain;

namespace Orders.Api.Metrics;

public sealed class OrderMetrics : IOrderMetrics
{
    public const string MeterName = "Orders.Api";

    private static readonly string[] ActiveStatuses =
        [OrderStatus.Placed, OrderStatus.Accepted, OrderStatus.Cooking,
         OrderStatus.ReadyForDelivery, OrderStatus.Delivering];

    private readonly Counter<long> _created;
    private readonly Counter<long> _confirmed;
    private readonly Counter<long> _statusChanged;
    private readonly Counter<long> _cancelled;
    private readonly Histogram<double> _duration;

    public OrderMetrics(IMeterFactory meterFactory, OrdersSnapshot snapshot)
    {
        var meter = meterFactory.Create(MeterName);

        _created = meter.CreateCounter<long>("orders.created", unit: "{order}",
            description: "Number of created orders");
        _confirmed = meter.CreateCounter<long>("orders.confirmed", unit: "{order}",
            description: "Number of paid and confirmed orders");
        _statusChanged = meter.CreateCounter<long>("orders.status.changed", unit: "{change}",
            description: "Number of kitchen/delivery status changes");
        _cancelled = meter.CreateCounter<long>("orders.cancelled", unit: "{order}",
            description: "Number of cancelled orders");

        _duration = meter.CreateHistogram<double>("orders.operation.duration", unit: "ms",
            description: "Duration of order operations");

        meter.CreateObservableGauge("orders.active",
            () => ActiveStatuses.Select(s =>
                new Measurement<int>(snapshot.Get(s), new KeyValuePair<string, object?>("order.status", s))),
            unit: "{order}", description: "Orders currently in progress, by status");
    }

    public void OrderCreated(string status) =>
        _created.Add(1, new KeyValuePair<string, object?>("order.status", status));

    public void OrderConfirmed() => _confirmed.Add(1);

    public void OrderStatusChanged(string newStatus) =>
        _statusChanged.Add(1, new KeyValuePair<string, object?>("order.status", newStatus));

    public void OrderCancelled() => _cancelled.Add(1);

    public void RecordDuration(string operation, bool success, double milliseconds) =>
        _duration.Record(milliseconds,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("result", success ? "success" : "failure"));
}