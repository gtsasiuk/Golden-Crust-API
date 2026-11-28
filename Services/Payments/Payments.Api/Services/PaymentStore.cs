
using Payments.Api.Models;

namespace Payments.Api.Services;

public sealed class PaymentStore
{
    private readonly List<PaymentDto> _payments = [];
    private readonly object _lock = new();

    public IReadOnlyList<PaymentDto> GetAll()
    {
        lock (_lock)
        {
            return _payments
                .OrderByDescending(x => x.CreatedAt)
                .ToArray();
        }
    }

    public PaymentDto Add(
        long orderId,
        decimal amount,
        string currency)
    {
        var payment = new PaymentDto(
            Id: Guid.NewGuid(),
            OrderId: orderId,
            Amount: amount,
            Currency: currency,
            Status: "Processed",
            CreatedAt: DateTimeOffset.UtcNow
        );

        lock (_lock)
        {
            _payments.Add(payment);
        }

        return payment;
    }

    public PaymentDto? Find(Guid id)
    {
        lock (_lock)
        {
            return _payments.FirstOrDefault(x => x.Id == id);
        }
    }

    public PaymentDto? Refund(Guid id, string? reason)
    {
        lock (_lock)
        {
            var index = _payments.FindIndex(x => x.Id == id);

            if (index < 0)
                return null;

            var existing = _payments[index];

            if (existing.Status == "Refunded")
                return null;

            var refunded = existing with
            {
                Status = "Refunded",
                RefundedAt = DateTimeOffset.UtcNow,
                RefundReason = reason
            };

            _payments[index] = refunded;

            return refunded;
        }
    }
}
