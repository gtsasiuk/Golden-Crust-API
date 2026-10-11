namespace Orders.Domain;

// Рядкові константи, а не enum: у БД статус це NVARCHAR з CHECK, тож маємо ті самі значення без перетворень.
public static class OrderStatus
{
    public const string Placed = "Placed";
    public const string Accepted = "Accepted";
    public const string Cooking = "Cooking";
    public const string ReadyForDelivery = "ReadyForDelivery";
    public const string Delivering = "Delivering";
    public const string Delivered = "Delivered";
    public const string Cancelled = "Cancelled";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Placed, Accepted, Cooking, ReadyForDelivery, Delivering, Delivered, Cancelled
    };
}

public static class PaymentStatus
{
    public const string Pending = "Pending";
    public const string Paid = "Paid";
    public const string Refunded = "Refunded";
    public const string Cancelled = "Cancelled";
}