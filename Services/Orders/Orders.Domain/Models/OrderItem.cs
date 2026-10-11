namespace Orders.Domain;

public class OrderItem
{
    public long OrderId { get; set; }
    public long MenuItemId { get; set; }
    public string ProductName { get; set; } = "";   // ЗНІМОК назви на момент замовлення
    public decimal UnitPrice { get; set; }          // ЗНІМОК ціни
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }          // обчислюється в БД (persisted), у INSERT не передаємо
}