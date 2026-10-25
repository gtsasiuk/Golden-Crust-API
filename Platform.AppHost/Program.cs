var builder = DistributedApplication.CreateBuilder(args);

var ordersDb = builder.AddConnectionString("OrdersDb");
var catalogDb = builder.AddConnectionString("CatalogDb");

builder.AddProject<Projects.Orders_Api>("orders-api")
    .WithReference(ordersDb);

builder.AddProject<Projects.Catalog_Api>("catalog-api")
    .WithReference(catalogDb);

builder.Build().Run();