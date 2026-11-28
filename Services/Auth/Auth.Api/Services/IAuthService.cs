using Auth.Api.Contracts;

namespace Auth.Api.Services;

public sealed record AuthResult(AuthResponse Response, string RefreshToken, DateTime RefreshExpiresAtUtc);

public interface IAuthService
{
    Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken ct);
    Task<AuthResult> LoginAsync(LoginRequest request, string? ip, CancellationToken ct);
    Task<AuthResult> RefreshAsync(string? refreshToken, string? ip, CancellationToken ct);
    Task RevokeAsync(string userId, string? refreshToken, string? ip, CancellationToken ct);
    Task LogoutAsync(string userId, string? refreshToken, string? ip, CancellationToken ct);

    Task RevokeAllForUserAsync(string userId, string? ip, string reason, CancellationToken ct);
}