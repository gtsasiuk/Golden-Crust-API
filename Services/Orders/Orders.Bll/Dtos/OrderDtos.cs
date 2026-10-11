using System.ComponentModel.DataAnnotations;

namespace Orders.Bll.Dtos;

public class CreateOrderDto
{
    [Range(1, long.MaxValue)]
    public long CustomerId { get; set; }

    [Required, StringLength(400, MinimumLength = 5)]
    public string DeliveryAddress { get; set; } = "";

    [Range(-90.0, 90.0)] public decimal? DeliveryLat { get; set; }
    [Range(-180.0, 180.0)] public decimal? DeliveryLng { get; set; }

    [StringLength(1000)]
    public string? Comment { get; set; }

    [Required, MinLength(1)]
    public List<CreateOrderItemDto> Items { get; set; } = new();
}

public class CreateOrderItemDto
{
    [Range(1, long.MaxValue)] public long MenuItemId { get; set; }
    [Range(1, 1000)] public int Quantity { get; set; }
}

public class ChangeOrderStatusDto
{
    [Required] public string NewStatus { get; set; } = "";
    [StringLength(500)] public string? Note { get; set; }
}

public record OrderItemDto(long MenuItemId, string ProductName, decimal UnitPrice, int Quantity, decimal LineTotal);

public record OrderDto(
    long Id, long CustomerId, string Status, decimal TotalAmount, DateTime OrderDate,
    string DeliveryAddress, decimal? DeliveryLat, decimal? DeliveryLng, string? Comment,
    IReadOnlyList<OrderItemDto> Items)
{
    // Заповнює сервіс (із таблиці Payments): null, якщо активного платежу немає (наприклад, замовлення скасоване)
    public string? PaymentStatus { get; init; }
}

public record OrderSummaryDto(long Id, string Status, decimal TotalAmount, DateTime OrderDate,
                              string DeliveryAddress, int ItemCount);