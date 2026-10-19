using System.Data;

namespace Orders.Dal.Interfaces;

public interface IUnitOfWork : IAsyncDisposable
{
    ICustomerRepository Customers { get; }
    IMenuItemRepository MenuItems { get; }
    IOrderRepository Orders { get; }
    IPaymentRepository Payments { get; }

    Task BeginTransactionAsync(IsolationLevel level = IsolationLevel.ReadCommitted, CancellationToken ct = default);
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}