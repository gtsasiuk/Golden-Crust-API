using System.Security.Claims;

namespace Auth.Api.Common;

public static class ClaimsPrincipalExtensions
{
    public static string? GetUserId(this ClaimsPrincipal principal) => principal.FindFirst("sub")?.Value;
}