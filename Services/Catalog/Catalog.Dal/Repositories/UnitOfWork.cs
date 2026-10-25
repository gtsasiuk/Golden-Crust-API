using Catalog.Dal.Interfaces;
using Catalog.Domain.Entities;

namespace Catalog.Dal.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly CatalogDbContext _context;

    public UnitOfWork(CatalogDbContext context)
    {
        _context = context;
        MenuItems = new MenuItemRepository(context);
        Categories = new MenuCategoryRepository(context);
        ItemTags = new MenuItemTagRepository(context);
        Tags = new GenericRepository<Tag>(context);
    }

    public IMenuItemRepository MenuItems { get; }
    public IMenuCategoryRepository Categories { get; }
    public IMenuItemTagRepository ItemTags { get; }
    public IGenericRepository<Tag> Tags { get; }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _context.SaveChangesAsync(ct);

    public async Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken ct = default)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(ct);
        await action();
        await tx.CommitAsync(ct);
    }
}