using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Platform.ServiceDefaults;

public static class Permissions
{
    public const string OrdersRead = "orders:read";
    public const string OrdersCreate = "orders:create";
    public const string OrdersUpdate = "orders:update";
    public const string OrdersDelete = "orders:delete";
    public const string OrdersApprove = "orders:approve";

    public const string ProductsRead = "products:read";
    public const string ProductsCreate = "products:create";
    public const string ProductsUpdate = "products:update";
    public const string ProductsDelete = "products:delete";
    public const string ProductsPublish = "products:publish";

    public const string PaymentsRead = "payments:read";
    public const string PaymentsProcess = "payments:process";
    public const string PaymentsRefund = "payments:refund";

    // Не входять у матрицю Keycloak: потрібні для Reviews і Aggregator
    public const string ReviewsRead = "reviews:read";
    public const string ReviewsWrite = "reviews:write";
    public const string AggregatorRead = "aggregator:read";
}

internal static class PermissionCatalog
{
    // IdentityServer: permission → scope
    public static readonly IReadOnlyDictionary<string, string> IdentityServerScopes =
        new Dictionary<string, string>
        {
            [Permissions.OrdersRead] = "orders.read",
            [Permissions.OrdersCreate] = "orders.write",
            [Permissions.OrdersUpdate] = "orders.write",
            [Permissions.OrdersDelete] = "orders.write",
            [Permissions.OrdersApprove] = "orders.write",

            [Permissions.ProductsRead] = "products.read",
            [Permissions.ProductsCreate] = "products.write",
            [Permissions.ProductsUpdate] = "products.write",
            [Permissions.ProductsDelete] = "products.write",
            [Permissions.ProductsPublish] = "products.write",

            [Permissions.PaymentsRead] = "payments.read",
            [Permissions.PaymentsProcess] = "payments.process",
            [Permissions.PaymentsRefund] = "payments.refund",

            [Permissions.ReviewsRead] = "reviews.read",
            [Permissions.ReviewsWrite] = "reviews.write",
            [Permissions.AggregatorRead] = "orders.read"
        };

    // Auth.Api: роль User (Admin має все). Володіння даними перевіряє AccessGuard.
    public static readonly HashSet<string> LocalUserPermissions =
    [
        Permissions.OrdersRead, Permissions.OrdersCreate, Permissions.OrdersUpdate, Permissions.OrdersDelete,
        Permissions.ProductsRead,
        Permissions.ReviewsRead, Permissions.ReviewsWrite
    ];
}

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePermissionAttribute(string permission) : AuthorizeAttribute(PolicyPrefix + permission)
{
    public const string PolicyPrefix = "permission:";
    public string Permission { get; } = permission;
}

// Створює політику permission:<назва> на льоту: не треба реєструвати кожну в Program.cs
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    public override Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(RequirePermissionAttribute.PolicyPrefix, StringComparison.Ordinal))
        {
            var permission = policyName[RequirePermissionAttribute.PolicyPrefix.Length..];

            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permission))
                .Build();

            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return base.GetPolicyAsync(policyName);
    }
}

public sealed class PermissionAuthorizationHandler(IConfiguration configuration)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var user = context.User;
        var issuer = user.FindFirst("iss")?.Value;

        if (string.IsNullOrEmpty(issuer))
            return Task.CompletedTask;

        var allowed =
            string.Equals(issuer, configuration["Jwt:Issuer"], StringComparison.Ordinal)
                ? HasLocalPermission(user, requirement.Permission)
            : string.Equals(issuer, configuration["IdentityServer:Issuer"], StringComparison.Ordinal)
                ? HasIdentityServerScope(user, requirement.Permission)
            : issuer.Contains("/realms/", StringComparison.OrdinalIgnoreCase)
                && HasKeycloakPermission(user, requirement.Permission);

        if (allowed)
            context.Succeed(requirement);

        return Task.CompletedTask;
    }

    private static bool HasLocalPermission(System.Security.Claims.ClaimsPrincipal user, string permission)
    {
        if (user.IsInRole(UserClaimsExtensions.AdminRole))
            return true;

        return user.IsInRole("User") && PermissionCatalog.LocalUserPermissions.Contains(permission);
    }

    private static bool HasIdentityServerScope(System.Security.Claims.ClaimsPrincipal user, string permission)
    {
        if (!PermissionCatalog.IdentityServerScopes.TryGetValue(permission, out var scope))
            return false;

        return user.FindAll("scope")
            .Any(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(scope));
    }

    // RPT: { "authorization": { "permissions": [ { "rsname": "...", "scopes": [ ... ] } ] } }
    private static bool HasKeycloakPermission(System.Security.Claims.ClaimsPrincipal user, string permission)
    {
        var raw = user.FindFirst("authorization")?.Value;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        try
        {
            using var document = JsonDocument.Parse(raw);

            if (!document.RootElement.TryGetProperty("permissions", out var permissions)
                || permissions.ValueKind != JsonValueKind.Array)
                return false;

            foreach (var entry in permissions.EnumerateArray())
            {
                if (!entry.TryGetProperty("scopes", out var scopes) || scopes.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var scope in scopes.EnumerateArray())
                {
                    if (scope.ValueKind == JsonValueKind.String && scope.GetString() == permission)
                        return true;
                }
            }
        }
        catch (JsonException)
        {
            // Некоректний claim означає відсутність permissions
        }

        return false;
    }
}

public static class PermissionServiceExtensions
{
    // Викликати ПІСЛЯ AddAuthorization(): остання реєстрація provider-а перемагає
    public static IServiceCollection AddPermissionAuthorization(this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        return services;
    }
}