using Catalog.Bll.Dto;
using Catalog.Bll.Services;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Controllers;

[Route("api/menu-items")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
public class MenuItemsController(IMenuItemService service) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<MenuItemListDto>>> GetList(
        [FromQuery] MenuItemsQuery query, CancellationToken ct)
    {
        if (await ValidateAsync(query, ct) is { } invalid) return invalid;
        var result = await service.GetListAsync(query, ct);
        Response.Headers["X-Total-Count"] = result.TotalCount.ToString();
        return Ok(result);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<MenuItemDto>> GetById(long id, CancellationToken ct)
        => Ok(await service.GetByIdAsync(id, ct));

    [HttpPost]
    [ProducesResponseType(typeof(MenuItemDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<MenuItemDto>> Create(CreateMenuItemDto input, CancellationToken ct)
    {
        if (await ValidateAsync(input, ct) is { } invalid) return invalid;
        var created = await service.CreateAsync(input, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<MenuItemDto>> Update(long id, UpdateMenuItemDto input, CancellationToken ct)
    {
        if (await ValidateAsync(input, ct) is { } invalid) return invalid;
        return Ok(await service.UpdateAsync(id, input, ct));
    }

    [HttpPatch("{id:long}/price")]
    public async Task<ActionResult<MenuItemDto>> ChangePrice(long id, ChangePriceDto input, CancellationToken ct)
    {
        if (await ValidateAsync(input, ct) is { } invalid) return invalid;
        return Ok(await service.ChangePriceAsync(id, input, ct));
    }

    [HttpPatch("{id:long}/availability")]
    public async Task<ActionResult<MenuItemDto>> SetAvailability(long id, SetAvailabilityDto input, CancellationToken ct)
        => Ok(await service.SetAvailabilityAsync(id, input, ct));

    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPut("{id:long}/details")]
    public async Task<ActionResult<MenuItemDto>> SetDetails(long id, UpdateDetailsDto input, CancellationToken ct)
    {
        if (await ValidateAsync(input, ct) is { } invalid) return invalid;
        return Ok(await service.SetDetailsAsync(id, input, ct));
    }

    [HttpPost("{id:long}/images")]
    [ProducesResponseType(typeof(ImageDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<ImageDto>> AddImage(long id, AddImageDto input, CancellationToken ct)
    {
        if (await ValidateAsync(input, ct) is { } invalid) return invalid;
        var image = await service.AddImageAsync(id, input, ct);
        return CreatedAtAction(nameof(GetById), new { id }, image);
    }

    [HttpPut("{id:long}/images/{imageId:long}/primary")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetPrimaryImage(long id, long imageId, CancellationToken ct)
    {
        await service.SetPrimaryImageAsync(id, imageId, ct);
        return NoContent();
    }

    [HttpDelete("{id:long}/images/{imageId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveImage(long id, long imageId, CancellationToken ct)
    {
        await service.RemoveImageAsync(id, imageId, ct);
        return NoContent();
    }

    [HttpPost("{id:long}/tags/{tagId:long}")]
    public async Task<ActionResult<MenuItemDto>> AddTag(long id, long tagId, CancellationToken ct)
        => Ok(await service.AddTagAsync(id, tagId, ct));

    [HttpDelete("{id:long}/tags/{tagId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveTag(long id, long tagId, CancellationToken ct)
    {
        await service.RemoveTagAsync(id, tagId, ct);
        return NoContent();
    }
}