using Auth.Api.Common;
using Auth.Api.Contracts;
using Auth.Api.Models;
using Microsoft.AspNetCore.Identity;

namespace Auth.Api.Services;

public interface IAccountService
{
    Task VerifyEmailAsync(VerifyEmailRequest request, CancellationToken ct);
    Task ResendVerificationAsync(string email, CancellationToken ct);
    Task ForgotPasswordAsync(string email, CancellationToken ct);
    Task ResetPasswordAsync(ResetPasswordRequest request, string? ip, CancellationToken ct);
    Task ChangePasswordAsync(string userId, ChangePasswordRequest request, string? ip, CancellationToken ct);
}

public class AccountService(
    UserManager<ApplicationUser> userManager,
    IAuthService auth,
    IEmailService email,
    ILogger<AccountService> logger) : IAccountService
{
    private static AuthException InvalidToken() => AuthException.BadRequest("Invalid or expired token");

    // ── Підтвердження email ───────────────────────────────────────────────────
    public async Task VerifyEmailAsync(VerifyEmailRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(request.UserId) ?? throw InvalidToken();

        if (user.EmailConfirmed)
            return;

        var token = IdentityTokenEncoder.Decode(request.Token) ?? throw InvalidToken();

        var result = await userManager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
            throw InvalidToken();

        await userManager.UpdateSecurityStampAsync(user);

        logger.LogInformation("Email confirmed for user {UserId}", user.Id);
    }

    public async Task ResendVerificationAsync(string emailAddress, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(emailAddress);
        if (user is null || user.EmailConfirmed)
            return;

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var body =
            "Щоб підтвердити email, виконайте POST /api/auth/verify-email з тілом:" + Environment.NewLine +
            $"{{ \"userId\": \"{user.Id}\", \"token\": \"{IdentityTokenEncoder.Encode(token)}\" }}";

        await email.SendAsync(user.Email!, "Підтвердіть ваш email", body, ct);
    }

    // ── Відновлення пароля ────────────────────────────────────────────────────
    public async Task ForgotPasswordAsync(string emailAddress, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(emailAddress);

        if (user is null || !user.EmailConfirmed)
            return;

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var body =
            "Щоб скинути пароль, виконайте POST /api/auth/reset-password з тілом:" + Environment.NewLine +
            $"{{ \"userId\": \"{user.Id}\", \"token\": \"{IdentityTokenEncoder.Encode(token)}\", \"newPassword\": \"<новий пароль>\" }}";

        await email.SendAsync(user.Email!, "Скидання пароля", body, ct);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, string? ip, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(request.UserId) ?? throw InvalidToken();
        var token = IdentityTokenEncoder.Decode(request.Token) ?? throw InvalidToken();

        var result = await userManager.ResetPasswordAsync(user, token, request.NewPassword);
        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Code == "InvalidToken"))
                throw InvalidToken();

            throw result.ToAuthException("Password reset failed");
        }

        await userManager.ResetAccessFailedCountAsync(user);
        await userManager.SetLockoutEndDateAsync(user, null);

        await auth.RevokeAllForUserAsync(user.Id, ip, "Password reset", ct);

        logger.LogInformation("Password reset for user {UserId}", user.Id);
    }

    // ── Зміна пароля ──────────────────────────────────────────────────────────
    public async Task ChangePasswordAsync(string userId, ChangePasswordRequest request, string? ip, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId)
                   ?? throw AuthException.Unauthorized("User no longer exists");

        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Code == "PasswordMismatch"))
                throw AuthException.BadRequest("Current password is incorrect");

            throw result.ToAuthException("Password change failed");
        }

        await auth.RevokeAllForUserAsync(user.Id, ip, "Password changed", ct);

        logger.LogInformation("Password changed for user {UserId}", user.Id);
    }
}