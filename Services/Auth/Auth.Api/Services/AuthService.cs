using Auth.Api.Common;
using Auth.Api.Constants;
using Auth.Api.Contracts;
using Auth.Api.Data;
using Auth.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Auth.Api.Services;

public class AuthService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ApplicationDbContext db,
    IJwtTokenService jwt,
    IEmailService email,
    ILogger<AuthService> logger) : IAuthService
{
    // ── Реєстрація ────────────────────────────────────────────────────────────
    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var user = new ApplicationUser
        {
            UserName = request.UserName,
            Email = request.Email,
            CreatedAtUtc = DateTime.UtcNow
        };

        var create = await userManager.CreateAsync(user, request.Password);
        if (!create.Succeeded)
            throw FromIdentityErrors(create);

        var addRole = await userManager.AddToRoleAsync(user, Roles.User);
        if (!addRole.Succeeded)
        {
            await userManager.DeleteAsync(user);
            throw new InvalidOperationException(
                $"Cannot assign role '{Roles.User}': {string.Join("; ", addRole.Errors.Select(e => e.Description))}");
        }

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var body =
            "Щоб підтвердити email, виконайте POST /api/auth/verify-email з тілом:" + Environment.NewLine +
            $"{{ \"userId\": \"{user.Id}\", \"token\": \"{IdentityTokenEncoder.Encode(token)}\" }}";

        await email.SendAsync(user.Email!, "Підтвердіть ваш email", body, ct);

        logger.LogInformation("User {UserName} registered", user.UserName);

        return new RegisterResponse(user.Id, user.UserName!, user.Email!,
            "Registration successful. Check your email to confirm the address.");
    }

    // ── Логін ─────────────────────────────────────────────────────────────────
    public async Task<AuthResult> LoginAsync(LoginRequest request, string? ip, CancellationToken ct)
    {
        var user = await userManager.FindByNameAsync(request.UserNameOrEmail)
                   ?? await userManager.FindByEmailAsync(request.UserNameOrEmail);

        if (user is null)
            throw AuthException.Unauthorized("Invalid credentials");

        // lockoutOnFailure: після 5 невдалих спроб акаунт блокується на 15 хв
        var signIn = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

        if (signIn.IsLockedOut)
            throw AuthException.Locked("Account is temporarily locked due to too many failed attempts");

        if (!signIn.Succeeded)
            throw AuthException.Unauthorized("Invalid credentials");

        if (!user.EmailConfirmed)
            throw AuthException.Forbidden("Email is not confirmed");

        return await IssueTokensAsync(user, ip, replacing: null, ct);
    }

    // ── Оновлення токена (rotation) ───────────────────────────────────────────
    public async Task<AuthResult> RefreshAsync(string? refreshToken, string? ip, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw AuthException.Unauthorized("Refresh token is missing");

        var hash = jwt.HashToken(refreshToken);
        var stored = await db.RefreshTokens
            .Include(t => t.User)
            .SingleOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (stored is null)
            throw AuthException.Unauthorized("Invalid refresh token");

        if (stored.IsRevoked)
        {
            if (stored.ReplacedByTokenHash is not null)
            {
                logger.LogWarning("Reuse of rotated refresh token detected for user {UserId}", stored.UserId);
                await RevokeAllForUserAsync(stored.UserId, ip, "Reuse of rotated refresh token detected", ct);
            }

            throw AuthException.Unauthorized("Refresh token has been revoked");
        }

        if (stored.IsExpired)
            throw AuthException.Unauthorized("Refresh token has expired");

        var user = stored.User;
        if (!user.EmailConfirmed || await userManager.IsLockedOutAsync(user))
            throw AuthException.Unauthorized("User is not allowed to sign in");

        return await IssueTokensAsync(user, ip, replacing: stored, ct);
    }

    // ── Відкликання ───────────────────────────────────────────────────────────
    public async Task RevokeAsync(string userId, string? refreshToken, string? ip, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw AuthException.BadRequest("Token is required (in body or refreshToken cookie)");

        var stored = await FindUserTokenAsync(userId, refreshToken, ct);
        if (stored is null)
            throw AuthException.NotFound("Refresh token not found");

        if (stored.IsRevoked)
            return; // відкликання ідемпотентне

        Revoke(stored, ip, "Revoked by user");
        await db.SaveChangesAsync(ct);
    }

    public async Task LogoutAsync(string userId, string? refreshToken, string? ip, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return;

        var stored = await FindUserTokenAsync(userId, refreshToken, ct);
        if (stored is null || stored.IsRevoked)
            return;

        Revoke(stored, ip, "Logout");
        await db.SaveChangesAsync(ct);
    }

    public async Task RevokeAllForUserAsync(string userId, string? ip, string reason, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var active = await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null && t.ExpiresAtUtc > now)
            .ToListAsync(ct);

        foreach (var token in active)
            Revoke(token, ip, reason);

        await db.SaveChangesAsync(ct);
    }

    // ── Допоміжні методи ──────────────────────────────────────────────────────
    private async Task<AuthResult> IssueTokensAsync(
        ApplicationUser user, string? ip, RefreshToken? replacing, CancellationToken ct)
    {
        var roles = (await userManager.GetRolesAsync(user)).ToList();

        var access = jwt.GenerateAccessToken(user, roles);
        var refresh = jwt.GenerateRefreshToken();

        if (replacing is not null)
        {
            Revoke(replacing, ip, "Replaced by new token");
            replacing.ReplacedByTokenHash = refresh.Hash;
        }

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refresh.Hash,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = refresh.ExpiresAtUtc,
            CreatedByIp = ip
        });

        // Старий токен відкликається і новий створюється в одному SaveChanges (одна транзакція)
        await db.SaveChangesAsync(ct);

        var response = new AuthResponse
        {
            AccessToken = access.Token,
            ExpiresAtUtc = access.ExpiresAtUtc,
            User = new UserInfo(user.Id, user.UserName!, user.Email!, roles)
        };

        return new AuthResult(response, refresh.Raw, refresh.ExpiresAtUtc);
    }

    private Task<RefreshToken?> FindUserTokenAsync(string userId, string rawToken, CancellationToken ct)
    {
        var hash = jwt.HashToken(rawToken);
        return db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash && t.UserId == userId, ct);
    }

    private static void Revoke(RefreshToken token, string? ip, string reason)
    {
        token.RevokedAtUtc = DateTime.UtcNow;
        token.RevokedByIp = ip;
        token.RevokeReason = reason;
    }

    private static AuthException FromIdentityErrors(IdentityResult result)
    {
        var errors = result.Errors
            .GroupBy(e => e.Code)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());

        var isDuplicate = result.Errors.Any(e => e.Code is "DuplicateUserName" or "DuplicateEmail");

        return isDuplicate
            ? AuthException.Conflict("User with this username or email already exists", errors)
            : AuthException.BadRequest("Registration failed", errors);
    }
}