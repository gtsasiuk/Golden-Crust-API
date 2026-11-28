
namespace Payments.Api.Models;

public sealed record PaymentDto(
    Guid Id,
    long OrderId,
    decimal Amount,
    string Currency,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RefundedAt = null,
    string? RefundReason = null
);

public sealed record ProcessPaymentRequest(
    long OrderId,
    decimal Amount,
    string Currency = "UAH"
);

public sealed record RefundPaymentRequest(
    Guid PaymentId,
    string? Reason
);
