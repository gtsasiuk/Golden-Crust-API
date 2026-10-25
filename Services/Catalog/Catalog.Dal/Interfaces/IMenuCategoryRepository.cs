using Catalog.Domain.Entities;

namespace Catalog.Dal.Interfaces;

public interface IMenuCategoryRepository : IGenericRepository<MenuCategory>
{
	Task<MenuCategory?> GetWithItemsAsync(long id, CancellationToken ct = default);

	Task<IReadOnlyList<MenuCategory>> GetOrderedAsync(CancellationToken ct = default);
	Task<bool> HasItemsAsync(long categoryId, CancellationToken ct = default);
}
