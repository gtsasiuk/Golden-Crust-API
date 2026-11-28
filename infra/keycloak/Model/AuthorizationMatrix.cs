namespace GoldenCrust.Infra.Model;

public sealed record ResourceServerDefinition(
    string ClientId,
    string ResourceName,
    string DisplayName,
    IReadOnlyList<string> Scopes);

public static class AuthorizationMatrix
{
    public const string RealmName = "DevRealmDrivers";

    // Таблиця 6: resource servers і повний набір scopes
    public static readonly IReadOnlyList<ResourceServerDefinition> ResourceServers =
    [
        new("orders-api", "orders", "Orders",
            ["orders:read", "orders:create", "orders:update", "orders:delete", "orders:approve"]),
        new("products-api", "products", "Products",
            ["products:read", "products:create", "products:update", "products:delete", "products:publish"]),
        new("payments-api", "payments", "Payments",
            ["payments:read", "payments:process", "payments:refund"])
    ];

    public static readonly IReadOnlyList<string> Roles = ["admin", "manager", "user", "guest"];

    // Таблиця 7: які scopes отримує роль на кожному resource server.
    // Порожній результат означає "permission не створюється": payments лише для admin (свідоме рішення).
    public static IReadOnlyList<string> ScopesFor(string role, ResourceServerDefinition server) =>
        (role, server.ClientId) switch
        {
            ("admin", _) => server.Scopes,

            ("manager", "orders-api") => new[] { "orders:read", "orders:update", "orders:approve" },
            ("manager", "products-api") => new[] { "products:read", "products:update", "products:publish" },

            ("user", "orders-api") => new[] { "orders:read", "orders:create" },
            ("user", "products-api") => new[] { "products:read" },

            ("guest", "products-api") => new[] { "products:read" },

            _ => Array.Empty<string>()
        };
}