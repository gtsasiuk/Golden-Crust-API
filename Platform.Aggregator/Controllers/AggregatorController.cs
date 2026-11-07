using Microsoft.AspNetCore.Mvc;
using Platform.Aggregator.Models;
using Platform.Aggregator.Services;

namespace Platform.Aggregator.Controllers;

[ApiController]
[Route("api/aggregator")]
public class AggregatorController(OrderAggregationService service, ILogger<AggregatorController> logger) : ControllerBase
{
    [HttpGet("orders/{id:long}/full")]
    [ProducesResponseType<FullOrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<FullOrderDto>> GetFullOrder(long id, CancellationToken ct)
    {
        try
        {
            var result = await service.GetFullOrderAsync(id, ct);
            return result is null ? NotFound() : Ok(result);
        }
        catch (UpstreamUnavailableException ex)
        {
            logger.LogError(ex, "Cannot build the full order {OrderId}: {Service}", id, ex.Message);
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                           title: "Orders service is unavailable",
                           detail: "The order cannot be assembled without orders-api. Try again later.");
        }
    }
}