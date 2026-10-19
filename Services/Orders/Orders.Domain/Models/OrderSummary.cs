namespace Orders.Domain;

// Рядок історії замовлень: те, що повертає usp_GetCustomerOrders.
public class OrderSummary
{
    public long Id { get; set; }
    public string Status { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public DateTime OrderDate { get; set; }
    public string DeliveryAddress { get; set; } = "";
    public int ItemCount { get; set; }
}