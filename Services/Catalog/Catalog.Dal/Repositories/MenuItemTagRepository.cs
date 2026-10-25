using Catalog.Dal.Interfaces;
using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Dal.Repositories;

public class MenuItemTagRepository(CatalogDbContext context)
    : GenericRepository<MenuItemTag>(context), IMenuItemTagRepository
{
    public override Task<MenuItemTag?> GetByIdAsync(long id, CancellationToken ct = default)
        => throw new NotSupportedException("MenuItemTag has a composite key");

    public async Task<IReadOnlyList<MenuItemTag>> GetByTagAsync(
        long tagId, DateTime? since, CancellationToken ct = default)
        => await Set.AsNoTracking()
            .Where(x => x.TagId == tagId && (since == null || x.AddedAt >= since))
            .Include(x => x.MenuItem).ThenInclude(m => m.Images)
            .OrderByDescending(x => x.AddedAt)
            .ToListAsync(ct);
}