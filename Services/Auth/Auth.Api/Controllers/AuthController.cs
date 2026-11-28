using Auth.Api.Common;
using Auth.Api.Contracts;
using Auth.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Auth.Api.Controllers;

[ApiController]
[Route("api/auth")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
public class AuthController(IAuthService auth, IWebHostEnvironment env) : ControllerBase
{
    private const string RefreshCookieName = "refreshToken";
    private const string RefreshCookiePath = "/api/auth";

    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType<RegisterResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RegisterResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        var result = await auth.RegisterAsync(request, ct);
        return Created($"/api/users/{result.Id}", result);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await auth.LoginAsync(request, ClientIp, ct);
        SetRefreshCookie(result.RefreshToken, result.RefreshExpiresAtUtc);
        return Ok(result.Response);
    }

    [HttpPost("refresh-token")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Refresh(CancellationToken ct)
    {
        var result = await auth.RefreshAsync(Request.Cookies[RefreshCookieName], ClientIp, ct);
        SetRefreshCookie(result.RefreshToken, result.RefreshExpiresAtUtc);
        return Ok(result.Response);
    }

    [HttpPost("revoke-token")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revoke(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RevokeTokenRequest? request,
        CancellationToken ct)
    {
        var token = request?.Token ?? Request.Cookies[RefreshCookieName];
        await auth.RevokeAsync(User.GetUserId()!, token, ClientIp, ct);

        if (request?.Token is null)
            DeleteRefreshCookie();

        return NoContent();
    }

    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await auth.LogoutAsync(User.GetUserId()!, Request.Cookies[RefreshCookieName], ClientIp, ct);
        DeleteRefreshCookie();
        return NoContent();
    }

    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    private CookieOptions BuildCookieOptions(DateTime? expiresUtc = null) => new()
    {
        HttpOnly = true,
        Secure = !env.IsDevelopment(),
        SameSite = SameSiteMode.Strict,
        Path = RefreshCookiePath,
        IsEssential = true,
        Expires = expiresUtc is null
            ? null
            : new DateTimeOffset(DateTime.SpecifyKind(expiresUtc.Value, DateTimeKind.Utc))
    };

    private void SetRefreshCookie(string token, DateTime expiresUtc)
        => Response.Cookies.Append(RefreshCookieName, token, BuildCookieOptions(expiresUtc));

    private void DeleteRefreshCookie()
        => Response.Cookies.Delete(RefreshCookieName, BuildCookieOptions());
}