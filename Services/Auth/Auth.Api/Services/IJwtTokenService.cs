using System.Security.Claims;
using Auth.Api.Models;

namespace Auth.Api.Services;

public sealed record AccessToken(string Token, DateTime ExpiresAtUtc);

public sealed record RefreshTokenValue(string Raw, string Hash, DateTime ExpiresAtUtc);

public interface IJwtTokenService
{
    AccessToken GenerateAccessToken(ApplicationUser user, IEnumerable<string> roles);
    RefreshTokenValue GenerateRefreshToken();
    Task<ClaimsPrincipal?> ValidateTokenAsync(string token);
    string HashToken(string rawToken);
}