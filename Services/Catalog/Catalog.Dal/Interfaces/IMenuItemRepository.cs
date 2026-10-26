using Catalog.Domain.Entities;

namespace Catalog.Dal.Interfaces;

public interface IMenuItemRepository : IGenericRepository<MenuItem>
{
    Task<MenuItem?> GetWithDetailsAsync(long id, CancellationToken ct = default);
}