using Auth.Api.Constants;
using Auth.Api.Models;
using Auth.Api.Options;
using Microsoft.AspNetCore.Identity;

namespace Auth.Api.Data;

public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration, ILogger logger)
    {
        await SeedRolesAsync(services, logger);
        await SeedAdminAsync(services, configuration, logger);
    }

    private static async Task SeedRolesAsync(IServiceProvider services, ILogger logger)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        foreach (var role in Roles.All)
        {
            if (await roleManager.RoleExistsAsync(role))
                continue;

            var result = await roleManager.CreateAsync(new IdentityRole(role));
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    $"Cannot create role '{role}': {string.Join("; ", result.Errors.Select(e => e.Description))}");

            logger.LogInformation("Role {Role} created", role);
        }
    }

    // Адмін створюється лише якщо секція SeedAdmin є в конфігурації (у нас — тільки Development)
    private static async Task SeedAdminAsync(IServiceProvider services, IConfiguration configuration, ILogger logger)
    {
        var admin = configuration.GetSection(SeedAdminSettings.SectionName).Get<SeedAdminSettings>();
        if (admin is null
            || string.IsNullOrWhiteSpace(admin.UserName)
            || string.IsNullOrWhiteSpace(admin.Email)
            || string.IsNullOrWhiteSpace(admin.Password))
            return;

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        if (await userManager.FindByNameAsync(admin.UserName) is not null)
            return;

        var user = new ApplicationUser
        {
            UserName = admin.UserName,
            Email = admin.Email,
            EmailConfirmed = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var create = await userManager.CreateAsync(user, admin.Password);
        if (!create.Succeeded)
            throw new InvalidOperationException(
                $"Cannot seed admin: {string.Join("; ", create.Errors.Select(e => e.Description))}");

        var addRoles = await userManager.AddToRolesAsync(user, [Roles.Admin, Roles.User]);
        if (!addRoles.Succeeded)
            throw new InvalidOperationException(
                $"Cannot assign roles to admin: {string.Join("; ", addRoles.Errors.Select(e => e.Description))}");

        logger.LogInformation("Seed admin {UserName} created", admin.UserName);
    }
}