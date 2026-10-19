var builder = DistributedApplication.CreateBuilder(args);

// Існуюча БД з лаби 1 (контейнер golden-crust-sql). Рядок підключення береться з appsettings.json AppHost.
var ordersDb = builder.AddConnectionString("OrdersDb");

builder.AddProject<Projects.Orders_Api>("orders-api")
    .WithReference(ordersDb);   // Aspire підставить ConnectionStrings__OrdersDb, код API не змінюється

builder.Build().Run();