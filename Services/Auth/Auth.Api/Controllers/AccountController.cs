using Auth.Api.Common;
using Auth.Api.Contracts;
using Auth.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Auth.Api.Controllers;

[ApiController]
[Route("api/auth")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
public class AccountController(IAccountService account) : ControllerBase
{
    [HttpPost("verify-email")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> VerifyEmail(VerifyEmailRequest request, CancellationToken ct)
    {
        await account.VerifyEmailAsync(request, ct);
        return NoContent();
    }

    [HttpPost("resend-verification")]
    [AllowAnonymous]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MessageResponse>> ResendVerification(EmailRequest request, CancellationToken ct)
    {
        await account.ResendVerificationAsync(request.Email, ct);
        return Ok(new MessageResponse("If the address is registered and not yet confirmed, a new email has been sent."));
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MessageResponse>> ForgotPassword(EmailRequest request, CancellationToken ct)
    {
        await account.ForgotPasswordAsync(request.Email, ct);
        return Ok(new MessageResponse("If the address is registered, a password reset email has been sent."));
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        await account.ResetPasswordAsync(request, ClientIp, ct);
        return NoContent();
    }

    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        await account.ChangePasswordAsync(User.GetUserId()!, request, ClientIp, ct);
        return NoContent();
    }

    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();
}