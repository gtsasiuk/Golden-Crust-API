using Microsoft.AspNetCore.Mvc;
using Orders.Bll.Dtos;
using Orders.Bll.Services;

namespace Orders.Api.Controllers;

[ApiController]
[Route("api/customers")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
public class CustomersController(ICustomerService customers) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CustomerDto>>> GetAll(CancellationToken ct)
        => Ok(await customers.GetAllAsync(ct));

    [HttpGet("{id:long}")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerDto>> GetById(long id, CancellationToken ct)
        => Ok(await customers.GetByIdAsync(id, ct));

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]   // email уже зайнятий
    public async Task<ActionResult<CustomerDto>> Create(CreateCustomerDto input, CancellationToken ct)
    {
        var created = await customers.CreateAsync(input, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(long id, UpdateCustomerDto input, CancellationToken ct)
    {
        await customers.UpdateAsync(id, input, ct);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await customers.DeleteAsync(id, ct);
        return NoContent();
    }
}