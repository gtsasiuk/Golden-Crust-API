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
        await SeedUserAsync(services, configuration, logger, "SeedAdmin", [Roles.Admin, Roles.User]);
        await SeedUserAsync(services, configuration, logger, "SeedUser", [Roles.User]);
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

    private static async Task SeedUserAsync(
        IServiceProvider services, IConfiguration configuration, ILogger logger,
        string sectionName, string[] roles)
    {
        var seed = configuration.GetSection(sectionName).Get<SeedAdminSettings>();
        if (seed is null
            || string.IsNullOrWhiteSpace(seed.UserName)
            || string.IsNullOrWhiteSpace(seed.Email)
            || string.IsNullOrWhiteSpace(seed.Password))
            return;

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        if (await userManager.FindByNameAsync(seed.UserName) is not null)
            return;

        var user = new ApplicationUser
        {
            UserName = seed.UserName,
            Email = seed.Email,
            EmailConfirmed = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var create = await userManager.CreateAsync(user, seed.Password);
        if (!create.Succeeded)
            throw new InvalidOperationException(
                $"Cannot seed {sectionName}: {string.Join("; ", create.Errors.Select(e => e.Description))}");

        var addRoles = await userManager.AddToRolesAsync(user, roles);
        if (!addRoles.Succeeded)
            throw new InvalidOperationException(
                $"Cannot assign roles to {sectionName}: {string.Join("; ", addRoles.Errors.Select(e => e.Description))}");

        logger.LogInformation("Seed user {UserName} created ({Section})", seed.UserName, sectionName);
    }
}