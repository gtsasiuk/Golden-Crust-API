using Microsoft.AspNetCore.Mvc;
using Orders.Bll.Dtos;
using Orders.Bll.Services;

namespace Orders.Api.Controllers;

// Лише читання: локальна копія довідника, власник даних Catalog
[ApiController]
[Route("api/menu-items")]
public class MenuItemsController(IMenuService menu) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MenuItemDto>>> GetAll(CancellationToken ct)
        => Ok(await menu.GetAllAsync(ct));

    [HttpGet("{id:long}")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MenuItemDto>> GetById(long id, CancellationToken ct)
        => Ok(await menu.GetByIdAsync(id, ct));
}