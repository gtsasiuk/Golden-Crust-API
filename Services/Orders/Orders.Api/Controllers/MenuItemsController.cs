using Microsoft.AspNetCore.Mvc;
using Orders.Bll.Dtos;
using Orders.Bll.Services;
using Microsoft.AspNetCore.Authorization;

namespace Orders.Api.Controllers;

// Лише читання: локальна копія довідника, власник даних Catalog
[ApiController]
[Authorize]
[Route("api/menu-items")]
public class MenuItemsController(IMenuService menu) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "orders.read")]
    public async Task<ActionResult<IReadOnlyList<MenuItemDto>>> GetAll(CancellationToken ct)
        => Ok(await menu.GetAllAsync(ct));

    [HttpGet("{id:long}")]
    [Authorize(Policy = "orders.read")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MenuItemDto>> GetById(long id, CancellationToken ct)
        => Ok(await menu.GetByIdAsync(id, ct));
}