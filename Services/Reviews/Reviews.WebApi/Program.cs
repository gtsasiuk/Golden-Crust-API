using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Reviews.Infrastructure.Persistence;
using Reviews.Application;
using Reviews.Application.Reviews.Mappings;
using Reviews.Application.Metrics;
using Reviews.Infrastructure;
using Reviews.WebApi.ErrorHandling;
using Reviews.WebApi.GrpcServices;
using Reviews.WebApi.Mapping;
using Reviews.WebApi.Metrics;
using Reviews.WebApi.IntegrationEvents;
using Platform.ServiceDefaults;
using Platform.EventBus.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddRabbitMQClient("messaging");
builder.Services.AddMessagingTopology();
builder.Services.AddHostedService<ReviewsCatalogEventsConsumer>();

builder.AddRedisDistributedCache("redis");
builder.Services.AddMemoryCache(o => o.SizeLimit = 10_000);

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

builder.Services
    .AddApplication(builder.Configuration)
    .AddInfrastructure(builder.Configuration);

builder.Services.AddHealthChecks()
    .AddMongoDb(
        clientFactory: sp =>
            sp.GetRequiredService<MongoDbContext>().Client,
        name: "reviews-db",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["database", "mongodb", "ready"],
        timeout: TimeSpan.FromSeconds(5));

builder.Services.AddSingleton<ReviewsSnapshot>();
builder.Services.AddSingleton<IReviewMetrics, ReviewMetrics>();
builder.Services.AddHostedService<ReviewsSnapshotRefresher>();

builder.Services.AddAutoMapper(cfg =>
{
    cfg.LicenseKey = builder.Configuration["LuckyPenny:LicenseKey"];
    cfg.AddMaps(typeof(ReviewProfile).Assembly);
    cfg.AddProfile<RequestProfile>();
});

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(
        new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));
builder.Services.AddGrpc(o => o.Interceptors.Add<LoggingInterceptor>());
builder.Services.AddGrpcHealthChecks();
if (builder.Environment.IsDevelopment())
    builder.Services.AddGrpcReflection();

builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
    ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

var jwt = builder.Configuration.GetLocalJwtSettings();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = jwt.ToValidationParameters();
    });

builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    using var mapScope = app.Services.CreateScope();
    mapScope.ServiceProvider.GetRequiredService<AutoMapper.IMapper>()
            .ConfigurationProvider.AssertConfigurationIsValid();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGrpcService<ReviewsGrpcService>();
app.MapGrpcHealthChecksService();
if (app.Environment.IsDevelopment())
    app.MapGrpcReflectionService();
app.MapDefaultEndpoints();
app.Run();