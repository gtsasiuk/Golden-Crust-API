using Catalog.Api.Infrastructure;
using Catalog.Bll;
using Catalog.Bll.Mapping;
using Catalog.Dal;
using Catalog.Dal.Seeding;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Context;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("CatalogDb")
    ?? throw new InvalidOperationException("Connection string 'CatalogDb' is missing");

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "catalog-api")
    .WriteTo.Console()
    .WriteTo.File(new CompactJsonFormatter(), "logs/catalog-.json", rollingInterval: RollingInterval.Day));

builder.Services.AddCatalogDal(connectionString);
builder.Services.AddCatalogBll();

builder.Services.AddAutoMapper(cfg =>
{
    cfg.LicenseKey = builder.Configuration["AutoMapper:LicenseKey"];
    cfg.AddProfile<CatalogProfile>();
});

builder.Services.AddControllers();

builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
    ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.Use(async (ctx, next) =>
{
    var correlationId = ctx.Request.Headers["X-Correlation-Id"].FirstOrDefault() ?? ctx.TraceIdentifier;
    ctx.Response.Headers["X-Correlation-Id"] = correlationId;
    using (LogContext.PushProperty("CorrelationId", correlationId))
        await next();
});

app.UseSerilogRequestLogging(o => o.EnrichDiagnosticContext = (diag, http) =>
    diag.Set("UserId", http.User.Identity?.Name ?? "anonymous"));
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

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    await db.Database.MigrateAsync();
    if (!app.Environment.IsProduction())
        await DbInitializer.SeedAsync(db);
}

app.MapControllers();
app.Run();