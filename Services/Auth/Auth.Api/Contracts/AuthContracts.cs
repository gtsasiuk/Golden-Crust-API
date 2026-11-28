using System.ComponentModel.DataAnnotations;

namespace Auth.Api.Contracts;

public sealed class RegisterRequest
{
    [Required, StringLength(50, MinimumLength = 3)]
    public string UserName { get; init; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required, StringLength(128)]
    public string Password { get; init; } = string.Empty;
}

public sealed class LoginRequest
{
    [Required]
    public string UserNameOrEmail { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}

public sealed class RevokeTokenRequest
{
    public string? Token { get; init; }
}

public sealed record UserInfo(string Id, string UserName, string Email, IReadOnlyList<string> Roles);

public sealed class AuthResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public string TokenType { get; init; } = "Bearer";
    public DateTime ExpiresAtUtc { get; init; }
    public UserInfo User { get; init; } = default!;
}

public sealed record RegisterResponse(string Id, string UserName, string Email, string Message);