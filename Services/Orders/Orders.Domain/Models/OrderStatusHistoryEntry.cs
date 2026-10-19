namespace Orders.Domain;

public class OrderStatusHistoryEntry
{
    public long OrderId { get; set; }
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = "";
    public string? ChangedBy { get; set; }
    public string? Note { get; set; }
}