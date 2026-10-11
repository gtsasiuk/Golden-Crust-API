using Microsoft.AspNetCore.Mvc;
using Orders.Bll.Dtos;
using Orders.Bll.Services;

namespace Orders.Api.Controllers;

[ApiController]
[Route("api/orders")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
public class OrdersController(IOrderService orders, IWebHostEnvironment env) : ControllerBase
{
    [HttpGet("{id:long}")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> GetById(long id, CancellationToken ct)
        => Ok(await orders.GetByIdAsync(id, ct));

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]   // клієнта не існує
    public async Task<ActionResult<OrderDto>> Create(CreateOrderDto input, CancellationToken ct)
    {
        var created = await orders.CreateAsync(input, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    // Абсолютний шлях: ресурс належить клієнту, але відповідь це замовлення
    [HttpGet("/api/customers/{customerId:long}/orders")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<OrderSummaryDto>>> GetByCustomer(
        long customerId, [FromQuery] string? status, CancellationToken ct,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await orders.GetByCustomerAsync(customerId, status, page, pageSize, ct));

    // Оплата + підтвердження в одній транзакції
    [HttpPost("{id:long}/checkout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Checkout(long id, CancellationToken ct, [FromQuery] bool simulateFailure = false)
    {
        // Демо відкату працює лише в Development
        await orders.CheckoutAsync(id, simulateFailure && env.IsDevelopment(), ct);
        return NoContent();
    }

    [HttpPost("{id:long}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeStatus(long id, ChangeOrderStatusDto input, CancellationToken ct)
    {
        await orders.ChangeStatusAsync(id, input, ct);
        return NoContent();
    }

    [HttpPost("{id:long}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(long id, CancellationToken ct, [FromQuery] string? reason = null)
    {
        await orders.CancelAsync(id, reason, ct);
        return NoContent();
    }
}