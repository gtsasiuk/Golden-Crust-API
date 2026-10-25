using Catalog.Domain.Entities;

namespace Catalog.Dal.Interfaces;

public interface IUnitOfWork
{
	IMenuItemRepository MenuItems { get; }
	IMenuCategoryRepository Categories { get; }
	IMenuItemTagRepository ItemTags { get; }
	IGenericRepository<Tag> Tags { get; }

	Task<int> SaveChangesAsync(CancellationToken ct = default);

	Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken ct = default);
}