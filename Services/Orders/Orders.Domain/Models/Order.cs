namespace Orders.Domain;

public class Order
{
    public long Id { get; set; }
    public long CustomerId { get; set; }
    public string Status { get; set; } = OrderStatus.Placed;
    public decimal TotalAmount { get; set; }
    public DateTime OrderDate { get; set; }

    // Поля з OrderDetails (1:1). Тримаємо їх у моделі замовлення, бо клієнту потрібні разом.
    public string DeliveryAddress { get; set; } = "";
    public decimal? DeliveryLat { get; set; }
    public decimal? DeliveryLng { get; set; }
    public string? Comment { get; set; }

    public List<OrderItem> Items { get; set; } = new();
}