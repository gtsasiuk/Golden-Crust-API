using Ardalis.Specification;
using Catalog.Domain.Entities;

namespace Catalog.Dal.Specifications;

public abstract class MenuItemsBaseSpec : Specification<MenuItem>
{
    protected MenuItemsBaseSpec(MenuItemFilter f)
    {
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var search = f.Search.Trim();
            Query.Where(m => m.Name.Contains(search));
        }
        if (f.CategoryId is { } categoryId)
            Query.Where(m => m.CategoryId == categoryId);
        if (f.TagId is { } tagId)
            Query.Where(m => m.ItemTags.Any(t => t.TagId == tagId));
        if (f.MinPrice is { } min)
            Query.Where(m => m.Price >= min);
        if (f.MaxPrice is { } max)
            Query.Where(m => m.Price <= max);
        if (f.IsAvailable is { } available)
            Query.Where(m => m.IsAvailable == available);
    }
}

public class MenuItemsCountSpec(MenuItemFilter filter) : MenuItemsBaseSpec(filter);

public class MenuItemsPagedSpec : MenuItemsBaseSpec
{
    public MenuItemsPagedSpec(MenuItemFilter f, int page, int pageSize) : base(f)
    {
        Query.AsNoTracking();
        Query.Include(m => m.Category).Include(m => m.Images);

        switch (f.SortBy.ToLowerInvariant(), f.Descending)
        {
            case ("price", true): Query.OrderByDescending(m => m.Price).ThenBy(m => m.Id); break;
            case ("price", false): Query.OrderBy(m => m.Price).ThenBy(m => m.Id); break;
            case (_, true): Query.OrderByDescending(m => m.Name).ThenBy(m => m.Id); break;
            default: Query.OrderBy(m => m.Name).ThenBy(m => m.Id); break;
        }

        Query.Skip((page - 1) * pageSize).Take(pageSize);
    }
}