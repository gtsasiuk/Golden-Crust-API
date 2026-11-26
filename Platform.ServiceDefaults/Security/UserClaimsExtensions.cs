using System.Security.Claims;

namespace Platform.ServiceDefaults;

public static class UserClaimsExtensions
{
    public const string AdminRole = "Admin";

    public static string? GetUserId(this ClaimsPrincipal user) => user.FindFirst("sub")?.Value;
    public static string? GetUserName(this ClaimsPrincipal user) => user.FindFirst("unique_name")?.Value;
    public static string? GetEmail(this ClaimsPrincipal user) => user.FindFirst("email")?.Value;
    public static bool IsAdmin(this ClaimsPrincipal user) => user.IsInRole(AdminRole);
}