using Duende.IdentityServer;
using Duende.IdentityServer.Models;

namespace Platform.IdentityServer;

public static class Config
{
    // ── Identity resources (дані про користувача) ─────────────────────────────
    public static IEnumerable<IdentityResource> IdentityResources =>
    [
        new IdentityResources.OpenId(),
        new IdentityResources.Profile()
    ];

    // ── API scopes (гранульовані дозволи) ─────────────────────────────────────
    public static IEnumerable<ApiScope> ApiScopes =>
    [
        new("orders_api", "Orders API (повний доступ)"),
        new("orders.read", "Читання замовлень і клієнтів"),
        new("orders.write", "Створення й зміна замовлень"),

        new("products_api", "Products API (повний доступ)"),
        new("products.read", "Читання меню"),
        new("products.write", "Зміна меню"),

        new("reviews_api", "Reviews API (повний доступ)"),
        new("reviews.read", "Читання відгуків"),
        new("reviews.write", "Написання й зміна відгуків"),

        new("payments_api", "Payments API (повний доступ)"),
        new("payments.read", "Читання платежів"),
        new("payments.process", "Проведення платежів"),
        new("payments.refund", "Повернення платежів")
    ];

    // ── API resources (потрапляють у claim aud токена) ────────────────────────
    public static IEnumerable<ApiResource> ApiResources =>
    [
        new("orders_api", "Orders API") { Scopes = { "orders_api", "orders.read", "orders.write" } },
        new("products_api", "Products API") { Scopes = { "products_api", "products.read", "products.write" } },
        new("reviews_api", "Reviews API") { Scopes = { "reviews_api", "reviews.read", "reviews.write" } },
        new("payments_api", "Payments API")
        {
            Scopes = { "payments_api", "payments.read", "payments.process", "payments.refund" }
        }
    ];

    // ── Clients ───────────────────────────────────────────────────────────────
    public static IEnumerable<Client> GetClients(string aggregatorSecret)
    {
        // Postman: Authorization Code (з PKCE) для користувача. Секрет учбовий, лише для dev.
        yield return new Client
        {
            ClientId = "postman",
            ClientName = "Postman",
            AllowedGrantTypes = GrantTypes.Code,
            RequirePkce = true,
            ClientSecrets = { new Secret("postman_secret".Sha256()) },
            RedirectUris =
            {
                "https://oauth.pstmn.io/v1/callback",
                "https://oauth.pstmn.io/v1/browser-callback"
            },
            AllowOfflineAccess = true,
            AllowedScopes =
            {
                IdentityServerConstants.StandardScopes.OpenId,
                IdentityServerConstants.StandardScopes.Profile,
                "orders_api", "orders.read", "orders.write",
                "products_api", "products.read", "products.write",
                "reviews_api", "reviews.read", "reviews.write",
                "payments_api", "payments.read", "payments.process", "payments.refund"
            }
        };

        // Aggregator: Client Credentials (service-to-service), без користувача
        yield return new Client
        {
            ClientId = "aggregator_service",
            ClientName = "Aggregator Service",
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            ClientSecrets = { new Secret(aggregatorSecret.Sha256()) },
            AllowedScopes =
            {
                "orders_api", "orders.read",
                "products_api", "products.read",
                "reviews_api", "reviews.read"
            }
        };

        // Swagger UI: public client, PKCE обов'язковий, секрету немає
        yield return new Client
        {
            ClientId = "swagger",
            ClientName = "Swagger UI",
            AllowedGrantTypes = GrantTypes.Code,
            RequirePkce = true,
            RequireClientSecret = false,
            RedirectUris =
            {
                // Orders.Api, Catalog.Api, Reviews.WebApi (порти з launchSettings)
                "http://localhost:5091/swagger/oauth2-redirect.html",
                "https://localhost:7277/swagger/oauth2-redirect.html",
                "http://localhost:5217/swagger/oauth2-redirect.html",
                "https://localhost:7220/swagger/oauth2-redirect.html",
                "http://localhost:5130/swagger/oauth2-redirect.html",
                "https://localhost:7262/swagger/oauth2-redirect.html"
            },
            // Swagger UI обмінює code на токен запитом із браузера, тому потрібен CORS
            AllowedCorsOrigins =
            {
                "http://localhost:5091", "https://localhost:7277",
                "http://localhost:5217", "https://localhost:7220",
                "http://localhost:5130", "https://localhost:7262"
            },
            AllowedScopes =
            {
                IdentityServerConstants.StandardScopes.OpenId,
                IdentityServerConstants.StandardScopes.Profile,
                "orders_api", "orders.read", "orders.write",
                "products_api", "products.read", "products.write",
                "reviews_api", "reviews.read", "reviews.write"
            }
        };
    }
}