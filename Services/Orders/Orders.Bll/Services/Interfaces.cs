using Orders.Bll.Dtos;

namespace Orders.Bll.Services;

public interface ICustomerService
{
    Task<IReadOnlyList<CustomerDto>> GetAllAsync(CancellationToken ct = default);
    Task<CustomerDto> GetByIdAsync(long id, CancellationToken ct = default);
    Task<CustomerDto> CreateAsync(CreateCustomerDto dto, CancellationToken ct = default);
    Task UpdateAsync(long id, UpdateCustomerDto dto, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);
}

public interface IMenuService
{
    Task<IReadOnlyList<MenuItemDto>> GetAllAsync(CancellationToken ct = default);
    Task<MenuItemDto> GetByIdAsync(long id, CancellationToken ct = default);
}

public interface IOrderService
{
    Task<OrderDto> CreateAsync(CreateOrderDto dto, CancellationToken ct = default);
    Task<OrderDto> GetByIdAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<OrderSummaryDto>> GetByCustomerAsync(
        long customerId, string? status, int pageNumber, int pageSize, CancellationToken ct = default);

    // Транзакція: оплата + підтвердження. simulateFailure: демо відкату (кидає помилку після оплати)
    Task CheckoutAsync(long orderId, bool simulateFailure = false, CancellationToken ct = default);
    Task ChangeStatusAsync(long orderId, ChangeOrderStatusDto dto, CancellationToken ct = default);
    Task CancelAsync(long orderId, string? reason = null, CancellationToken ct = default);
}