using GoldenCrust.Infra.Model;
using Pulumi.Keycloak;
using Pulumi.Keycloak.OpenId;
using Pulumi.Keycloak.OpenId.Inputs;

namespace GoldenCrust.Infra;

public static class ClientFactory
{
    // Resource server: клієнт з увімкненою Authorization (resources, scopes, policies, permissions)
    public static Client CreateResourceServer(Realm realm, ResourceServerDefinition definition)
        => new($"client-{definition.ClientId}", new ClientArgs
        {
            RealmId = realm.Id,
            ClientId = definition.ClientId,
            Name = $"{definition.DisplayName} API",
            Enabled = true,
            AccessType = "CONFIDENTIAL",
            ServiceAccountsEnabled = true,
            StandardFlowEnabled = false,
            DirectAccessGrantsEnabled = false,
            Authorization = new ClientAuthorizationArgs
            {
                PolicyEnforcementMode = "ENFORCING",
                // AFFIRMATIVE: достатньо, щоб дозвіл дала хоча б одна permission
                DecisionStrategy = "AFFIRMATIVE",
                KeepDefaults = false,
                AllowRemoteResourceManagement = false
            }
        });

    // Authorization Code Flow (Postman, Swagger) з PKCE
    public static Client CreateAuthorizationCodeClient(
        Realm realm, string clientId, string name,
        IEnumerable<string> redirectUris, IEnumerable<string> webOrigins,
        bool isPublic, bool allowPasswordGrantForTests = false)
        => new($"client-{clientId}", new ClientArgs
        {
            RealmId = realm.Id,
            ClientId = clientId,
            Name = name,
            Enabled = true,
            AccessType = isPublic ? "PUBLIC" : "CONFIDENTIAL",
            StandardFlowEnabled = true,
            DirectAccessGrantsEnabled = allowPasswordGrantForTests,
            PkceCodeChallengeMethod = "S256",
            ValidRedirectUris = redirectUris.ToList(),
            WebOrigins = webOrigins.ToList()
        });

    // Client Credentials Flow (service-to-service)
    public static Client CreateClientCredentialsClient(Realm realm, string clientId, string name)
        => new($"client-{clientId}", new ClientArgs
        {
            RealmId = realm.Id,
            ClientId = clientId,
            Name = name,
            Enabled = true,
            AccessType = "CONFIDENTIAL",
            ServiceAccountsEnabled = true,
            StandardFlowEnabled = false,
            DirectAccessGrantsEnabled = false
        });
}

public static class ClientExtensions
{
    // "basic" і "acr" обов'язкові: без "basic" у токені не буде claim sub
    public static readonly string[] StandardScopes =
        ["basic", "acr", "profile", "email", "roles", "web-origins"];

    public static Client WithDefaultScopes(this Client client, Realm realm, string key, params string[] scopes)
    {
        _ = new ClientDefaultScopes($"default-scopes-{key}", new ClientDefaultScopesArgs
        {
            RealmId = realm.Id,
            ClientId = client.Id,
            DefaultScopes = scopes.ToList()
        });
        return client;
    }

    // Додає resource servers в aud звичайного access-токена
    public static Client WithAudienceMappers(this Client client, Realm realm, string key, params string[] audiences)
    {
        foreach (var audience in audiences)
        {
            _ = new AudienceProtocolMapper($"audience-{key}-{audience}", new AudienceProtocolMapperArgs
            {
                RealmId = realm.Id,
                ClientId = client.Id,
                Name = $"audience-{audience}",
                IncludedClientAudience = audience,
                AddToAccessToken = true,
                AddToIdToken = false
            });
        }
        return client;
    }
}