using AutoMapper;
using Catalog.Bll.Caching;
using Catalog.Bll.Dto;
using Catalog.Dal.Interfaces;
using Catalog.Domain.Entities;
using Catalog.Domain.Exceptions;

namespace Catalog.Bll.Services;

public class CategoryService(IUnitOfWork uow, IMapper mapper, ICacheService cache) : ICategoryService
{
    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken ct = default)
    {
        var categories = await cache.GetOrCreateAsync(CacheKeys.Categories, async () =>
            mapper.Map<List<CategoryDto>>(await uow.Categories.GetOrderedAsync(ct)),
            CachePolicy.Categories, ct);
        return categories!;
    }

    public async Task<CategoryWithItemsDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var category = await uow.Categories.GetWithItemsAsync(id, ct)
                       ?? throw new EntityNotFoundException(nameof(MenuCategory), id);
        return mapper.Map<CategoryWithItemsDto>(category);
    }

    public async Task<CategoryDto> CreateAsync(CreateCategoryDto input, CancellationToken ct = default)
    {
        var name = input.Name.Trim();
        if (await uow.Categories.NameExistsAsync(name, null, ct))
            throw new ConflictException($"Category '{name}' already exists");

        var category = new MenuCategory(name, input.SortOrder);
        await uow.Categories.AddAsync(category, ct);
        await uow.SaveChangesAsync(ct);
        await cache.RemoveAsync(CacheKeys.Categories, "CategoryCreated", ct);
        return mapper.Map<CategoryDto>(category);
    }

    public async Task<CategoryDto> UpdateAsync(long id, UpdateCategoryDto input, CancellationToken ct = default)
    {
        var category = await uow.Categories.GetByIdAsync(id, ct)
                       ?? throw new EntityNotFoundException(nameof(MenuCategory), id);

        var name = input.Name.Trim();
        if (await uow.Categories.NameExistsAsync(name, id, ct))
            throw new ConflictException($"Category '{name}' already exists");

        var nameChanged = category.Name != name;

        category.Rename(name);
        category.ChangeSortOrder(input.SortOrder);
        await uow.SaveChangesAsync(ct);

        await cache.RemoveAsync(CacheKeys.Categories, "CategoryUpdated", ct);

        if (nameChanged)
            foreach (var itemId in await uow.Categories.GetItemIdsAsync(id, ct))
                await cache.RemoveAsync(CacheKeys.MenuItem(itemId), "CategoryRenamed", ct);

        return mapper.Map<CategoryDto>(category);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var category = await uow.Categories.GetByIdAsync(id, ct)
                       ?? throw new EntityNotFoundException(nameof(MenuCategory), id);

        if (await uow.Categories.HasItemsAsync(id, ct))
            throw new ConflictException("Category has menu items and cannot be deleted");

        category.SoftDelete();
        await uow.SaveChangesAsync(ct);
        await cache.RemoveAsync(CacheKeys.Categories, "CategoryDeleted", ct);
    }
}