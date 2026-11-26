using Orders.Api.Infrastructure;
using Orders.Api.GrpcServices;
using Orders.Api.Security;
using Orders.Bll.Mapping;
using Orders.Bll.Services;
using Orders.Dal;
using Orders.Dal.Interfaces;
using Orders.Api.Metrics;
using Orders.Bll.Metrics;
using Platform.ServiceDefaults;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Platform.EventBus.DependencyInjection;
using Orders.Api.IntegrationEvents;
using Orders.Bll.IntegrationEvents.Handlers;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddRabbitMQClient("messaging");
builder.Services.AddMessagingTopology();

var connectionString = builder.Configuration.GetConnectionString("OrdersDb")
    ?? throw new InvalidOperationException("Connection string 'OrdersDb' is missing");

builder.Services.AddHealthChecks()
    .AddSqlServer(
        connectionString: connectionString,
        name: "orders-db",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["database", "sql", "ready"],
        timeout: TimeSpan.FromSeconds(5));

builder.Services.AddScoped<IUnitOfWork>(_ => new UnitOfWork(connectionString));
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IMenuService, MenuService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IAccessGuard, AccessGuard>();

// Обробники integration events
builder.Services.AddScoped<MenuItemPriceChangedHandler>();
builder.Services.AddScoped<MenuItemRenamedHandler>();

// Фоновий RabbitMQ Consumer
builder.Services.AddHostedService<OrdersCatalogEventsConsumer>();

builder.Services.AddAutoMapper(cfg =>
{
    cfg.LicenseKey = builder.Configuration["AutoMapper:LicenseKey"];
    cfg.AddProfile<OrdersProfile>();
});

builder.Services.AddControllers();
builder.Services.AddGrpc(o => o.Interceptors.Add<LoggingInterceptor>());
builder.Services.AddGrpcHealthChecks();
if (builder.Environment.IsDevelopment())
    builder.Services.AddGrpcReflection();

builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
    ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

var jwt = builder.Configuration.GetLocalJwtSettings();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = jwt.ToValidationParameters();
    });

builder.Services.AddAuthorization();

builder.Services.AddSingleton<OrdersSnapshot>();
builder.Services.AddSingleton<IOrderMetrics, OrderMetrics>();
builder.Services.AddHostedService<OrdersSnapshotRefresher>();

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

    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AutoMapper.IMapper>()
         .ConfigurationProvider.AssertConfigurationIsValid();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGrpcService<OrdersGrpcService>();
app.MapGrpcHealthChecksService();
if (app.Environment.IsDevelopment())
    app.MapGrpcReflectionService();
app.MapDefaultEndpoints();
app.Run();