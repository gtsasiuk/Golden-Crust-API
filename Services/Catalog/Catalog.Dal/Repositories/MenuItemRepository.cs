using Catalog.Dal.Interfaces;
using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Dal.Repositories;

public class MenuItemRepository(CatalogDbContext context)
    : GenericRepository<MenuItem>(context), IMenuItemRepository
{
    public Task<MenuItem?> GetWithDetailsAsync(long id, CancellationToken ct = default)
        => Set.Include(m => m.Category)
              .Include(m => m.Detail)
              .Include(m => m.Images)
              .Include(m => m.ItemTags).ThenInclude(t => t.Tag)
              .FirstOrDefaultAsync(m => m.Id == id, ct);
}