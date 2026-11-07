using System.Text.Json.Serialization;
using Catalog.Contracts;
using Orders.Contracts;
using Platform.Aggregator.Services;
using Platform.ServiceDefaults;
using Reviews.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddGrpcClient<OrdersService.OrdersServiceClient>(o => o.Address = new Uri("https://orders-api"));
builder.Services.AddGrpcClient<CatalogService.CatalogServiceClient>(o => o.Address = new Uri("https://catalog-api"));
builder.Services.AddGrpcClient<ReviewsService.ReviewsServiceClient>(o => o.Address = new Uri("https://reviews-api"));

builder.Services.AddScoped<OrderAggregationService>();

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapControllers();
app.MapDefaultEndpoints();
app.Run();