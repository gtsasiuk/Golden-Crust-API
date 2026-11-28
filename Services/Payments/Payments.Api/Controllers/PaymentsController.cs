
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Payments.Api.Models;
using Payments.Api.Services;

namespace Payments.Api.Controllers;

[ApiController]
[Route("payments")]
[Authorize]
public class PaymentsController(
    PaymentStore store) : ControllerBase
{
    // GET /payments
    [HttpGet]
    [ProducesResponseType(
        typeof(IReadOnlyList<PaymentDto>),
        StatusCodes.Status200OK)]
    public IActionResult GetPayments()
    {
        return Ok(store.GetAll());
    }

    // POST /payments/process
    [HttpPost("process")]
    [ProducesResponseType(
        typeof(PaymentDto),
        StatusCodes.Status201Created)]
    public IActionResult ProcessPayment(
        ProcessPaymentRequest request)
    {
        if (request.OrderId <= 0)
            return BadRequest("OrderId must be greater than zero.");

        if (request.Amount <= 0)
            return BadRequest("Amount must be greater than zero.");

        if (string.IsNullOrWhiteSpace(request.Currency))
            return BadRequest("Currency is required.");

        var payment = store.Add(
            request.OrderId,
            request.Amount,
            request.Currency.ToUpperInvariant());

        return StatusCode(
            StatusCodes.Status201Created,
            payment);
    }

    // POST /payments/refund
    [HttpPost("refund")]
    [ProducesResponseType(
        typeof(PaymentDto),
        StatusCodes.Status200OK)]
    public IActionResult RefundPayment(
        RefundPaymentRequest request)
    {
        var existing = store.Find(request.PaymentId);

        if (existing is null)
            return NotFound("Payment was not found.");

        if (existing.Status == "Refunded")
            return Conflict("Payment has already been refunded.");

        var refunded = store.Refund(
            request.PaymentId,
            request.Reason);

        if (refunded is null)
            return Conflict("Payment could not be refunded.");

        return Ok(refunded);
    }
}
