using System.Text.Json.Serialization;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Catalog.Contracts;
using Orders.Contracts;
using Platform.Aggregator.Caching;
using Platform.Aggregator.Resilience;
using Platform.Aggregator.Services;
using Platform.ServiceDefaults;
using Platform.Aggregator.Security;
using Reviews.Contracts;
using Grpc.Health.V1;
using Platform.Aggregator.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddRedisDistributedCache("redis");

// IdentityServer Client Credentials token provider
var identityServerAuthority =
    builder.Configuration["IdentityServer:Authority"]
    ?? throw new InvalidOperationException(
        "IdentityServer:Authority is missing.");

builder.Services.AddHttpClient<IdentityServerTokenProvider>(client =>
{
    client.BaseAddress = new Uri(
        identityServerAuthority.TrimEnd('/') + "/");
});

builder.Services.AddTransient<IdentityServerTokenHandler>();
builder.Services.AddTransient<IdentityServerGrpcInterceptor>();

var jwt = builder.Configuration.GetLocalJwtSettings();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = jwt.ToValidationParameters();
    });

builder.Services.AddAuthorization();

builder.Services.AddGrpcClient<Health.HealthClient>(
    "orders-health",
    o => o.Address = new Uri(ResilienceProfiles.Orders.Address));

builder.Services.AddGrpcClient<Health.HealthClient>(
    "catalog-health",
    o => o.Address = new Uri(ResilienceProfiles.Catalog.Address));

builder.Services.AddGrpcClient<Health.HealthClient>(
    "reviews-health",
    o => o.Address = new Uri(ResilienceProfiles.Reviews.Address));

builder.Services.AddHealthChecks()
    .AddCheck<OrdersGrpcHealthCheck>(
        "orders-api",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["grpc", "downstream", "ready"])
    .AddCheck<CatalogGrpcHealthCheck>(
        "catalog-api",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["grpc", "downstream", "ready"])
    .AddCheck<ReviewsGrpcHealthCheck>(
        "reviews-api",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["grpc", "downstream", "ready"]);

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


builder.Services.AddGrpcClient<OrdersService.OrdersServiceClient>(
    o => o.Address = new Uri(ResilienceProfiles.Orders.Address))
    .ConfigureChannel(c =>
        c.ServiceConfig = ResilienceProfiles.Orders.ToServiceConfig())
    .AddInterceptor<IdentityServerGrpcInterceptor>()
    .AddProfiledResilience(ResilienceProfiles.Orders);

builder.Services.AddGrpcClient<CatalogService.CatalogServiceClient>(
    o => o.Address = new Uri(ResilienceProfiles.Catalog.Address))
    .ConfigureChannel(c =>
        c.ServiceConfig = ResilienceProfiles.Catalog.ToServiceConfig())
    .AddInterceptor<IdentityServerGrpcInterceptor>()
    .AddProfiledResilience(ResilienceProfiles.Catalog);

builder.Services.AddGrpcClient<ReviewsService.ReviewsServiceClient>(
    o => o.Address = new Uri(ResilienceProfiles.Reviews.Address))
    .ConfigureChannel(c =>
        c.ServiceConfig = ResilienceProfiles.Reviews.ToServiceConfig())
    .AddInterceptor<IdentityServerGrpcInterceptor>()
    .AddProfiledResilience(ResilienceProfiles.Reviews);


foreach (var profile in ResilienceProfiles.All)
{
    builder.Services
        .AddHttpClient(profile.Name, c =>
            c.BaseAddress = new Uri(profile.Address))
        .AddHttpMessageHandler<IdentityServerTokenHandler>()
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

app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapControllers();
app.MapDefaultEndpoints();
app.Run();