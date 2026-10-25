using Catalog.Bll.Dto;

namespace Catalog.Bll.Services;

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken ct = default);
    Task<CategoryWithItemsDto> GetByIdAsync(long id, CancellationToken ct = default);
    Task<CategoryDto> CreateAsync(CreateCategoryDto input, CancellationToken ct = default);
    Task<CategoryDto> UpdateAsync(long id, UpdateCategoryDto input, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);
}

public interface IMenuItemService
{
    Task<PagedResult<MenuItemListDto>> GetListAsync(MenuItemsQuery query, CancellationToken ct = default);
    Task<MenuItemDto> GetByIdAsync(long id, CancellationToken ct = default);
    Task<MenuItemDto> CreateAsync(CreateMenuItemDto input, CancellationToken ct = default);
    Task<MenuItemDto> UpdateAsync(long id, UpdateMenuItemDto input, CancellationToken ct = default);
    Task<MenuItemDto> ChangePriceAsync(long id, ChangePriceDto input, CancellationToken ct = default);
    Task<MenuItemDto> SetAvailabilityAsync(long id, SetAvailabilityDto input, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);
    Task<MenuItemDto> SetDetailsAsync(long id, UpdateDetailsDto input, CancellationToken ct = default);
    Task<ImageDto> AddImageAsync(long id, AddImageDto input, CancellationToken ct = default);
    Task SetPrimaryImageAsync(long id, long imageId, CancellationToken ct = default);
    Task RemoveImageAsync(long id, long imageId, CancellationToken ct = default);
    Task<MenuItemDto> AddTagAsync(long id, long tagId, CancellationToken ct = default);
    Task RemoveTagAsync(long id, long tagId, CancellationToken ct = default);
}

public interface ITagService
{
    Task<IReadOnlyList<TagDto>> GetAllAsync(CancellationToken ct = default);
    Task<TagDto> CreateAsync(CreateTagDto input, CancellationToken ct = default);
    Task<IReadOnlyList<TagMenuItemDto>> GetMenuItemsAsync(long tagId, DateTime? since, CancellationToken ct = default);
}