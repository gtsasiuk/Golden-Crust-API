using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Platform.ServiceDefaults.Security;

public static class KeycloakRoleClaims
{
    public static Task MapRealmRolesAsync(
        TokenValidatedContext context)
    {
        var realmAccess = context.Principal?
            .FindFirst("realm_access")?.Value;

        if (string.IsNullOrWhiteSpace(realmAccess))
            return Task.CompletedTask;

        try
        {
            using var document = JsonDocument.Parse(realmAccess);

            if (!document.RootElement.TryGetProperty(
                    "roles", out var roles))
                return Task.CompletedTask;

            var identity = context.Principal?.Identity as ClaimsIdentity;

            if (identity is null)
                return Task.CompletedTask;

            foreach (var role in roles.EnumerateArray())
            {
                if (role.ValueKind != JsonValueKind.String)
                    continue;

                var roleName = role.GetString();

                if (string.IsNullOrWhiteSpace(roleName))
                    continue;

                var mappedRole = roleName.Equals(
                    "admin",
                    StringComparison.OrdinalIgnoreCase)
                    ? "Admin"
                    : roleName;

                if (!identity.HasClaim(
                        ClaimTypes.Role, mappedRole))
                {
                    identity.AddClaim(
                        new Claim(ClaimTypes.Role, mappedRole));
                }
            }
        }
        catch (JsonException)
        {
            // Некоректний realm_access не додає жодних ролей.
        }

        return Task.CompletedTask;
    }
}
