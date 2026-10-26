using AutoMapper;
using Catalog.Bll.Dto;
using Catalog.Dal.Interfaces;
using Catalog.Domain.Entities;
using Catalog.Domain.Exceptions;

namespace Catalog.Bll.Services;

public class CategoryService(IUnitOfWork uow, IMapper mapper) : ICategoryService
{
    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken ct = default)
        => mapper.Map<List<CategoryDto>>(await uow.Categories.GetOrderedAsync(ct));

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
        return mapper.Map<CategoryDto>(category);
    }

    public async Task<CategoryDto> UpdateAsync(long id, UpdateCategoryDto input, CancellationToken ct = default)
    {
        var category = await uow.Categories.GetByIdAsync(id, ct)
                       ?? throw new EntityNotFoundException(nameof(MenuCategory), id);

        var name = input.Name.Trim();
        if (await uow.Categories.NameExistsAsync(name, id, ct))
            throw new ConflictException($"Category '{name}' already exists");

        category.Rename(name);
        category.ChangeSortOrder(input.SortOrder);
        await uow.SaveChangesAsync(ct);
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
    }
}