using Orders.Domain;

namespace Orders.Dal.Interfaces;

public interface IMenuItemRepository
{
    Task<MenuItem?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<MenuItem>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<MenuItem>> GetByIdsAsync(IEnumerable<long> ids, CancellationToken ct = default);
}