using System.Text.Json.Serialization;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Catalog.Contracts;
using Orders.Contracts;
using Platform.Aggregator.Caching;
using Platform.Aggregator.Resilience;
using Platform.Aggregator.Services;
using Platform.ServiceDefaults;
using Reviews.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddRedisDistributedCache("redis");

var redisConnectionString =
    builder.Configuration.GetConnectionString("redis")
    ?? throw new InvalidOperationException(
        "Redis connection string is missing");

builder.Services.AddHealthChecks()
    .AddRedis(
        redisConnectionString: redisConnectionString,
        name: "redis-cache",
        failureStatus: HealthStatus.Degraded,
        tags: ["cache", "redis", "ready"],
        timeout: TimeSpan.FromSeconds(3));

builder.Services.AddGrpcClient<OrdersService.OrdersServiceClient>(o => o.Address = new Uri(ResilienceProfiles.Orders.Address))
    .ConfigureChannel(c => c.ServiceConfig = ResilienceProfiles.Orders.ToServiceConfig())
    .AddProfiledResilience(ResilienceProfiles.Orders);

builder.Services.AddGrpcClient<CatalogService.CatalogServiceClient>(o => o.Address = new Uri(ResilienceProfiles.Catalog.Address))
    .ConfigureChannel(c => c.ServiceConfig = ResilienceProfiles.Catalog.ToServiceConfig())
    .AddProfiledResilience(ResilienceProfiles.Catalog);

builder.Services.AddGrpcClient<ReviewsService.ReviewsServiceClient>(o => o.Address = new Uri(ResilienceProfiles.Reviews.Address))
    .ConfigureChannel(c => c.ServiceConfig = ResilienceProfiles.Reviews.ToServiceConfig())
    .AddProfiledResilience(ResilienceProfiles.Reviews);

foreach (var profile in ResilienceProfiles.All)
{
    builder.Services
        .AddHttpClient(profile.Name, c => c.BaseAddress = new Uri(profile.Address))
        .AddProfiledResilience(profile);
}

builder.Services.AddScoped<OrderAggregationService>();
builder.Services.AddScoped<CachedOrderAggregationService>();
builder.Services.AddSingleton<DownstreamStatusService>();

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

ResilienceLog.Init(app.Services.GetRequiredService<ILoggerFactory>());

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapControllers();
app.MapDefaultEndpoints();
app.Run();