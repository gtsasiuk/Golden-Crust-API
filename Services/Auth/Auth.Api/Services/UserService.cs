using Auth.Api.Common;
using Auth.Api.Constants;
using Auth.Api.Contracts;
using Auth.Api.Data;
using Auth.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Auth.Api.Services;

public interface IUserService
{
    Task<PagedResult<UserDto>> GetPageAsync(int page, int pageSize, string? search, CancellationToken ct);
    Task<UserDto> GetByIdAsync(string id, CancellationToken ct);
    Task<UserDto> UpdateAsync(string id, UpdateUserRequest request, CancellationToken ct);
    Task DeleteAsync(string id, string currentUserId, CancellationToken ct);
    Task<IReadOnlyList<RefreshTokenDto>> GetRefreshTokensAsync(string id, CancellationToken ct);
    Task<IReadOnlyList<string>> GetRolesAsync(string id, CancellationToken ct);
    Task<IReadOnlyList<string>> AddRoleAsync(string id, string role, CancellationToken ct);
    Task RemoveRoleAsync(string id, string role, string currentUserId, CancellationToken ct);
}

public class UserService(UserManager<ApplicationUser> userManager, ApplicationDbContext db) : IUserService
{
    // ── Список ────────────────────────────────────────────────────────────────
    public async Task<PagedResult<UserDto>> GetPageAsync(int page, int pageSize, string? search, CancellationToken ct)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(u => u.UserName!.Contains(s) || u.Email!.Contains(s));
        }

        var total = await query.CountAsync(ct);

        var users = await query
            .OrderBy(u => u.UserName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        // Ролі для всієї сторінки одним запитом, без N+1
        var ids = users.Select(u => u.Id).ToList();
        var roleRows = await (
            from ur in db.UserRoles
            join r in db.Roles on ur.RoleId equals r.Id
            where ids.Contains(ur.UserId)
            select new { ur.UserId, RoleName = r.Name }).ToListAsync(ct);

        var rolesByUser = roleRows
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.RoleName!).OrderBy(n => n).ToList());

        var items = users
            .Select(u => ToDto(u, rolesByUser.TryGetValue(u.Id, out var roles) ? roles : []))
            .ToList();

        return new PagedResult<UserDto>(items, page, pageSize, total);
    }

    // ── Один користувач ───────────────────────────────────────────────────────
    public async Task<UserDto> GetByIdAsync(string id, CancellationToken ct)
    {
        var user = await FindAsync(id);
        return ToDto(user, (await userManager.GetRolesAsync(user)).ToList());
    }

    public async Task<UserDto> UpdateAsync(string id, UpdateUserRequest request, CancellationToken ct)
    {
        var user = await FindAsync(id);

        if (!string.Equals(user.UserName, request.UserName, StringComparison.Ordinal))
        {
            var rename = await userManager.SetUserNameAsync(user, request.UserName);
            if (!rename.Succeeded)
                throw rename.ToAuthException("Update failed");
        }

        if (request.PhoneNumber != user.PhoneNumber)
        {
            var phone = await userManager.SetPhoneNumberAsync(user, request.PhoneNumber);
            if (!phone.Succeeded)
                throw phone.ToAuthException("Update failed");
        }

        return ToDto(user, (await userManager.GetRolesAsync(user)).ToList());
    }

    public async Task DeleteAsync(string id, string currentUserId, CancellationToken ct)
    {
        var user = await FindAsync(id);

        if (user.Id == currentUserId)
            throw AuthException.Conflict("You cannot delete your own account");

        // Refresh-токени видаляються каскадно (FK з DeleteBehavior.Cascade)
        var result = await userManager.DeleteAsync(user);
        if (!result.Succeeded)
            throw result.ToAuthException("Delete failed");
    }

    // ── Історія refresh-токенів ───────────────────────────────────────────────
    public async Task<IReadOnlyList<RefreshTokenDto>> GetRefreshTokensAsync(string id, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(u => u.Id == id, ct))
            throw AuthException.NotFound("User not found");

        var now = DateTime.UtcNow;

        return await db.RefreshTokens
            .AsNoTracking()
            .Where(t => t.UserId == id)
            .OrderByDescending(t => t.CreatedAtUtc)
            .Take(50)
            .Select(t => new RefreshTokenDto(
                t.Id, t.CreatedAtUtc, t.ExpiresAtUtc, t.CreatedByIp,
                t.RevokedAtUtc, t.RevokedByIp, t.RevokeReason,
                t.ReplacedByTokenHash != null,
                t.RevokedAtUtc == null && t.ExpiresAtUtc > now))
            .ToListAsync(ct);
    }

    // ── Ролі ──────────────────────────────────────────────────────────────────
    public async Task<IReadOnlyList<string>> GetRolesAsync(string id, CancellationToken ct)
    {
        var user = await FindAsync(id);
        return (await userManager.GetRolesAsync(user)).OrderBy(r => r).ToList();
    }

    public async Task<IReadOnlyList<string>> AddRoleAsync(string id, string role, CancellationToken ct)
    {
        var user = await FindAsync(id);
        var roleName = NormalizeRole(role);

        if (await userManager.IsInRoleAsync(user, roleName))
            throw AuthException.Conflict($"User already has role '{roleName}'");

        var result = await userManager.AddToRoleAsync(user, roleName);
        if (!result.Succeeded)
            throw result.ToAuthException("Cannot assign role");

        return (await userManager.GetRolesAsync(user)).OrderBy(r => r).ToList();
    }

    public async Task RemoveRoleAsync(string id, string role, string currentUserId, CancellationToken ct)
    {
        var user = await FindAsync(id);
        var roleName = NormalizeRole(role);

        if (!await userManager.IsInRoleAsync(user, roleName))
            throw AuthException.NotFound($"User does not have role '{roleName}'");

        if (roleName == Roles.Admin)
        {
            if (user.Id == currentUserId)
                throw AuthException.Conflict("You cannot remove your own Admin role");

            var admins = await userManager.GetUsersInRoleAsync(Roles.Admin);
            if (admins.Count <= 1)
                throw AuthException.Conflict("Cannot remove the Admin role from the last administrator");
        }

        var result = await userManager.RemoveFromRoleAsync(user, roleName);
        if (!result.Succeeded)
            throw result.ToAuthException("Cannot remove role");
    }

    // ── Допоміжні ─────────────────────────────────────────────────────────────
    private async Task<ApplicationUser> FindAsync(string id)
        => await userManager.FindByIdAsync(id) ?? throw AuthException.NotFound("User not found");

    private static string NormalizeRole(string role)
        => Roles.All.FirstOrDefault(r => r.Equals(role, StringComparison.OrdinalIgnoreCase))
           ?? throw AuthException.BadRequest($"Unknown role '{role}'. Allowed: {string.Join(", ", Roles.All)}");

    private static UserDto ToDto(ApplicationUser u, IReadOnlyList<string> roles)
        => new(u.Id, u.UserName!, u.Email!, u.EmailConfirmed, u.PhoneNumber, u.CreatedAtUtc, roles);
}