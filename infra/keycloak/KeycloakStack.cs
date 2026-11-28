using System.Collections.Immutable;
using GoldenCrust.Infra.Model;
using Pulumi;
using Pulumi.Keycloak;
using Pulumi.Keycloak.Inputs;
using Pulumi.Keycloak.OpenId;
using Pulumi.Keycloak.OpenId.Inputs;

namespace GoldenCrust.Infra;

public sealed class KeycloakStack : Stack
{
    [Output] public Output<string> RealmUrl { get; set; }
    [Output] public Output<string> AdminConsoleUrl { get; set; }
    [Output] public Output<string> ClientIds { get; set; }
    [Output] public Output<ImmutableDictionary<string, string>> ClientSecrets { get; set; }

    public KeycloakStack()
    {
        var config = new Pulumi.Config("goldencrust");
        var publicUrl = (config.Get("keycloakPublicUrl") ?? "http://localhost:8080").TrimEnd('/');
        var testPassword = config.RequireSecret("testUserPassword");
        var temporaryPasswords = config.GetBoolean("temporaryPasswords") ?? true;

        // ── Realm ─────────────────────────────────────────────────────────────
        var realm = new Realm("realm", new RealmArgs
        {
            RealmName = AuthorizationMatrix.RealmName,
            Enabled = true,
            DisplayName = "Golden Crust (dev)",
            RegistrationAllowed = true,
            ResetPasswordAllowed = true,
            RememberMe = true,
            LoginWithEmailAllowed = true,
            VerifyEmail = false,
            SslRequired = "none",               // лише dev: Keycloak працює по http
            LoginTheme = "keycloak",
            AccountTheme = "keycloak.v3",
            AdminTheme = "keycloak.v2",
            EmailTheme = "keycloak",
            AccessTokenLifespan = "15m"
        });

        // ── Realm-ролі: admin, manager, user, guest ───────────────────────────
        var roles = AuthorizationMatrix.Roles.ToDictionary(
            role => role,
            role => new Role($"role-{role}", new RoleArgs
            {
                RealmId = realm.Id,
                Name = role,
                Description = $"Golden Crust: {role}"
            }));

        // ── Resource servers: scopes, resource, role policies, permissions ───
        var resourceServers = new Dictionary<string, Client>();

        foreach (var definition in AuthorizationMatrix.ResourceServers)
        {
            var server = ClientFactory.CreateResourceServer(realm, definition);
            resourceServers[definition.ClientId] = server;

            var scopes = definition.Scopes.ToDictionary(
                scope => scope,
                scope => new ClientAuthorizationScope($"{definition.ClientId}-scope-{scope}", new ClientAuthorizationScopeArgs
                {
                    RealmId = realm.Id,
                    ResourceServerId = server.ResourceServerId,
                    Name = scope
                }));

            var resource = new ClientAuthorizationResource($"{definition.ClientId}-resource", new ClientAuthorizationResourceArgs
            {
                RealmId = realm.Id,
                ResourceServerId = server.ResourceServerId,
                Name = definition.ResourceName,
                DisplayName = definition.DisplayName,
                Scopes = definition.Scopes.ToList()
            },
            new CustomResourceOptions { DependsOn = scopes.Values.Cast<Resource>().ToList() });

            foreach (var role in AuthorizationMatrix.Roles)
            {
                var allowed = AuthorizationMatrix.ScopesFor(role, definition);

                // Роль без жодного scope на цьому сервері (наприклад manager на payments-api)
                // не отримує ні policy, ні permission: доступу немає за замовчуванням.
                if (allowed.Count == 0)
                    continue;

                var policy = new ClientRolePolicy($"{definition.ClientId}-policy-{role}", new ClientRolePolicyArgs
                {
                    RealmId = realm.Id,
                    ResourceServerId = server.ResourceServerId,
                    Name = $"role-{role}",
                    Type = "role",
                    Logic = "POSITIVE",
                    DecisionStrategy = "UNANIMOUS",
                    Roles = { new ClientRolePolicyRoleArgs { Id = roles[role].Id, Required = true } }
                });

                var scopeIds = allowed.Select(s => (Input<string>)scopes[s].Id).ToArray();

                _ = new ClientAuthorizationPermission($"{definition.ClientId}-permission-{role}", new ClientAuthorizationPermissionArgs
                {
                    RealmId = realm.Id,
                    ResourceServerId = server.ResourceServerId,
                    Name = $"{role}-{definition.ResourceName}",
                    Type = "scope",
                    DecisionStrategy = "UNANIMOUS",
                    Policies = { policy.Id },
                    Resources = { resource.Id },
                    Scopes = Output.All(scopeIds)
                });
            }
        }

        // ── Clients (через фабрику й extension-методи) ───────────────────────
        var audiences = AuthorizationMatrix.ResourceServers.Select(s => s.ClientId).ToArray();

        var postman = ClientFactory
            .CreateAuthorizationCodeClient(
                realm, "postman", "Postman",
                redirectUris: ["https://oauth.pstmn.io/v1/callback", "https://oauth.pstmn.io/v1/browser-callback"],
                webOrigins: ["+"],
                isPublic: false,
                allowPasswordGrantForTests: true)   // лише dev: швидкий вхід тестовим користувачем
            .WithDefaultScopes(realm, "postman", ClientExtensions.StandardScopes)
            .WithAudienceMappers(realm, "postman", audiences);

        var swagger = ClientFactory
            .CreateAuthorizationCodeClient(
                realm, "swagger", "Swagger UI",
                redirectUris:
                [
                    "http://localhost:5091/swagger/oauth2-redirect.html", "https://localhost:7277/swagger/oauth2-redirect.html",
                    "http://localhost:5217/swagger/oauth2-redirect.html", "https://localhost:7220/swagger/oauth2-redirect.html",
                    "http://localhost:5121/swagger/oauth2-redirect.html", "https://localhost:7104/swagger/oauth2-redirect.html"
                ],
                webOrigins:
                [
                    "http://localhost:5091", "https://localhost:7277",
                    "http://localhost:5217", "https://localhost:7220",
                    "http://localhost:5121", "https://localhost:7104"
                ],
                isPublic: true)
            .WithDefaultScopes(realm, "swagger", ClientExtensions.StandardScopes)
            .WithAudienceMappers(realm, "swagger", audiences);

        var aggregator = ClientFactory
            .CreateClientCredentialsClient(realm, "aggregator", "Aggregator Service")
            .WithAudienceMappers(realm, "aggregator", audiences);

        // ── Client-level ролі для service account і composite role ────────────
        var ordersServiceRole = new Role("role-orders-api-service", new RoleArgs
        {
            RealmId = realm.Id,
            ClientId = resourceServers["orders-api"].Id,
            Name = "service",
            Description = "Service-to-service access to Orders API"
        });

        var productsServiceRole = new Role("role-products-api-service", new RoleArgs
        {
            RealmId = realm.Id,
            ClientId = resourceServers["products-api"].Id,
            Name = "service",
            Description = "Service-to-service access to Products API"
        });

        // Composite role групує client-ролі в один набір
        var serviceReader = new Role("role-service-reader", new RoleArgs
        {
            RealmId = realm.Id,
            Name = "service-reader",
            Description = "Composite: service access to Orders and Products",
            CompositeRoles = { ordersServiceRole.Id, productsServiceRole.Id }
        });

        _ = new ClientServiceAccountRealmRole("aggregator-service-reader", new ClientServiceAccountRealmRoleArgs
        {
            RealmId = realm.Id,
            ServiceAccountUserId = aggregator.ServiceAccountUserId,
            Role = serviceReader.Name
        });

        // ── Тестові користувачі: по одному на кожну роль ──────────────────────
        var testUsers = new (string UserName, string Role, string FirstName, string LastName)[]
        {
            ("alice.admin", "admin", "Alice", "Admin"),
            ("john.manager", "manager", "John", "Manager"),
            ("bob.user", "user", "Bob", "User"),
            ("gina.guest", "guest", "Gina", "Guest")
        };

        foreach (var (userName, role, firstName, lastName) in testUsers)
        {
            var user = new User($"user-{userName}", new UserArgs
            {
                RealmId = realm.Id,
                Username = userName,
                Email = $"{userName}@goldencrust.local",
                EmailVerified = true,
                Enabled = true,
                FirstName = firstName,
                LastName = lastName,
                InitialPassword = new UserInitialPasswordArgs
                {
                    Value = testPassword,
                    Temporary = temporaryPasswords
                }
            });

            _ = new UserRoles($"user-roles-{userName}", new UserRolesArgs
            {
                RealmId = realm.Id,
                UserId = user.Id,
                RoleIds = { roles[role].Id },
                Exhaustive = false   // не чіпаємо default-roles-* і інші ролі
            });
        }

        // ── Outputs ───────────────────────────────────────────────────────────
        RealmUrl = Output.Create($"{publicUrl}/realms/{AuthorizationMatrix.RealmName}");
        AdminConsoleUrl = Output.Create($"{publicUrl}/admin/master/console/#/{AuthorizationMatrix.RealmName}");
        ClientIds = Output.Create("postman, swagger, aggregator");

        ClientSecrets = Output
            .Tuple(postman.ClientSecret, aggregator.ClientSecret)
            .Apply(t => ImmutableDictionary<string, string>.Empty
                .Add("postman", t.Item1)
                .Add("aggregator", t.Item2));
    }
}