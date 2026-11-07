var builder = DistributedApplication.CreateBuilder(args);

var sqlPassword = builder.AddParameter("sql-password", secret: true);
var mongoPassword = builder.AddParameter("mongo-password", secret: true);

var sql = builder.AddSqlServer("sql", sqlPassword, port: 14330)
    .WithDataVolume();

var ordersDb = sql.AddDatabase("OrdersDb", "GoldenCrust_Orders");
var catalogDb = sql.AddDatabase("CatalogDb", "GoldenCrust_Catalog");

var mongo = builder.AddMongoDB("mongo", port: 27018, password: mongoPassword)
    .WithDataVolume();
var reviewsDb = mongo.AddDatabase("ReviewsDb", "golden_crust_reviews");

var orders = builder.AddProject<Projects.Orders_Api>("orders-api")
    .WithReference(ordersDb).WaitFor(ordersDb);

var catalog = builder.AddProject<Projects.Catalog_Api>("catalog-api")
    .WithReference(catalogDb).WaitFor(catalogDb);

var reviews = builder.AddProject<Projects.Reviews_WebApi>("reviews-api")
    .WithReference(reviewsDb).WaitFor(reviewsDb);


var aggregator = builder.AddProject<Projects.Platform_Aggregator>("aggregator")
    .WithReference(orders).WithReference(catalog).WithReference(reviews)
    .WaitFor(orders).WaitFor(catalog).WaitFor(reviews);

builder.AddProject<Projects.Platform_ApiGateway>("gateway")
    .WithHttpEndpoint(port: 5000, name: "public")
    .WithReference(orders).WithReference(catalog).WithReference(reviews)
    .WithReference(aggregator)
    .WaitFor(aggregator);

builder.Build().Run();