using Catalog.Dal.Interfaces;
using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Dal.Repositories;

public class MenuCategoryRepository(CatalogDbContext context)
    : GenericRepository<MenuCategory>(context), IMenuCategoryRepository
{
    public async Task<MenuCategory?> GetWithItemsAsync(long id, CancellationToken ct = default)
    {
        var category = await Set.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (category is null) return null;

        await Context.Entry(category)
            .Collection(c => c.Items)
            .Query().Include(i => i.Images)
            .LoadAsync(ct);
        return category;
    }

    public async Task<IReadOnlyList<MenuCategory>> GetOrderedAsync(CancellationToken ct = default)
        => await Set.AsNoTracking().OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToListAsync(ct);

    public Task<bool> HasItemsAsync(long categoryId, CancellationToken ct = default)
        => Context.MenuItems.AnyAsync(m => m.CategoryId == categoryId, ct);

    public Task<bool> NameExistsAsync(string name, long? excludeId, CancellationToken ct = default)
    => Set.IgnoreQueryFilters()
          .AnyAsync(c => c.Name == name && (excludeId == null || c.Id != excludeId), ct);

    public async Task<IReadOnlyList<long>> GetItemIdsAsync(long categoryId, CancellationToken ct = default)
        => await Context.MenuItems.AsNoTracking()
            .Where(m => m.CategoryId == categoryId)
            .Select(m => m.Id)
            .ToListAsync(ct);
}