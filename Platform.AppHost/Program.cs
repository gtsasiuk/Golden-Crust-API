var builder = DistributedApplication.CreateBuilder(args);

var sqlPassword = builder.AddParameter("sql-password", secret: true);
var mongoPassword = builder.AddParameter("mongo-password", secret: true);
var jwtSecret = builder.AddParameter("jwt-secret", secret: true);

// SQL Server
var sql = builder.AddSqlServer("sql", sqlPassword, port: 14330)
    .WithDataVolume();

var ordersDb = sql.AddDatabase("OrdersDb", "GoldenCrust_Orders");
var catalogDb = sql.AddDatabase("CatalogDb", "GoldenCrust_Catalog");
var identityDb = sql.AddDatabase("IdentityDb", "GoldenCrust_Identity");

// MongoDB
var mongo = builder.AddMongoDB("mongo", port: 27018, password: mongoPassword)
    .WithDataVolume();

var reviewsDb = mongo.AddDatabase("ReviewsDb", "golden_crust_reviews");

// Redis
var redis = builder.AddRedis("redis")
    .WithDataVolume();

// RabbitMQ
var messaging = builder.AddRabbitMQ("messaging")
    .WithDataVolume()
    .WithManagementPlugin(port: 15672);

// Auth API
var auth = builder.AddProject<Projects.Auth_Api>("auth-api")
    .WithReference(identityDb)
    .WaitFor(identityDb)
    .WithEnvironment("Jwt__SecretKey", jwtSecret)
    .WithHttpHealthCheck("/health");

// Orders API
var orders = builder.AddProject<Projects.Orders_Api>("orders-api")
    .WithReference(ordersDb)
    .WaitFor(ordersDb)
    .WithReference(messaging)
    .WaitFor(messaging)
    .WithHttpHealthCheck("/health");

// Catalog API
var catalog = builder.AddProject<Projects.Catalog_Api>("catalog-api")
    .WithReference(catalogDb)
    .WaitFor(catalogDb)
    .WithReference(redis)
    .WithReference(messaging)
    .WaitFor(messaging)
    .WithHttpHealthCheck("/health");

// Reviews API
var reviews = builder.AddProject<Projects.Reviews_WebApi>("reviews-api")
    .WithReference(reviewsDb)
    .WaitFor(reviewsDb)
    .WithReference(redis)
    .WithReference(messaging)
    .WaitFor(messaging)
    .WithHttpHealthCheck("/health");

// Aggregator
var aggregator = builder.AddProject<Projects.Platform_Aggregator>("aggregator")
    .WithReference(orders)
    .WithReference(catalog)
    .WithReference(reviews)
    .WithReference(redis)
    .WaitFor(orders)
    .WaitFor(catalog)
    .WaitFor(reviews)
    .WithHttpHealthCheck("/health");

// API Gateway
builder.AddProject<Projects.Platform_ApiGateway>("gateway")
    .WithHttpEndpoint(port: 5000, name: "public")
    .WithReference(orders)
    .WithReference(catalog)
    .WithReference(reviews)
    .WithReference(aggregator)
    .WaitFor(aggregator)
    .WithHttpHealthCheck("/health");

builder.Build().Run();