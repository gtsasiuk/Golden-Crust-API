using Catalog.Bll.Dto;
using Catalog.Bll.Services;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Controllers;

[Route("api/tags")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
public class TagsController(ITagService service) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TagDto>>> GetAll(CancellationToken ct)
        => Ok(await service.GetAllAsync(ct));

    [HttpPost]
    [ProducesResponseType(typeof(TagDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<TagDto>> Create(CreateTagDto input, CancellationToken ct)
    {
        var created = await service.CreateAsync(input, ct);
        return CreatedAtAction(nameof(GetMenuItems), new { id = created.Id }, created);
    }

    [HttpGet("{id:long}/menu-items")]
    public async Task<ActionResult<IReadOnlyList<TagMenuItemDto>>> GetMenuItems(
        long id, [FromQuery] DateTime? since, CancellationToken ct)
        => Ok(await service.GetMenuItemsAsync(id, since, ct));
}