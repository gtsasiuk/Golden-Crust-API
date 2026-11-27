using System.Security.Claims;
using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Mappers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Platform.IdentityServer.Data;

public static class IdentityServerInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration, ILogger logger)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        // Три контексти в одній базі: кожен застосовує лише власні міграції
        await sp.GetRequiredService<PersistedGrantDbContext>().Database.MigrateAsync();
        await sp.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();

        var config = sp.GetRequiredService<ConfigurationDbContext>();
        await config.Database.MigrateAsync();

        await SeedConfigurationAsync(config, configuration, logger);
        await SeedUsersAsync(sp.GetRequiredService<UserManager<ApplicationUser>>(), configuration, logger);
    }

    private static async Task SeedConfigurationAsync(
        ConfigurationDbContext db, IConfiguration configuration, ILogger logger)
    {
        var aggregatorSecret = configuration["Clients:AggregatorSecret"]
            ?? throw new InvalidOperationException("Clients:AggregatorSecret is missing");

        foreach (var resource in Config.IdentityResources)
        {
            await db.IdentityResources.Where(x => x.Name == resource.Name).ExecuteDeleteAsync();
            db.IdentityResources.Add(resource.ToEntity());
        }

        foreach (var scope in Config.ApiScopes)
        {
            await db.ApiScopes.Where(x => x.Name == scope.Name).ExecuteDeleteAsync();
            db.ApiScopes.Add(scope.ToEntity());
        }

        foreach (var resource in Config.ApiResources)
        {
            await db.ApiResources.Where(x => x.Name == resource.Name).ExecuteDeleteAsync();
            db.ApiResources.Add(resource.ToEntity());
        }

        foreach (var client in Config.GetClients(aggregatorSecret))
        {
            await db.Clients.Where(x => x.ClientId == client.ClientId).ExecuteDeleteAsync();
            db.Clients.Add(client.ToEntity());
        }

        await db.SaveChangesAsync();
        logger.LogInformation("IdentityServer configuration synchronized (clients, scopes, resources)");
    }

    // Ті самі admin і user1, що й в Auth.Api: параметри приходять з AppHost
    private static async Task SeedUsersAsync(
        UserManager<ApplicationUser> userManager, IConfiguration configuration, ILogger logger)
    {
        foreach (var section in new[] { "SeedAdmin", "SeedUser" })
        {
            var userName = configuration[$"{section}:UserName"];
            var email = configuration[$"{section}:Email"];
            var password = configuration[$"{section}:Password"];

            if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(email)
                || string.IsNullOrWhiteSpace(password))
                continue;

            if (await userManager.FindByNameAsync(userName) is not null)
                continue;

            var user = new ApplicationUser { UserName = userName, Email = email, EmailConfirmed = true };

            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    $"Cannot seed {section}: {string.Join("; ", result.Errors.Select(e => e.Description))}");

            logger.LogInformation("Seed user {UserName} created", userName);
        }
    }
}