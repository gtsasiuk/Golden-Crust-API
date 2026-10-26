using AutoMapper;
using Catalog.Bll.Dto;
using Catalog.Dal.Interfaces;
using Catalog.Dal.Specifications;
using Catalog.Domain.Entities;
using Catalog.Domain.Exceptions;

namespace Catalog.Bll.Services;

public class MenuItemService(IUnitOfWork uow, IMapper mapper) : IMenuItemService
{
    private const int MaxPageSize = 100;

    public async Task<PagedResult<MenuItemListDto>> GetListAsync(MenuItemsQuery q, CancellationToken ct = default)
    {
        var page = Math.Max(q.Page, 1);
        var pageSize = Math.Clamp(q.PageSize, 1, MaxPageSize);
        var filter = new MenuItemFilter(q.Search, q.CategoryId, q.TagId, q.MinPrice, q.MaxPrice,
                                        q.IsAvailable, q.SortBy, string.Equals(q.SortDir, "desc", StringComparison.OrdinalIgnoreCase));

        var total = await uow.MenuItems.CountAsync(new MenuItemsCountSpec(filter), ct);
        var items = await uow.MenuItems.ListAsync(new MenuItemsPagedSpec(filter, page, pageSize), ct);

        return new PagedResult<MenuItemListDto>(mapper.Map<List<MenuItemListDto>>(items), total, page, pageSize);
    }

    public async Task<MenuItemDto> GetByIdAsync(long id, CancellationToken ct = default)
        => mapper.Map<MenuItemDto>(await LoadAsync(id, ct));

    public async Task<MenuItemDto> CreateAsync(CreateMenuItemDto input, CancellationToken ct = default)
    {
        await EnsureCategoryExistsAsync(input.CategoryId, ct);

        var name = input.Name.Trim();
        if (await uow.MenuItems.ExistsAsync(m => m.Name == name, ct))
            throw new ConflictException($"Menu item '{name}' already exists");

        var item = new MenuItem(input.CategoryId, name, input.Price);
        await uow.MenuItems.AddAsync(item, ct);
        await uow.SaveChangesAsync(ct);
        return await GetByIdAsync(item.Id, ct);
    }

    public async Task<MenuItemDto> UpdateAsync(long id, UpdateMenuItemDto input, CancellationToken ct = default)
    {
        var item = await uow.MenuItems.GetByIdAsync(id, ct)
                   ?? throw new EntityNotFoundException(nameof(MenuItem), id);

        await EnsureCategoryExistsAsync(input.CategoryId, ct);
        var name = input.Name.Trim();
        if (await uow.MenuItems.ExistsAsync(m => m.Name == name && m.Id != id, ct))
            throw new ConflictException($"Menu item '{name}' already exists");

        item.Rename(name);
        item.MoveToCategory(input.CategoryId);
        item.ChangePrice(input.Price);
        await uow.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task<MenuItemDto> ChangePriceAsync(long id, ChangePriceDto input, CancellationToken ct = default)
    {
        var item = await uow.MenuItems.GetByIdAsync(id, ct)
                   ?? throw new EntityNotFoundException(nameof(MenuItem), id);
        item.ChangePrice(input.Price);
        await uow.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task<MenuItemDto> SetAvailabilityAsync(long id, SetAvailabilityDto input, CancellationToken ct = default)
    {
        var item = await uow.MenuItems.GetByIdAsync(id, ct)
                   ?? throw new EntityNotFoundException(nameof(MenuItem), id);
        item.SetAvailability(input.IsAvailable);
        await uow.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var item = await uow.MenuItems.GetByIdAsync(id, ct)
                   ?? throw new EntityNotFoundException(nameof(MenuItem), id);
        item.SoftDelete();
        await uow.SaveChangesAsync(ct);
    }

    public async Task<MenuItemDto> SetDetailsAsync(long id, UpdateDetailsDto input, CancellationToken ct = default)
    {
        var item = await LoadAsync(id, ct);
        item.SetDetail(input.Description, input.Ingredients, input.WeightGrams, input.Calories);
        await uow.SaveChangesAsync(ct);
        return mapper.Map<MenuItemDto>(item);
    }

    public async Task<ImageDto> AddImageAsync(long id, AddImageDto input, CancellationToken ct = default)
    {
        var item = await LoadAsync(id, ct);
        var image = item.AddImage(input.Url, input.AltText);
        await uow.SaveChangesAsync(ct);
        return mapper.Map<ImageDto>(image);
    }

    public async Task SetPrimaryImageAsync(long id, long imageId, CancellationToken ct = default)
    {
        var item = await LoadAsync(id, ct);
        var target = item.Images.FirstOrDefault(i => i.Id == imageId)
                     ?? throw new EntityNotFoundException(nameof(MenuItemImage), imageId);
        if (target.IsPrimary) return;

        await uow.ExecuteInTransactionAsync(async () =>
        {
            item.UnsetPrimaryImages();
            await uow.SaveChangesAsync(ct);
            item.SetPrimaryImage(imageId);
            await uow.SaveChangesAsync(ct);
        }, ct);
    }

    public async Task RemoveImageAsync(long id, long imageId, CancellationToken ct = default)
    {
        var item = await LoadAsync(id, ct);
        item.RemoveImage(imageId);
        await uow.SaveChangesAsync(ct);
    }

    public async Task<MenuItemDto> AddTagAsync(long id, long tagId, CancellationToken ct = default)
    {
        var item = await LoadAsync(id, ct);
        if (await uow.Tags.GetByIdAsync(tagId, ct) is null)
            throw new EntityNotFoundException(nameof(Tag), tagId);

        item.AddTag(tagId);
        await uow.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task RemoveTagAsync(long id, long tagId, CancellationToken ct = default)
    {
        var item = await LoadAsync(id, ct);
        item.RemoveTag(tagId);
        await uow.SaveChangesAsync(ct);
    }

    private async Task<MenuItem> LoadAsync(long id, CancellationToken ct)
        => await uow.MenuItems.GetWithDetailsAsync(id, ct)
           ?? throw new EntityNotFoundException(nameof(MenuItem), id);

    private async Task EnsureCategoryExistsAsync(long categoryId, CancellationToken ct)
    {
        if (await uow.Categories.GetByIdAsync(categoryId, ct) is null)
            throw new EntityNotFoundException(nameof(MenuCategory), categoryId);
    }
}