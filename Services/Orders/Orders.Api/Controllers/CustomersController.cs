using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orders.Api.Security;
using Orders.Bll.Dtos;
using Orders.Bll.Services;
using Platform.ServiceDefaults;

namespace Orders.Api.Controllers;

[ApiController]
[Route("api/customers")]
[Authorize]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public class CustomersController(ICustomerService customers, IAccessGuard guard) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = UserClaimsExtensions.AdminRole)]
    public async Task<ActionResult<IReadOnlyList<CustomerDto>>> GetAll(CancellationToken ct)
        => Ok(await customers.GetAllAsync(ct));

    [HttpGet("{id:long}")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerDto>> GetById(long id, CancellationToken ct)
    {
        await guard.EnsureCustomerAccessAsync(User, id, ct);
        return Ok(await customers.GetByIdAsync(id, ct));
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CustomerDto>> Create(CreateCustomerDto input, CancellationToken ct)
    {
        guard.EnsureOwnEmail(User, input.Email);
        var created = await customers.CreateAsync(input, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(long id, UpdateCustomerDto input, CancellationToken ct)
    {
        await guard.EnsureCustomerAccessAsync(User, id, ct);
        guard.EnsureOwnEmail(User, input.Email);
        await customers.UpdateAsync(id, input, ct);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    [Authorize(Roles = UserClaimsExtensions.AdminRole)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await customers.DeleteAsync(id, ct);
        return NoContent();
    }
}