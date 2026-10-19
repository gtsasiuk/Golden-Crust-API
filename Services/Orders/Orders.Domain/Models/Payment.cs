namespace Orders.Domain;

public class Payment
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = PaymentStatus.Pending;
    public DateTime? PaidAt { get; set; }
    public string? TransactionRef { get; set; }
}