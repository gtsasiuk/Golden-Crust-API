using System.ComponentModel.DataAnnotations;

namespace Auth.Api.Contracts;

public sealed record UserDto(
    string Id,
    string UserName,
    string Email,
    bool EmailConfirmed,
    string? PhoneNumber,
    DateTime CreatedAtUtc,
    IReadOnlyList<string> Roles);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed class UpdateUserRequest
{
    [Required, StringLength(50, MinimumLength = 3)]
    public string UserName { get; init; } = string.Empty;

    [Phone, StringLength(32)]
    public string? PhoneNumber { get; init; }
}

public sealed class AssignRoleRequest
{
    [Required] public string Role { get; init; } = string.Empty;
}

public sealed record RefreshTokenDto(
    long Id,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc,
    string? CreatedByIp,
    DateTime? RevokedAtUtc,
    string? RevokedByIp,
    string? RevokeReason,
    bool WasRotated,
    bool IsActive);