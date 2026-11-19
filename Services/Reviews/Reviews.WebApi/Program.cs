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
using Platform.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
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
if (builder.Environment.IsDevelopment())
    builder.Services.AddGrpcReflection();

builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
    ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

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

app.MapControllers();
app.MapGrpcService<ReviewsGrpcService>();
if (app.Environment.IsDevelopment())
    app.MapGrpcReflectionService();
app.MapDefaultEndpoints();
app.Run();