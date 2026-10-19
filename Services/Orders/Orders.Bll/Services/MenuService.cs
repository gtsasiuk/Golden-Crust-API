using AutoMapper;
using Orders.Bll.Dtos;
using Orders.Dal.Interfaces;
using Orders.Domain;

namespace Orders.Bll.Services;

public class MenuService(IUnitOfWork uow, IMapper mapper) : IMenuService
{
    public async Task<IReadOnlyList<MenuItemDto>> GetAllAsync(CancellationToken ct = default)
        => mapper.Map<IReadOnlyList<MenuItemDto>>(await uow.MenuItems.GetAllAsync(ct));

    public async Task<MenuItemDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var item = await uow.MenuItems.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Menu item {id} not found");
        return mapper.Map<MenuItemDto>(item);
    }
}