using Orders.Domain;

namespace Orders.Dal.Interfaces;

public interface IPaymentRepository : IGenericRepository<Payment>
{
    Task<Payment?> GetActiveByOrderIdAsync(long orderId, CancellationToken ct = default);
    Task PayAsync(long orderId, string? transactionRef = null, CancellationToken ct = default);   // usp_PayOrder
}