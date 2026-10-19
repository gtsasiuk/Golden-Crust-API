using Orders.Domain;

namespace Orders.Dal.Interfaces;

public interface IOrderRepository : IGenericRepository<Order>
{
    Task<Order?> GetWithItemsAsync(long orderId, CancellationToken ct = default);
    Task AddItemsAsync(IEnumerable<OrderItem> items, CancellationToken ct = default);
    Task AddHistoryAsync(OrderStatusHistoryEntry entry, CancellationToken ct = default);
    Task<IReadOnlyList<OrderSummary>> GetByCustomerAsync(
        long customerId, string? status, int pageNumber, int pageSize, CancellationToken ct = default); // usp_GetCustomerOrders

    Task ConfirmAsync(long orderId, CancellationToken ct = default);                                     // usp_ConfirmOrder
    Task ChangeStatusAsync(long orderId, string newStatus, string? note = null, CancellationToken ct = default); // usp_ChangeStatus
    Task CancelAsync(long orderId, string? reason = null, CancellationToken ct = default);               // usp_CancelOrder
}