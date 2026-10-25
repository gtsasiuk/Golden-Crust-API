using Catalog.Domain.Entities;

namespace Catalog.Dal.Interfaces;

public interface IMenuItemTagRepository : IGenericRepository<MenuItemTag>
{
	Task<IReadOnlyList<MenuItemTag>> GetByTagAsync(long tagId, DateTime? since, CancellationToken ct = default);
}