using Catalog.Bll.Dto;
using Catalog.Bll.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Catalog.Api.Controllers;

[Route("api/categories")]
[Authorize(Roles = "Admin")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
public class CategoriesController(ICategoryService service) : ApiControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    [Authorize(Policy = "products.read")]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> GetAll(CancellationToken ct)
        => Ok(await service.GetAllAsync(ct));

    [HttpGet("{id:long}")]
    [AllowAnonymous]
    [Authorize(Policy = "products.read")]
    public async Task<ActionResult<CategoryWithItemsDto>> GetById(long id, CancellationToken ct)
        => Ok(await service.GetByIdAsync(id, ct));

    [HttpPost]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
    [Authorize(Policy = "products.write")]
    public async Task<ActionResult<CategoryDto>> Create(CreateCategoryDto input, CancellationToken ct)
    {
        if (await ValidateAsync(input, ct) is { } invalid) return invalid;
        var created = await service.CreateAsync(input, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = "products.write")]
    public async Task<ActionResult<CategoryDto>> Update(long id, UpdateCategoryDto input, CancellationToken ct)
    {
        if (await ValidateAsync(input, ct) is { } invalid) return invalid;
        return Ok(await service.UpdateAsync(id, input, ct));
    }

    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [Authorize(Policy = "products.write")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}