var builder = DistributedApplication.CreateBuilder(args);

var ordersDb = builder.AddConnectionString("OrdersDb");
var catalogDb = builder.AddConnectionString("CatalogDb");
var reviewsDb = builder.AddConnectionString("ReviewsDb");

builder.AddProject<Projects.Orders_Api>("orders-api")
    .WithReference(ordersDb);

builder.AddProject<Projects.Catalog_Api>("catalog-api")
    .WithReference(catalogDb);

builder.AddProject<Projects.Reviews_WebApi>("reviews-api")
    .WithReference(reviewsDb);

builder.Build().Run();