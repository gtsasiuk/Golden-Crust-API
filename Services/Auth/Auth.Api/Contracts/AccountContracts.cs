using System.ComponentModel.DataAnnotations;

namespace Auth.Api.Contracts;

public sealed class VerifyEmailRequest
{
    [Required] public string UserId { get; init; } = string.Empty;
    [Required] public string Token { get; init; } = string.Empty;
}

// Спільний для resend-verification і forgot-password
public sealed class EmailRequest
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; init; } = string.Empty;
}

public sealed class ResetPasswordRequest
{
    [Required] public string UserId { get; init; } = string.Empty;
    [Required] public string Token { get; init; } = string.Empty;
    [Required, StringLength(128)] public string NewPassword { get; init; } = string.Empty;
}

public sealed class ChangePasswordRequest
{
    [Required] public string CurrentPassword { get; init; } = string.Empty;
    [Required, StringLength(128)] public string NewPassword { get; init; } = string.Empty;
}

public sealed record MessageResponse(string Message);