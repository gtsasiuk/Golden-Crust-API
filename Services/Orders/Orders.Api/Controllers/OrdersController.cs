using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orders.Api.Security;
using Orders.Bll.Dtos;
using Orders.Bll.Services;
using Platform.ServiceDefaults;

namespace Orders.Api.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public class OrdersController(IOrderService orders, IAccessGuard guard, IWebHostEnvironment env) : ControllerBase
{
    [HttpGet("{id:long}")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> GetById(long id, CancellationToken ct)
    {
        await guard.EnsureOrderAccessAsync(User, id, ct);
        return Ok(await orders.GetByIdAsync(id, ct));
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]   // клієнта не існує
    public async Task<ActionResult<OrderDto>> Create(CreateOrderDto input, CancellationToken ct)
    {
        // Замовлення можна створити лише на власного клієнта (Admin: на будь-якого)
        await guard.EnsureCustomerAccessAsync(User, input.CustomerId, ct);

        var created = await orders.CreateAsync(input, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    // Абсолютний шлях: ресурс належить клієнту, але відповідь це замовлення
    [HttpGet("/api/customers/{customerId:long}/orders")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<OrderSummaryDto>>> GetByCustomer(
        long customerId, [FromQuery] string? status, CancellationToken ct,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        await guard.EnsureCustomerAccessAsync(User, customerId, ct);
        return Ok(await orders.GetByCustomerAsync(customerId, status, page, pageSize, ct));
    }

    // Оплата + підтвердження в одній транзакції
    [HttpPost("{id:long}/checkout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Checkout(long id, CancellationToken ct, [FromQuery] bool simulateFailure = false)
    {
        await guard.EnsureOrderAccessAsync(User, id, ct);

        // Демо відкату працює лише в Development
        await orders.CheckoutAsync(id, simulateFailure && env.IsDevelopment(), ct);
        return NoContent();
    }

    // Кухонні статуси змінює лише Admin (ролі "кухар" поки немає)
    [HttpPost("{id:long}/status")]
    [Authorize(Roles = UserClaimsExtensions.AdminRole)]
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
        await guard.EnsureOrderAccessAsync(User, id, ct);
        await orders.CancelAsync(id, reason, ct);
        return NoContent();
    }
}