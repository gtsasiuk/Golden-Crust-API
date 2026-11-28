var builder = DistributedApplication.CreateBuilder(args);

var sqlPassword = builder.AddParameter("sql-password", secret: true);
var mongoPassword = builder.AddParameter("mongo-password", secret: true);
var jwtSecret = builder.AddParameter("jwt-secret", secret: true);
var aggregatorClientSecret = builder.AddParameter("aggregator-client-secret", secret: true);

// Тестовий Admin
var seedAdminUserName = builder.AddParameter("seed-admin-username");
var seedAdminEmail = builder.AddParameter("seed-admin-email");
var seedAdminPassword = builder.AddParameter("seed-admin-password", secret: true);

// Тестовий User
var seedUserUserName = builder.AddParameter("seed-user-username");
var seedUserEmail = builder.AddParameter("seed-user-email");
var seedUserPassword = builder.AddParameter("seed-user-password", secret: true);

// SQL Server
var sql = builder.AddSqlServer("sql", sqlPassword, port: 14330)
    .WithDataVolume();

var ordersDb = sql.AddDatabase("OrdersDb", "GoldenCrust_Orders");
var catalogDb = sql.AddDatabase("CatalogDb", "GoldenCrust_Catalog");
var identityDb = sql.AddDatabase("IdentityDb", "GoldenCrust_Identity");
var identityServerDb = sql.AddDatabase("IdentityServerDb", "GoldenCrust_IdentityServer");

// MongoDB
var mongo = builder.AddMongoDB("mongo", port: 27018, password: mongoPassword)
    .WithDataVolume();

var reviewsDb = mongo.AddDatabase("ReviewsDb", "golden_crust_reviews");

// Redis
var redis = builder.AddRedis("redis")
    .WithDataVolume();

// Keycloak Identity Provider
var keycloak = builder.AddKeycloak("keycloak", 8080)
    .WithDataVolume();

// RabbitMQ
var messaging = builder.AddRabbitMQ("messaging")
    .WithDataVolume()
    .WithManagementPlugin(port: 15672);

// Auth API
var auth = builder.AddProject<Projects.Auth_Api>("auth-api")
    .WithReference(identityDb)
    .WaitFor(identityDb)
    .WithJwtSettings(jwtSecret)
    .WithEnvironment("SeedAdmin__UserName", seedAdminUserName)
    .WithEnvironment("SeedAdmin__Email", seedAdminEmail)
    .WithEnvironment("SeedAdmin__Password", seedAdminPassword)
    .WithEnvironment("SeedUser__UserName", seedUserUserName)
    .WithEnvironment("SeedUser__Email", seedUserEmail)
    .WithEnvironment("SeedUser__Password", seedUserPassword)
    .WithHttpHealthCheck("/health");

// Duende IdentityServer (Частина 2)
var identityServer = builder.AddProject<Projects.Platform_IdentityServer>("identityserver")
    .WithReference(identityServerDb)
    .WaitFor(identityServerDb)
    .WithHttpEndpoint(port: 5100, name: "public")
    .WithEnvironment("Clients__AggregatorSecret", aggregatorClientSecret)
    .WithEnvironment("SeedAdmin__UserName", seedAdminUserName)
    .WithEnvironment("SeedAdmin__Email", seedAdminEmail)
    .WithEnvironment("SeedAdmin__Password", seedAdminPassword)
    .WithEnvironment("SeedUser__UserName", seedUserUserName)
    .WithEnvironment("SeedUser__Email", seedUserEmail)
    .WithEnvironment("SeedUser__Password", seedUserPassword)
    .WithHttpHealthCheck("/health");

// Orders API
var orders = builder.AddProject<Projects.Orders_Api>("orders-api")
    .WithReference(ordersDb)
    .WaitFor(ordersDb)
    .WithReference(messaging)
    .WaitFor(messaging)
    .WithReference(keycloak)
    .WaitFor(keycloak)
    .WithReference(identityServer)
    .WaitFor(identityServer)
    .WithEnvironment("IdentityServer__Authority", identityServer.GetEndpoint("public"))
    .WithEnvironment( "IdentityServer__Issuer", "http://localhost:5100")
    .WithJwtSettings(jwtSecret)
    .WithHttpHealthCheck("/health");

// Catalog API
var catalog = builder.AddProject<Projects.Catalog_Api>("catalog-api")
    .WithReference(catalogDb)
    .WaitFor(catalogDb)
    .WithReference(redis)
    .WithReference(messaging)
    .WaitFor(messaging)
    .WithReference(keycloak)
    .WaitFor(keycloak)
    .WithReference(identityServer)
    .WaitFor(identityServer)
    .WithEnvironment( "IdentityServer__Authority", identityServer.GetEndpoint("public"))
    .WithEnvironment("IdentityServer__Issuer", "http://localhost:5100")
    .WithJwtSettings(jwtSecret)
    .WithHttpHealthCheck("/health");

// Reviews API
var reviews = builder.AddProject<Projects.Reviews_WebApi>("reviews-api")
    .WithReference(reviewsDb)
    .WaitFor(reviewsDb)
    .WithReference(redis)
    .WithReference(messaging)
    .WaitFor(messaging)
    .WithReference(identityServer)
    .WaitFor(identityServer)
    .WithEnvironment("IdentityServer__Authority", identityServer.GetEndpoint("public"))
    .WithEnvironment("IdentityServer__Issuer", "http://localhost:5100")
    .WithJwtSettings(jwtSecret)
    .WithHttpHealthCheck("/health");

// Payments API
var payments = builder.AddProject<Projects.Payments_Api>("payments-api")
    .WithReference(keycloak)
    .WaitFor(keycloak)
    .WithHttpHealthCheck("/health");

// Aggregator
var aggregator = builder.AddProject<Projects.Platform_Aggregator>("aggregator")
    .WithReference(orders)
    .WithReference(catalog)
    .WithReference(reviews)
    .WithReference(redis)
    .WithReference(identityServer)
    .WaitFor(orders)
    .WaitFor(catalog)
    .WaitFor(reviews)
    .WaitFor(identityServer)
    .WithEnvironment("IdentityServer__Authority", identityServer.GetEndpoint("public"))
    .WithEnvironment("IdentityServer__ClientId", "aggregator_service")
    .WithEnvironment("IdentityServer__ClientSecret", aggregatorClientSecret)
    .WithEnvironment("IdentityServer__Scopes", "orders_api orders.read products_api products.read reviews_api reviews.read")
    .WithJwtSettings(jwtSecret)
    .WithHttpHealthCheck("/health");

// API Gateway
builder.AddProject<Projects.Platform_ApiGateway>("gateway")
    .WithHttpEndpoint(port: 5000, name: "public")
    .WithReference(orders)
    .WithReference(catalog)
    .WithReference(reviews)
    .WithReference(aggregator)
    .WaitFor(aggregator)
    .WithReference(auth).WaitFor(auth)
    .WithReference(identityServer)
    .WaitFor(identityServer)
    .WithReference(keycloak)
    .WaitFor(keycloak)
    .WithReference(payments)
    .WaitFor(payments)
    .WithEnvironment("IdentityServer__Authority", identityServer.GetEndpoint("public"))
    .WithEnvironment("IdentityServer__Issuer","http://localhost:5100")
    .WithJwtSettings(jwtSecret)
    .WithHttpHealthCheck("/health");

builder.Build().Run();