namespace Orders.Bll.Metrics;

public interface IOrderMetrics
{
    void OrderCreated(string status);
    void OrderConfirmed();
    void OrderStatusChanged(string newStatus);
    void OrderCancelled();
    void RecordDuration(string operation, bool success, double milliseconds);
}